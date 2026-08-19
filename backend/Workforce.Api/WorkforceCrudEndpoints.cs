using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;
using Workforce.Api.Models;

namespace Workforce.Api;

public static class WorkforceCrudEndpoints
{
    public static void MapWorkforceCrudEndpoints(this WebApplication app)
    {
        app.MapGet("/api/employees", async (AppDbContext db) =>
        {
            var employees = await db.Employees
                .Include(x => x.Competences).ThenInclude(x => x.Competence)
                .OrderBy(x => x.Name)
                .Select(x => new
                {
                    x.Id, x.Name, x.Role, x.PositionPercent, x.IsActive, x.IdentitySubject,
                    competences = x.Competences.Select(c => new
                    {
                        c.CompetenceId,
                        name = c.Competence.Name,
                        c.Level,
                        c.ValidUntil,
                        status = c.ValidUntil.HasValue && c.ValidUntil.Value < DateOnly.FromDateTime(DateTime.UtcNow) ? "EXPIRED" : "VALID"
                    })
                }).ToListAsync();
            return Results.Ok(employees);
        }).RequireAuthorization("CoverageRead").WithTags("employees");

        app.MapPost("/api/employees", async (EmployeeRequest request, AppDbContext db) =>
        {
            var employee = new Employee
            {
                Name = request.Name.Trim(),
                Role = request.Role.Trim(),
                PositionPercent = request.PositionPercent,
                IdentitySubject = string.IsNullOrWhiteSpace(request.IdentitySubject) ? null : request.IdentitySubject.Trim()
            };
            db.Employees.Add(employee);
            await db.SaveChangesAsync();
            return Results.Created($"/api/employees/{employee.Id}", employee);
        }).RequireAuthorization("CoverageManage").WithTags("employees");

        app.MapPut("/api/employees/{id:int}", async (int id, EmployeeRequest request, AppDbContext db) =>
        {
            var employee = await db.Employees.FindAsync(id);
            if (employee is null) return Results.NotFound();
            employee.Name = request.Name.Trim();
            employee.Role = request.Role.Trim();
            employee.PositionPercent = request.PositionPercent;
            employee.IdentitySubject = string.IsNullOrWhiteSpace(request.IdentitySubject) ? null : request.IdentitySubject.Trim();
            await db.SaveChangesAsync();
            return Results.Ok(employee);
        }).RequireAuthorization("CoverageManage").WithTags("employees");

        app.MapDelete("/api/employees/{id:int}", async (int id, AppDbContext db) =>
        {
            var employee = await db.Employees.FindAsync(id);
            if (employee is null) return Results.NotFound();
            employee.IsActive = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("CoverageManage").WithTags("employees");

        app.MapGet("/api/competences", async (AppDbContext db) =>
            Results.Ok(await db.Competences.OrderBy(x => x.Name).ToListAsync()))
            .RequireAuthorization("CoverageRead").WithTags("competences");

        app.MapPost("/api/competences", async (CompetenceRequest request, AppDbContext db) =>
        {
            if (await db.Competences.AnyAsync(x => x.Name == request.Name.Trim()))
                return Results.Conflict(new { message = "Competence already exists." });
            var item = new Competence { Name = request.Name.Trim(), Category = request.Category?.Trim() ?? "General" };
            db.Competences.Add(item);
            await db.SaveChangesAsync();
            return Results.Created($"/api/competences/{item.Id}", item);
        }).RequireAuthorization("CoverageManage").WithTags("competences");

        app.MapDelete("/api/competences/{id:int}", async (int id, AppDbContext db) =>
        {
            var item = await db.Competences.FindAsync(id);
            if (item is null) return Results.NotFound();
            if (await db.EmployeeCompetences.AnyAsync(x => x.CompetenceId == id) || await db.ShiftRequirements.AnyAsync(x => x.CompetenceId == id))
                return Results.Conflict(new { message = "Competence is still referenced and cannot be deleted." });
            db.Competences.Remove(item);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("CoverageManage").WithTags("competences");

        app.MapPost("/api/employees/{employeeId:int}/competences", async (int employeeId, EmployeeCompetenceRequest request, AppDbContext db) =>
        {
            if (!await db.Employees.AnyAsync(x => x.Id == employeeId)) return Results.NotFound(new { message = "Employee not found." });
            if (!await db.Competences.AnyAsync(x => x.Id == request.CompetenceId)) return Results.NotFound(new { message = "Competence not found." });
            var item = await db.EmployeeCompetences.FindAsync(employeeId, request.CompetenceId);
            if (item is null)
            {
                item = new EmployeeCompetence { EmployeeId = employeeId, CompetenceId = request.CompetenceId };
                db.EmployeeCompetences.Add(item);
            }
            item.Level = request.Level.Trim();
            item.ValidUntil = request.ValidUntil;
            await db.SaveChangesAsync();
            return Results.Ok(item);
        }).RequireAuthorization("CoverageManage").WithTags("competences");

        app.MapDelete("/api/employees/{employeeId:int}/competences/{competenceId:int}", async (int employeeId, int competenceId, AppDbContext db) =>
        {
            var item = await db.EmployeeCompetences.FindAsync(employeeId, competenceId);
            if (item is null) return Results.NotFound();
            db.EmployeeCompetences.Remove(item);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("CoverageManage").WithTags("competences");

        app.MapGet("/api/shifts", async (AppDbContext db) =>
        {
            var shifts = await db.Shifts
                .Include(x => x.Assignments).ThenInclude(x => x.Employee)
                .Include(x => x.Requirements).ThenInclude(x => x.Competence)
                .Include(x => x.ShiftTasks).ThenInclude(x => x.WorkTask)
                .OrderBy(x => x.Date).ThenBy(x => x.StartTime)
                .ToListAsync();

            var result = shifts.Select(shift => new
            {
                shift.Id,
                shift.Date,
                shift.ShiftType,
                shift.Hours,
                shift.MinimumStaff,
                shift.StartTime,
                shift.EndTime,
                assignedStaff = shift.Assignments.Count,
                staffingCovered = shift.Assignments.Count >= shift.MinimumStaff,
                missingStaff = Math.Max(0, shift.MinimumStaff - shift.Assignments.Count),
                taskNames = shift.ShiftTasks.Select(x => x.WorkTask.Name).ToArray(),
                requirements = shift.Requirements.Select(req => new
                {
                    competence = req.Competence.Name,
                    minimumCount = req.MinimumCount,
                    minimumLevel = req.MinimumLevel,
                    qualifiedCount = shift.Assignments.Count(a => a.Employee.Competences.Any(c => c.CompetenceId == req.CompetenceId && LevelRank(c.Level) >= LevelRank(req.MinimumLevel) && (!c.ValidUntil.HasValue || c.ValidUntil.Value >= shift.Date))),
                    covered = shift.Assignments.Count(a => a.Employee.Competences.Any(c => c.CompetenceId == req.CompetenceId && LevelRank(c.Level) >= LevelRank(req.MinimumLevel) && (!c.ValidUntil.HasValue || c.ValidUntil.Value >= shift.Date))) >= req.MinimumCount
                })
            }).ToList();
            return Results.Ok(result);
        }).RequireAuthorization("CoverageRead").WithTags("shifts");

        app.MapPost("/api/shifts", async (ShiftRequest request, AppDbContext db) =>
        {
            var shift = new Shift
            {
                Date = request.Date,
                ShiftType = request.ShiftType.Trim(),
                Hours = request.Hours,
                MinimumStaff = request.MinimumStaff,
                StartTime = request.StartTime,
                EndTime = request.EndTime
            };
            db.Shifts.Add(shift);
            await db.SaveChangesAsync();
            return Results.Created($"/api/shifts/{shift.Id}", shift);
        }).RequireAuthorization("CoverageManage").WithTags("shifts");

        app.MapPut("/api/shifts/{id:int}", async (int id, ShiftRequest request, AppDbContext db) =>
        {
            var shift = await db.Shifts.FindAsync(id);
            if (shift is null) return Results.NotFound();
            shift.Date = request.Date;
            shift.ShiftType = request.ShiftType.Trim();
            shift.Hours = request.Hours;
            shift.MinimumStaff = request.MinimumStaff;
            shift.StartTime = request.StartTime;
            shift.EndTime = request.EndTime;
            await db.SaveChangesAsync();
            return Results.Ok(shift);
        }).RequireAuthorization("CoverageManage").WithTags("shifts");

        app.MapDelete("/api/shifts/{id:int}", async (int id, AppDbContext db) =>
        {
            var shift = await db.Shifts.FindAsync(id);
            if (shift is null) return Results.NotFound();
            db.Shifts.Remove(shift);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("CoverageManage").WithTags("shifts");

        app.MapPost("/api/shifts/{shiftId:int}/assignments", async (int shiftId, AssignmentRequest request, AppDbContext db) =>
        {
            if (!await db.Shifts.AnyAsync(x => x.Id == shiftId) || !await db.Employees.AnyAsync(x => x.Id == request.EmployeeId && x.IsActive)) return Results.NotFound();
            if (await db.ShiftAssignments.AnyAsync(x => x.ShiftId == shiftId && x.EmployeeId == request.EmployeeId)) return Results.Conflict(new { message = "Employee is already assigned." });
            db.ShiftAssignments.Add(new ShiftAssignment { ShiftId = shiftId, EmployeeId = request.EmployeeId });
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("CoverageManage").WithTags("shifts");

        app.MapDelete("/api/shifts/{shiftId:int}/assignments/{employeeId:int}", async (int shiftId, int employeeId, AppDbContext db) =>
        {
            var item = await db.ShiftAssignments.FindAsync(shiftId, employeeId);
            if (item is null) return Results.NotFound();
            db.ShiftAssignments.Remove(item);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("CoverageManage").WithTags("shifts");

        app.MapPost("/api/shifts/{shiftId:int}/requirements", async (int shiftId, ShiftRequirementRequest request, AppDbContext db) =>
        {
            if (!await db.Shifts.AnyAsync(x => x.Id == shiftId) || !await db.Competences.AnyAsync(x => x.Id == request.CompetenceId)) return Results.NotFound();
            var item = await db.ShiftRequirements.FindAsync(shiftId, request.CompetenceId);
            if (item is null)
            {
                item = new ShiftRequirement { ShiftId = shiftId, CompetenceId = request.CompetenceId };
                db.ShiftRequirements.Add(item);
            }
            item.MinimumCount = Math.Max(1, request.MinimumCount);
            item.MinimumLevel = request.MinimumLevel.Trim();
            await db.SaveChangesAsync();
            return Results.Ok(item);
        }).RequireAuthorization("CoverageManage").WithTags("shifts");

        app.MapDelete("/api/shifts/{shiftId:int}/requirements/{competenceId:int}", async (int shiftId, int competenceId, AppDbContext db) =>
        {
            var item = await db.ShiftRequirements.FindAsync(shiftId, competenceId);
            if (item is null) return Results.NotFound();
            db.ShiftRequirements.Remove(item);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("CoverageManage").WithTags("shifts");

        app.MapGet("/api/dashboard", async (AppDbContext db) =>
        {
            var employees = await db.Employees.Where(x => x.IsActive).ToListAsync();
            var competences = await db.Competences.ToListAsync();
            var shifts = await db.Shifts
                .Include(x => x.Assignments).ThenInclude(x => x.Employee).ThenInclude(x => x.Competences)
                .Include(x => x.Requirements).ThenInclude(x => x.Competence)
                .OrderBy(x => x.Date).ThenBy(x => x.StartTime)
                .Take(20).ToListAsync();

            var summaries = shifts.Select(shift =>
            {
                var coveredRequirements = shift.Requirements.Count == 0 || shift.Requirements.All(req => shift.Assignments.Count(a => a.Employee.Competences.Any(c => c.CompetenceId == req.CompetenceId && LevelRank(c.Level) >= LevelRank(req.MinimumLevel) && (!c.ValidUntil.HasValue || c.ValidUntil.Value >= shift.Date))) >= req.MinimumCount);
                var staffingCovered = shift.Assignments.Count >= shift.MinimumStaff;
                var overallCovered = staffingCovered && coveredRequirements;
                return new
                {
                    shift.Id,
                    date = shift.Date.ToString("yyyy-MM-dd"),
                    shiftType = shift.ShiftType,
                    assignedStaff = shift.Assignments.Count,
                    minimumStaff = shift.MinimumStaff,
                    staffingCovered,
                    missingStaff = Math.Max(0, shift.MinimumStaff - shift.Assignments.Count),
                    competenceCoverage = shift.Requirements.Count == 0 ? 100 : (int)Math.Round(100.0 * shift.Requirements.Count(req => shift.Assignments.Count(a => a.Employee.Competences.Any(c => c.CompetenceId == req.CompetenceId && LevelRank(c.Level) >= LevelRank(req.MinimumLevel) && (!c.ValidUntil.HasValue || c.ValidUntil.Value >= shift.Date))) >= req.MinimumCount) / shift.Requirements.Count),
                    overallCovered,
                    overallStatus = overallCovered ? "GOOD" : "ACTION_REQUIRED"
                };
            }).ToList();

            return Results.Ok(new
            {
                totalEmployees = employees.Count,
                activeCompetences = competences.Count,
                competenceCoverage = summaries.Count == 0 ? 100 : (int)Math.Round(summaries.Average(x => x.competenceCoverage)),
                actionRequiredShifts = summaries.Count(x => !x.overallCovered),
                upcomingShifts = summaries
            });
        }).RequireAuthorization("CoverageRead").WithTags("dashboard");
    }

    private static int LevelRank(string level) => int.TryParse(level, out var numeric) ? numeric : level.Trim().ToLowerInvariant() switch { "basic" => 1, "intermediate" => 2, "advanced" => 3, "expert" => 4, _ => 0 };
}

public sealed record EmployeeRequest(string Name, string Role, decimal PositionPercent, string? IdentitySubject);
public sealed record CompetenceRequest(string Name, string? Category);
public sealed record EmployeeCompetenceRequest(int CompetenceId, string Level, DateOnly? ValidUntil);
public sealed record ShiftRequest(DateOnly Date, string ShiftType, decimal Hours, int MinimumStaff, DateTime? StartTime, DateTime? EndTime);
public sealed record AssignmentRequest(int EmployeeId);
public sealed record ShiftRequirementRequest(int CompetenceId, int MinimumCount, string MinimumLevel);
