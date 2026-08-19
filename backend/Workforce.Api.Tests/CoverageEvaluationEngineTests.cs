using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Workforce.Api.Data;
using Workforce.Api.Models;
using Workforce.Api.Services;
using Xunit;

namespace Workforce.Api.Tests;

public sealed class CoverageEvaluationEngineTests
{
    [Fact]
    public async Task CriticalTaskWithoutQualifiedCoverage_ReturnsRed()
    {
        await using var db = CreateDb();
        var competence = new Competence { Name = "Medication management", Category = "Clinical" };
        var employee = new Employee { Name = "Test", Role = "Associate", PositionPercent = 100, IsActive = true };
        var workTask = new WorkTask
        {
            Name = "Medication administration",
            RequiredRole = "Senior Specialist",
            Competence = competence,
            MinimumLevel = 2,
            RequiredCount = 1,
            IsCritical = true
        };
        var shift = new Shift
        {
            Date = new DateOnly(2026, 8, 20),
            ShiftType = "Day",
            Hours = 7.5m,
            MinimumStaff = 1,
            StartTime = new DateTime(2026, 8, 20, 7, 0, 0),
            EndTime = new DateTime(2026, 8, 20, 14, 30, 0)
        };

        db.Competences.Add(competence);
        db.Employees.Add(employee);
        db.WorkTasks.Add(workTask);
        db.Shifts.Add(shift);
        await db.SaveChangesAsync();

        var task = new ShiftTask
        {
            ShiftId = shift.Id,
            WorkTaskId = workTask.Id,
            RequiredCount = 1,
            MinCompetenceLevel = 2,
            IsCritical = true
        };
        db.ShiftTasks.Add(task);
        db.ShiftAssignments.Add(new ShiftAssignment { ShiftId = shift.Id, EmployeeId = employee.Id });
        db.ShiftTaskCoverages.Add(new ShiftTaskCoverage
        {
            ShiftTask = task,
            EmployeeId = employee.Id,
            RequiredCount = 1,
            MinCompetenceLevel = 2,
            IsCritical = true,
            IsValid = true
        });
        await db.SaveChangesAsync();

        var engine = CreateEngine(db);
        var result = await engine.EvaluateAsync(shift.Id, "test", writeAudit: false);

        Assert.Equal(CoverageStatus.Red, result.Status);
        Assert.Contains(result.Tasks, x => x.Critical && x.Gaps.Count > 0);
    }

    [Fact]
    public async Task QualifiedReplacement_IsReturnedAsAvailable()
    {
        await using var db = CreateDb();
        var competence = new Competence { Name = "First aid", Category = "Clinical" };
        var assigned = new Employee { Name = "Assigned", Role = "Senior Specialist", PositionPercent = 100, IsActive = true };
        var candidate = new Employee { Name = "Candidate", Role = "Senior Specialist", PositionPercent = 100, IsActive = true };
        var workTask = new WorkTask
        {
            Name = "First aid",
            RequiredRole = "Senior Specialist",
            Competence = competence,
            MinimumLevel = 2,
            RequiredCount = 1,
            IsCritical = true
        };
        var shift = new Shift
        {
            Date = new DateOnly(2026, 8, 20),
            ShiftType = "Day",
            Hours = 7.5m,
            MinimumStaff = 1,
            StartTime = new DateTime(2026, 8, 20, 7, 0, 0),
            EndTime = new DateTime(2026, 8, 20, 14, 30, 0)
        };

        db.Competences.Add(competence);
        db.Employees.AddRange(assigned, candidate);
        db.WorkTasks.Add(workTask);
        db.Shifts.Add(shift);
        await db.SaveChangesAsync();

        db.EmployeeCompetences.Add(new EmployeeCompetence
        {
            EmployeeId = candidate.Id,
            CompetenceId = competence.Id,
            Level = "Advanced",
            ValidUntil = new DateOnly(2027, 8, 20)
        });
        var task = new ShiftTask
        {
            ShiftId = shift.Id,
            WorkTaskId = workTask.Id,
            RequiredCount = 1,
            MinCompetenceLevel = 2,
            IsCritical = true
        };
        db.ShiftTasks.Add(task);
        db.ShiftAssignments.Add(new ShiftAssignment { ShiftId = shift.Id, EmployeeId = assigned.Id });
        await db.SaveChangesAsync();

        var engine = CreateEngine(db);
        var replacements = await engine.FindQualifiedReplacementsAsync(shift.Id, []);

        var match = Assert.Single(replacements.Where(x => x.EmployeeId == candidate.Id));
        Assert.True(match.Available);
        Assert.Empty(match.MissingRequirements);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static CoverageEvaluationEngine CreateEngine(AppDbContext db)
    {
        var provider = DataProtectionProvider.Create("Workforce.Api.Tests");
        var audit = new AuditProtectionService(provider);
        return new CoverageEvaluationEngine(db, audit, NullLogger<CoverageEvaluationEngine>.Instance);
    }
}
