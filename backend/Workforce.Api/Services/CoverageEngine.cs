using Workforce.Api.Models;

namespace Workforce.Api.Services;

public sealed record CoverageIssue(string Code, string Severity, string Message, string? Competence = null, int? Required = null, int? Actual = null);
public sealed record RequirementCoverageResult(int CompetenceId, string Competence, int Required, string MinimumLevel, int Qualified, bool Covered, string Status, IReadOnlyList<CoverageIssue> Issues);
public sealed record CoverageEngineResult(int ShiftId, bool IsReady, string Status, int Assigned, int MinimumStaff, IReadOnlyList<RequirementCoverageResult> Requirements, IReadOnlyList<CoverageIssue> Issues);

public sealed class CoverageEngine
{
    private static readonly Dictionary<string, int> LevelRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Basic"] = 1, ["Intermediate"] = 2, ["Advanced"] = 3
    };

    public CoverageEngineResult Analyze(Shift shift)
    {
        var issues = new List<CoverageIssue>();
        if (shift.Assignments.Count < shift.MinimumStaff)
            issues.Add(new("STAFFING_SHORTAGE", "RED", $"Mangler {shift.MinimumStaff - shift.Assignments.Count} person(er)", Required: shift.MinimumStaff, Actual: shift.Assignments.Count));

        var requirements = shift.Requirements.Select(r =>
        {
            var minimum = LevelRank.GetValueOrDefault(r.MinimumLevel, 1);
            var qualified = shift.Assignments.Count(a => a.Employee.IsActive &&
                a.Employee.Competences.Any(ec => ec.CompetenceId == r.CompetenceId &&
                    LevelRank.GetValueOrDefault(ec.Level, 1) >= minimum &&
                    (!ec.ValidUntil.HasValue || ec.ValidUntil.Value >= DateOnly.FromDateTime(DateTime.UtcNow.Date))));
            var covered = qualified >= r.MinimumCount;
            var requirementIssues = covered
                ? Array.Empty<CoverageIssue>()
                : new[] { new CoverageIssue("COMPETENCE_GAP", "RED", $"{r.Competence.Name}: {qualified}/{r.MinimumCount} kvalifisert", r.Competence.Name, r.MinimumCount, qualified) };
            return new RequirementCoverageResult(r.CompetenceId, r.Competence.Name, r.MinimumCount, r.MinimumLevel, qualified, covered, covered ? "COVERED" : "MISSING", requirementIssues);
        }).ToList();

        foreach (var requirement in requirements) issues.AddRange(requirement.Issues);
        var status = issues.Any(x => x.Severity == "RED") ? "RED" : issues.Count > 0 ? "YELLOW" : "GREEN";
        return new CoverageEngineResult(shift.Id, status == "GREEN", status, shift.Assignments.Count, shift.MinimumStaff, requirements, issues);
    }
}
