namespace Workforce.Api.DTOs;

public sealed record LoginRequest(string Username, string Password);
public sealed record BootstrapRequest(string BootstrapKey, string Username, string Password);
