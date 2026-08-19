using Workforce.Api.Models;

namespace Workforce.Api.Services;

public sealed record CoverageIssue(string Code, string Severity, string Message, string? Task = null, int? Required = null, int? Actual = null);
public sealed record TaskCoverageResult(int TaskId, string TaskName, bool Covered, int Required, int Qualified, IReadOnlyList<CoverageIssue> Issues);
public sealed record ShiftCoverageResult(int ShiftId, bool IsReady, string Status, int Assigned, int MinimumStaff, IReadOnlyList<TaskCoverageResult> Tasks, IReadOnlyList<CoverageIssue> Issues);

public sealed class CoverageEngine
{
    private static readonly string[] LevelOrder = ["Basic", "Intermediate", "Advanced"];

    public ShiftCoverageResult Analyze(
        Shift shift,
        IReadOnlyCollection<WorkTask> tasks,
        IReadOnlyCollection<Employee> employees)
    {
        var assignments = shift.Assignments.Select(a => a.Employee).Where(e => e.IsActive).ToList();
        var issues = new List<CoverageIssue>();
        if (assignments.Count < shift.MinimumStaff)
            issues.Add(new("STAFFING_SHORTAGE", "RED", $"Mangler {shift.MinimumStaff - assignments.Count} person(er)", Required: shift.MinimumStaff, Actual: assignments.Count));

        var taskResults = new List<TaskCoverageResult>();
        foreach (var task in tasks)
        {
            var eligible = assignments.Where(e => HasRole(e, task.RequiredRole) && HasAuthorization(e, task.RequiredAuthorization) && HasCompetence(e, task.RequiredCompetence, task.MinimumLevel)).ToList();
            var taskIssues = new List<CoverageIssue>();
            if (eligible.Count < task.RequiredCount)
            {
                taskIssues.Add(new("TASK_COVERAGE", task.IsCritical ? "RED" : "YELLOW",
                    $"{task.Name}: {eligible.Count}/{task.RequiredCount} kvalifisert",
                    task.Name, task.RequiredCount, eligible.Count));
            }
            var covered = eligible.Count >= task.RequiredCount;
            taskResults.Add(new(task.Id, task.Name, covered, task.RequiredCount, eligible.Count, taskIssues));
            issues.AddRange(taskIssues);
        }

        var status = issues.Any(i => i.Severity == "RED") ? "RED" : issues.Count > 0 ? "YELLOW" : "GREEN";
        return new(shift.Id, status == "GREEN", status, assignments.Count, shift.MinimumStaff, taskResults, issues);
    }

    private static bool HasRole(Employee employee, string? role) => string.IsNullOrWhiteSpace(role) || employee.Role.Equals(role, StringComparison.OrdinalIgnoreCase);
    private static bool HasAuthorization(Employee employee, string? authorization) => string.IsNullOrWhiteSpace(authorization) || employee.Authorization?.Equals(authorization, StringComparison.OrdinalIgnoreCase) == true;
    private static bool HasCompetence(Employee employee, string? competence, string minimumLevel)
    {
        if (string.IsNullOrWhiteSpace(competence)) return true;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var record = employee.Competences.FirstOrDefault(c => c.Competence.Name.Equals(competence, StringComparison.OrdinalIgnoreCase));
        if (record is null || (record.ValidUntil.HasValue && record.ValidUntil.Value < today)) return false;
        return LevelOrder.IndexOf(record.Level) >= LevelOrder.IndexOf(minimumLevel);
    }
}
