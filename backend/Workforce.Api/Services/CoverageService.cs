using Workforce.Api.DTOs;
using Workforce.Api.Models;

namespace Workforce.Api.Services;

public sealed class CoverageService
{
    private static readonly Dictionary<string, int> LevelRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Basic"] = 1, ["Intermediate"] = 2, ["Advanced"] = 3
    };

    public ShiftCoverageResult AnalyzeShift(Shift shift)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var staffingCovered = shift.Assignments.Count >= shift.MinimumStaff;
        var warnings = new List<string>();

        var requirementResults = shift.Requirements.Select(requirement =>
        {
            var minimumRank = LevelRank.GetValueOrDefault(requirement.MinimumLevel, 1);
            var qualified = shift.Assignments.Count(a =>
            {
                var employee = a.Employee;
                if (requirement.RequiredRole is not null &&
                    !string.Equals(employee.Role, requirement.RequiredRole, StringComparison.OrdinalIgnoreCase)) return false;
                var ec = employee.Competences.FirstOrDefault(c => c.CompetenceId == requirement.CompetenceId);
                return ec is not null &&
                       LevelRank.GetValueOrDefault(ec.Level, 1) >= minimumRank &&
                       (!ec.ValidUntil.HasValue || ec.ValidUntil.Value >= shift.Date);
            });

            var covered = qualified >= requirement.MinimumCount;
            if (!covered && requirement.IsCritical)
                warnings.Add($"Kritisk kompetanse mangler: {requirement.Competence.Name}");
            else if (!covered)
                warnings.Add($"Kompetanse mangler: {requirement.Competence.Name}");

            return new RequirementCoverageResult(
                requirement.CompetenceId, requirement.Competence.Name,
                requirement.MinimumCount, requirement.MinimumLevel,
                qualified, covered, covered ? "COVERED" : "MISSING",
                requirement.RequiredRole, requirement.IsCritical);
        }).ToList();

        if (!staffingCovered)
            warnings.Add($"Bemanning: mangler {shift.MinimumStaff - shift.Assignments.Count} person(er)");

        var coveredRequirements = requirementResults.Count(x => x.Covered);
        var competenceCoverage = requirementResults.Count == 0 ? 100 :
            (int)Math.Round((decimal)coveredRequirements / requirementResults.Count * 100);
        var criticalMissing = requirementResults.Any(x => x.IsCritical && !x.Covered);
        var overallCovered = staffingCovered && requirementResults.All(x => x.Covered);
        var status = overallCovered ? "GREEN" : criticalMissing || !staffingCovered ? "RED" : "YELLOW";

        if (shift.Date < today) warnings.Add("Vakten ligger tilbake i tid");

        return new ShiftCoverageResult(
            shift.Id, shift.Date, shift.ShiftType, shift.Hours,
            shift.MinimumStaff, shift.Assignments.Count, staffingCovered,
            staffingCovered ? "COVERED" : "UNDERSTAFFED",
            Math.Max(0, shift.MinimumStaff - shift.Assignments.Count),
            competenceCoverage, overallCovered, status,
            shift.Assignments.OrderBy(x => x.Employee.Name)
                .Select(x => new ShiftAssignmentResult(x.EmployeeId, x.Employee.Name, x.Employee.Role)).ToList(),
            requirementResults, warnings);
    }
}
