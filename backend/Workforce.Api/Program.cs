using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using Workforce.Api.Data;
using Workforce.Api.DTOs;
using Workforce.Api.Models;
using Workforce.Api.Security;
using Workforce.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<CoverageService>();
builder.Services.AddScoped<PlanningAdvisor>();
builder.Services.AddVaktklarAuthentication(builder.Configuration);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", o =>
    {
        o.PermitLimit = 5;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
        o.AutoReplenishment = true;
    });
});
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173", "http://localhost:8088"])
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") && !context.Request.Path.StartsWithSegments("/api/auth"))
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "Authentication required." });
            return;
        }
    }
    await next();
});

app.MapOpenApi();
app.MapAuthEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.InitializeAsync(db);
}

app.MapGet("/", () => Results.Ok(new { name = "Workforce & Competence Management API", version = "4.1.0", status = "running" }));
app.MapGet("/health", async (AppDbContext db) =>
{
    var databaseOk = await db.Database.CanConnectAsync();
    return databaseOk ? Results.Ok(new { status = "healthy", database = "ok", timestamp = DateTime.UtcNow })
                      : Results.Json(new { status = "degraded", database = "unavailable" }, statusCode: 503);
});

app.MapGet("/api/employees", async (AppDbContext db, string? search, string? role, bool activeOnly = false) =>
{
    var query = db.Employees.Include(x => x.Competences).ThenInclude(x => x.Competence).Include(x => x.Absences).AsQueryable();
    if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search));
    if (!string.IsNullOrWhiteSpace(role)) query = query.Where(x => x.Role == role);
    if (activeOnly) query = query.Where(x => x.IsActive);
    var employees = await query.OrderBy(x => x.Name).ToListAsync();
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    return Results.Ok(employees.Select(e => new
    {
        e.Id, e.Name, e.Role, e.Department, e.Authorization, e.PositionPercent, e.MaxWeeklyHours, e.IsActive,
        CurrentAbsence = e.Absences.Where(a => a.Approved && a.From <= today && a.To >= today).Select(a => new { a.Type, a.From, a.To }).FirstOrDefault(),
        Competences = e.Competences.Select(c => new
        {
            c.CompetenceId, c.Competence.Name, c.Competence.Category, c.Level, c.ValidUntil,
            Status = c.ValidUntil.HasValue && c.ValidUntil.Value < today ? "EXPIRED" : c.ValidUntil.HasValue && c.ValidUntil.Value <= today.AddDays(45) ? "REVIEW_DUE" : "ACTIVE"
        })
    }));
});

