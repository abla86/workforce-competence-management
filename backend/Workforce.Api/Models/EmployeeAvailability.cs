namespace Workforce.Api.Models;

public sealed class EmployeeAvailability
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateOnly Date { get; set; }
    public bool IsAvailable { get; set; }
    public string Reason { get; set; } = "";
}
