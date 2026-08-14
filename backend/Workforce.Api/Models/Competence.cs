namespace Workforce.Api.Models;

public sealed class Competence
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public List<EmployeeCompetence> Employees { get; set; } = [];
    public List<ShiftRequirement> ShiftRequirements { get; set; } = [];
}
