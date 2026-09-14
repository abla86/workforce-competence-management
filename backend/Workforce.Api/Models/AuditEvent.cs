namespace Workforce.Api.Models;

public sealed class AuditEvent
{
    public long Id { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string? Actor { get; set; }
    public string? Reason { get; set; }
    public string? DetailsJson { get; set; }
}
