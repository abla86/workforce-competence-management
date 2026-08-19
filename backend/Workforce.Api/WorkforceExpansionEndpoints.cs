using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;
using Workforce.Api.DTOs;
using Workforce.Api.Services;

namespace Workforce.Api;

public static class WorkforceExpansionEndpoints
{
    public static void MapWorkforceExpansionEndpoints(this WebApplication app)
    {
        app.MapPut("/api/employees/{id:int}/availability", async (int id, SetEmployeeAvailabilityRequest request, AppDbContext db, EmployeeAccessService access, HttpContext http) =>
        {
            if (!await access.CanAccessEmployeeAsync(http.User, id)) return Results.Forbid();
            var item = await db.EmployeeAvailability.FindAsync(id, request.Date);
            if (item is null)
            {
                item = new Models.EmployeeAvailability { EmployeeId = id, Date = request.Date, IsAvailable = request.IsAvailable, Reason = request.Reason.Trim() };
                db.EmployeeAvailability.Add(item);
            }
            else
            {
                item.IsAvailable = request.IsAvailable;
                item.Reason = request.Reason.Trim();
            }
            await db.SaveChangesAsync();
            return Results.Ok(item);
        })
        .RequireAuthorization("CoverageManage");

        app.MapGet("/api/employees/{id:int}/availability", async (int id, AppDbContext db, EmployeeAccessService access, HttpContext http) =>
        {
            if (!await access.CanAccessEmployeeAsync(http.User, id)) return Results.Forbid();
            return Results.Ok(await db.EmployeeAvailability.Where(x => x.EmployeeId == id).OrderBy(x => x.Date).ToListAsync());
        })
        .RequireAuthorization("CoverageRead");

        app.MapDelete("/api/employees/{id:int}/availability/{date}", async (int id, DateOnly date, AppDbContext db, EmployeeAccessService access, HttpContext http) =>
        {
            if (!await access.CanAccessEmployeeAsync(http.User, id)) return Results.Forbid();
            var item = await db.EmployeeAvailability.FindAsync(id, date);
            if (item is null) return Results.NotFound();
            db.EmployeeAvailability.Remove(item);
            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .RequireAuthorization("CoverageManage");

        app.MapGet("/api/shifts/{id:int}/candidates", async (int id, ClaimsPrincipal user, ShiftAccessService access, CoverageEvaluationEngine engine) =>
        {
            if (!await access.CanAccessShiftAsync(user, id)) return Results.Forbid();
            try
            {
                return Results.Ok(await engine.FindQualifiedReplacementsAsync(id, []));
            }
            catch (ArgumentException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
        })
        .RequireAuthorization("CoverageManage")
        .RequireRateLimiting("coverage")
        .WithTags("coverage");
    }
}
