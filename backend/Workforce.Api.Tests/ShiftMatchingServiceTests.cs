using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;
using Workforce.Api.Models;
using Workforce.Api.Services;

namespace Workforce.Api.Tests;

public sealed class ShiftMatchingServiceTests
{
    [Fact]
    public async Task Returns_eligible_employee_when_requirements_are_met()
    {
        await using var db = CreateDb();
        var competence = new Competence { Id = 1, Name = "Medication management", Category = "Safety" };
        var employee = new Employee { Id = 1, Name = "Kari", Role = "Nurse", PositionPercent = 100 };
        employee.Competences.Add(new EmployeeCompetence
        {
            EmployeeId = 1,
            CompetenceId = 1,
            Competence = competence,
            Level = "Advanced",
            ValidUntil = new DateOnly(2026, 12, 31)
        });

        db.Competences.Add(competence);
        db.Employees.Add(employee);
        db.Shifts.Add(new Shift
        {
            Id = 10,
            Date = new DateOnly(2026, 8, 20),
            ShiftType = "Day",
            Hours = 7.5m,
            MinimumStaff = 1,
            Requirements =
            [
                new ShiftRequirement
                {
                    ShiftId = 10,
                    CompetenceId = 1,
                    Competence = competence,
                    MinimumCount = 1,
                    MinimumLevel = "Intermediate"
                }
            ]
        });
        await db.SaveChangesAsync();

        var result = await new ShiftMatchingService(db).FindCandidatesAsync(10);

        Assert.Single(result);
        Assert.Equal("Kari", result[0].EmployeeName);
        Assert.True(result[0].Eligible);
        Assert.Contains("All required competences", result[0].Reasons[0]);
    }

    [Fact]
    public async Task Excludes_employee_marked_unavailable()
    {
        await using var db = CreateDb();
        var employee = new Employee { Id = 1, Name = "Kari", Role = "Nurse", PositionPercent = 100 };
        employee.Availability.Add(new EmployeeAvailability
        {
            EmployeeId = 1,
            Date = new DateOnly(2026, 8, 20),
            IsAvailable = false,
            Reason = "Vacation"
        });

        db.Employees.Add(employee);
        db.Shifts.Add(new Shift
        {
            Id = 10,
            Date = new DateOnly(2026, 8, 20),
            ShiftType = "Day",
            Hours = 7.5m,
            MinimumStaff = 1
        });
        await db.SaveChangesAsync();

        var result = await new ShiftMatchingService(db).FindCandidatesAsync(10);

        Assert.Empty(result);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
