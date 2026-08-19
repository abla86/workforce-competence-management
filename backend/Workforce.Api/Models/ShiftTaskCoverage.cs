namespace Vaktklar.Domain.Models.Coverage;

public class ShiftTaskCoverage
{
    public int Id { get; set; }

    public int ShiftTaskId { get; set; }
    public ShiftTask ShiftTask { get; set; } = null!;

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int RequiredCount { get; set; }
    public int MinCompetenceLevel { get; set; }
    public bool IsCritical { get; set; }

    public DateTime? AuthorizationExpiry { get; set; }
    public bool IsValid { get; set; }
    public string? InvalidReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
