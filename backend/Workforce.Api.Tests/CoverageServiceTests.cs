using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Workforce.Api.Data;
using Workforce.Api.Models;
using Workforce.Api.Services;
using Xunit;

namespace Workforce.Api.Tests;

public sealed class CoverageServiceTests
{
    [Fact]
    public async Task CoveredShift_ReturnsGreen()
    {
        await using var db = CreateDb();
        var competence = new Competence { Id = 1, Name = "First aid" };
        var employee = new Employee { Id = 1, Name = "Test", Role = "Nurse", Competences = [new EmployeeCompetence { EmployeeId = 1, CompetenceId = 1, Competence = competence, Level = "Advanced" }] };
        var task = new WorkTask { Id = 1, Name = "First aid", CompetenceId = 1, Competence = competence };
        var shift = CreateShift(1, employee, task, 1, 3, false);
        shift.ShiftTasks[0].ShiftTaskCoverages.Add(new ShiftTaskCoverage { Id = 1, ShiftTaskId = 1, EmployeeId = 1, Employee = employee, IsValid = true });
        db.Competences.Add(competence); db.Employees.Add(employee); db.WorkTasks.Add(task); db.Shifts.Add(shift);
        await db.SaveChangesAsync();

        var result = await Engine(db).EvaluateAsync(1, "test", writeAudit: false);

        Assert.Equal(CoverageStatus.Green, result.Status);
        Assert.Equal(1, result.Tasks[0].Actual);
        Assert.Empty(result.Tasks[0].Gaps);
    }

    [Fact]
    public async Task CriticalTaskWithoutCompetence_ReturnsRed()
    {
        await using var db = CreateDb();
        var competence = new Competence { Id = 1, Name = "Advanced care" };
        var employee = new Employee { Id = 1, Name = "Test", Role = "Assistant" };
        var task = new WorkTask { Id = 1, Name = "Advanced care", CompetenceId = 1, Competence = competence };
        var shift = CreateShift(1, employee, task, 1, 2, true);
        shift.ShiftTasks[0].ShiftTaskCoverages.Add(new ShiftTaskCoverage { Id = 1, ShiftTaskId = 1, EmployeeId = 1, Employee = employee, IsValid = true });
        db.Competences.Add(competence); db.Employees.Add(employee); db.WorkTasks.Add(task); db.Shifts.Add(shift);
        await db.SaveChangesAsync();

        var result = await Engine(db).EvaluateAsync(1, "test", writeAudit: false);

        Assert.Equal(CoverageStatus.Red, result.Status);
        Assert.Contains(result.Tasks[0].Gaps, g => g.Type == GapType.MissingCompetence);
    }

    [Fact]
    public async Task UnderstaffedNonCriticalShift_ReturnsYellow()
    {
        await using var db = CreateDb();
        var employee = new Employee { Id = 1, Name = "Test" };
        var task = new WorkTask { Id = 1, Name = "General task" };
        var shift = CreateShift(1, employee, task, 2, 1, false);
        shift.ShiftTasks[0].ShiftTaskCoverages.Add(new ShiftTaskCoverage { Id = 1, ShiftTaskId = 1, EmployeeId = 1, Employee = employee, IsValid = true });
        db.Employees.Add(employee); db.WorkTasks.Add(task); db.Shifts.Add(shift);
        await db.SaveChangesAsync();

        var result = await Engine(db).EvaluateAsync(1, "test", writeAudit: false);

        Assert.Equal(CoverageStatus.Yellow, result.Status);
        Assert.Contains(result.Warnings, x => x.Contains("krever minst 2"));
    }

    [Fact]
    public async Task ScenarioRemovalDoesNotChangeDatabaseAndShowsGap()
    {
        await using var db = CreateDb();
        var employee = new Employee { Id = 1, Name = "Test" };
        var task = new WorkTask { Id = 1, Name = "General task" };
        var shift = CreateShift(1, employee, task, 1, 1, true);
        shift.ShiftTasks[0].ShiftTaskCoverages.Add(new ShiftTaskCoverage { Id = 1, ShiftTaskId = 1, EmployeeId = 1, Employee = employee, IsValid = true });
        db.Employees.Add(employee); db.WorkTasks.Add(task); db.Shifts.Add(shift);
        await db.SaveChangesAsync();

        var result = await Engine(db).EvaluateScenarioWithoutEmployeesAsync(1, [1]);
        var persisted = await db.ShiftTaskCoverages.CountAsync(x => x.EmployeeId == 1);

        Assert.Equal(CoverageStatus.Red, result.Status);
        Assert.Contains(result.Tasks[0].Gaps, g => g.Type == GapType.InsufficientStaff);
        Assert.Equal(1, persisted);
    }

    [Fact]
    public async Task ReplacementSearchMarksQualifiedCandidateAvailable()
    {
        await using var db = CreateDb();
        var competence = new Competence { Id = 1, Name = "First aid" };
        var assigned = new Employee { Id = 1, Name = "Assigned", Role = "Nurse" };
        var candidate = new Employee { Id = 2, Name = "Candidate", Role = "Nurse", Competences = [new EmployeeCompetence { EmployeeId = 2, CompetenceId = 1, Competence = competence, Level = "Advanced" }] };
        var task = new WorkTask { Id = 1, Name = "First aid", CompetenceId = 1, Competence = competence };
        var shift = CreateShift(1, assigned, task, 1, 3, false);
        shift.ShiftTasks[0].ShiftTaskCoverages.Add(new ShiftTaskCoverage { Id = 1, ShiftTaskId = 1, EmployeeId = 1, Employee = assigned, IsValid = true });
        db.Competences.Add(competence); db.Employees.AddRange(assigned, candidate); db.WorkTasks.Add(task); db.Shifts.Add(shift);
        await db.SaveChangesAsync();

        var candidates = await Engine(db).FindQualifiedReplacementsAsync(1, [1]);
        var replacement = Assert.Single(candidates);

        Assert.Equal(2, replacement.EmployeeId);
        Assert.True(replacement.Available);
        Assert.Empty(replacement.MissingRequirements);
    }

    private static Shift CreateShift(int id, Employee employee, WorkTask task, int required, int minimumLevel, bool critical) => new()
    {
        Id = id,
        Date = new DateOnly(2026, 8, 20),
        StartTime = new DateTime(2026, 8, 20, 8, 0, 0, DateTimeKind.Utc),
        EndTime = new DateTime(2026, 8, 20, 16, 0, 0, DateTimeKind.Utc),
        MinimumStaff = 1,
        Assignments = [new ShiftAssignment { ShiftId = id, EmployeeId = employee.Id, Employee = employee }],
        ShiftTasks = [new ShiftTask { Id = id, ShiftId = id, WorkTaskId = task.Id, WorkTask = task, RequiredCount = required, MinCompetenceLevel = minimumLevel, IsCritical = critical }]
    };

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new AppDbContext(options);
    }

    private static CoverageEvaluationEngine Engine(AppDbContext db)
    {
        var provider = DataProtectionProvider.Create("Workforce.Api.Tests");
        return new CoverageEvaluationEngine(db, new AuditProtectionService(provider), NullLogger<CoverageEvaluationEngine>.Instance);
    }
}
