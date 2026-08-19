using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;

namespace Workforce.Api.Services;

public sealed class EmployeeAccessService
{
    private readonly AppDbContext _db;

    public EmployeeAccessService(AppDbContext db) => _db = db;

    public async Task<bool> CanAccessEmployeeAsync(ClaimsPrincipal user, int employeeId)
    {
        if (user.IsInRole("Admin") || user.IsInRole("HR") || user.IsInRole("Manager"))
            return await _db.Employees.AnyAsync(e => e.Id == employeeId);

        var subject = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return !string.IsNullOrWhiteSpace(subject) &&
               await _db.Employees.AnyAsync(e => e.Id == employeeId && e.IdentitySubject == subject);
    }
}
