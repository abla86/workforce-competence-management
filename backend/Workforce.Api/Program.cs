using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;
using Workforce.Api.DTOs;
using Workforce.Api.Models;
using Workforce.Api.Services;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<CoverageService>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length > 0) policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseCors();
app.MapOpenApi();
app.MapWorkforceExpansionEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await SeedData.InitializeAsync(db);
}

app.MapGet("/", () => Results.Ok(new { name = "Workforce & Competence Management API", version = "2.0.0", status = "running" }));
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Existing employee/competence/shift endpoints remain in the expansion endpoint module.

app.MapGet("/api/shifts/{shiftId:int}/coverage", async (int shiftId, AppDbContext db, CoverageService coverage, ILogger<CoverageService> logger) =>
{
    try
    {
        var shift = await db.Shifts
            .Include(s => s.Assignments).ThenInclude(a => a.Employee).ThenInclude(e => e.Competences).ThenInclude(c => c.Competence)
            .Include(s => s.Requirements).ThenInclude(r => r.Competence)
            .FirstOrDefaultAsync(s => s.Id == shiftId);
        if (shift is null) return Results.NotFound(new { message = $"Shift {shiftId} not found." });
        var result = coverage.AnalyzeShift(shift);
        var audit = new CoverageAuditEntry
        {
            ShiftId = shiftId,
            Status = result.IsReady ? "Green" : "Red",
            DetailsJson = JsonSerializer.Serialize(result),
            TriggeredBy = "system"
        };
        db.CoverageAuditEntries.Add(audit);
        await db.SaveChangesAsync();
        logger.LogInformation("Coverage evaluated for shift {ShiftId}: {Status}", shiftId, audit.Status);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error evaluating coverage for shift {ShiftId}", shiftId);
        return Results.Problem("Internal server error");
    }
}).WithTags("coverage");

app.MapPost("/api/shifts/{shiftId:int}/coverage/scenario", async (int shiftId, RemoveEmployeeRequest request, AppDbContext db, CoverageService coverage) =>
{
    var shift = await db.Shifts
        .Include(s => s.Assignments).ThenInclude(a => a.Employee).ThenInclude(e => e.Competences).ThenInclude(c => c.Competence)
        .Include(s => s.Requirements).ThenInclude(r => r.Competence)
        .FirstOrDefaultAsync(s => s.Id == shiftId);
    if (shift is null) return Results.NotFound(new { message = $"Shift {shiftId} not found." });
    var removed = request.EmployeeIds.ToHashSet();
    var original = shift.Assignments.ToList();
    shift.Assignments = original.Where(a => !removed.Contains(a.EmployeeId)).ToList();
    var result = coverage.AnalyzeShift(shift);
    shift.Assignments = original;
    var replacements = await db.Employees.Where(e => e.IsActive && !removed.Contains(e.Id)).ToListAsync();
    var suggested = replacements.Select(e => new { e.Id, e.Name, e.Role }).ToList();
    return Results.Ok(new { coverageWithoutEmployee = result, suggestedReplacements = suggested });
}).WithTags("coverage");

app.MapGet("/api/shifts/{shiftId:int}/coverage/history", async (int shiftId, AppDbContext db) =>
{
    var audits = await db.CoverageAuditEntries.Where(a => a.ShiftId == shiftId).OrderByDescending(a => a.EvaluatedAt).Take(10).ToListAsync();
    return Results.Ok(audits);
}).WithTags("coverage");

app.Run();

public sealed record RemoveEmployeeRequest(List<int> EmployeeIds);