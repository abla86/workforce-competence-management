namespace Workforce.Api.Models;

public sealed class WorkTask
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? RequiredRole { get; set; }
    public string? RequiredAuthorization { get; set; }
    public int? CompetenceId { get; set; }
    public Competence? Competence { get; set; }
    public int MinimumLevel { get; set; } = 1;
    public int RequiredCount { get; set; } = 1;
    public bool IsCritical { get; set; }
    public bool IsActive { get; set; } = true;
    public List<ShiftTask> ShiftTasks { get; set; } = [];
}
