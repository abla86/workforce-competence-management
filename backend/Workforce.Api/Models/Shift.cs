namespace Workforce.Api.Models;

public sealed class Shift
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string ShiftType { get; set; } = "Day";
    public decimal Hours { get; set; }
    public int MinimumStaff { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<ShiftAssignment> Assignments { get; set; } = [];
    public List<ShiftRequirement> Requirements { get; set; } = [];
    public List<ShiftTask> ShiftTasks { get; set; } = [];
    public List<CoverageAuditEntry> CoverageAudits { get; set; } = [];
}
