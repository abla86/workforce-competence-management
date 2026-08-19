using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;
using Workforce.Api.Models;

namespace Workforce.Api.Services;

public sealed class GdprService
{
    private readonly AppDbContext _db;
    private readonly AuditProtectionService _auditProtection;
    private readonly ILogger<GdprService> _logger;

    public GdprService(AppDbContext db, AuditProtectionService auditProtection, ILogger<GdprService> logger)
    {
        _db = db;
        _auditProtection = auditProtection;
        _logger = logger;
    }

    public async Task<object> ExportAsync(string subject)
    {
        var audits = await _db.CoverageAuditEntries
            .Where(x => x.TriggeredBy == subject)
            .OrderByDescending(x => x.EvaluatedAt)
            .Select(x => new
            {
                x.Id,
                x.ShiftId,
                x.EvaluatedAt,
                x.Status,
                x.AnonymizedSummary
            })
            .ToListAsync();

        var privacyRequests = await _db.PrivacyRequests
            .Where(x => x.IdentitySubject == subject)
            .OrderByDescending(x => x.RequestedAt)
            .ToListAsync();

        return new
        {
            exportVersion = "1.0",
            generatedAt = DateTime.UtcNow,
            identitySubject = subject,
            auditHistory = audits,
            privacyRequests,
            note = "Prototype export. Only data linked to the authenticated identity is included."
        };
    }

    public async Task<PrivacyRequest> RequestCorrectionAsync(string subject, string details)
    {
        var request = new PrivacyRequest
        {
            IdentitySubject = subject,
            Type = "Correction",
            Status = "Pending",
            RequestedAt = DateTime.UtcNow
        };
        _db.PrivacyRequests.Add(request);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Privacy correction request created for subject {Subject}", subject);
        return request;
    }

    public async Task<PrivacyRequest> RequestDeletionAsync(string subject)
    {
        var request = new PrivacyRequest
        {
            IdentitySubject = subject,
            Type = "Deletion",
            Status = "Pending",
            RequestedAt = DateTime.UtcNow
        };
        _db.PrivacyRequests.Add(request);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Privacy deletion request created for subject {Subject}", subject);
        return request;
    }

    public async Task<int> CleanupAuditEntriesAsync(int retentionDays = 90)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var entries = await _db.CoverageAuditEntries
            .Where(x => x.EvaluatedAt < cutoff)
            .ToListAsync();
        if (entries.Count == 0) return 0;

        _db.CoverageAuditEntries.RemoveRange(entries);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Removed {Count} coverage audit entries older than {Cutoff}", entries.Count, cutoff);
        return entries.Count;
    }
}
