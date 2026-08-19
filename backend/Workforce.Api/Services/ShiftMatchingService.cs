using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;
using Workforce.Api.Models;

namespace Workforce.Api.Services;

public sealed class ShiftMatchingService(AppDbContext db)
{
    private static readonly Dictionary<string, int> LevelRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Basic"] = 1,
        ["Intermediate"] = 2,
        ["Advanced"] = 3
    };

    public async Task<IReadOnlyList<ShiftCandidateResult>> FindCandidatesAsync(int shiftId)
    {
        var shift = await db.Shifts
            .Include(x => x.Requirements)
                .ThenInclude(x => x.Competence)
            .Include(x => x.Assignments)
            .FirstOrDefaultAsync(x => x.Id == shiftId);

        if (shift is null)
            return [];

        var assignedEmployeeIds = shift.Assignments.Select(x => x.EmployeeId).ToHashSet();

        var employees = await db.Employees
            .Where(x => x.IsActive && !assignedEmployeeIds.Contains(x.Id))
            .Include(x => x.Competences)
            .Include(x => x.Availability)
            .ToListAsync();

        var results = employees
            .Select(employee => Evaluate(employee, shift))
            .Where(x => x.Eligible)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.EmployeeName)
            .ToList();

        return results;
    }

    private static ShiftCandidateResult Evaluate(Employee employee, Shift shift)
    {
        var availability = employee.Availability.FirstOrDefault(x => x.Date == shift.Date);
        if (availability is not null && !availability.IsAvailable)
        {
            return new(employee.Id, employee.Name, employee.Role, 0, false,
                ["Employee is marked unavailable for this date."]);
        }

        var existingCompetenceIds = employee.Competences
            .Where(x => !x.ValidUntil.HasValue || x.ValidUntil.Value >= shift.Date)
            .Select(x => x.CompetenceId)
            .ToHashSet();

        var reasons = new List<string>();
        var matched = 0;
        var score = 50;

        foreach (var requirement in shift.Requirements)
        {
            var competence = employee.Competences.FirstOrDefault(x =>
                x.CompetenceId == requirement.CompetenceId &&
                (!x.ValidUntil.HasValue || x.ValidUntil.Value >= shift.Date) &&
                LevelRank.GetValueOrDefault(x.Level, 1) >= LevelRank.GetValueOrDefault(requirement.MinimumLevel, 1));

            if (competence is null)
            {
                reasons.Add($"Missing {requirement.Competence.Name} at {requirement.MinimumLevel} level.");
                continue;
            }

            matched++;
            score += 20;
        }

        if (shift.Requirements.Count > 0 && matched < shift.Requirements.Count)
            return new(employee.Id, employee.Name, employee.Role, Math.Max(0, score), false, reasons);

        if (shift.Requirements.Count == 0)
            reasons.Add("No specific competence requirements defined for this shift.");
        else
            reasons.Add("All required competences are valid for the shift date.");

        score += Math.Min(30, (int)Math.Round(employee.PositionPercent / 10));

        return new(employee.Id, employee.Name, employee.Role, Math.Min(100, score), true, reasons);
    }
}

public sealed record ShiftCandidateResult(
    int EmployeeId,
    string EmployeeName,
    string Role,
    int Score,
    bool Eligible,
    IReadOnlyList<string> Reasons);
