namespace Workforce.Api.Models;

public sealed class ShiftRequirement
{
    public int ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;
    public int CompetenceId { get; set; }
    public Competence Competence { get; set; } = null!;
    public int MinimumCount { get; set; } = 1;
    public string MinimumLevel { get; set; } = "Basic";
    public string? RequiredRole { get; set; }
    public bool IsCritical { get; set; }
}
