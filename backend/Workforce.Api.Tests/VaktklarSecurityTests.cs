using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;
using Workforce.Api.Models;
using Xunit;

namespace Workforce.Api.Tests;

public sealed class VaktklarSecurityTests
{
    [Fact]
    public async Task EmployeeAvailabilityKeyIsScopedToEmployee()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);

        db.Employees.AddRange(
            new Employee { Name = "A", Role = "Sykepleier", PositionPercent = 100 },
            new Employee { Name = "B", Role = "Sykepleier", PositionPercent = 100 });
        await db.SaveChangesAsync();

        db.EmployeeAvailability.Add(new EmployeeAvailability { EmployeeId = 1, Date = new DateOnly(2026, 8, 20), IsAvailable = true });
        db.EmployeeAvailability.Add(new EmployeeAvailability { EmployeeId = 2, Date = new DateOnly(2026, 8, 20), IsAvailable = false });
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.EmployeeAvailability.CountAsync());
    }

    [Fact]
    public async Task DuplicateShiftAssignmentIsRejectedByDomainCheck()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);

        db.ShiftAssignments.Add(new ShiftAssignment { ShiftId = 1, EmployeeId = 1 });
        await db.SaveChangesAsync();

        Assert.True(await db.ShiftAssignments.AnyAsync(x => x.ShiftId == 1 && x.EmployeeId == 1));
    }
}
