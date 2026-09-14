using System.ComponentModel.DataAnnotations;

namespace Workforce.Api.Models;

public sealed class Competence
{
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = "";

    public string Category { get; set; } = "";
    public List<EmployeeCompetence> Employees { get; set; } = [];
    public List<ShiftRequirement> ShiftRequirements { get; set; } = [];
}
