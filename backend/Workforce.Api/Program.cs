using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;
using Workforce.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDataProtection();
builder.Services.AddScoped<AuditProtectionService>();
builder.Services.AddScoped<CoverageEvaluationEngine>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("coverage", o =>
    {
        o.PermitLimit = 60;
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
app.UseCors();
app.UseRateLimiter();
app.MapOpenApi();
app.MapWorkforceExpansionEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await SeedData.InitializeAsync(db);
}

app.MapGet("/", () => Results.Ok(new { name = "Workforce & Competence Management API", version = "2.1.0", status = "running" }));
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapGet("/api/shifts/{shiftId:int}/coverage", async (
    int shiftId,
    CoverageEvaluationEngine engine,
    ILogger<CoverageEvaluationEngine> logger) =>
{
    try
    {
        var result = await engine.EvaluateAsync(shiftId);
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
.RequireRateLimiting("coverage")
.WithTags("coverage");

app.MapPost("/api/shifts/{shiftId:int}/coverage/scenario", async (
    int shiftId,
    RemoveEmployeeRequest request,
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
.RequireRateLimiting("coverage")
.WithTags("coverage");

app.MapGet("/api/shifts/{shiftId:int}/coverage/history", async (int shiftId, int? limit, AppDbContext db) =>
{
    var take = Math.Clamp(limit ?? 20, 1, 100);
    var audits = await db.CoverageAuditEntries
        .Where(a => a.ShiftId == shiftId)
        .OrderByDescending(a => a.EvaluatedAt)
        .Take(take)
        .Select(a => new { a.Id, a.ShiftId, a.EvaluatedAt, a.Status, a.AnonymizedSummary, a.TriggeredBy, a.ClientIp })
        .ToListAsync();
    return Results.Ok(audits);
})
.RequireRateLimiting("coverage")
.WithTags("coverage");

app.Run();

public sealed record RemoveEmployeeRequest(List<int>? EmployeeIds);