app.MapPost("/api/employees", async (CreateEmployeeRequest request, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Role)) return Results.BadRequest(new { message = "Name and role are required." });
    if (request.PositionPercent <= 0 || request.PositionPercent > 100) return Results.BadRequest(new { message = "Position percent must be between 1 and 100." });
    var employee = new Employee { Name = request.Name.Trim(), Role = request.Role.Trim(), PositionPercent = request.PositionPercent };
    db.Employees.Add(employee); await db.SaveChangesAsync(); await Audit(db, "employee.created", "Employee", employee.Id.ToString()); return Results.Created($"/api/employees/{employee.Id}", employee);
});
app.MapPut("/api/employees/{id:int}", async (int id, UpdateEmployeeRequest request, AppDbContext db) =>
{
    var employee = await db.Employees.FindAsync(id); if (employee is null) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Role)) return Results.BadRequest(new { message = "Name and role are required." });
    if (request.PositionPercent <= 0 || request.PositionPercent > 100) return Results.BadRequest(new { message = "Position percent must be between 1 and 100." });
    employee.Name = request.Name.Trim(); employee.Role = request.Role.Trim(); employee.PositionPercent = request.PositionPercent; employee.IsActive = request.IsActive;
    await db.SaveChangesAsync(); await Audit(db, "employee.updated", "Employee", id.ToString()); return Results.Ok(employee);
});
app.MapDelete("/api/employees/{id:int}", async (int id, AppDbContext db) =>
{
    var employee = await db.Employees.FindAsync(id); if (employee is null) return Results.NotFound();
    if (await db.ShiftAssignments.AnyAsync(x => x.EmployeeId == id)) return Results.Conflict(new { message = "Employee has shift assignments. Deactivate the employee instead of deleting historical scheduling data." });
    db.Employees.Remove(employee); await db.SaveChangesAsync(); await Audit(db, "employee.deleted", "Employee", id.ToString()); return Results.NoContent();
});
app.MapPost("/api/employees/{id:int}/competences", async (int id, AddCompetenceRequest request, AppDbContext db) =>
{
    if (!await db.Employees.AnyAsync(x => x.Id == id) || !await db.Competences.AnyAsync(x => x.Id == request.CompetenceId)) return Results.NotFound();
    var existing = await db.EmployeeCompetences.FindAsync(id, request.CompetenceId);
    if (existing is null) db.EmployeeCompetences.Add(new EmployeeCompetence { EmployeeId = id, CompetenceId = request.CompetenceId, Level = request.Level, ValidUntil = request.ValidUntil });
    else { existing.Level = request.Level; existing.ValidUntil = request.ValidUntil; }
    await db.SaveChangesAsync(); await Audit(db, "employee.competence.updated", "Employee", id.ToString()); return Results.NoContent();
});
app.MapDelete("/api/employees/{id:int}/competences/{competenceId:int}", async (int id, int competenceId, AppDbContext db) =>
{
    var item = await db.EmployeeCompetences.FindAsync(id, competenceId); if (item is null) return Results.NotFound();
    db.EmployeeCompetences.Remove(item); await db.SaveChangesAsync(); await Audit(db, "employee.competence.removed", "Employee", id.ToString()); return Results.NoContent();
});

app.MapGet("/api/competences", async (AppDbContext db) => Results.Ok(await db.Competences.OrderBy(x => x.Category).ThenBy(x => x.Name).ToListAsync()));
app.MapPost("/api/competences", async (CreateCompetenceRequest request, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest(new { message = "Competence name is required." });
    if (await db.Competences.AnyAsync(x => x.Name == request.Name.Trim())) return Results.Conflict(new { message = "Competence already exists." });
    var item = new Competence { Name = request.Name.Trim(), Category = request.Category.Trim() }; db.Competences.Add(item); await db.SaveChangesAsync(); return Results.Created($"/api/competences/{item.Id}", item);
});
app.MapDelete("/api/competences/{id:int}", async (int id, AppDbContext db) =>
{
    var competence = await db.Competences.FindAsync(id); if (competence is null) return Results.NotFound();
    if (await db.ShiftRequirements.AnyAsync(x => x.CompetenceId == id)) return Results.Conflict(new { message = "Competence is used by a shift requirement and cannot be deleted." });
    db.Competences.Remove(competence); await db.SaveChangesAsync(); return Results.NoContent();
});

app.MapGet("/api/shifts", async (AppDbContext db, CoverageService coverage) =>
{
    var shifts = await db.Shifts.Include(x => x.Assignments).ThenInclude(x => x.Employee).ThenInclude(x => x.Competences).Include(x => x.Requirements).ThenInclude(x => x.Competence).OrderBy(x => x.Date).ThenBy(x => x.StartTime).ToListAsync();
    return Results.Ok(shifts.Select(coverage.AnalyzeShift));
});

app.MapGet("/api/shifts/{id:int}/coverage", async (int id, AppDbContext db, CoverageService coverage, HttpContext http) =>
{
    try
    {
        var actor = http.User.Identity?.Name ?? "system";
        return Results.Ok(await coverage.EvaluateShiftAsync(db, id, actor));
    }
    catch (ArgumentException ex) { return Results.NotFound(new { message = ex.Message }); }
});

app.MapPost("/api/shifts/{id:int}/coverage/scenario", async (int id, CoverageScenarioRequest request, AppDbContext db, CoverageService coverage, HttpContext http) =>
{
    if (request.RemoveEmployeeIds.Count == 0)
        return Results.BadRequest(new { message = "At least one employee ID must be supplied." });
    try
    {
        var actor = http.User.Identity?.Name ?? "system";
        return Results.Ok(await coverage.EvaluateScenarioAsync(db, id, request.RemoveEmployeeIds.Distinct().ToArray(), actor));
    }
    catch (ArgumentException ex) { return Results.NotFound(new { message = ex.Message }); }
});

