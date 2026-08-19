namespace Workforce.Api.Models;

public sealed class ShiftRule
{
    public int Id { get; set; }
    public RuleType RuleType { get; set; }
    public int MinimumRestHours { get; set; } = 11;
    public bool AllowDispensation { get; set; } = true;
    public int MaxDispensationHours { get; set; } = 4;
    public int MaxDispensationsPerMonth { get; set; } = 3;
    public bool IsActive { get; set; } = true;
}

public enum RuleType
{
    MinimumRest,
    EveningMorningBlock,
    MaxWeeklyHours,
    MaxNightShifts,
    CompetenceRequired
}
