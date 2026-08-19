using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;
using Workforce.Api.Models;
using Workforce.Api.Services;
using Xunit;

namespace Workforce.Api.Tests;

public sealed class PlanningServiceTests
{
    private static AppDbContext CreateDb(string name) => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    [Fact]
    public async Task DailyPlan_IsCreatedOnlyOncePerDepartmentAndDate()
    {
        await using var db = CreateDb(nameof(DailyPlan_IsCreatedOnlyOncePerDepartmentAndDate));
        var service = new DailyPlanService(db, new NotificationService(db));

        var first = await service.CreateDailyPlanAsync(1, "manager");
        var second = await service.CreateDailyPlanAsync(1, "manager");

        Assert.Equal(first.Id, second.Id);
        Assert.Single(await db.DailyPlans.ToListAsync());
    }

    [Fact]
    public async Task Availability_ReturnsSickStatusForApprovedAbsence()
    {
        await using var db = CreateDb(nameof(Availability_ReturnsSickStatusForApprovedAbsence));
        var employee = new Employee { Id = 1, Name = "Test", Role = "Nurse", DepartmentId = 1, PositionPercent = 100, IdentitySubject = "1" };
        db.Employees.Add(employee);
        db.Absences.Add(new Absence { EmployeeId = 1, Type = AbsenceType.SickLeave, StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(1), IsApproved = true });
        await db.SaveChangesAsync();

        var service = new EmployeeAvailabilityService(db, new NotificationService(db));
        var status = await service.GetEmployeeStatusAsync(1, DateTime.Today);

        Assert.Equal(EmployeeAvailabilityStatus.Sick, status.Status);
        Assert.True(status.IsAutomatic);
    }

    [Fact]
    public async Task Availability_ReturnsBusyWhenEmployeeHasCurrentShift()
    {
        await using var db = CreateDb(nameof(Availability_ReturnsBusyWhenEmployeeHasCurrentShift));
        var employee = new Employee { Id = 1, Name = "Test", Role = "Nurse", DepartmentId = 1, PositionPercent = 100, IdentitySubject = "1" };
        var shift = new Shift { Id = 1, Date = DateOnly.FromDateTime(DateTime.Today), ShiftType = "Day", StartTime = DateTime.Now.AddHours(-1), EndTime = DateTime.Now.AddHours(4), Hours = 5, MinimumStaff = 1 };
        shift.Assignments.Add(new ShiftAssignment { ShiftId = 1, EmployeeId = 1, Employee = employee, Shift = shift });
        db.Employees.Add(employee); db.Shifts.Add(shift);
        await db.SaveChangesAsync();

        var service = new EmployeeAvailabilityService(db, new NotificationService(db));
        var status = await service.GetEmployeeStatusAsync(1, DateTime.Today);

        Assert.Equal(EmployeeAvailabilityStatus.Busy, status.Status);
    }
}
