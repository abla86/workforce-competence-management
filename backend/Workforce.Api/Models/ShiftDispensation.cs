namespace Workforce.Api.Models;

public sealed class ShiftDispensation
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;
    public RuleType BreachedRule { get; set; }
    public int HoursGranted { get; set; }
    public string Reason { get; set; } = "";
    public string GrantedBySubject { get; set; } = "";
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DispensationStatus Status { get; set; } = DispensationStatus.Pending;
    public string? Comments { get; set; }
}

public enum DispensationStatus
{
    Pending,
    Approved,
    Rejected,
    Expired
}
