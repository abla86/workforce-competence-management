namespace ShiftPlanner.Models;

public sealed record Shift(
    int Id,
    DateOnly Date,
    string ShiftType,
    decimal Hours,
    int MinimumStaff,
    IReadOnlyList<string> RequiredCompetencies,
    IReadOnlyList<int> EmployeeIds
);
