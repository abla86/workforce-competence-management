using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;
using Workforce.Api.Models;

namespace Workforce.Api.Services;

public sealed class CoverageService
{
    private readonly AppDbContext _context;
    private readonly ILogger<CoverageService> _logger;
    private readonly AuditProtectionService _auditProtection;

    public CoverageService(AppDbContext context, ILogger<CoverageService> logger, AuditProtectionService auditProtection)
    {
        _context = context;
        _logger = logger;
        _auditProtection = auditProtection;
    }

    public async Task<CoverageResult> EvaluateAsync(int shiftId, string triggeredBy = "system", HttpContext? httpContext = null)
    {
        var shift = await _context.Shifts
            .Include(s => s.ShiftTasks).ThenInclude(st => st.WorkTask)
            .Include(s => s.ShiftTasks).ThenInclude(st => st.ShiftTaskCoverages).ThenInclude(stc => stc.Employee).ThenInclude(e => e.Competences)
            .Include(s => s.Assignments).ThenInclude(a => a.Employee).ThenInclude(e => e.Availability)
            .FirstOrDefaultAsync(s => s.Id == shiftId);

        if (shift is null) throw new ArgumentException($"Shift {shiftId} not found");

        var result = Evaluate(shift);
        var employeeIds = shift.ShiftTasks.SelectMany(t => t.ShiftTaskCoverages).Select(c => c.EmployeeId).Distinct().ToList();
        var conflictingShifts = await _context.Shifts
            .Where(s => s.Id != shift.Id && s.StartTime < shift.EndTime && s.EndTime > shift.StartTime && s.Assignments.Any(a => employeeIds.Contains(a.EmployeeId)))
            .Include(s => s.Assignments)
            .ToListAsync();
        var previousShifts = await _context.Shifts
            .Where(s => s.Id != shift.Id && s.EndTime <= shift.StartTime && s.Assignments.Any(a => employeeIds.Contains(a.EmployeeId)))
            .Include(s => s.Assignments)
            .ToListAsync();

        foreach (var task in shift.ShiftTasks)
        {
            var detail = result.Tasks.First(t => t.TaskName == task.WorkTask.Name);
            foreach (var coverage in task.ShiftTaskCoverages.Where(c => c.IsValid))
            {
                if (conflictingShifts.Any(s => s.Assignments.Any(a => a.EmployeeId == coverage.EmployeeId)))
                    detail.Gaps.Add(new CoverageGap { Type = GapType.DoubleBooked, EmployeeId = coverage.EmployeeId, Description = $"{coverage.Employee.Name} har overlappende vakt." });

                var previous = previousShifts.Where(s => s.Assignments.Any(a => a.EmployeeId == coverage.EmployeeId)).OrderByDescending(s => s.EndTime).FirstOrDefault();
                if (previous is not null && (shift.StartTime - previous.EndTime).TotalHours < 11)
                    detail.Gaps.Add(new CoverageGap { Type = GapType.RestPeriodViolation, EmployeeId = coverage.EmployeeId, Description = $"{coverage.Employee.Name} har bare {(shift.StartTime - previous.EndTime).TotalHours:F1} timer hvile." });

                var availability = coverage.Employee.Availability.FirstOrDefault(a => a.Date == shift.Date);
                if (availability is not null && !availability.IsAvailable)
                    detail.Gaps.Add(new CoverageGap { Type = GapType.EmployeeUnavailable, EmployeeId = coverage.EmployeeId, Description = $"{coverage.Employee.Name} er registrert utilgjengelig {shift.Date:dd.MM.yyyy}." });

                if (!string.IsNullOrWhiteSpace(task.WorkTask.RequiredRole) && !string.Equals(coverage.Employee.Role, task.WorkTask.RequiredRole, StringComparison.OrdinalIgnoreCase))
                    detail.Gaps.Add(new CoverageGap { Type = GapType.MissingRole, EmployeeId = coverage.EmployeeId, Description = $"{coverage.Employee.Name} har rolle {coverage.Employee.Role}, krever {task.WorkTask.RequiredRole}." });

                if (!string.IsNullOrWhiteSpace(task.WorkTask.RequiredAuthorization) && (!string.Equals(coverage.Employee.Authorization, task.WorkTask.RequiredAuthorization, StringComparison.OrdinalIgnoreCase) || (coverage.Employee.AuthorizationExpiry.HasValue && coverage.Employee.AuthorizationExpiry.Value < shift.StartTime)))
                    detail.Gaps.Add(new CoverageGap { Type = GapType.UnauthorizedRole, EmployeeId = coverage.EmployeeId, Description = $"{coverage.Employee.Name} mangler gyldig autorisasjon {task.WorkTask.RequiredAuthorization}." });
            }
        }

        RecalculateStatus(result, shift.Assignments.Count < shift.MinimumStaff);
        var audit = new CoverageAuditEntry
        {
            ShiftId = shiftId,
            Status = result.Status.ToString(),
            TriggeredBy = triggeredBy,
            ClientIp = httpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext?.Request.Headers.UserAgent.ToString()
        };
        _auditProtection.ProtectResult(audit, result);
        _context.CoverageAuditEntries.Add(audit);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Coverage evaluated for shift {ShiftId}: {Status}", shiftId, result.Status);
        return result;
    }

