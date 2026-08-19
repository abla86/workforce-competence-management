using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;
using Workforce.Api.Models;

namespace Workforce.Api.Services;

public sealed class CoverageEvaluationEngine
{
    private const int MinimumRestHours = 11;
    private readonly AppDbContext _db;
    private readonly AuditProtectionService _auditProtection;
    private readonly ILogger<CoverageEvaluationEngine> _logger;

    public CoverageEvaluationEngine(AppDbContext db, AuditProtectionService auditProtection, ILogger<CoverageEvaluationEngine> logger)
    {
        _db = db;
        _auditProtection = auditProtection;
        _logger = logger;
    }

    public async Task<CoverageResult> EvaluateAsync(int shiftId, string triggeredBy, HttpContext? http = null, bool writeAudit = true)
    {
        var shift = await LoadShiftAsync(shiftId);
        if (shift is null) throw new ArgumentException($"Shift {shiftId} not found");

        var result = new CoverageResult();
        foreach (var task in shift.ShiftTasks.OrderByDescending(x => x.IsCritical).ThenBy(x => x.WorkTask.Name))
            result.Tasks.Add(await EvaluateTaskAsync(shift, task));

        var understaffed = shift.Assignments.Count < shift.MinimumStaff;
        if (understaffed)
            result.Warnings.Add($"Vakten har {shift.Assignments.Count} ansatte, men krever minst {shift.MinimumStaff}.");

        result.Status = DetermineStatus(result, understaffed);
        if (writeAudit)
        {
            var entry = new CoverageAuditEntry
            {
                ShiftId = shiftId,
                EvaluatedAt = DateTime.UtcNow,
                Status = result.Status.ToString(),
                TriggeredBy = triggeredBy,
                ClientIp = http?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = http?.Request.Headers.UserAgent.ToString()
            };
            _auditProtection.ProtectResult(entry, result);
            _db.CoverageAuditEntries.Add(entry);
            await _db.SaveChangesAsync();
        }

        _logger.LogInformation("Coverage evaluated for shift {ShiftId}: {Status}", shiftId, result.Status);
        return result;
    }

    public async Task<CoverageResult> EvaluateScenarioWithoutEmployeesAsync(int shiftId, IReadOnlyCollection<int> employeeIds)
    {
        var shift = await LoadShiftAsync(shiftId);
        if (shift is null) throw new ArgumentException($"Shift {shiftId} not found");
        var excluded = employeeIds.ToHashSet();
        var originalAssignments = shift.Assignments;
        var originalCoverages = shift.ShiftTasks.ToDictionary(x => x.Id, x => x.ShiftTaskCoverages);

        try
        {
            shift.Assignments = originalAssignments.Where(x => !excluded.Contains(x.EmployeeId)).ToList();
            foreach (var task in shift.ShiftTasks)
                task.ShiftTaskCoverages = originalCoverages[task.Id].Where(x => !excluded.Contains(x.EmployeeId)).ToList();

            var result = new CoverageResult();
            foreach (var task in shift.ShiftTasks)
                result.Tasks.Add(await EvaluateTaskAsync(shift, task));
            result.Status = DetermineStatus(result, shift.Assignments.Count < shift.MinimumStaff);
            return result;
        }
        finally
        {
            shift.Assignments = originalAssignments;
            foreach (var task in shift.ShiftTasks)
                task.ShiftTaskCoverages = originalCoverages[task.Id];
        }
    }

