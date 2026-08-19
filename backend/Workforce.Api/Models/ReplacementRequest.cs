namespace Workforce.Api.Models;

public sealed class ReplacementRequest
{
    public int ShiftTaskCoverageId { get; set; }
    public int EmployeeId { get; set; }
}

public sealed class SuggestedReplacement
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public string Role { get; set; } = "";
    public int CompetenceLevel { get; set; }
    public bool Available { get; set; }
    public List<string> MissingRequirements { get; set; } = [];
}
