using Workforce.Api.DTOs;
using Workforce.Api.Models;

namespace Workforce.Api.Services;

public sealed class CoverageService
{
    private static readonly Dictionary<string, int> LevelRank =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Basic"] = 1,
            ["Intermediate"] = 2,
            ["Advanced"] = 3
        };

    public ShiftCoverageResult AnalyzeShift(Shift shift)
    {
        var staffingCovered =
            shift.Assignments.Count >= shift.MinimumStaff;

        var requirementResults = shift.Requirements
            .Select(requirement =>
            {
                var minimumRank = LevelRank.GetValueOrDefault(
                    requirement.MinimumLevel,
                    1
                );

                var qualified = shift.Assignments.Count(assignment =>
                    assignment.Employee.Competences.Any(ec =>
                        ec.CompetenceId == requirement.CompetenceId &&
                        LevelRank.GetValueOrDefault(ec.Level, 1) >= minimumRank &&
                        (!ec.ValidUntil.HasValue ||
                         ec.ValidUntil.Value >= DateOnly.FromDateTime(DateTime.Today))
                    )
                );

                var covered = qualified >= requirement.MinimumCount;

                return new RequirementCoverageResult(
                    requirement.CompetenceId,
                    requirement.Competence.Name,
                    requirement.MinimumCount,
                    requirement.MinimumLevel,
                    qualified,
                    covered,
                    covered ? "COVERED" : "MISSING"
                );
            })
            .ToList();

        var coveredRequirements =
            requirementResults.Count(x => x.Covered);

        var competenceCoverage = requirementResults.Count == 0
            ? 100
            : (int)Math.Round(
                (decimal)coveredRequirements / requirementResults.Count * 100
            );

        var overallCovered =
            staffingCovered && requirementResults.All(x => x.Covered);

        return new ShiftCoverageResult(
            shift.Id,
            shift.Date,
            shift.ShiftType,
            shift.MinimumStaff,
            shift.Assignments.Count,
            staffingCovered,
            staffingCovered ? "COVERED" : "UNDERSTAFFED",
            Math.Max(0, shift.MinimumStaff - shift.Assignments.Count),
            competenceCoverage,
            overallCovered,
            overallCovered ? "GOOD" : "ACTION_REQUIRED",
            requirementResults
        );
    }
}
