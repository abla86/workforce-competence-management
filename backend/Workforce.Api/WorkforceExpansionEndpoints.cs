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
        app.MapPut("/api/employees/{id:int}/availability", async (int id, SetEmployeeAvailabilityRequest request, AppDbContext db) =>
        {
            if (!await db.Employees.AnyAsync(x => x.Id == id)) return Results.NotFound();
            var item = await db.EmployeeAvailability.FindAsync(id, request.Date);
            if (item is null)
            {
                item = new EmployeeAvailability { EmployeeId = id, Date = request.Date, IsAvailable = request.IsAvailable, Reason = request.Reason.Trim() };
                db.EmployeeAvailability.Add(item);
            }
            else { item.IsAvailable = request.IsAvailable; item.Reason = request.Reason.Trim(); }
            await db.SaveChangesAsync(); return Results.Ok(item);
        });

        app.MapGet("/api/employees/{id:int}/availability", async (int id, AppDbContext db) =>
        {
            if (!await db.Employees.AnyAsync(x => x.Id == id)) return Results.NotFound();
            return Results.Ok(await db.EmployeeAvailability.Where(x => x.EmployeeId == id).OrderBy(x => x.Date).ToListAsync());
        });

        app.MapDelete("/api/employees/{id:int}/availability/{date}", async (int id, DateOnly date, AppDbContext db) =>
        {
            var item = await db.EmployeeAvailability.FindAsync(id, date); if (item is null) return Results.NotFound();
            db.EmployeeAvailability.Remove(item); await db.SaveChangesAsync(); return Results.NoContent();
        });

        app.MapGet("/api/shifts/{id:int}/coverage", async (int id, AppDbContext db) =>
        {
            var shift = await db.Shifts
                .Include(x => x.Assignments).ThenInclude(x => x.Employee).ThenInclude(x => x.Competences).ThenInclude(x => x.Competence)
                .Include(x => x.Requirements).ThenInclude(x => x.Competence)
                .SingleOrDefaultAsync(x => x.Id == id);
            if (shift is null) return Results.NotFound();
            return Results.Ok(new CoverageEngine().Analyze(shift));
        });

        app.MapGet("/api/shifts/{id:int}/candidates", async (int id, AppDbContext db) =>
        {
            if (!await db.Shifts.AnyAsync(x => x.Id == id)) return Results.NotFound();
            var matching = new ShiftMatchingService(db);
            return Results.Ok(await matching.FindCandidatesAsync(id));
        });
    }
}
