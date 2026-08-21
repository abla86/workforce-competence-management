namespace Workforce.Api.Models;

public sealed class EmployeeCompetence
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int CompetenceId { get; set; }
    public Competence Competence { get; set; } = null!;
    public CompetenceLevel Level { get; set; } = CompetenceLevel.Basic;
    public DateOnly? ValidUntil { get; set; }
}
