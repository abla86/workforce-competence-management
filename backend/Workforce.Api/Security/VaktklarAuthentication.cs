using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Workforce.Api.Data;
using Workforce.Api.Models;

namespace Workforce.Api.Security;

public static class VaktklarAuthentication
{
    public const string CookieName = "vaktklar_access";

    public static void AddVaktklarAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var secret = configuration["Jwt:SecretKey"];
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
            throw new InvalidOperationException("Jwt:SecretKey must contain at least 32 UTF-8 bytes.");

        services.AddHttpContextAccessor();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"] ?? "vaktklar",
                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:Audience"] ?? "vaktklar-web",
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (context.Request.Cookies.TryGetValue(CookieName, out var token))
                            context.Token = token;
                        return Task.CompletedTask;
                    }
                };
            });
        services.AddAuthorization(options =>
        {
            options.AddPolicy("Manager", p => p.RequireRole("Admin", "Manager"));
            options.AddPolicy("HR", p => p.RequireRole("Admin", "HR"));
            options.AddPolicy("Admin", p => p.RequireRole("Admin"));
        });
    }

    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Authentication");

        group.MapPost("/login", async (LoginRequest request, AppDbContext db, IConfiguration config, HttpResponse response) =>
        {
            var normalized = request.Username.Trim().ToLowerInvariant();
            var user = await db.UserAccounts.SingleOrDefaultAsync(x => x.Username == normalized && x.IsActive);
            if (user is null) return Results.Unauthorized();

            if (user.LockedUntilUtc is { } locked && locked > DateTime.UtcNow)
                return Results.Json(new { message = "Kontoen er midlertidig låst. Prøv igjen senere." }, statusCode: 423);

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= 5)
                {
                    user.FailedLoginAttempts = 0;
                    user.LockedUntilUtc = DateTime.UtcNow.AddMinutes(15);
                }
                await db.SaveChangesAsync();
                return Results.Unauthorized();
            }

            user.FailedLoginAttempts = 0;
            user.LockedUntilUtc = null;
            user.LastLoginAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var token = CreateToken(user, config);
            response.Cookies.Append(CookieName, token, CookieOptions(config, 60));
            return Results.Ok(new { user = new { user.Id, user.Username, user.Role } });
        });

        group.MapPost("/logout", (HttpResponse response) =>
        {
            response.Cookies.Delete(CookieName, CookieOptions(config: null, maxAgeMinutes: 0));
            return Results.NoContent();
        });

        group.MapGet("/me", (ClaimsPrincipal principal) =>
        {
            if (principal.Identity?.IsAuthenticated != true) return Results.Unauthorized();
            return Results.Ok(new
            {
                id = principal.FindFirstValue(ClaimTypes.NameIdentifier),
                username = principal.FindFirstValue(ClaimTypes.Name),
                role = principal.FindFirstValue(ClaimTypes.Role)
            });
        }).RequireAuthorization();

        group.MapPost("/bootstrap", async (BootstrapRequest request, AppDbContext db, IConfiguration config) =>
        {
            var bootstrapKey = config["VAKTKLAR_BOOTSTRAP_KEY"];
            if (string.IsNullOrWhiteSpace(bootstrapKey) || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(bootstrapKey), Encoding.UTF8.GetBytes(request.BootstrapKey)))
                return Results.Unauthorized();
            if (await db.UserAccounts.AnyAsync())
                return Results.Conflict(new { message = "Initial setup is already completed." });
            if (request.Password.Length < 12)
                return Results.BadRequest(new { message = "Administrator password must contain at least 12 characters." });

            var user = new UserAccount
            {
                Username = request.Username.Trim().ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, 12),
                Role = "Admin"
            };
            db.UserAccounts.Add(user);
            await db.SaveChangesAsync();
            return Results.Created("/api/auth/me", new { user.Id, user.Username, user.Role });
        });

        return group;
    }

    private static string CreateToken(UserAccount user, IConfiguration config)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:SecretKey"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role)
        };
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"] ?? "vaktklar",
            audience: config["Jwt:Audience"] ?? "vaktklar-web",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static CookieOptions CookieOptions(IConfiguration? config, int maxAgeMinutes) => new()
    {
        HttpOnly = true,
        Secure = config?["Security:CookieSecure"] is "true" || config is null,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        MaxAge = maxAgeMinutes <= 0 ? TimeSpan.Zero : TimeSpan.FromMinutes(maxAgeMinutes)
    };
}

public sealed record LoginRequest(string Username, string Password);
public sealed record BootstrapRequest(string BootstrapKey, string Username, string Password);
