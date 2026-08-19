using System.Text.Json;
using Workforce.Api.DTOs;
using Workforce.Api.Models;

namespace Workforce.Api.Services;

public sealed class PlanningAdvisor
{
    private static readonly Dictionary<string, int> LevelRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Basic"] = 1, ["Intermediate"] = 2, ["Advanced"] = 3
    };

    public IReadOnlyList<CandidateResult> RankCandidates(Shift shift, IReadOnlyList<Employee> employees, IReadOnlyList<Shift> allShifts)
    {
        var results = new List<CandidateResult>();
        var shiftStart = GetStart(shift);
        var shiftEnd = GetEnd(shift);

        foreach (var employee in employees.Where(e => e.IsActive))
        {
            var hardFailures = new List<string>();
            var warnings = new List<string>();
            var score = 100;

            if (employee.PositionPercent <= 0)
            {
                hardFailures.Add("Ingen aktiv stillingsprosent");
            }

            var absence = employee.Absences.Any(a => a.Approved && a.From <= shift.Date && a.To >= shift.Date);
            if (absence) hardFailures.Add("Fravær på vaktdato");

            var overlap = allShifts.Any(s => s.Id != shift.Id && s.Date == shift.Date &&
                s.Assignments.Any(a => a.EmployeeId == employee.Id) &&
                GetStart(s) < shiftEnd && GetEnd(s) > shiftStart);
            if (overlap) hardFailures.Add("Allerede tildelt overlappende vakt");

            var restViolation = allShifts.Any(s => s.Id != shift.Id && s.Assignments.Any(a => a.EmployeeId == employee.Id) &&
                GetEnd(s) <= shiftStart && (shiftStart - GetEnd(s)).TotalHours < 11);
            if (restViolation) hardFailures.Add("Mulig brudd på 11-timers hvile mellom vakter");

            foreach (var requirement in shift.Requirements)
            {
                var required = LevelRank.GetValueOrDefault(requirement.MinimumLevel, 1);
                var competence = employee.Competences.FirstOrDefault(c => c.CompetenceId == requirement.CompetenceId);
                if (competence is null)
                {
                    hardFailures.Add($"Mangler {requirement.Competence.Name}");
                    continue;
                }
                if (LevelRank.GetValueOrDefault(competence.Level, 1) < required)
                    hardFailures.Add($"For lavt nivå i {requirement.Competence.Name}");
                if (competence.ValidUntil.HasValue && competence.ValidUntil.Value < shift.Date)
                    hardFailures.Add($"Utløpt {requirement.Competence.Name}");
                else if (competence.ValidUntil.HasValue && competence.ValidUntil.Value <= shift.Date.AddDays(45))
                    warnings.Add($"{requirement.Competence.Name} utløper snart");
            }

            var assignedHours = allShifts.Where(s => s.Date >= shift.Date.AddDays(-6) && s.Date <= shift.Date)
                .Where(s => s.Assignments.Any(a => a.EmployeeId == employee.Id))
                .Sum(s => (double)s.Hours);
            if (assignedHours >= 35) { warnings.Add("Høy planlagt belastning siste 7 dager"); score -= 20; }
            else if (assignedHours >= 30) { warnings.Add("Moderat høy belastning siste 7 dager"); score -= 10; }

            score -= hardFailures.Count * 40;
            score -= warnings.Count * 5;

            results.Add(new CandidateResult(
                employee.Id, employee.Name, employee.Role,
                Math.Max(0, score), hardFailures.Count == 0, hardFailures, warnings,
                assignedHours));
        }

        return results.OrderByDescending(x => x.Eligible).ThenByDescending(x => x.Score).ThenBy(x => x.RecentHours).ToList();
    }

    private static DateTime GetStart(Shift shift)
    {
        var time = shift.StartTime ?? shift.ShiftType.ToLowerInvariant() switch
        {
            "night" => new TimeOnly(22, 0),
            "evening" => new TimeOnly(15, 0),
            _ => new TimeOnly(7, 30)
        };
        return shift.Date.ToDateTime(time);
    }

    private static DateTime GetEnd(Shift shift)
    {
        var start = GetStart(shift);
        return start.AddHours((double)shift.Hours);
    }
}
