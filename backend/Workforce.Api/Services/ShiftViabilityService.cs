using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;
using Workforce.Api.Models;

namespace Workforce.Api.Services;

public sealed class ShiftViabilityService
{
    private readonly AppDbContext _db;
    public ShiftViabilityService(AppDbContext db) => _db = db;

    public async Task<ViabilityCheck> CheckAsync(int employeeId, DateTime start, DateTime end)
    {
        var employee = await _db.Employees.Include(e => e.ShiftAssignments).ThenInclude(a => a.Shift).FirstOrDefaultAsync(e => e.Id == employeeId);
        if (employee is null) throw new ArgumentException($"Employee {employeeId} not found");

        var result = new ViabilityCheck();
        var overlapping = employee.ShiftAssignments.Any(a => a.Shift.StartTime < end && a.Shift.EndTime > start);
        if (overlapping)
        {
            result.Violations.Add(new RuleViolation { RuleType = RuleType.MinimumRest, Severity = RuleSeverity.Critical, Message = "Vakten overlapper en eksisterende vakt." });
        }

        var previous = employee.ShiftAssignments.Where(a => a.Shift.EndTime <= start).Select(a => a.Shift).OrderByDescending(s => s.EndTime).FirstOrDefault();
        if (previous is not null)
        {
            var rest = (start - previous.EndTime).TotalHours;
            if (rest < 11)
            {
                var approved = await _db.ShiftDispensations.AnyAsync(d => d.EmployeeId == employeeId && d.ShiftId == previous.Id && d.BreachedRule == RuleType.MinimumRest && d.Status == DispensationStatus.Approved && (d.ExpiresAt == null || d.ExpiresAt >= start));
                if (!approved)
                {
                    result.DispensationsNeeded.Add(new DispensationNeed { RuleType = RuleType.MinimumRest, HoursShortfall = 11 - rest, RequiresApproval = true, CanBeAutoApproved = false });
                    result.Warnings.Add(new StaffingWarning { Type = StaffingWarningType.RestPeriodViolation, Message = $"Kun {rest:F1} timer hvile siden forrige vakt." });
                }
            }
        }

        var weekStart = start.Date.AddDays(-(int)start.DayOfWeek + 1);
        var weekEnd = weekStart.AddDays(7);
        var weeklyHours = employee.ShiftAssignments.Where(a => a.Shift.StartTime >= weekStart && a.Shift.StartTime < weekEnd).Sum(a => a.Shift.DurationHours);
        var projected = weeklyHours + Math.Max(0, (end - start).TotalHours);
        if (projected > employee.WeeklyContractHours)
        {
            result.DispensationsNeeded.Add(new DispensationNeed { RuleType = RuleType.MaxWeeklyHours, HoursShortfall = projected - employee.WeeklyContractHours, RequiresApproval = true, CanBeAutoApproved = false });
            result.Warnings.Add(new StaffingWarning { Type = StaffingWarningType.OvertimeRisk, Message = $"Planlagt {projected:F1} timer mot kontrakt {employee.WeeklyContractHours:F1} timer." });
        }

        result.CanProceed = !result.Violations.Any(v => v.Severity == RuleSeverity.Critical);
        result.NeedsManualApproval = result.DispensationsNeeded.Count > 0;
        result.Message = !result.CanProceed
            ? $"Vakt kan ikke legges: {string.Join(" ", result.Violations.Select(v => v.Message))}"
            : result.NeedsManualApproval
                ? "Vakt kan legges etter manuell vurdering/dispensasjon."
                : "Vakt kan legges.";
        return result;
    }
}
