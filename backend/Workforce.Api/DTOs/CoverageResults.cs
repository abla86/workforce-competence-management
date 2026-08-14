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

public sealed record ShiftAssignmentResult(
    int EmployeeId,
    string Name,
    string Role
);

public sealed record ShiftCoverageResult(
    int Id,
    DateOnly Date,
    string ShiftType,
    decimal Hours,
    int MinimumStaff,
    int AssignedStaff,
    bool StaffingCovered,
    string StaffingStatus,
    int MissingStaff,
    int CompetenceCoverage,
    bool OverallCovered,
    string OverallStatus,
    IReadOnlyList<ShiftAssignmentResult> Assignments,
    IReadOnlyList<RequirementCoverageResult> Requirements
);
