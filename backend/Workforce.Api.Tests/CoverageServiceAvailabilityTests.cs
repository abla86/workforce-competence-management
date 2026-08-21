using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;
using Workforce.Api.Models;
using Workforce.Api.Services;
using Xunit;

namespace Workforce.Api.Tests;

/// <summary>
/// Regression tests for the availability-evaluation bugs fixed on 2026-08-21.
/// </summary>
public sealed class CoverageServiceAvailabilityTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task EvaluateShiftAsync_DetectsAbsenceWarning_ThatBareAnalyzeShiftMisses()
    {
        using var db = CreateDb();
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var employee = new Employee
        {
            Id = 1,
            Name = "Per Olsen",
            IsActive = true,
            PositionPercent = 100m,
            Absences = [new Absence { EmployeeId = 1, From = tomorrow, To = tomorrow, Approved = true, Type = AbsenceType.Sick }]
        };

        var shift = new Shift
        {
            Id = 300,
            Date = tomorrow,
            ShiftType = "Day",
            Hours = 8,
            MinimumStaff = 1,
            Assignments = [new ShiftAssignment { EmployeeId = employee.Id, Employee = employee }]
        };

        var coverage = new CoverageService();
        var bareAnalysis = coverage.AnalyzeShift(shift);
        var fullEvaluation = await coverage.EvaluateShiftAsync(db, shift, writeAudit: false);

        Assert.DoesNotContain(bareAnalysis.Warnings ?? [], w => w.Contains("Tilgjengelighet"));
        Assert.Contains(fullEvaluation.Warnings!, w => w.Contains("Tilgjengelighet"));
        Assert.Equal("YELLOW", fullEvaluation.OverallStatus);
    }

    [Fact]
    public async Task AddAvailabilityWarnings_DetectsOverlap_ForShiftsWithoutExplicitStartTime()
    {
        using var db = CreateDb();
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var employee = new Employee { Id = 1, Name = "Kari Hansen", IsActive = true, PositionPercent = 100m };

        var otherShift = new Shift
        {
            Id = 100,
            Date = tomorrow,
            ShiftType = "Day",
            Hours = 8,
            MinimumStaff = 1,
            Assignments = [new ShiftAssignment { EmployeeId = employee.Id, Employee = employee }]
        };
        db.Shifts.Add(otherShift);
        await db.SaveChangesAsync();

        var targetShift = new Shift
        {
            Id = 200,
            Date = tomorrow,
            ShiftType = "Day",
            Hours = 8,
            MinimumStaff = 1,
            Assignments = [new ShiftAssignment { EmployeeId = employee.Id, Employee = employee }]
        };

        var result = await new CoverageService().EvaluateShiftAsync(db, targetShift, writeAudit: false);

        Assert.Contains(result.Warnings!, w => w.Contains("Dobbeltbooking"));
        Assert.Equal("YELLOW", result.OverallStatus);
    }
}
