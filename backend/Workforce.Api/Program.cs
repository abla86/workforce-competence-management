using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Workforce.Api.Data;
using Workforce.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var isDevelopment = builder.Environment.IsDevelopment();

builder.Services.AddOpenApi();
builder.Services.AddDataProtection();
builder.Services.AddScoped<AuditProtectionService>();
builder.Services.AddScoped<CoverageEvaluationEngine>();
builder.Services.AddScoped<GdprService>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddHostedService<AuditRetentionWorker>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

if (isDevelopment)
{
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = DevelopmentAuthenticationHandler.Scheme;
        options.DefaultChallengeScheme = DevelopmentAuthenticationHandler.Scheme;
    }).AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(
        DevelopmentAuthenticationHandler.Scheme, _ => { });
}
else
{
    var secret = builder.Configuration["Jwt:SecretKey"];
    if (string.IsNullOrWhiteSpace(secret))
        throw new InvalidOperationException("Jwt:SecretKey must be supplied through secure production configuration.");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(secret)),
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CoverageRead", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("CoverageManage", policy => policy.RequireAuthenticatedUser().RequireRole("Manager", "HR", "Admin"));
    options.AddPolicy("CoverageAdmin", policy => policy.RequireAuthenticatedUser().RequireRole("Admin"));
    options.AddPolicy("PrivacyManage", policy => policy.RequireAuthenticatedUser());
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("coverage", o =>
    {
        o.PermitLimit = 60;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("privacy", o =>
    {
        o.PermitLimit = 10;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
    });
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length > 0)
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    else
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();

if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    if (!app.Environment.IsDevelopment())
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    await next();
});
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapOpenApi();
app.MapWorkforceExpansionEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await SeedData.InitializeAsync(db);
}

app.MapGet("/", () => Results.Ok(new { name = "Workforce & Competence Management API", version = "3.0.0", status = "running" }));
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapGet("/api/me", (ClaimsPrincipal user) => Results.Ok(new
{
    id = user.FindFirstValue(ClaimTypes.NameIdentifier),
    name = user.Identity?.Name,
    roles = user.FindAll(ClaimTypes.Role).Select(x => x.Value).Distinct().ToArray(),
    authentication = user.Identity?.AuthenticationType
})).RequireAuthorization();

app.MapGet("/api/shifts/{shiftId:int}/coverage", async (
    int shiftId,
    ClaimsPrincipal user,
    CoverageEvaluationEngine engine,
    ILogger<CoverageEvaluationEngine> logger,
    HttpContext httpContext) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
    try
    {
        var result = await engine.EvaluateAsync(
            shiftId,
            userId,
            writeAudit: true,
            clientIp: httpContext.Connection.RemoteIpAddress?.ToString(),
            userAgent: httpContext.Request.Headers.UserAgent.ToString());
        return Results.Ok(result);
    }
    catch (ArgumentException ex)
    {
        logger.LogWarning(ex, "Shift {ShiftId} not found", shiftId);
        return Results.NotFound(new { message = ex.Message });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error evaluating coverage for shift {ShiftId}", shiftId);
        return Results.Problem("Internal server error");
    }
})
.RequireAuthorization("CoverageRead")
.RequireRateLimiting("coverage")
.WithTags("coverage");

app.MapPost("/api/shifts/{shiftId:int}/coverage/scenario", async (
    int shiftId,
    RemoveEmployeeRequest request,
    ClaimsPrincipal user,
    CoverageEvaluationEngine engine,
    ILogger<CoverageEvaluationEngine> logger) =>
{
    try
    {
        var employeeIds = request.EmployeeIds ?? [];
        var result = await engine.EvaluateScenarioWithoutEmployeesAsync(shiftId, employeeIds);
        var replacements = await engine.FindQualifiedReplacementsAsync(shiftId, employeeIds);
        return Results.Ok(new { coverageWithoutEmployees = result, suggestedReplacements = replacements });
    }
    catch (ArgumentException ex)
    {
        return Results.NotFound(new { message = ex.Message });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error running coverage scenario for shift {ShiftId}", shiftId);
        return Results.Problem("Internal server error");
    }
})
.RequireAuthorization("CoverageManage")
.RequireRateLimiting("coverage")
.WithTags("coverage");

