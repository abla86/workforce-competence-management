namespace Workforce.Api.Models;

public sealed class UserAccount
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Manager";
    public bool IsActive { get; set; } = true;
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAtUtc { get; set; }
    public string? EmployeeId { get; set; }
}
