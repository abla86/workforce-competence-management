using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Workforce.Api.Services;

public sealed class PrototypeAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public PrototypeAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock) : base(options, logger, encoder, clock) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!(Context.Connection.RemoteIpAddress?.IsLoopback() ?? false))
            return Task.FromResult(AuthenticateResult.Fail("Prototype authentication is restricted to loopback development requests."));

        var subject = Request.Headers["X-Prototype-User"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(subject))
            return Task.FromResult(AuthenticateResult.Fail("Missing X-Prototype-User header."));

        var roles = Request.Headers["X-Prototype-Roles"].FirstOrDefault() ?? "Employee";
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, subject),
            new(ClaimTypes.Name, subject)
        };
        claims.AddRange(roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
