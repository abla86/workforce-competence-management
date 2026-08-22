using Workforce.Api.DTOs;
using Workforce.Api.Models;

namespace Workforce.Api.Services;

public sealed class PlanningAdvisor
{
    public IReadOnlyList<CandidateResult> RankCandidates(Shift shift, IReadOnlyList<Employee> employees, IReadOnlyList<Shift> allShifts)
    {
        var results = new List<CandidateResult>();
        var shiftStart = SchedulingRules.GetStart(shift);
        var shiftEnd = SchedulingRules.GetEnd(shift);

        foreach (var employee in employees.Where(e => e.IsActive))
        {
            var hardFailures = new List<string>();
            var warnings = new List<string>();
            var score = 100;

            if (employee.PositionPercent <= 0)
                hardFailures.Add("Ingen aktiv stillingsprosent");

            if (shift.Assignments.Any(a => a.EmployeeId == employee.Id))
                hardFailures.Add("Allerede tildelt denne vakten");

            if (employee.Absences.Any(a => a.Approved && a.From <= shift.Date && a.To >= shift.Date))
                hardFailures.Add("Fravær på vaktdato");

            var employeeShifts = allShifts
                .Where(s => s.Id != shift.Id && s.Assignments.Any(a => a.EmployeeId == employee.Id))
                .ToList();

            if (employeeShifts.Any(s => SchedulingRules.GetStart(s) < shiftEnd && SchedulingRules.GetEnd(s) > shiftStart))
                hardFailures.Add("Allerede tildelt overlappende vakt");

            foreach (var existing in employeeShifts)
            {
                var existingStart = SchedulingRules.GetStart(existing);
                var existingEnd = SchedulingRules.GetEnd(existing);

                // Rest is a compliance warning, not an automatic block. Some healthcare
                // schedules can use lawful agreements/dispensations and compensatory rest.
                if (existingEnd <= shiftStart && (shiftStart - existingEnd).TotalHours < 11)
                    warnings.Add($"Kort hvile før vakten ({(shiftStart - existingEnd).TotalHours:F1}t)");

                if (existingStart >= shiftEnd && (existingStart - shiftEnd).TotalHours < 11)
                    warnings.Add($"Kort hvile etter vakten ({(existingStart - shiftEnd).TotalHours:F1}t)");
            }

            foreach (var requirement in shift.Requirements)
            {
                var competence = employee.Competences.FirstOrDefault(c => c.CompetenceId == requirement.CompetenceId);
                if (competence is null)
                {
                    hardFailures.Add($"Mangler {requirement.Competence.Name}");
                    continue;
                }

                if (competence.Level < requirement.MinimumLevel)
                    hardFailures.Add($"For lavt nivå i {requirement.Competence.Name}");

                if (competence.ValidUntil.HasValue && competence.ValidUntil.Value < shift.Date)
                    hardFailures.Add($"Utløpt {requirement.Competence.Name}");
                else if (competence.ValidUntil.HasValue && competence.ValidUntil.Value <= shift.Date.AddDays(45))
                    warnings.Add($"{requirement.Competence.Name} utløper snart");

                if (requirement.RequiredRole is not null &&
                    !string.Equals(employee.Role, requirement.RequiredRole, StringComparison.OrdinalIgnoreCase))
                    hardFailures.Add($"Feil rolle for {requirement.Competence.Name}");
            }

            // Contract hours are derived from employment percentage unless a lower
            // configured maximum has been supplied. Planned hours above the contract
            // are surfaced as overtime risk, but are not silently treated as illegal.
            var contractualWeeklyHours = employee.PositionPercent > 0
                ? Math.Min(employee.MaxWeeklyHours > 0 ? employee.MaxWeeklyHours : 37.5m,
                    37.5m * employee.PositionPercent / 100m)
                : 0m;

            var weekStart = shift.Date.AddDays(-(int)shift.Date.DayOfWeek + (int)DayOfWeek.Monday);
            if (shift.Date.DayOfWeek == DayOfWeek.Sunday)
                weekStart = shift.Date.AddDays(-6);
            var weekEnd = weekStart.AddDays(6);

            var scheduledHours = allShifts
                .Where(s => s.Date >= weekStart && s.Date <= weekEnd)
                .Where(s => s.Assignments.Any(a => a.EmployeeId == employee.Id))
                .Sum(s => (double)s.Hours);
            var projectedHours = scheduledHours + (double)shift.Hours;

            if (contractualWeeklyHours > 0 && projectedHours > (double)contractualWeeklyHours)
            {
                var overtime = projectedHours - (double)contractualWeeklyHours;
                warnings.Add($"Planlagt over avtalt stillingsomfang: {overtime:F1}t over {contractualWeeklyHours:F1}t/uke");
                score -= Math.Min(30, (int)Math.Ceiling(overtime * 3));
            }
            else if (contractualWeeklyHours > 0 && projectedHours >= (double)contractualWeeklyHours * 0.9)
            {
                warnings.Add("Nær avtalt ukentlig timeomfang");
                score -= 8;
            }

            var recentHours = allShifts
                .Where(s => s.Date >= shift.Date.AddDays(-6) && s.Date <= shift.Date)
                .Where(s => s.Assignments.Any(a => a.EmployeeId == employee.Id))
                .Sum(s => (double)s.Hours);

            if (recentHours >= 35)
            {
                warnings.Add("Høy planlagt belastning siste 7 dager");
                score -= 15;
            }
            else if (recentHours >= 30)
            {
                warnings.Add("Moderat høy belastning siste 7 dager");
                score -= 8;
            }

            score -= hardFailures.Count * 40;
            score -= warnings.Count * 5;

            results.Add(new CandidateResult(
                employee.Id,
                employee.Name,
                employee.Role,
                Math.Max(0, score),
                hardFailures.Count == 0,
                hardFailures,
                warnings,
                recentHours));
        }

        return results
            .OrderByDescending(x => x.Eligible)
            .ThenByDescending(x => x.Score)
            .ThenBy(x => x.RecentHours)
            .ToList();
    }
}
