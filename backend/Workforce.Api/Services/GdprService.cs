using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;
using Workforce.Api.Models;

namespace Workforce.Api.Services;

public sealed class GdprService
{
    private readonly AppDbContext _db;
    private readonly ILogger<GdprService> _logger;

    public GdprService(AppDbContext db, ILogger<GdprService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<object> ExportAsync(string subject)
    {
        var employee = await _db.Employees
            .Include(x => x.Competences).ThenInclude(x => x.Competence)
            .Include(x => x.Availability)
            .Include(x => x.ShiftAssignments).ThenInclude(x => x.Shift)
            .SingleOrDefaultAsync(x => x.IdentitySubject == subject);

        var audits = await _db.CoverageAuditEntries
            .Where(x => x.TriggeredBy == subject)
            .OrderByDescending(x => x.EvaluatedAt)
            .Select(x => new { x.Id, x.ShiftId, x.EvaluatedAt, x.Status, x.AnonymizedSummary })
            .ToListAsync();

        var requests = await _db.PrivacyRequests
            .Where(x => x.IdentitySubject == subject)
            .OrderByDescending(x => x.RequestedAt)
            .ToListAsync();

        return new
        {
            exportVersion = "1.0",
            generatedAt = DateTime.UtcNow,
            employee = employee is null ? null : new
            {
                employee.Id,
                employee.Name,
                employee.Role,
                employee.PositionPercent,
                employee.IsActive,
                competences = employee.Competences.Select(x => new { x.CompetenceId, Name = x.Competence.Name, x.Level, x.ValidUntil }),
                availability = employee.Availability.Select(x => new { x.Date, x.IsAvailable, x.Reason }),
                shifts = employee.ShiftAssignments.Select(x => new { x.ShiftId, x.Shift.Date, x.Shift.ShiftType, x.Shift.Hours })
            },
            auditHistory = audits,
            privacyRequests = requests
        };
    }

    public async Task<PrivacyRequest> CreateRequestAsync(string subject, string type)
    {
        var request = new PrivacyRequest { IdentitySubject = subject, Type = type.Trim().ToLowerInvariant() };
        _db.PrivacyRequests.Add(request);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Privacy request {Type} created for subject {Subject}", request.Type, subject);
        return request;
    }

    public async Task<int> CleanupAuditEntriesAsync(int retentionDays = 90)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var entries = await _db.CoverageAuditEntries.Where(x => x.EvaluatedAt < cutoff).ToListAsync();
        if (entries.Count == 0) return 0;
        _db.CoverageAuditEntries.RemoveRange(entries);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Removed {Count} audit entries older than {Cutoff}", entries.Count, cutoff);
        return entries.Count;
    }
}
