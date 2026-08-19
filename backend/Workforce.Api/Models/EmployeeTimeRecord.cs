namespace Workforce.Api.Models;

public sealed class EmployeeTimeRecord
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    public double ContractedHours { get; set; }
    public double ScheduledHours { get; set; }
    public double ActualHours { get; set; }
    public double OvertimeHours => Math.Max(0, ActualHours - ScheduledHours);
    public double UndertimeHours => Math.Max(0, ScheduledHours - ActualHours);
    public double RegularOvertime => Math.Min(OvertimeHours, 8);
    public double ExcessOvertime => Math.Max(0, OvertimeHours - 8);
    public string? Notes { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
