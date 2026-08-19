namespace Workforce.Api.Models;

public sealed class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public int DepartmentId { get; set; }
    public string? Authorization { get; set; }
    public DateTime? AuthorizationExpiry { get; set; }
    public decimal PositionPercent { get; set; }
    public double WeeklyContractHours { get; set; } = 37.5;
    public bool CanWorkMorning { get; set; } = true;
    public bool CanWorkDay { get; set; } = true;
    public bool CanWorkEvening { get; set; } = true;
    public bool CanWorkNight { get; set; } = true;
    public string? PreferredShiftType { get; set; }
    public bool IsActive { get; set; } = true;
    public string? IdentitySubject { get; set; }
    public double ExpectedMonthlyHours => WeeklyContractHours * (double)PositionPercent / 100.0 * 4.33;
    public List<EmployeeCompetence> Competences { get; set; } = [];
    public List<ShiftAssignment> ShiftAssignments { get; set; } = [];
    public List<EmployeeAvailability> Availability { get; set; } = [];
    public List<Absence> Absences { get; set; } = [];
    public List<EmployeeTimeRecord> TimeRecords { get; set; } = [];
    public EmployeeStatus? Status { get; set; }
}
