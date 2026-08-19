using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;
using Workforce.Api.Models;

namespace Workforce.Api.Services;

public sealed class CoverageEvaluationEngine
{
    private const int DefaultMinimumRestHours = 11;
    private readonly AppDbContext _db;
    private readonly ILogger<CoverageEvaluationEngine> _logger;

    public CoverageEvaluationEngine(AppDbContext db, ILogger<CoverageEvaluationEngine> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CoverageResult> EvaluateAsync(int shiftId, string triggeredBy = "system", bool writeAudit = true)
    {
        var shift = await LoadShiftAsync(shiftId);
        if (shift is null) throw new ArgumentException($"Shift {shiftId} not found");

        var result = new CoverageResult();
        foreach (var task in shift.ShiftTasks.OrderByDescending(x => x.IsCritical).ThenBy(x => x.WorkTask.Name))
            result.Tasks.Add(await EvaluateTaskAsync(shift, task));

        // Keep the existing minimum-staff rule as a separate global safety check.
        if (shift.Assignments.Count < shift.MinimumStaff)
        {
            result.Warnings.Add($"Vakten har {shift.Assignments.Count} ansatte, men krever minst {shift.MinimumStaff}.");
            if (result.Tasks.Count == 0)
                result.Status = CoverageStatus.Red;
        }

        result.Status = DetermineOverallStatus(result, shift.Assignments.Count < shift.MinimumStaff);

        if (writeAudit)
            await LogAuditAsync(shiftId, result, triggeredBy);

        return result;
    }

    public async Task<CoverageResult> EvaluateScenarioWithoutEmployeesAsync(int shiftId, IReadOnlyCollection<int> employeeIds)
    {
        var shift = await LoadShiftAsync(shiftId);
        if (shift is null) throw new ArgumentException($"Shift {shiftId} not found");

        var excluded = employeeIds.ToHashSet();
        foreach (var task in shift.ShiftTasks)
            task.ShiftTaskCoverages = task.ShiftTaskCoverages.Where(x => !excluded.Contains(x.EmployeeId)).ToList();
        shift.Assignments = shift.Assignments.Where(x => !excluded.Contains(x.EmployeeId)).ToList();

        var result = new CoverageResult();
        foreach (var task in shift.ShiftTasks)
            result.Tasks.Add(await EvaluateTaskAsync(shift, task));

        result.Status = DetermineOverallStatus(result, shift.Assignments.Count < shift.MinimumStaff);
        return result;
    }

    public async Task<List<SuggestedReplacement>> FindQualifiedReplacementsAsync(int shiftId, IReadOnlyCollection<int> excludedEmployeeIds)
    {
        var shift = await LoadShiftAsync(shiftId);
        if (shift is null) throw new ArgumentException($"Shift {shiftId} not found");

        var excluded = excludedEmployeeIds.ToHashSet();
        var assigned = shift.Assignments.Select(x => x.EmployeeId).ToHashSet();
        var candidates = await _db.Employees
            .Include(e => e.Competences)
            .Include(e => e.Availability)
            .Where(e => e.IsActive && !excluded.Contains(e.Id) && !assigned.Contains(e.Id))
            .ToListAsync();

        var replacements = new List<SuggestedReplacement>();
        foreach (var employee in candidates)
        {
            var missing = new List<string>();
            var levels = new List<int>();

            foreach (var task in shift.ShiftTasks)
            {
                if (employee.Availability.FirstOrDefault(a => a.Date == shift.Date) is { IsAvailable: false } unavailable)
                {
                    missing.Add($"Ikke tilgjengelig: {unavailable.Reason}");
                    continue;
                }

                if (task.WorkTask.CompetenceId is int competenceId)
                {
                    var competence = employee.Competences.FirstOrDefault(x => x.CompetenceId == competenceId);
                    if (competence is null)
                    {
                        missing.Add($"Mangler kompetanse: {task.WorkTask.Name}");
                        continue;
                    }

                    var level = LevelRank(competence.Level);
                    levels.Add(level);
                    if (level < task.MinCompetenceLevel)
                        missing.Add($"For lavt nivå i {task.WorkTask.Name}: {competence.Level}");
                    if (competence.ValidUntil.HasValue && competence.ValidUntil.Value < shift.Date)
                        missing.Add($"Utløpt kompetanse: {task.WorkTask.Name}");
                }

                if (!string.IsNullOrWhiteSpace(task.WorkTask.RequiredRole) &&
                    !string.Equals(employee.Role, task.WorkTask.RequiredRole, StringComparison.OrdinalIgnoreCase))
                    missing.Add($"Rolle krever {task.WorkTask.RequiredRole}");
            }

            if (await HasSchedulingConflictAsync(employee.Id, shift))
                missing.Add("Har overlappende vakt");
            if (await ViolatesRestAsync(employee.Id, shift))
                missing.Add($"Har mindre enn {DefaultMinimumRestHours} timer hvile");

            replacements.Add(new SuggestedReplacement
            {
                EmployeeId = employee.Id,
                EmployeeName = employee.Name,
                Role = employee.Role,
                CompetenceLevel = levels.DefaultIfEmpty(0).Max(),
                Available = missing.Count == 0,
                MissingRequirements = missing.Distinct().ToList()
            });
        }

        return replacements
            .OrderByDescending(x => x.Available)
            .ThenBy(x => x.MissingRequirements.Count)
            .ThenBy(x => x.EmployeeName)
            .ToList();
    }

    private async Task<Shift?> LoadShiftAsync(int shiftId) => await _db.Shifts
        .Include(s => s.Assignments).ThenInclude(a => a.Employee).ThenInclude(e => e.Competences)
        .Include(s => s.Assignments).ThenInclude(a => a.Employee).ThenInclude(e => e.Availability)
        .Include(s => s.ShiftTasks).ThenInclude(st => st.WorkTask).ThenInclude(w => w.Competence)
        .Include(s => s.ShiftTasks).ThenInclude(st => st.ShiftTaskCoverages).ThenInclude(sc => sc.Employee).ThenInclude(e => e.Competences)
        .FirstOrDefaultAsync(s => s.Id == shiftId);

    private async Task<TaskCoverageDetail> EvaluateTaskAsync(Shift shift, ShiftTask task)
    {
        var detail = new TaskCoverageDetail
        {
            TaskName = task.WorkTask.Name,
            CompetenceName = task.WorkTask.Competence?.Name ?? "",
            Required = task.RequiredCount,
            Critical = task.IsCritical
        };

        var validCoverages = new List<ShiftTaskCoverage>();
        foreach (var coverage in task.ShiftTaskCoverages)
        {
            var employee = coverage.Employee;
            var gaps = ValidateCoverage(shift, task, coverage);
            if (gaps.Count == 0) validCoverages.Add(coverage);
            detail.Gaps.AddRange(gaps);
        }

        detail.Actual = validCoverages.Count;
        if (detail.Actual < detail.Required)
        {
            detail.Gaps.Add(new CoverageGap
            {
                Type = GapType.InsufficientStaff,
                Description = $"Trenger {detail.Required} kvalifiserte, har {detail.Actual}"
            });
        }

        await AddScheduleGapsAsync(shift, validCoverages, detail);
        return detail;
    }

    private List<CoverageGap> ValidateCoverage(Shift shift, ShiftTask task, ShiftTaskCoverage coverage)
    {
        var employee = coverage.Employee;
        var gaps = new List<CoverageGap>();
        var prefix = new { EmployeeId = employee.Id, EmployeeName = employee.Name };

        if (employee.Availability.FirstOrDefault(a => a.Date == shift.Date) is { IsAvailable: false } unavailable)
            gaps.Add(new CoverageGap { Type = GapType.EmployeeUnavailable, EmployeeId = prefix.EmployeeId, EmployeeName = prefix.EmployeeName, Description = $"{employee.Name} er ikke tilgjengelig{(string.IsNullOrWhiteSpace(unavailable.Reason) ? "" : $": {unavailable.Reason}")}" });

        if (!string.IsNullOrWhiteSpace(task.WorkTask.RequiredRole) && !string.Equals(employee.Role, task.WorkTask.RequiredRole, StringComparison.OrdinalIgnoreCase))
            gaps.Add(new CoverageGap { Type = GapType.UnauthorizedRole, EmployeeId = prefix.EmployeeId, EmployeeName = prefix.EmployeeName, Description = $"{employee.Name} har rolle {employee.Role}, krever {task.WorkTask.RequiredRole}" });

        if (task.WorkTask.CompetenceId is int competenceId)
        {
            var competence = employee.Competences.FirstOrDefault(x => x.CompetenceId == competenceId);
            if (competence is null)
                gaps.Add(new CoverageGap { Type = GapType.MissingCompetence, EmployeeId = prefix.EmployeeId, EmployeeName = prefix.EmployeeName, Description = $"{employee.Name} mangler kompetanse for {task.WorkTask.Name}" });
            else
            {
                if (LevelRank(competence.Level) < task.MinCompetenceLevel)
                    gaps.Add(new CoverageGap { Type = GapType.MissingCompetence, EmployeeId = prefix.EmployeeId, EmployeeName = prefix.EmployeeName, Description = $"{employee.Name} har nivå {competence.Level}, krever minst {task.MinCompetenceLevel}" });
                if (competence.ValidUntil.HasValue && competence.ValidUntil.Value < shift.Date)
                    gaps.Add(new CoverageGap { Type = GapType.CompetenceExpired, EmployeeId = prefix.EmployeeId, EmployeeName = prefix.EmployeeName, Description = $"{employee.Name} sin kompetanse utløp {competence.ValidUntil:dd.MM.yyyy}" });
            }
        }

        if (coverage.AuthorizationExpiry.HasValue && coverage.AuthorizationExpiry.Value.Date < shift.Date)
            gaps.Add(new CoverageGap { Type = GapType.AuthorizationExpired, EmployeeId = prefix.EmployeeId, EmployeeName = prefix.EmployeeName, Description = $"{employee.Name} sin autorisasjon er utgått" });

        return gaps;
    }

    private async Task AddScheduleGapsAsync(Shift shift, List<ShiftTaskCoverage> coverages, TaskCoverageDetail detail)
    {
        if (shift.StartTime is null || shift.EndTime is null) return;
        foreach (var coverage in coverages)
        {
            if (await HasSchedulingConflictAsync(coverage.EmployeeId, shift))
                detail.Gaps.Add(new CoverageGap { Type = GapType.DoubleBooked, EmployeeId = coverage.EmployeeId, EmployeeName = coverage.Employee.Name, Description = $"{coverage.Employee.Name} har overlappende vakt" });
            if (await ViolatesRestAsync(coverage.EmployeeId, shift))
                detail.Gaps.Add(new CoverageGap { Type = GapType.RestPeriodViolation, EmployeeId = coverage.EmployeeId, EmployeeName = coverage.Employee.Name, Description = $"{coverage.Employee.Name} har mindre enn {DefaultMinimumRestHours} timer hvile" });
        }
    }

    private async Task<bool> HasSchedulingConflictAsync(int employeeId, Shift shift)
    {
        if (shift.StartTime is null || shift.EndTime is null) return false;
        return await _db.Shifts.AnyAsync(s => s.Id != shift.Id && s.StartTime.HasValue && s.EndTime.HasValue &&
            s.StartTime < shift.EndTime && s.EndTime > shift.StartTime &&
            (s.Assignments.Any(a => a.EmployeeId == employeeId) || s.ShiftTaskCoverages().Any(sc => sc.EmployeeId == employeeId)));
    }

    private async Task<bool> ViolatesRestAsync(int employeeId, Shift shift)
    {
        if (shift.StartTime is null) return false;
        var previous = await _db.Shifts
            .Where(s => s.Id != shift.Id && s.EndTime.HasValue && s.EndTime <= shift.StartTime &&
                        (s.Assignments.Any(a => a.EmployeeId == employeeId) || s.ShiftTaskCoverages().Any(sc => sc.EmployeeId == employeeId)))
            .OrderByDescending(s => s.EndTime)
            .FirstOrDefaultAsync();
        return previous?.EndTime is DateTime end && (shift.StartTime.Value - end).TotalHours < DefaultMinimumRestHours;
    }

    private static int LevelRank(string level) => int.TryParse(level, out var numeric)
        ? numeric
        : level.Trim().ToLowerInvariant() switch
        {
            "basic" => 1,
            "intermediate" => 2,
            "advanced" => 3,
            "expert" => 4,
            _ => 0
        };

    private static CoverageStatus DetermineOverallStatus(CoverageResult result, bool understaffed)
    {
        if (understaffed && result.Tasks.Count == 0) return CoverageStatus.Red;
        if (result.Tasks.Any(t => t.Critical && t.Gaps.Any())) return CoverageStatus.Red;
        if (understaffed || result.Tasks.Any(t => t.Gaps.Any())) return CoverageStatus.Yellow;
        return CoverageStatus.Green;
    }

    private async Task LogAuditAsync(int shiftId, CoverageResult result, string triggeredBy)
    {
        _db.CoverageAuditEntries.Add(new CoverageAuditEntry
        {
            ShiftId = shiftId,
            EvaluatedAt = DateTime.UtcNow,
            Status = result.Status.ToString(),
            DetailsJson = System.Text.Json.JsonSerializer.Serialize(result),
            TriggeredBy = triggeredBy
        });
        await _db.SaveChangesAsync();
        _logger.LogInformation("Coverage evaluated for shift {ShiftId}: {Status}", shiftId, result.Status);
    }
}
