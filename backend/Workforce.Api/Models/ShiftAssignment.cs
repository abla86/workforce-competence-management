namespace Workforce.Api.Models;

public sealed class ShiftAssignment
{
    public int ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
}