app.MapGet("/api/shifts/{id:int}/coverage/history", async (int id, AppDbContext db, int take = 20) =>
{
    if (!await db.Shifts.AnyAsync(x => x.Id == id)) return Results.NotFound();
    var entries = await db.AuditEvents
        .Where(x => x.EntityType == "Shift" && x.EntityId == id.ToString() &&
                    (x.Action == "shift.coverage.evaluated" || x.Action == "shift.coverage.scenario"))
        .OrderByDescending(x => x.OccurredAtUtc)
        .Take(Math.Clamp(take, 1, 100))
        .ToListAsync();
    return Results.Ok(entries);
});

app.MapPost("/api/shifts", async (CreateShiftRequest request, AppDbContext db) =>
{
    if (request.MinimumStaff <= 0 || request.Hours <= 0 || request.Hours > 24) return Results.BadRequest(new { message = "Invalid shift values." });
    var shift = new Shift { Date = request.Date, ShiftType = request.ShiftType.Trim(), Department = request.Department?.Trim() ?? "", StartTime = request.StartTime, Hours = request.Hours, MinimumStaff = request.MinimumStaff, IsCritical = request.IsCritical };
    db.Shifts.Add(shift); await db.SaveChangesAsync(); await Audit(db, "shift.created", "Shift", shift.Id.ToString()); return Results.Created($"/api/shifts/{shift.Id}", shift);
});
app.MapPut("/api/shifts/{id:int}", async (int id, UpdateShiftRequest request, AppDbContext db) =>
{
    var shift = await db.Shifts.FindAsync(id); if (shift is null) return Results.NotFound();
    if (request.MinimumStaff <= 0 || request.Hours <= 0 || request.Hours > 24) return Results.BadRequest(new { message = "Invalid shift values." });
    shift.Date = request.Date; shift.ShiftType = request.ShiftType.Trim(); shift.Department = request.Department?.Trim() ?? ""; shift.StartTime = request.StartTime; shift.Hours = request.Hours; shift.MinimumStaff = request.MinimumStaff; shift.IsCritical = request.IsCritical; shift.IsPublished = request.IsPublished;
    await db.SaveChangesAsync(); await Audit(db, "shift.updated", "Shift", id.ToString()); return Results.Ok(shift);
});
app.MapDelete("/api/shifts/{id:int}", async (int id, AppDbContext db) =>
{
    var shift = await db.Shifts.FindAsync(id); if (shift is null) return Results.NotFound();
    db.Shifts.Remove(shift); await db.SaveChangesAsync(); await Audit(db, "shift.deleted", "Shift", id.ToString()); return Results.NoContent();
});
app.MapPost("/api/shifts/{id:int}/assignments", async (int id, AssignEmployeeRequest request, AppDbContext db, PlanningAdvisor advisor) =>
{
    var shift = await db.Shifts.Include(x => x.Assignments).ThenInclude(x => x.Employee).Include(x => x.Requirements).ThenInclude(x => x.Competence).FirstOrDefaultAsync(x => x.Id == id); if (shift is null) return Results.NotFound();
    var employee = await db.Employees.Include(x => x.Competences).ThenInclude(x => x.Competence).Include(x => x.Absences).FirstOrDefaultAsync(x => x.Id == request.EmployeeId && x.IsActive); if (employee is null) return Results.NotFound();
    var allShifts = await db.Shifts.Include(x => x.Assignments).ToListAsync(); var candidate = advisor.RankCandidates(shift, [employee], allShifts).Single();
    if (!candidate.Eligible) return Results.Conflict(new { message = "Employee cannot safely be assigned to this shift.", reasons = candidate.HardFailures, warnings = candidate.Warnings });
    if (!shift.Assignments.Any(x => x.EmployeeId == request.EmployeeId)) db.ShiftAssignments.Add(new ShiftAssignment { ShiftId = id, EmployeeId = request.EmployeeId });
    await db.SaveChangesAsync(); await Audit(db, "shift.assignment.created", "Shift", id.ToString()); return Results.NoContent();
});
app.MapDelete("/api/shifts/{id:int}/assignments/{employeeId:int}", async (int id, int employeeId, AppDbContext db) =>
{
    var assignment = await db.ShiftAssignments.FindAsync(id, employeeId); if (assignment is null) return Results.NotFound(); db.ShiftAssignments.Remove(assignment); await db.SaveChangesAsync(); await Audit(db, "shift.assignment.removed", "Shift", id.ToString()); return Results.NoContent();
});

