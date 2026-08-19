namespace Vaktklar.Domain.Models.Coverage;

public class ReplacementRequest
{
    public int ShiftTaskCoverageId { get; set; }
    public int EmployeeId { get; set; }
}

public class SuggestedReplacement
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public int CompetenceLevel { get; set; }
    public int AvailableSlots { get; set; }
    public List<string> MissingRequirements { get; set; } = new();
}
