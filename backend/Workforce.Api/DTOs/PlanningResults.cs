namespace Workforce.Api.DTOs;

public sealed record CandidateResult(
    int EmployeeId,
    string Name,
    string Role,
    int Score,
    bool Eligible,
    IReadOnlyList<string> HardFailures,
    IReadOnlyList<string> Warnings,
    double RecentHours);

public sealed record ScenarioAbsenceRequest(int EmployeeId, DateOnly Date);
public sealed record ScenarioResult(
    int ShiftId,
    bool WouldBeCovered,
    int StaffGap,
    int CompetenceGapCount,
    IReadOnlyList<CandidateResult> ReplacementCandidates,
    IReadOnlyList<string> Reasons);