app.MapPost("/api/shifts/{id:int}/requirements", async (int id, AddRequirementRequest request, AppDbContext db) =>
{
    if (!await db.Shifts.AnyAsync(x => x.Id == id) || !await db.Competences.AnyAsync(x => x.Id == request.CompetenceId)) return Results.NotFound();
    if (request.MinimumCount <= 0) return Results.BadRequest(new { message = "Minimum count must be greater than zero." });
    var existing = await db.ShiftRequirements.FindAsync(id, request.CompetenceId);
    if (existing is null) db.ShiftRequirements.Add(new ShiftRequirement { ShiftId = id, CompetenceId = request.CompetenceId, MinimumCount = request.MinimumCount, MinimumLevel = request.MinimumLevel, RequiredRole = request.RequiredRole, IsCritical = request.IsCritical });
    else { existing.MinimumCount = request.MinimumCount; existing.MinimumLevel = request.MinimumLevel; existing.RequiredRole = request.RequiredRole; existing.IsCritical = request.IsCritical; }
    await db.SaveChangesAsync(); return Results.NoContent();
});
app.MapDelete("/api/shifts/{id:int}/requirements/{competenceId:int}", async (int id, int competenceId, AppDbContext db) =>
{
    var requirement = await db.ShiftRequirements.FindAsync(id, competenceId); if (requirement is null) return Results.NotFound(); db.ShiftRequirements.Remove(requirement); await db.SaveChangesAsync(); return Results.NoContent();
});

app.MapGet("/api/shifts/{id:int}/candidates", async (int id, AppDbContext db, PlanningAdvisor advisor) =>
{
    var shift = await db.Shifts.Include(x => x.Assignments).Include(x => x.Requirements).ThenInclude(x => x.Competence).FirstOrDefaultAsync(x => x.Id == id); if (shift is null) return Results.NotFound();
    var employees = await db.Employees.Include(x => x.Competences).ThenInclude(x => x.Competence).Include(x => x.Absences).Where(x => x.IsActive).ToListAsync();
    var shifts = await db.Shifts.Include(x => x.Assignments).ToListAsync(); return Results.Ok(advisor.RankCandidates(shift, employees, shifts));
});

app.MapPost("/api/scenarios/absence", async (ScenarioAbsenceRequest request, AppDbContext db, PlanningAdvisor advisor, CoverageService coverage) =>
{
    var affected = await db.Shifts.Include(x => x.Assignments).ThenInclude(x => x.Employee).ThenInclude(x => x.Competences).Include(x => x.Requirements).ThenInclude(x => x.Competence).Where(x => x.Date == request.Date && x.Assignments.Any(a => a.EmployeeId == request.EmployeeId)).ToListAsync();
    var allShifts = await db.Shifts.Include(x => x.Assignments).ToListAsync();
    var employees = await db.Employees.Include(x => x.Competences).ThenInclude(x => x.Competence).Include(x => x.Absences).Where(x => x.IsActive && x.Id != request.EmployeeId).ToListAsync();
    var result = affected.Select(shift =>
    {
        var remaining = shift.Assignments.Where(a => a.EmployeeId != request.EmployeeId).ToList();
        var clone = new Shift { Id = shift.Id, Date = shift.Date, ShiftType = shift.ShiftType, Hours = shift.Hours, MinimumStaff = shift.MinimumStaff, StartTime = shift.StartTime, Assignments = remaining, Requirements = shift.Requirements };
        var analysis = coverage.AnalyzeShift(clone);
        return new ScenarioResult(shift.Id, analysis.OverallCovered, analysis.MissingStaff, analysis.Requirements.Count(x => !x.Covered), advisor.RankCandidates(shift, employees, allShifts), analysis.Warnings ?? []);
    }).ToList();
    return Results.Ok(new { simulatedEmployeeId = request.EmployeeId, simulatedDate = request.Date, affectedShifts = result });
});

