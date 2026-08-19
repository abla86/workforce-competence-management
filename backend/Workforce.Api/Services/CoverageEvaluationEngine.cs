using Workforce.Api.Models;

namespace Workforce.Api.Services;

public sealed class CoverageEvaluationEngine
{
    public CoverageResult Evaluate(Shift shift)
    {
        var result = new CoverageResult();

        foreach (var shiftTask in shift.ShiftTasks)
            result.Tasks.Add(EvaluateTask(shiftTask, shift));

        result.Status = DetermineOverallStatus(result.Tasks);
        return result;
    }

    private static TaskCoverageDetail EvaluateTask(ShiftTask shiftTask, Shift shift)
    {
        var assignments = shift.Assignments
            .Where(a => a.ShiftTaskId == shiftTask.Id)
            .ToList();

        var detail = new TaskCoverageDetail
        {
            TaskName = shiftTask.WorkTask.Name,
            Required = shiftTask.RequiredCount,
            Actual = assignments.Count,
            Critical = shiftTask.IsCritical
        };

        if (assignments.Count < shiftTask.RequiredCount)
        {
            detail.Gaps.Add(new CoverageGap
            {
                Type = GapType.InsufficientStaff,
                Description = $"Trenger {shiftTask.RequiredCount}, har {assignments.Count}"
            });
        }

        foreach (var assignment in assignments)
            ValidateAssignment(assignment, shiftTask, detail);

        return detail;
    }

    private static void ValidateAssignment(ShiftTaskAssignment assignment, ShiftTask shiftTask, TaskCoverageDetail detail)
    {
        var employee = assignment.Employee;

        if (!string.IsNullOrWhiteSpace(shiftTask.WorkTask.RequiredRole) &&
            !string.Equals(employee.Role, shiftTask.WorkTask.RequiredRole, StringComparison.OrdinalIgnoreCase))
        {
            detail.Gaps.Add(new CoverageGap
            {
                Type = GapType.MissingRole,
                EmployeeId = employee.Id,
                Description = $"{employee.Name} har rolle {employee.Role}, krever {shiftTask.WorkTask.RequiredRole}"
            });
        }

        if (!string.IsNullOrWhiteSpace(shiftTask.WorkTask.RequiredAuthorization) &&
            !string.Equals(employee.Authorization, shiftTask.WorkTask.RequiredAuthorization, StringComparison.OrdinalIgnoreCase))
        {
            detail.Gaps.Add(new CoverageGap
            {
                Type = GapType.UnauthorizedRole,
                EmployeeId = employee.Id,
                Description = $"{employee.Name} mangler autorisasjon {shiftTask.WorkTask.RequiredAuthorization}"
            });
        }

        if (shiftTask.WorkTask.CompetenceId is null)
            return;

        var competence = employee.Competences.FirstOrDefault(c => c.CompetenceId == shiftTask.WorkTask.CompetenceId);
        if (competence is null)
        {
            detail.Gaps.Add(new CoverageGap
            {
                Type = GapType.MissingCompetence,
                EmployeeId = employee.Id,
                Description = $"{employee.Name} mangler nødvendig kompetanse"
            });
            return;
        }

        if (competence.Level < shiftTask.MinCompetenceLevel)
        {
            detail.Gaps.Add(new CoverageGap
            {
                Type = GapType.MissingCompetence,
                EmployeeId = employee.Id,
                Description = $"{employee.Name} har utilstrekkelig kompetansenivå"
            });
        }

        if (competence.ValidUntil.HasValue && competence.ValidUntil.Value < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            detail.Gaps.Add(new CoverageGap
            {
                Type = GapType.CompetenceExpired,
                EmployeeId = employee.Id,
                Description = $"Kompetansen til {employee.Name} er utløpt"
            });
        }
    }

    private static CoverageStatus DetermineOverallStatus(IEnumerable<TaskCoverageDetail> tasks)
    {
        var list = tasks.ToList();
        if (list.Any(t => t.Critical && t.Gaps.Count > 0)) return CoverageStatus.Red;
        if (list.Any(t => t.Gaps.Count > 0)) return CoverageStatus.Yellow;
        return CoverageStatus.Green;
    }
}
