namespace Workforce.Api.Models;

public sealed class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string Department { get; set; } = "";
    public string? Authorization { get; set; }
    public decimal PositionPercent { get; set; }
    public decimal MaxWeeklyHours { get; set; } = 37.5m;
    public bool IsActive { get; set; } = true;
    public List<EmployeeCompetence> Competences { get; set; } = [];
    public List<ShiftAssignment> ShiftAssignments { get; set; } = [];
    public List<Absence> Absences { get; set; } = [];
}
