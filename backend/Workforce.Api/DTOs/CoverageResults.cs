namespace Workforce.Api.DTOs;

public sealed record RequirementCoverageResult(
    int CompetenceId,
    string Competence,
    int MinimumCount,
    string MinimumLevel,
    int QualifiedCount,
    bool Covered,
    string Status
);

public sealed record ShiftCoverageResult(
    int Id,
    DateOnly Date,
    string ShiftType,
    int MinimumStaff,
    int AssignedStaff,
    bool StaffingCovered,
    string StaffingStatus,
    int MissingStaff,
    int CompetenceCoverage,
    bool OverallCovered,
    string OverallStatus,
    IReadOnlyList<RequirementCoverageResult> Requirements
);
