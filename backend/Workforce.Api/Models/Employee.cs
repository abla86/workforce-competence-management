namespace Workforce.Api.Models;

public sealed class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public decimal PositionPercent { get; set; }
    public bool IsActive { get; set; } = true;
    public List<EmployeeCompetence> Competences { get; set; } = [];
    public List<ShiftAssignment> ShiftAssignments { get; set; } = [];
    public List<EmployeeAvailability> Availability { get; set; } = [];
}
