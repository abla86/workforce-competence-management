using Workforce.Api.Models;

namespace Workforce.Api.Services;

public sealed class CoverageService
{
    private readonly AppDbContext _context;
    private readonly ILogger<CoverageService> _logger;
    private readonly AuditProtectionService _auditProtection;

    public CoverageService(AppDbContext context, ILogger<CoverageService> logger, AuditProtectionService auditProtection)
    {
        _context = context;
        _logger = logger;
        _auditProtection = auditProtection;
    }

    public async Task<CoverageResult> EvaluateAsync(int shiftId, string triggeredBy = "system", HttpContext? httpContext = null)
    {
        var shift = await _context.Shifts
            .Include(s => s.ShiftTasks).ThenInclude(st => st.WorkTask)
            .Include(s => s.ShiftTasks).ThenInclude(st => st.ShiftTaskCoverages).ThenInclude(stc => stc.Employee).ThenInclude(e => e.Competences)
            .Include(s => s.Assignments).ThenInclude(a => a.Employee)
            .FirstOrDefaultAsync(s => s.Id == shiftId);

        if (shift is null)
            throw new ArgumentException($"Shift {shiftId} not found");

        var result = Evaluate(shift);
        var audit = new CoverageAuditEntry
        {
            ShiftId = shiftId,
            Status = result.Status.ToString(),
            TriggeredBy = triggeredBy,
            ClientIp = httpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext?.Request.Headers.UserAgent.ToString()
        };
        _auditProtection.ProtectResult(audit, result);
        _context.CoverageAuditEntries.Add(audit);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Coverage evaluated for shift {ShiftId}: {Status}", shiftId, result.Status);
        return result;
    }

    public CoverageResult Evaluate(Shift shift)
    {
        var result = new CoverageResult();
        foreach (var shiftTask in shift.ShiftTasks)
        {
            var detail = new TaskCoverageDetail
            {
                TaskName = shiftTask.WorkTask.Name,
                Required = shiftTask.RequiredCount,
                Critical = shiftTask.IsCritical
            };

            var coverages = shiftTask.ShiftTaskCoverages.Where(x => x.IsValid).ToList();
            detail.Actual = coverages.Count;
            if (detail.Actual < detail.Required)
                detail.Gaps.Add(new CoverageGap { Type = GapType.InsufficientStaff, Description = $"Trenger {detail.Required}, har {detail.Actual}" });

            foreach (var coverage in coverages)
                ValidateCoverage(coverage, shiftTask, detail);
            result.Tasks.Add(detail);
        }

        var staffingGap = shift.Assignments.Count < shift.MinimumStaff;
        if (staffingGap)
            result.Warnings.Add($"Vakten har {shift.Assignments.Count} ansatte, men krever minst {shift.MinimumStaff}.");
        result.Status = DetermineStatus(result.Tasks, staffingGap);
        return result;
    }

    private static void ValidateCoverage(ShiftTaskCoverage coverage, ShiftTask shiftTask, TaskCoverageDetail detail)
    {
        var employee = coverage.Employee;
        if (shiftTask.WorkTask.CompetenceId is null) return;
        var competence = employee.Competences.FirstOrDefault(x => x.CompetenceId == shiftTask.WorkTask.CompetenceId);
        if (competence is null)
        {
            detail.Gaps.Add(new CoverageGap { Type = GapType.MissingCompetence, EmployeeId = employee.Id, Description = $"{employee.Name} mangler nødvendig kompetanse for {shiftTask.WorkTask.Name}" });
            return;
        }
        if (Rank(competence.Level) < shiftTask.MinCompetenceLevel)
            detail.Gaps.Add(new CoverageGap { Type = GapType.MissingCompetence, EmployeeId = employee.Id, Description = $"{employee.Name} har kompetansenivå {competence.Level}, krever minst nivå {shiftTask.MinCompetenceLevel}" });
        if (competence.ValidUntil.HasValue && competence.ValidUntil.Value < DateOnly.FromDateTime(DateTime.UtcNow))
            detail.Gaps.Add(new CoverageGap { Type = GapType.CompetenceExpired, EmployeeId = employee.Id, Description = $"Kompetansen til {employee.Name} er utløpt" });
        if (coverage.AuthorizationExpiry.HasValue && coverage.AuthorizationExpiry.Value < DateTime.UtcNow)
            detail.Gaps.Add(new CoverageGap { Type = GapType.AuthorizationExpired, EmployeeId = employee.Id, Description = $"Autorisasjonen til {employee.Name} er utløpt" });
    }

    private static CoverageStatus DetermineStatus(IEnumerable<TaskCoverageDetail> tasks, bool staffingGap)
    {
        var list = tasks.ToList();
        if (staffingGap || list.Any(t => t.Critical && t.Gaps.Count > 0)) return CoverageStatus.Red;
        if (list.Any(t => t.Gaps.Count > 0)) return CoverageStatus.Yellow;
        return CoverageStatus.Green;
    }

    private static int Rank(string level) => level.ToLowerInvariant() switch
    {
        "basic" => 1,
        "intermediate" => 2,
        "advanced" => 3,
        _ => 1
    };
}
