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

                if (existingEnd <= shiftStart && (shiftStart - existingEnd).TotalHours < 11)
                {
                    hardFailures.Add("Mulig brudd på 11-timers hvile før vakten");
                    break;
                }

                if (existingStart >= shiftEnd && (existingStart - shiftEnd).TotalHours < 11)
                {
                    hardFailures.Add("Mulig brudd på 11-timers hvile etter vakten");
                    break;
                }
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

            var assignedHours = allShifts
                .Where(s => s.Date >= shift.Date.AddDays(-6) && s.Date <= shift.Date)
                .Where(s => s.Assignments.Any(a => a.EmployeeId == employee.Id))
                .Sum(s => (double)s.Hours);

            if (employee.MaxWeeklyHours > 0 && assignedHours + (double)shift.Hours > (double)employee.MaxWeeklyHours)
                hardFailures.Add($"Ukegrense overskrides ({assignedHours + (double)shift.Hours:F1}t > {employee.MaxWeeklyHours:F1}t)");
            else if (assignedHours >= 35)
            {
                warnings.Add("Høy planlagt belastning siste 7 dager");
                score -= 20;
            }
            else if (assignedHours >= 30)
            {
                warnings.Add("Moderat høy belastning siste 7 dager");
                score -= 10;
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
                assignedHours));
        }

        return results
            .OrderByDescending(x => x.Eligible)
            .ThenByDescending(x => x.Score)
            .ThenBy(x => x.RecentHours)
            .ToList();
    }
}
