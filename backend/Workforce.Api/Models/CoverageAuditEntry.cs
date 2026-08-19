namespace Workforce.Api.Models;

public sealed class CoverageAuditEntry
{
    public int Id { get; set; }
    public int ShiftId { get; set; }
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "";

    // Kept for backwards compatibility; new evaluations store protected details.
    public string DetailsJson { get; set; } = "[PROTECTED]";
    public string? EncryptedDetails { get; set; }
    public string AnonymizedSummary { get; set; } = "";

    public string? TriggeredBy { get; set; }
    public string? ClientIp { get; set; }
    public string? UserAgent { get; set; }

    public Shift Shift { get; set; } = null!;
}
