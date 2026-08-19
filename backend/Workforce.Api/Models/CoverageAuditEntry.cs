namespace Workforce.Api.Models;

/// <summary>
/// Immutable record of a coverage evaluation. DetailsJson contains the exact
/// CoverageResult that was evaluated at the time of the event.
/// </summary>
public sealed class CoverageAuditEntry
{
    public int Id { get; set; }
    public int ShiftId { get; set; }
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "";
    public string DetailsJson { get; set; } = "";
    public string? TriggeredBy { get; set; }
}
