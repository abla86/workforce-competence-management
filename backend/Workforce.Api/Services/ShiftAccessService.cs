using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;

namespace Workforce.Api.Services;

public sealed class ShiftAccessService
{
    private readonly AppDbContext _db;

    public ShiftAccessService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> CanAccessShiftAsync(ClaimsPrincipal user, int shiftId)
    {
        if (user.IsInRole("Admin") || user.IsInRole("HR") || user.IsInRole("Manager"))
            return await _db.Shifts.AnyAsync(s => s.Id == shiftId);

        var subject = user.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? user.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject))
            return false;

        return await _db.Shifts
            .Where(s => s.Id == shiftId)
            .SelectMany(s => s.Assignments)
            .AnyAsync(a => a.Employee.IdentitySubject == subject);
    }
}
