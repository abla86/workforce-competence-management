namespace Workforce.Api.Models;

public enum AbsenceType { Sick, Vacation, Leave, Other }

public sealed class Absence
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public AbsenceType Type { get; set; }
    public string? Note { get; set; }
    public bool Approved { get; set; } = true;
}
