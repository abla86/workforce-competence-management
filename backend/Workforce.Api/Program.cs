using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Workforce.Api;
using Workforce.Api.Data;
using Workforce.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var isDevelopment = builder.Environment.IsDevelopment();

builder.Services.AddOpenApi();
builder.Services.AddDataProtection();
builder.Services.AddScoped<AuditProtectionService>();
builder.Services.AddScoped<CoverageEvaluationEngine>();
builder.Services.AddScoped<ShiftAccessService>();
builder.Services.AddScoped<EmployeeAccessService>();
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
    }).AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(DevelopmentAuthenticationHandler.Scheme, _ => { });
}
else
{
    var authority = builder.Configuration["Authentication:Authority"];
    var audience = builder.Configuration["Authentication:Audience"];
    if (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(audience))
        throw new InvalidOperationException("Authentication:Authority and Authentication:Audience must be configured in non-development environments.");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = authority;
            options.Audience = audience;
            options.RequireHttpsMetadata = true;
        });
}

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("CoverageRead", policy => policy.RequireAuthenticatedUser().RequireRole("Employee", "Manager", "HR", "Admin"))
    .AddPolicy("CoverageManage", policy => policy.RequireAuthenticatedUser().RequireRole("Manager", "HR", "Admin"))
    .AddPolicy("CoverageAdmin", policy => policy.RequireAuthenticatedUser().RequireRole("Admin"));

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
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapOpenApi();
app.MapWorkforceExpansionEndpoints();
app.MapWorkforceCrudEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await SeedData.InitializeAsync(db);
}

app.MapGet("/", () => Results.Ok(new { name = "Workforce & Competence Management API", version = "3.0.0", status = "running" })).AllowAnonymous();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow })).AllowAnonymous();

app.MapGet("/api/me", (ClaimsPrincipal user) => Results.Ok(new
{
    id = user.FindFirstValue(ClaimTypes.NameIdentifier),
    name = user.Identity?.Name,
    roles = user.FindAll(ClaimTypes.Role).Select(x => x.Value).Distinct().ToArray(),
    authentication = user.Identity?.AuthenticationType
})).RequireAuthorization("CoverageRead");

app.MapGet("/api/shifts/{shiftId:int}/coverage", async (
    int shiftId,
    ClaimsPrincipal user,
    ShiftAccessService access,
    CoverageEvaluationEngine engine,
    HttpContext http,
    ILogger<CoverageEvaluationEngine> logger) =>
{
    if (!await access.CanAccessShiftAsync(user, shiftId)) return Results.Forbid();
    var subject = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
    try
    {
        var result = await engine.EvaluateAsync(shiftId, subject, http);
        return Results.Ok(result);
    }
    catch (ArgumentException ex) { return Results.NotFound(new { message = ex.Message }); }
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
    ShiftAccessService access,
    CoverageEvaluationEngine engine) =>
{
    if (!await access.CanAccessShiftAsync(user, shiftId)) return Results.Forbid();
    var employeeIds = request.EmployeeIds ?? [];
    var result = await engine.EvaluateScenarioWithoutEmployeesAsync(shiftId, employeeIds);
    var replacements = await engine.FindQualifiedReplacementsAsync(shiftId, employeeIds);
    return Results.Ok(new { coverageWithoutEmployees = result, suggestedReplacements = replacements });
})
.RequireAuthorization("CoverageManage")
.RequireRateLimiting("coverage")
.WithTags("coverage");

app.MapGet("/api/shifts/{shiftId:int}/coverage/history", async (int shiftId, ClaimsPrincipal user, ShiftAccessService access, int? limit, AppDbContext db) =>
{
    if (!await access.CanAccessShiftAsync(user, shiftId)) return Results.Forbid();
    var take = Math.Clamp(limit ?? 20, 1, 100);
    var audits = await db.CoverageAuditEntries
        .Where(x => x.ShiftId == shiftId)
        .OrderByDescending(x => x.EvaluatedAt)
        .Take(take)
        .Select(x => new { x.Id, x.ShiftId, x.EvaluatedAt, x.Status, x.AnonymizedSummary, x.TriggeredBy })
        .ToListAsync();
    return Results.Ok(audits);
})
.RequireAuthorization("CoverageRead")
.RequireRateLimiting("coverage")
.WithTags("coverage");

