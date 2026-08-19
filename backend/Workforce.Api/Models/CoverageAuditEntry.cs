namespace Workforce.Api.Models;

public sealed class CoverageAuditEntry
{
    public int Id { get; set; }
    public int ShiftId { get; set; }
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "";
    public string DetailsJson { get; set; } = "";
    public string? TriggeredBy { get; set; }
    public Shift Shift { get; set; } = null!;
}
