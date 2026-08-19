namespace Workforce.Api.Models;

public sealed class PrivacyRequest
{
    public int Id { get; set; }
    public string IdentitySubject { get; set; } = "";
    public string Type { get; set; } = "";
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Pending";
    public DateTime? CompletedAt { get; set; }
}
