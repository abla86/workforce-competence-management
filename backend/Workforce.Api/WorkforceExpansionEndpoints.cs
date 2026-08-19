using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;
using Workforce.Api.DTOs;
using Workforce.Api.Models;
using Workforce.Api.Services;

namespace Workforce.Api;

public static class WorkforceExpansionEndpoints
{
    public static void MapWorkforceExpansionEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.Use(async (context, next) =>
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.EnsureCreatedAsync();
                await next(context);
            });
        }

        app.MapPut("/api/employees/{id:int}/availability", async (int id, SetEmployeeAvailabilityRequest request, AppDbContext db, EmployeeAccessService access, HttpContext http) =>
        {
            if (!await access.CanAccessEmployeeAsync(http.User, id)) return Results.Forbid();
            var item = await db.EmployeeAvailability.FindAsync(id, request.Date);
            if (item is null) { item = new EmployeeAvailability { EmployeeId = id, Date = request.Date, IsAvailable = request.IsAvailable, Reason = request.Reason.Trim() }; db.EmployeeAvailability.Add(item); }
            else { item.IsAvailable = request.IsAvailable; item.Reason = request.Reason.Trim(); }
            await db.SaveChangesAsync(); return Results.Ok(item);
        }).RequireAuthorization("CoverageManage");

        app.MapGet("/api/employees/{id:int}/availability", async (int id, AppDbContext db, EmployeeAccessService access, HttpContext http) =>
        {
            if (!await access.CanAccessEmployeeAsync(http.User, id)) return Results.Forbid();
            return Results.Ok(await db.EmployeeAvailability.Where(x => x.EmployeeId == id).OrderBy(x => x.Date).ToListAsync());
        }).RequireAuthorization("CoverageRead");

        app.MapDelete("/api/employees/{id:int}/availability/{date}", async (int id, DateOnly date, AppDbContext db, EmployeeAccessService access, HttpContext http) =>
        {
            if (!await access.CanAccessEmployeeAsync(http.User, id)) return Results.Forbid();
            var item = await db.EmployeeAvailability.FindAsync(id, date); if (item is null) return Results.NotFound();
            db.EmployeeAvailability.Remove(item); await db.SaveChangesAsync(); return Results.NoContent();
        }).RequireAuthorization("CoverageManage");

        app.MapGet("/api/availability/team/{departmentId:int}", async (int departmentId, DateTime? date, AppDbContext db) =>
        {
            var requestedDate = date?.Date ?? DateTime.Today;
            var service = new EmployeeAvailabilityService(db, new NotificationService(db));
            var statuses = await service.GetTeamStatusAsync(departmentId, requestedDate);
            return Results.Ok(new { date = requestedDate.ToString("yyyy-MM-dd"), total = statuses.Count, available = statuses.Count(s => s.Status == EmployeeAvailabilityStatus.Available), busy = statuses.Count(s => s.Status == EmployeeAvailabilityStatus.Busy), absent = statuses.Count(s => s.Status is EmployeeAvailabilityStatus.Sick or EmployeeAvailabilityStatus.OnVacation or EmployeeAvailabilityStatus.Away), byStatus = statuses.GroupBy(s => s.Status).ToDictionary(g => g.Key.ToString(), g => g.ToList()) });
        }).RequireAuthorization("CoverageRead").WithName("GetTeamAvailability").WithTags("Tilgjengelighet");

        app.MapPut("/api/availability/me/status", async (EmployeeStatusRequest request, ClaimsPrincipal user, AppDbContext db) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
            if (!int.TryParse(userId, out var employeeId)) return Results.Unauthorized();
            if (request.EndTime.HasValue && request.EndTime.Value <= DateTime.UtcNow) return Results.BadRequest(new { message = "EndTime must be in the future." });
            var employee = await db.Employees.FindAsync(employeeId);
            if (employee is null) return Results.NotFound(new { message = $"Employee {employeeId} not found." });
            var service = new EmployeeAvailabilityService(db, new NotificationService(db));
            await service.SetEmployeeStatusAsync(employeeId, request.Status, request.StatusText, request.EndTime, userId!);
            return Results.Ok(new { employeeId, status = request.Status, statusText = request.StatusText, endTime = request.EndTime });
        }).RequireAuthorization().WithName("UpdateMyStatus").WithTags("Tilgjengelighet");

        app.MapGet("/api/employees/{id:int}/status", async (int id, DateTime? date, AppDbContext db, EmployeeAccessService access, HttpContext http) =>
        {
            if (!await access.CanAccessEmployeeAsync(http.User, id)) return Results.Forbid();
            return Results.Ok(await new EmployeeAvailabilityService(db, new NotificationService(db)).GetEmployeeStatusAsync(id, date ?? DateTime.Today));
        }).RequireAuthorization("CoverageRead");

        app.MapPut("/api/employees/{id:int}/status", async (int id, EmployeeStatusRequest request, AppDbContext db, EmployeeAccessService access, HttpContext http) =>
        {
            if (!await access.CanAccessEmployeeAsync(http.User, id)) return Results.Forbid();
            var service = new EmployeeAvailabilityService(db, new NotificationService(db));
            await service.SetEmployeeStatusAsync(id, request.Status, request.StatusText, request.EndTime, http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown");
            return Results.Ok(await service.GetEmployeeStatusAsync(id, DateTime.Today));
        }).RequireAuthorization("CoverageRead");

        app.MapGet("/api/who-is-working/today/{departmentId:int}", async (int departmentId, DateTime? date, AppDbContext db) =>
        {
            var target = date?.Date ?? DateTime.Today;
            var employees = await new ShiftPlanService(db, new NotificationService(db)).GetWhoIsWorkingTodayAsync(departmentId, target);
            var availability = new EmployeeAvailabilityService(db, new NotificationService(db));
            var result = new List<object>();
            foreach (var employee in employees)
            {
                var status = await availability.GetEmployeeStatusAsync(employee.Id, target);
                var shifts = employee.ShiftAssignments.Where(a => a.Shift.StartTime.Date == target.Date).OrderBy(a => a.Shift.StartTime).Select(a => new { a.ShiftId, a.Shift.StartTime, a.Shift.EndTime, a.Shift.ShiftType }).ToList();
                result.Add(new { id = employee.Id, name = employee.Name, role = employee.Role, status = status.Status, statusText = status.StatusText, shifts });
            }
            return Results.Ok(result);
        }).RequireAuthorization("CoverageRead").WithName("GetWhoIsWorkingToday").WithTags("Tilgjengelighet");

        app.MapGet("/api/dailyplans/today/{departmentId:int}", async (int departmentId, ClaimsPrincipal user, AppDbContext db) =>
        {
            var service = new DailyPlanService(db, new NotificationService(db));
            return Results.Ok(await service.GetTodayPlanAsync(departmentId) ?? await service.CreateDailyPlanAsync(departmentId, user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system"));
        }).RequireAuthorization("CoverageRead").WithName("GetTodayDailyPlan").WithTags("Dagsplan");
        app.MapGet("/api/dailyplans/history/{departmentId:int}", async (int departmentId, int? days, AppDbContext db) => Results.Ok(await new DailyPlanService(db, new NotificationService(db)).GetRecentDailyPlansAsync(departmentId, Math.Clamp(days ?? 7, 1, 90)))).RequireAuthorization("CoverageRead");
        app.MapPost("/api/dailyplans/today/publish/{departmentId:int}", async (int departmentId, ClaimsPrincipal user, AppDbContext db) =>
        {
            var service = new DailyPlanService(db, new NotificationService(db)); var userId = user.FindFirstValue(ClaimTypes.NameIdentifier); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var plan = await service.GetTodayPlanAsync(departmentId) ?? await service.CreateDailyPlanAsync(departmentId, userId); await service.PublishDailyPlanAsync(plan.Id, userId); return Results.Ok(new { planId = plan.Id });
        }).RequireAuthorization("CoverageManage");
        app.MapPost("/api/dailyplans/{planId:int}/tasks", async (int planId, DailyTaskRequest request, AppDbContext db, ClaimsPrincipal user) =>
        {
            var service = new DailyPlanService(db, new NotificationService(db)); await service.AddTaskToDailyPlanAsync(planId, new DailyTaskItem { Title = request.Title.Trim(), Description = request.Description, StartTime = request.StartTime, EndTime = request.EndTime, SortOrder = request.SortOrder }, user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown");
            return Results.Ok(await db.DailyPlans.Include(p => p.Tasks).FirstAsync(p => p.Id == planId));
        }).RequireAuthorization("CoverageManage");

        app.MapGet("/api/shiftplans/current/{departmentId:int}", async (int departmentId, AppDbContext db) => Results.Ok(await new ShiftPlanService(db, new NotificationService(db)).GetCurrentPublishedShiftPlanAsync(departmentId))).RequireAuthorization("CoverageRead").WithName("GetCurrentShiftPlan").WithTags("Skiftplan");
        app.MapGet("/api/shiftplans/history/{departmentId:int}", async (int departmentId, int? count, AppDbContext db) => Results.Ok(await new ShiftPlanService(db, new NotificationService(db)).GetShiftPlanHistoryAsync(departmentId, Math.Clamp(count ?? 5, 1, 50)))).RequireAuthorization("CoverageRead").WithName("GetShiftPlanHistory").WithTags("Skiftplan");
        app.MapPost("/api/shiftplans", async (CreateShiftPlanRequest request, ClaimsPrincipal user, AppDbContext db) => Results.Ok(await new ShiftPlanService(db, new NotificationService(db)).CreateShiftPlanAsync(request.DepartmentId, request.StartDate, request.EndDate, user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown"))).RequireAuthorization("CoverageManage");
        app.MapPost("/api/shiftplans/{planId:int}/publish", async (int planId, ClaimsPrincipal user, AppDbContext db) => Results.Ok(await new ShiftPlanService(db, new NotificationService(db)).PublishShiftPlanAsync(planId, user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown"))).RequireAuthorization("CoverageManage");

        app.MapPost("/api/shifts/{id:int}/auto-staff", async (int id, AutoStaffingRequest request, AppDbContext db) => { if (id != request.ShiftId) return Results.BadRequest(new { message = "Path shift id and request shift id must match." }); return Results.Ok(await new AutoStaffingService(db).GenerateAsync(request)); }).RequireAuthorization("CoverageManage");
        app.MapGet("/api/shifts/{id:int}/viability", async (int id, int employeeId, DateTime start, DateTime end, AppDbContext db) => Results.Ok(await new ShiftViabilityService(db).CheckAsync(employeeId, start, end))).RequireAuthorization("CoverageManage");
        app.MapGet("/api/shifts/{id:int}/candidates", async (int id, AppDbContext db) => { if (!await db.Shifts.AnyAsync(x => x.Id == id)) return Results.NotFound(); return Results.Ok(await new ShiftMatchingService(db).FindCandidatesAsync(id)); }).RequireAuthorization("CoverageManage");

        app.MapGet("/api/notifications", async (ClaimsPrincipal user, AppDbContext db) => { var subject = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"); if (string.IsNullOrWhiteSpace(subject)) return Results.Unauthorized(); var employee = await db.Employees.FirstOrDefaultAsync(e => e.IdentitySubject == subject); if (employee is null) return Results.Ok(Array.Empty<object>()); return Results.Ok(await db.Notifications.Where(n => n.EmployeeId == employee.Id).OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync()); }).RequireAuthorization("CoverageRead");
        app.MapPut("/api/notifications/{id:int}/read", async (int id, ClaimsPrincipal user, AppDbContext db) => { var subject = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"); var employee = await db.Employees.FirstOrDefaultAsync(e => e.IdentitySubject == subject); var item = employee is null ? null : await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.EmployeeId == employee.Id); if (item is null) return Results.NotFound(); item.IsRead = true; await db.SaveChangesAsync(); return Results.NoContent(); }).RequireAuthorization("CoverageRead");

        app.MapGet("/api/absences/{employeeId:int}", async (int employeeId, AppDbContext db, EmployeeAccessService access, HttpContext http) => { if (!await access.CanAccessEmployeeAsync(http.User, employeeId)) return Results.Forbid(); return Results.Ok(await db.Absences.Where(a => a.EmployeeId == employeeId).OrderByDescending(a => a.StartDate).ToListAsync()); }).RequireAuthorization("CoverageRead");
        app.MapPost("/api/absences", async (AbsenceRequest request, AppDbContext db) => { if (request.EndDate <= request.StartDate) return Results.BadRequest(new { message = "EndDate must be after StartDate." }); var item = new Absence { EmployeeId = request.EmployeeId, Type = request.Type, StartDate = request.StartDate, EndDate = request.EndDate, Description = request.Description }; db.Absences.Add(item); await db.SaveChangesAsync(); return Results.Created($"/api/absences/{item.Id}", item); }).RequireAuthorization("CoverageManage");
        app.MapPut("/api/absences/{id:int}/approve", async (int id, ClaimsPrincipal user, AppDbContext db) => { var item = await db.Absences.FindAsync(id); if (item is null) return Results.NotFound(); item.IsApproved = true; item.ApprovedBySubject = user.FindFirstValue(ClaimTypes.NameIdentifier); item.ApprovedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return Results.Ok(item); }).RequireAuthorization("CoverageManage");

        app.MapGet("/api/rules", async (AppDbContext db) => Results.Ok(await db.ShiftRules.Where(r => r.IsActive).OrderBy(r => r.RuleType).ToListAsync())).RequireAuthorization("CoverageRead");
        app.MapGet("/api/dispensations", async (int? employeeId, AppDbContext db) => { var query = db.ShiftDispensations.AsQueryable(); if (employeeId.HasValue) query = query.Where(x => x.EmployeeId == employeeId.Value); return Results.Ok(await query.OrderByDescending(x => x.GrantedAt).Take(100).ToListAsync()); }).RequireAuthorization("CoverageManage");
        app.MapPost("/api/dispensations", async (DispensationRequest request, ClaimsPrincipal user, AppDbContext db) => { if (request.HoursGranted <= 0 || string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest(); var item = new ShiftDispensation { EmployeeId = request.EmployeeId, ShiftId = request.ShiftId, BreachedRule = request.BreachedRule, HoursGranted = request.HoursGranted, Reason = request.Reason.Trim(), GrantedBySubject = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", Status = DispensationStatus.Pending, ExpiresAt = request.ExpiresAt }; db.ShiftDispensations.Add(item); await db.SaveChangesAsync(); return Results.Created($"/api/dispensations/{item.Id}", item); }).RequireAuthorization("CoverageManage");
        app.MapPut("/api/dispensations/{id:int}/decision", async (int id, DispensationDecisionRequest request, AppDbContext db) => { var item = await db.ShiftDispensations.FindAsync(id); if (item is null) return Results.NotFound(); item.Status = request.Approve ? DispensationStatus.Approved : DispensationStatus.Rejected; item.Comments = request.Comments; await db.SaveChangesAsync(); return Results.Ok(item); }).RequireAuthorization("CoverageManage");
    }
}

public sealed record EmployeeStatusRequest(EmployeeAvailabilityStatus Status, string? StatusText, DateTime? EndTime);
public sealed record DailyTaskRequest(string Title, string? Description, DateTime? StartTime, DateTime? EndTime, int SortOrder = 0);
public sealed record CreateShiftPlanRequest(int DepartmentId, DateTime StartDate, DateTime EndDate);
public sealed record AbsenceRequest(int EmployeeId, AbsenceType Type, DateTime StartDate, DateTime EndDate, string? Description);
public sealed record DispensationRequest(int EmployeeId, int ShiftId, RuleType BreachedRule, int HoursGranted, string Reason, DateTime? ExpiresAt);
public sealed record DispensationDecisionRequest(bool Approve, string? Comments);
