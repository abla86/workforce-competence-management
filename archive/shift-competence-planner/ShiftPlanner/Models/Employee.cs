namespace ShiftPlanner.Models;

public sealed record Employee(
    int Id,
    string Name,
    string Role,
    decimal PositionPercent,
    IReadOnlyList<string> Competencies
);
