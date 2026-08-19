namespace Workforce.Api.Models;

public sealed class Absence
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public AbsenceType Type { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Description { get; set; }
    public bool IsApproved { get; set; }
    public string? ApprovedBySubject { get; set; }
    public DateTime? ApprovedAt { get; set; }
}

public enum AbsenceType
{
    SickLeave,
    Vacation,
    ParentalLeave,
    Education,
    Other
}
