using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;
using Workforce.Api.Models;
using Workforce.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDataProtection();
builder.Services.AddScoped<CoverageService>();
builder.Services.AddScoped<AuditProtectionService>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("coverage", limiterOptions =>
    {
        limiterOptions.PermitLimit = 60;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
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

app.MapGet("/", () => Results.Ok(new { name = "Workforce & Competence Management API", version = "2.0.0", status = "running" }));
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapGet("/api/shifts/{shiftId:int}/coverage", async (
    int shiftId,
    CoverageService coverage,
    AuditProtectionService protection,
    HttpContext http,
    ILogger<CoverageService> logger) =>
{
    try
    {
        var result = await coverage.EvaluateAsync(shiftId, "system");
        // CoverageService owns persistence. This endpoint intentionally does not write a second audit row.
        logger.LogInformation("Coverage evaluated for shift {ShiftId}: {Status}", shiftId, result.Status);
        return Results.Ok(result);
    }
    catch (ArgumentException ex)
    {
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
    AppDbContext db,
    CoverageService coverage) =>
{
    var shift = await db.Shifts
        .Include(s => s.ShiftTasks).ThenInclude(st => st.WorkTask)
        .Include(s => s.ShiftTasks).ThenInclude(st => st.ShiftTaskCoverages).ThenInclude(c => c.Employee).ThenInclude(e => e.Competences)
        .Include(s => s.Assignments).ThenInclude(a => a.Employee)
        .FirstOrDefaultAsync(s => s.Id == shiftId);

    if (shift is null)
        return Results.NotFound(new { message = $"Shift {shiftId} not found" });

    var removed = request.EmployeeIds.ToHashSet();
    var originalAssignments = shift.Assignments.ToList();
    var originalCoverages = shift.ShiftTasks.ToDictionary(
        t => t.Id,
        t => t.ShiftTaskCoverages.ToList());

    shift.Assignments = originalAssignments.Where(a => !removed.Contains(a.EmployeeId)).ToList();
    foreach (var task in shift.ShiftTasks)
        task.ShiftTaskCoverages = originalCoverages[task.Id].Where(c => !removed.Contains(c.EmployeeId)).ToList();

    var result = coverage.Evaluate(shift);

    shift.Assignments = originalAssignments;
    foreach (var task in shift.ShiftTasks)
        task.ShiftTaskCoverages = originalCoverages[task.Id];

    var candidates = await db.Employees
        .Where(e => e.IsActive && !removed.Contains(e.Id))
        .Select(e => new { e.Id, e.Name, e.Role })
        .ToListAsync();

    return Results.Ok(new { coverageWithoutEmployees = result, suggestedReplacements = candidates });
})
.RequireRateLimiting("coverage")
.WithTags("coverage");

app.MapGet("/api/shifts/{shiftId:int}/coverage/history", async (int shiftId, AppDbContext db) =>
{
    var audits = await db.CoverageAuditEntries
        .Where(a => a.ShiftId == shiftId)
        .OrderByDescending(a => a.EvaluatedAt)
        .Take(20)
        .Select(a => new
        {
            a.Id,
            a.ShiftId,
            a.EvaluatedAt,
            a.Status,
            a.AnonymizedSummary,
            a.TriggeredBy,
            a.ClientIp
        })
        .ToListAsync();
    return Results.Ok(audits);
})
.RequireRateLimiting("coverage")
.WithTags("coverage");

app.Run();

public sealed record RemoveEmployeeRequest(List<int> EmployeeIds);