app.MapPost("/api/absences", async (CreateAbsenceRequest request, AppDbContext db) =>
{
    if (request.To < request.From) return Results.BadRequest(new { message = "To-date cannot be before from-date." });
    if (!await db.Employees.AnyAsync(x => x.Id == request.EmployeeId)) return Results.NotFound();
    var absence = new Absence { EmployeeId = request.EmployeeId, From = request.From, To = request.To, Type = request.Type, Note = request.Note, Approved = request.Approved };
    db.Absences.Add(absence); await db.SaveChangesAsync(); await Audit(db, "absence.created", "Employee", request.EmployeeId.ToString(), request.Note); return Results.Created($"/api/absences/{absence.Id}", absence);
});
app.MapGet("/api/absences", async (AppDbContext db, int? employeeId, DateOnly? from, DateOnly? to) =>
{
    var q = db.Absences.Include(x => x.Employee).AsQueryable(); if (employeeId.HasValue) q = q.Where(x => x.EmployeeId == employeeId); if (from.HasValue) q = q.Where(x => x.To >= from); if (to.HasValue) q = q.Where(x => x.From <= to); return Results.Ok(await q.OrderBy(x => x.From).ToListAsync());
});
app.MapDelete("/api/absences/{id:int}", async (int id, AppDbContext db) => { var a = await db.Absences.FindAsync(id); if (a is null) return Results.NotFound(); db.Absences.Remove(a); await db.SaveChangesAsync(); await Audit(db, "absence.deleted", "Absence", id.ToString()); return Results.NoContent(); });

app.MapGet("/api/dashboard", async (AppDbContext db, CoverageService coverage) =>
{
    var employees = await db.Employees.Include(x => x.Competences).Include(x => x.Absences).ToListAsync();
    var shifts = await db.Shifts.Include(x => x.Assignments).ThenInclude(x => x.Employee).ThenInclude(x => x.Competences).Include(x => x.Requirements).ThenInclude(x => x.Competence).OrderBy(x => x.Date).ToListAsync();
    var analyses = shifts.Select(coverage.AnalyzeShift).ToList(); var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var expiring = employees.SelectMany(e => e.Competences).Count(c => c.ValidUntil.HasValue && c.ValidUntil.Value >= today && c.ValidUntil.Value <= today.AddDays(45));
    return Results.Ok(new { TotalEmployees = employees.Count(x => x.IsActive), ActiveCompetences = await db.Competences.CountAsync(), ActionRequiredShifts = analyses.Count(x => x.OverallStatus == "RED"), WarningShifts = analyses.Count(x => x.OverallStatus == "YELLOW"), CompetencesExpiring45Days = expiring, CompetenceCoverage = analyses.Count == 0 ? 100 : (int)Math.Round(analyses.Average(x => x.CompetenceCoverage)), UpcomingShifts = analyses });
});
app.MapGet("/api/audit", async (AppDbContext db, int take = 100) => Results.Ok(await db.AuditEvents.OrderByDescending(x => x.OccurredAtUtc).Take(Math.Clamp(take, 1, 500)).ToListAsync()));

app.Run();

static async Task Audit(AppDbContext db, string action, string entityType, string entityId, string? reason = null)
{
    db.AuditEvents.Add(new AuditEvent { Action = action, EntityType = entityType, EntityId = entityId, Reason = reason, Actor = "system" });
    await db.SaveChangesAsync();
}

public sealed record CoverageScenarioRequest(List<int> RemoveEmployeeIds);

public partial class Program { }
