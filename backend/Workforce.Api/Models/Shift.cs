namespace Workforce.Api.Models;

public sealed class Shift
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string ShiftType { get; set; } = "Day";
    public string Department { get; set; } = "";
    public TimeOnly? StartTime { get; set; }
    public decimal Hours { get; set; }
    public int MinimumStaff { get; set; }
    public bool IsPublished { get; set; }
    public bool IsCritical { get; set; }
    public List<ShiftAssignment> Assignments { get; set; } = [];
    public List<ShiftRequirement> Requirements { get; set; } = [];
}