    public async Task<List<SuggestedReplacement>> FindQualifiedReplacementsAsync(int shiftId, IReadOnlyCollection<int> excludedEmployeeIds)
    {
        var shift = await LoadShiftAsync(shiftId);
        if (shift is null) throw new ArgumentException($"Shift {shiftId} not found");
        var excluded = excludedEmployeeIds.ToHashSet();
        var assigned = shift.Assignments.Select(x => x.EmployeeId).ToHashSet();

        var candidates = await _db.Employees
            .Include(x => x.Competences)
            .Include(x => x.Availability)
            .Where(x => x.IsActive && !excluded.Contains(x.Id) && !assigned.Contains(x.Id))
            .ToListAsync();

        var result = new List<SuggestedReplacement>();
        foreach (var employee in candidates)
        {
            var missing = new List<string>();
            var levels = new List<int>();
            var unavailable = employee.Availability.FirstOrDefault(x => x.Date == shift.Date && !x.IsAvailable);
            if (unavailable is not null) missing.Add($"Ikke tilgjengelig: {unavailable.Reason}");

            foreach (var task in shift.ShiftTasks)
            {
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
                    if (level < task.MinCompetenceLevel) missing.Add($"For lavt nivå i {task.WorkTask.Name}: {competence.Level}");
                    if (competence.ValidUntil.HasValue && competence.ValidUntil.Value < shift.Date) missing.Add($"Utløpt kompetanse: {task.WorkTask.Name}");
                }
                if (!string.IsNullOrWhiteSpace(task.WorkTask.RequiredRole) && !string.Equals(employee.Role, task.WorkTask.RequiredRole, StringComparison.OrdinalIgnoreCase))
                    missing.Add($"Rolle krever {task.WorkTask.RequiredRole}");
            }

            if (await HasSchedulingConflictAsync(employee.Id, shift)) missing.Add("Har overlappende vakt");
            if (await ViolatesRestAsync(employee.Id, shift)) missing.Add($"Har mindre enn {MinimumRestHours} timer hvile");

            result.Add(new SuggestedReplacement
            {
                EmployeeId = employee.Id,
                EmployeeName = employee.Name,
                Role = employee.Role,
                CompetenceLevel = levels.DefaultIfEmpty(0).Max(),
                Available = missing.Count == 0,
                MissingRequirements = missing.Distinct().ToList()
            });
        }