app.MapGet("/api/shifts/{shiftId:int}/coverage/history", async (int shiftId, int? limit, AppDbContext db) =>
{
    var take = Math.Clamp(limit ?? 20, 1, 100);
    var audits = await db.CoverageAuditEntries
        .Where(a => a.ShiftId == shiftId)
        .OrderByDescending(a => a.EvaluatedAt)
        .Take(take)
        .Select(a => new { a.Id, a.ShiftId, a.EvaluatedAt, a.Status, a.AnonymizedSummary, a.TriggeredBy })
        .ToListAsync();
    return Results.Ok(audits);
})
.RequireAuthorization("CoverageRead")
.RequireRateLimiting("coverage")
.WithTags("coverage");

app.MapGet("/api/me/privacy/export", async (ClaimsPrincipal user, GdprService gdpr) =>
{
    var subject = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var data = await gdpr.ExportAsync(subject);
    return Results.Json(data);
})
.RequireAuthorization("PrivacyManage")
.RequireRateLimiting("privacy")
.WithTags("privacy");

app.MapPost("/api/me/privacy/correction", async (PrivacyCorrectionRequest request, ClaimsPrincipal user, GdprService gdpr) =>
{
    var subject = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var result = await gdpr.RequestCorrectionAsync(subject, request.Details);
    return Results.Accepted($"/api/me/privacy/requests/{result.Id}", result);
})
.RequireAuthorization("PrivacyManage")
.RequireRateLimiting("privacy")
.WithTags("privacy");

app.MapPost("/api/me/privacy/deletion", async (ClaimsPrincipal user, GdprService gdpr) =>
{
    var subject = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var result = await gdpr.RequestDeletionAsync(subject);
    return Results.Accepted($"/api/me/privacy/requests/{result.Id}", result);
})
.RequireAuthorization("PrivacyManage")
.RequireRateLimiting("privacy")
.WithTags("privacy");

app.MapGet("/api/me/privacy/requests", async (ClaimsPrincipal user, AppDbContext db) =>
{
    var subject = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var requests = await db.PrivacyRequests
        .Where(x => x.IdentitySubject == subject)
        .OrderByDescending(x => x.RequestedAt)
        .ToListAsync();
    return Results.Ok(requests);
})
.RequireAuthorization("PrivacyManage")
.RequireRateLimiting("privacy")
.WithTags("privacy");

app.MapPost("/api/auth/token", (DemoLoginRequest request, JwtTokenService tokens, IConfiguration config, IWebHostEnvironment env) =>
{
    if (!env.IsDevelopment()) return Results.NotFound();

    var expectedUser = config["DevelopmentAuthentication:Username"] ?? "manager";
    var expectedPassword = config["DevelopmentAuthentication:Password"] ?? "vaktklar-demo";
    if (!string.Equals(request.Username, expectedUser, StringComparison.Ordinal) || request.Password != expectedPassword)
        return Results.Unauthorized();

    var userId = config["DevelopmentAuthentication:UserId"] ?? "dev-manager";
    var displayName = config["DevelopmentAuthentication:DisplayName"] ?? "Development Manager";
    var roles = config.GetSection("DevelopmentAuthentication:Roles").Get<string[]>() ?? ["Manager"];
    var accessToken = tokens.CreateAccessToken(userId, displayName, roles);
    var refreshToken = tokens.CreateRefreshToken(userId, roles);
    return Results.Ok(new { accessToken, refreshToken, expiresIn = 1800, tokenType = "Bearer" });
}).WithTags("authentication");

app.MapPost("/api/auth/refresh", (RefreshRequest request, JwtTokenService tokens) =>
{
    if (!tokens.TryUseRefreshToken(request.RefreshToken, out var record))
        return Results.Unauthorized();

    var displayName = record.UserId == "dev-manager" ? "Development Manager" : record.UserId;
    var accessToken = tokens.CreateAccessToken(record.UserId, displayName, record.Roles);
    var refreshToken = tokens.CreateRefreshToken(record.UserId, record.Roles);
    return Results.Ok(new { accessToken, refreshToken, expiresIn = 1800, tokenType = "Bearer" });
}).WithTags("authentication");

app.Run();

public sealed record RemoveEmployeeRequest(List<int>? EmployeeIds);
public sealed record PrivacyCorrectionRequest(string Details);
public sealed record DemoLoginRequest(string Username, string Password);
public sealed record RefreshRequest(string RefreshToken);
