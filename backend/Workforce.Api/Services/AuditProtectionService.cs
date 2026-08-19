using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Workforce.Api.Models;

namespace Workforce.Api.Services;

public sealed class AuditProtectionService
{
    private readonly IDataProtector _protector;

    public AuditProtectionService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("Workforce.Api.CoverageAudit.v1");
    }

    public void ProtectResult(CoverageAuditEntry entry, object result)
    {
        var json = JsonSerializer.Serialize(result);
        entry.EncryptedDetails = _protector.Protect(json);
        entry.AnonymizedSummary = JsonSerializer.Serialize(new
        {
            entry.Status,
            entry.EvaluatedAt,
            ShiftId = entry.ShiftId
        });
        entry.DetailsJson = "[PROTECTED]";
    }

    public string Unprotect(CoverageAuditEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.EncryptedDetails))
            return entry.DetailsJson;
        return _protector.Unprotect(entry.EncryptedDetails);
    }
}
