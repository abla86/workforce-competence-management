using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;
using Workforce.Api.Models;

namespace Workforce.Api.Services;

public sealed class AutoStaffingService
{
    private readonly AppDbContext _db;

    public AutoStaffingService(AppDbContext db) => _db = db;

    public async Task<List<StaffingProposal>> GenerateAsync(AutoStaffingRequest request, int maxSuggestions = 5)
    {
        var shift = await _db.Shifts
            .Include(s => s.ShiftTasks).ThenInclude(t => t.WorkTask).ThenInclude(w => w.Competence)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId);
        if (shift is null) throw new ArgumentException($"Shift {request.ShiftId} not found");

        var task = request.WorkTaskId.HasValue
            ? shift.ShiftTasks.FirstOrDefault(t => t.Id == request.WorkTaskId.Value)
            : null;

        var competenceId = request.CompetenceId ?? task?.WorkTask.CompetenceId;
        var requiredLevel = request.MinimumCompetenceLevel > 0 ? request.MinimumCompetenceLevel : task?.MinCompetenceLevel ?? 1;
        var requiredRole = task?.WorkTask.RequiredRole;
        var requiredAuthorization = task?.WorkTask.RequiredAuthorization;

        var candidates = await _db.Employees
            .Include(e => e.Competences)
            .Include(e => e.Absences)
            .Include(e => e.Availability)
            .Include(e => e.ShiftAssignments).ThenInclude(a => a.Shift)
            .Where(e => e.IsActive)
            .ToListAsync();

        var proposals = new List<StaffingProposal>();
        foreach (var employee in candidates)
        {
            var proposal = EvaluateCandidate(employee, shift, competenceId, requiredLevel, requiredRole, requiredAuthorization);
            if (proposal.MatchScore >= 50)
                proposals.Add(proposal);
        }

