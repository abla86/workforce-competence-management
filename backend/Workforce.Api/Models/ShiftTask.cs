namespace Workforce.Api.Models;

public sealed class ShiftTask
{
    public int Id { get; set; }
    public int ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;
    public int WorkTaskId { get; set; }
    public WorkTask WorkTask { get; set; } = null!;

    // Immutable snapshot of the requirement at the time the task is added to the shift.
    public int RequiredCount { get; set; }
    public int MinCompetenceLevel { get; set; }
    public bool IsCritical { get; set; }

    public List<ShiftTaskCoverage> ShiftTaskCoverages { get; set; } = [];
}