    public CoverageResult Evaluate(Shift shift)
    {
        var result = new CoverageResult();
        foreach (var shiftTask in shift.ShiftTasks)
        {
            var detail = new TaskCoverageDetail
            {
                TaskName = shiftTask.WorkTask.Name,
                Required = shiftTask.RequiredCount,
                Critical = shiftTask.IsCritical
            };
            var coverages = shiftTask.ShiftTaskCoverages.Where(x => x.IsValid).ToList();
            detail.Actual = coverages.Count;
            if (detail.Actual < detail.Required)
                detail.Gaps.Add(new CoverageGap { Type = GapType.InsufficientStaff, Description = $"Trenger {detail.Required}, har {detail.Actual}" });
            foreach (var coverage in coverages) ValidateCoverage(coverage, shiftTask, detail);
            result.Tasks.Add(detail);
        }
        var staffingGap = shift.Assignments.Count < shift.MinimumStaff;
        if (staffingGap) result.Warnings.Add($"Vakten har {shift.Assignments.Count} ansatte, men krever minst {shift.MinimumStaff}.");
        RecalculateStatus(result, staffingGap);
        return result;
    }

    private static void ValidateCoverage(ShiftTaskCoverage coverage, ShiftTask shiftTask, TaskCoverageDetail detail)
    {
        var employee = coverage.Employee;
        if (shiftTask.WorkTask.CompetenceId is not null)
        {
            var competence = employee.Competences.FirstOrDefault(x => x.CompetenceId == shiftTask.WorkTask.CompetenceId);
            if (competence is null)
                detail.Gaps.Add(new CoverageGap { Type = GapType.MissingCompetence, EmployeeId = employee.Id, Description = $"{employee.Name} mangler nødvendig kompetanse for {shiftTask.WorkTask.Name}." });
            else
            {
                if (Rank(competence.Level) < shiftTask.MinCompetenceLevel)
                    detail.Gaps.Add(new CoverageGap { Type = GapType.MissingCompetence, EmployeeId = employee.Id, Description = $"{employee.Name} har nivå {competence.Level}, krever minst {shiftTask.MinCompetenceLevel}." });
                if (competence.ValidUntil.HasValue && competence.ValidUntil.Value < DateOnly.FromDateTime(DateTime.UtcNow))
                    detail.Gaps.Add(new CoverageGap { Type = GapType.CompetenceExpired, EmployeeId = employee.Id, Description = $"Kompetansen til {employee.Name} er utløpt." });
            }
        }
        if (coverage.AuthorizationExpiry.HasValue && coverage.AuthorizationExpiry.Value < DateTime.UtcNow)
            detail.Gaps.Add(new CoverageGap { Type = GapType.AuthorizationExpired, EmployeeId = employee.Id, Description = $"Autorisasjonen til {employee.Name} er utløpt." });
    }

    private static void RecalculateStatus(CoverageResult result, bool staffingGap)
    {
        if (staffingGap || result.Tasks.Any(t => t.Critical && t.Gaps.Count > 0)) result.Status = CoverageStatus.Red;
        else if (result.Tasks.Any(t => t.Gaps.Count > 0)) result.Status = CoverageStatus.Yellow;
        else result.Status = CoverageStatus.Green;
    }

    private static int Rank(string level) => level.ToLowerInvariant() switch { "basic" => 1, "intermediate" => 2, "advanced" => 3, _ => 1 };
}