        return proposals.OrderByDescending(x => x.MatchScore).ThenBy(x => x.WillCauseOvertime).Take(maxSuggestions).ToList();
    }

    private StaffingProposal EvaluateCandidate(Employee employee, Shift shift, int? competenceId, int requiredLevel, string? requiredRole, string? requiredAuthorization)
    {
        var result = new StaffingProposal { EmployeeId = employee.Id, EmployeeName = employee.Name };
        var hours = Math.Max(0, shift.DurationHours);
        var mandatoryFailed = false;

        if (!string.IsNullOrWhiteSpace(requiredRole) && !string.Equals(employee.Role, requiredRole, StringComparison.OrdinalIgnoreCase))
        {
            mandatoryFailed = true;
            result.Warnings.Add(new StaffingWarning { Type = StaffingWarningType.MissingCompetence, Message = $"Rolle {requiredRole} kreves" });
        }
        else if (!string.IsNullOrWhiteSpace(requiredRole))
        {
            AddFactor(result, FactorType.CompetenceMatch, $"Riktig rolle: {employee.Role}", 15, true);
        }

        if (!string.IsNullOrWhiteSpace(requiredAuthorization))
        {
            var valid = string.Equals(employee.Authorization, requiredAuthorization, StringComparison.OrdinalIgnoreCase)
                && (!employee.AuthorizationExpiry.HasValue || employee.AuthorizationExpiry.Value >= shift.StartTime);
            if (!valid)
            {
                mandatoryFailed = true;
                result.Warnings.Add(new StaffingWarning { Type = StaffingWarningType.MissingCompetence, Message = $"Krever gyldig autorisasjon: {requiredAuthorization}" });
            }
            else AddFactor(result, FactorType.CompetenceMatch, "Gyldig autorisasjon", 15, true);
        }

        if (competenceId.HasValue)
        {
            var competence = employee.Competences.FirstOrDefault(c => c.CompetenceId == competenceId.Value);
            if (competence is null || Rank(competence.Level) < requiredLevel || (competence.ValidUntil.HasValue && competence.ValidUntil.Value < DateOnly.FromDateTime(shift.StartTime)))
            {
                mandatoryFailed = true;
                result.Warnings.Add(new StaffingWarning { Type = StaffingWarningType.MissingCompetence, Message = "Manglende, for lav eller utløpt kompetanse" });
            }
            else
            {
                AddFactor(result, FactorType.CompetenceMatch, "Nødvendig kompetanse er gyldig", 30, true);
                var bonus = Math.Max(0, (Rank(competence.Level) - requiredLevel) * 5);
                AddFactor(result, FactorType.CompetenceLevel, $"Kompetansenivå {competence.Level}", bonus);
            }
        }

        var availability = employee.Availability.FirstOrDefault(a => a.Date == DateOnly.FromDateTime(shift.StartTime));
        if (availability is not null && !availability.IsAvailable)
        {
            mandatoryFailed = true;
            result.Warnings.Add(new StaffingWarning { Type = StaffingWarningType.HasAbsence, Message = "Registrert utilgjengelighet" });
        }

        if (employee.Absences.Any(a => a.IsApproved && a.StartDate < shift.EndTime && a.EndDate > shift.StartTime))
        {
            mandatoryFailed = true;
            result.Warnings.Add(new StaffingWarning { Type = StaffingWarningType.HasAbsence, Message = "Godkjent fravær overlapper vakten" });
        }

        if (employee.ShiftAssignments.Any(a => a.Shift.StartTime < shift.EndTime && a.Shift.EndTime > shift.StartTime && a.Shift.Id != shift.Id))
        {
            mandatoryFailed = true;
            result.Warnings.Add(new StaffingWarning { Type = StaffingWarningType.DoubleBooked, Message = "Overlappende vakt" });
        }
        else AddFactor(result, FactorType.AvailableTimeSlot, "Ingen overlappende vakt", 10);

        var previous = employee.ShiftAssignments.Where(a => a.Shift.EndTime <= shift.StartTime).Select(a => a.Shift).OrderByDescending(s => s.EndTime).FirstOrDefault();
        if (previous is not null)
        {
            var rest = (shift.StartTime - previous.EndTime).TotalHours;
            if (rest < 11)
            {
                var dispensation = _db.ShiftDispensations.Local.Any(d => d.EmployeeId == employee.Id && d.ShiftId == previous.Id && d.BreachedRule == RuleType.MinimumRest && d.Status == DispensationStatus.Approved);
                if (!dispensation)
                {
                    mandatoryFailed = true;
                    result.Warnings.Add(new StaffingWarning { Type = StaffingWarningType.RestPeriodViolation, Message = $"Bare {rest:F1} timer hvile" });
                }
            }
            else AddFactor(result, FactorType.RestPeriodCompliant, $"{rest:F1} timer hvile", 10);
        }

        var weekly = employee.ShiftAssignments.Where(a => a.Shift.StartTime >= shift.StartTime.Date.AddDays(-(int)shift.StartTime.DayOfWeek + 1) && a.Shift.StartTime < shift.StartTime.Date.AddDays(8 - (int)shift.StartTime.DayOfWeek)).Sum(a => a.Shift.DurationHours);
        var projected = weekly + hours;
        result.AddedHours = hours;
        result.ProjectedOvertimeHours = Math.Max(0, projected - employee.WeeklyContractHours);
        result.WillCauseOvertime = result.ProjectedOvertimeHours > 0;
        if (result.WillCauseOvertime)
        {
            result.Warnings.Add(new StaffingWarning { Type = StaffingWarningType.OvertimeRisk, Message = $"Ca. {result.ProjectedOvertimeHours:F1} timer over kontraktsnivå" });
        }
        else AddFactor(result, FactorType.UnderWeeklyLimit, "Innenfor ukentlig kontraktsnivå", 10);

        var shiftType = GetShiftType(shift.StartTime);
        var preferenceAllowed = shiftType switch
        {
            "Morning" => employee.CanWorkMorning,
            "Day" => employee.CanWorkDay,
            "Evening" => employee.CanWorkEvening,
            _ => employee.CanWorkNight
        };
        if (preferenceAllowed) AddFactor(result, FactorType.PreferenceMatch, $"Kan arbeide {shiftType.ToLowerInvariant()}", 5);
        else result.MatchScore -= 10;
        if (!string.IsNullOrWhiteSpace(employee.PreferredShiftType) && employee.PreferredShiftType.Equals(shiftType, StringComparison.OrdinalIgnoreCase))
            AddFactor(result, FactorType.PreferenceMatch, "Matcher foretrukket vakttype", 10);

        if (mandatoryFailed) result.MatchScore = 0;
        result.MatchScore = Math.Clamp(result.MatchScore, 0, 100);
        return result;
    }

    private static void AddFactor(StaffingProposal result, FactorType type, string description, int points, bool mandatory = false)
    {
        result.Factors.Add(new MatchingFactor { Type = type, Description = description, ScoreContribution = points, IsMandatory = mandatory });
        result.MatchScore += points;
    }

    private static int Rank(string level) => level.ToLowerInvariant() switch { "basic" => 1, "intermediate" => 2, "advanced" => 3, _ => 1 };

    private static string GetShiftType(DateTime start) => start.Hour switch { >= 6 and < 14 => "Morning", >= 14 and < 18 => "Day", >= 18 and < 22 => "Evening", _ => "Night" };
}