        return result.OrderByDescending(x => x.Available).ThenBy(x => x.MissingRequirements.Count).ThenBy(x => x.EmployeeName).ToList();
    }

    private async Task<Shift?> LoadShiftAsync(int shiftId) => await _db.Shifts
        .Include(x => x.Assignments).ThenInclude(x => x.Employee).ThenInclude(x => x.Competences)
        .Include(x => x.Assignments).ThenInclude(x => x.Employee).ThenInclude(x => x.Availability)
        .Include(x => x.ShiftTasks).ThenInclude(x => x.WorkTask).ThenInclude(x => x.Competence)
        .Include(x => x.ShiftTasks).ThenInclude(x => x.ShiftTaskCoverages).ThenInclude(x => x.Employee).ThenInclude(x => x.Competences)
        .Include(x => x.ShiftTasks).ThenInclude(x => x.ShiftTaskCoverages).ThenInclude(x => x.Employee).ThenInclude(x => x.Availability)
        .FirstOrDefaultAsync(x => x.Id == shiftId);

    private async Task<TaskCoverageDetail> EvaluateTaskAsync(Shift shift, ShiftTask task)
    {
        var detail = new TaskCoverageDetail
        {
            TaskName = task.WorkTask.Name,
            CompetenceName = task.WorkTask.Competence?.Name ?? "",
            Required = task.RequiredCount,
            Critical = task.IsCritical
        };

        foreach (var coverage in task.ShiftTaskCoverages)
        {
            var gaps = ValidateCoverage(shift, task, coverage);
            if (gaps.Count == 0 && await HasSchedulingConflictAsync(coverage.EmployeeId, shift))
                gaps.Add(new CoverageGap { Type = GapType.DoubleBooked, EmployeeId = coverage.EmployeeId, EmployeeName = coverage.Employee.Name, Description = $"{coverage.Employee.Name} har overlappende vakt" });
            if (gaps.Count == 0 && await ViolatesRestAsync(coverage.EmployeeId, shift))
                gaps.Add(new CoverageGap { Type = GapType.RestPeriodViolation, EmployeeId = coverage.EmployeeId, EmployeeName = coverage.Employee.Name, Description = $"{coverage.Employee.Name} har mindre enn {MinimumRestHours} timer hvile" });
            detail.Gaps.AddRange(gaps);
            if (gaps.Count == 0) detail.Actual++;
        }

        if (detail.Actual < detail.Required)
            detail.Gaps.Add(new CoverageGap { Type = GapType.InsufficientStaff, Description = $"Trenger {detail.Required} kvalifiserte, har {detail.Actual}" });
        return detail;
    }

    private static List<CoverageGap> ValidateCoverage(Shift shift, ShiftTask task, ShiftTaskCoverage coverage)
    {
        var employee = coverage.Employee;
        var gaps = new List<CoverageGap>();
        var unavailable = employee.Availability.FirstOrDefault(x => x.Date == shift.Date && !x.IsAvailable);
        if (unavailable is not null) gaps.Add(new CoverageGap { Type = GapType.EmployeeUnavailable, EmployeeId = employee.Id, EmployeeName = employee.Name, Description = $"{employee.Name} er ikke tilgjengelig{(string.IsNullOrWhiteSpace(unavailable.Reason) ? "" : $": {unavailable.Reason}")}" });
        if (!string.IsNullOrWhiteSpace(task.WorkTask.RequiredRole) && !string.Equals(employee.Role, task.WorkTask.RequiredRole, StringComparison.OrdinalIgnoreCase))
            gaps.Add(new CoverageGap { Type = GapType.UnauthorizedRole, EmployeeId = employee.Id, EmployeeName = employee.Name, Description = $"{employee.Name} har rolle {employee.Role}, krever {task.WorkTask.RequiredRole}" });

        if (task.WorkTask.CompetenceId is int competenceId)
        {
            var competence = employee.Competences.FirstOrDefault(x => x.CompetenceId == competenceId);
            if (competence is null)
                gaps.Add(new CoverageGap { Type = GapType.MissingCompetence, EmployeeId = employee.Id, EmployeeName = employee.Name, Description = $"{employee.Name} mangler kompetanse for {task.WorkTask.Name}" });
            else
            {
                if (LevelRank(competence.Level) < task.MinCompetenceLevel)
                    gaps.Add(new CoverageGap { Type = GapType.MissingCompetence, EmployeeId = employee.Id, EmployeeName = employee.Name, Description = $"{employee.Name} har nivå {competence.Level}, krever minst {task.MinCompetenceLevel}" });
                if (competence.ValidUntil.HasValue && competence.ValidUntil.Value < shift.Date)
                    gaps.Add(new CoverageGap { Type = GapType.CompetenceExpired, EmployeeId = employee.Id, EmployeeName = employee.Name, Description = $"{employee.Name} sin kompetanse utløp {competence.ValidUntil:dd.MM.yyyy}" });
            }
        }
        if (coverage.AuthorizationExpiry.HasValue && coverage.AuthorizationExpiry.Value.Date < shift.Date)
            gaps.Add(new CoverageGap { Type = GapType.AuthorizationExpired, EmployeeId = employee.Id, EmployeeName = employee.Name, Description = $"{employee.Name} sin autorisasjon er utgått" });
        return gaps;
    }

    private async Task<bool> HasSchedulingConflictAsync(int employeeId, Shift shift)
    {
        return await _db.Shifts.AnyAsync(s => s.Id != shift.Id && s.StartTime < shift.EndTime && s.EndTime > shift.StartTime &&
            (s.Assignments.Any(a => a.EmployeeId == employeeId) || s.ShiftTasks.Any(st => st.ShiftTaskCoverages.Any(sc => sc.EmployeeId == employeeId))));
    }

    private async Task<bool> ViolatesRestAsync(int employeeId, Shift shift)
    {
        var previous = await _db.Shifts
            .Where(s => s.Id != shift.Id && s.EndTime <= shift.StartTime &&
                (s.Assignments.Any(a => a.EmployeeId == employeeId) || s.ShiftTasks.Any(st => st.ShiftTaskCoverages.Any(sc => sc.EmployeeId == employeeId))))
            .OrderByDescending(s => s.EndTime)
            .FirstOrDefaultAsync();
        return previous is not null && (shift.StartTime - previous.EndTime).TotalHours < MinimumRestHours;
    }

    private static int LevelRank(string level) => int.TryParse(level, out var numeric) ? numeric : level.Trim().ToLowerInvariant() switch { "basic" => 1, "intermediate" => 2, "advanced" => 3, "expert" => 4, _ => 0 };

    private static CoverageStatus DetermineStatus(CoverageResult result, bool understaffed) =>
        result.Tasks.Any(x => x.Critical && x.Gaps.Any()) ? CoverageStatus.Red : understaffed || result.Tasks.Any(x => x.Gaps.Any()) ? CoverageStatus.Yellow : CoverageStatus.Green;
}