app.MapGet("/api/me/data-export", async (ClaimsPrincipal user, GdprService gdpr) =>
{
    var subject = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(subject)) return Results.Unauthorized();
    return Results.Ok(await gdpr.ExportAsync(subject));
})
.RequireAuthorization("CoverageRead")
.RequireRateLimiting("privacy")
.WithTags("privacy");

app.MapPost("/api/me/privacy-requests", async (PrivacyRequestType request, ClaimsPrincipal user, GdprService gdpr) =>
{
    var subject = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(subject)) return Results.Unauthorized();
    var type = request.Type.Trim().ToLowerInvariant();
    if (type is not ("access" or "rectification" or "erasure" or "portability"))
        return Results.BadRequest(new { message = "Unsupported privacy request type." });
    var item = await gdpr.CreateRequestAsync(subject, type);
    return Results.Accepted($"/api/me/privacy-requests/{item.Id}", new { item.Id, item.Type, item.Status, item.RequestedAt });
})
.RequireAuthorization("CoverageRead")
.RequireRateLimiting("privacy")
.WithTags("privacy");

app.MapGet("/api/me/privacy-requests", async (ClaimsPrincipal user, AppDbContext db) =>
{
    var subject = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(subject)) return Results.Unauthorized();
    var requests = await db.PrivacyRequests.Where(x => x.IdentitySubject == subject).OrderByDescending(x => x.RequestedAt)
        .Select(x => new { x.Id, x.Type, x.RequestedAt, x.Status, x.CompletedAt }).ToListAsync();
    return Results.Ok(requests);
})
.RequireAuthorization("CoverageRead")
.RequireRateLimiting("privacy")
.WithTags("privacy");

app.MapPost("/api/auth/token", (DemoLoginRequest request, JwtTokenService tokens, IConfiguration config, IWebHostEnvironment env) =>
{
    if (!env.IsDevelopment()) return Results.NotFound();
    var username = config["DevelopmentAuthentication:Username"] ?? "manager";
    var password = config["DevelopmentAuthentication:Password"] ?? "vaktklar-demo";
    if (request.Username != username || request.Password != password) return Results.Unauthorized();
    var userId = config["DevelopmentAuthentication:UserId"] ?? "dev-manager";
    var name = config["DevelopmentAuthentication:DisplayName"] ?? "Development Manager";
    var roles = config.GetSection("DevelopmentAuthentication:Roles").Get<string[]>() ?? ["Manager"];
    return Results.Ok(new { accessToken = tokens.CreateAccessToken(userId, name, roles), refreshToken = tokens.CreateRefreshToken(userId, roles), expiresIn = 1800, tokenType = "Bearer" });
}).AllowAnonymous().WithTags("authentication");

app.MapPost("/api/auth/refresh", (RefreshRequest request, JwtTokenService tokens, IWebHostEnvironment env) =>
{
    if (!env.IsDevelopment()) return Results.NotFound();
    if (!tokens.TryUseRefreshToken(request.RefreshToken, out var record)) return Results.Unauthorized();
    var access = tokens.CreateAccessToken(record.UserId, record.UserId, record.Roles);
    var refresh = tokens.CreateRefreshToken(record.UserId, record.Roles);
    return Results.Ok(new { accessToken = access, refreshToken = refresh, expiresIn = 1800, tokenType = "Bearer" });
}).AllowAnonymous().WithTags("authentication");

app.Run();

public sealed record RemoveEmployeeRequest(List<int>? EmployeeIds);
public sealed record PrivacyRequestType(string Type);
public sealed record DemoLoginRequest(string Username, string Password);
public sealed record RefreshRequest(string RefreshToken);
