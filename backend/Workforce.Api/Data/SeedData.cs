using Microsoft.EntityFrameworkCore;
using Workforce.Api.Models;

namespace Workforce.Api.Data;

public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.Employees.AnyAsync()) return;

        var competences = new[]
        {
            new Competence { Name = "Advanced assessment", Category = "Professional" },
            new Competence { Name = "Medication management", Category = "Safety" },
            new Competence { Name = "First aid", Category = "Safety" },
            new Competence { Name = "System training", Category = "Digital" },
            new Competence { Name = "Team leadership", Category = "Leadership" },
            new Competence { Name = "Quality improvement", Category = "Quality" }
        };
        db.Competences.AddRange(competences);
        await db.SaveChangesAsync();

        var employees = new[]
        {
            new Employee { Name = "Anne Andersen", Role = "Senior Specialist", PositionPercent = 100 },
            new Employee { Name = "Kari Hansen", Role = "Specialist", PositionPercent = 80 },
            new Employee { Name = "Per Olsen", Role = "Associate", PositionPercent = 100 },
            new Employee { Name = "Liv Johansen", Role = "Senior Specialist", PositionPercent = 60 },
            new Employee { Name = "Ola Berg", Role = "Associate", PositionPercent = 50 },
            new Employee { Name = "Mina Solheim", Role = "Team Lead", PositionPercent = 100 }
        };
        db.Employees.AddRange(employees);
        await db.SaveChangesAsync();

        db.EmployeeCompetences.AddRange(
            new EmployeeCompetence { EmployeeId = employees[0].Id, CompetenceId = competences[0].Id, Level = "Advanced" },
            new EmployeeCompetence { EmployeeId = employees[0].Id, CompetenceId = competences[1].Id, Level = "Advanced" },
            new EmployeeCompetence { EmployeeId = employees[0].Id, CompetenceId = competences[5].Id, Level = "Intermediate" },
            new EmployeeCompetence { EmployeeId = employees[1].Id, CompetenceId = competences[1].Id, Level = "Intermediate" },
            new EmployeeCompetence { EmployeeId = employees[1].Id, CompetenceId = competences[2].Id, Level = "Advanced" },
            new EmployeeCompetence { EmployeeId = employees[2].Id, CompetenceId = competences[2].Id, Level = "Intermediate" },
            new EmployeeCompetence { EmployeeId = employees[3].Id, CompetenceId = competences[0].Id, Level = "Intermediate" },
            new EmployeeCompetence { EmployeeId = employees[3].Id, CompetenceId = competences[3].Id, Level = "Advanced", ValidUntil = DateOnly.FromDateTime(DateTime.Today.AddDays(30)) },
            new EmployeeCompetence { EmployeeId = employees[4].Id, CompetenceId = competences[3].Id, Level = "Basic" },
            new EmployeeCompetence { EmployeeId = employees[5].Id, CompetenceId = competences[4].Id, Level = "Advanced" },
            new EmployeeCompetence { EmployeeId = employees[5].Id, CompetenceId = competences[5].Id, Level = "Advanced" }
        );

        var today = DateOnly.FromDateTime(DateTime.Today);
        var shifts = new[]
        {
            new Shift { Date = today, ShiftType = "Day", Hours = 7.5m, MinimumStaff = 3, StartTime = today.ToDateTime(new TimeOnly(07, 0)), EndTime = today.ToDateTime(new TimeOnly(14, 30)) },
            new Shift { Date = today, ShiftType = "Evening", Hours = 7m, MinimumStaff = 4, StartTime = today.ToDateTime(new TimeOnly(15, 0)), EndTime = today.ToDateTime(new TimeOnly(22, 0)) },
            new Shift { Date = today, ShiftType = "Night", Hours = 10m, MinimumStaff = 2, StartTime = today.ToDateTime(new TimeOnly(22, 0)), EndTime = today.AddDays(1).ToDateTime(new TimeOnly(08, 0)) },
            new Shift { Date = today.AddDays(1), ShiftType = "Day", Hours = 7.5m, MinimumStaff = 3, StartTime = today.AddDays(1).ToDateTime(new TimeOnly(07, 0)), EndTime = today.AddDays(1).ToDateTime(new TimeOnly(14, 30)) }
        };
        db.Shifts.AddRange(shifts);
        await db.SaveChangesAsync();

        db.ShiftAssignments.AddRange(
            new ShiftAssignment { ShiftId = shifts[0].Id, EmployeeId = employees[0].Id },
            new ShiftAssignment { ShiftId = shifts[0].Id, EmployeeId = employees[1].Id },
            new ShiftAssignment { ShiftId = shifts[0].Id, EmployeeId = employees[5].Id },
            new ShiftAssignment { ShiftId = shifts[1].Id, EmployeeId = employees[1].Id },
            new ShiftAssignment { ShiftId = shifts[1].Id, EmployeeId = employees[2].Id },
            new ShiftAssignment { ShiftId = shifts[1].Id, EmployeeId = employees[4].Id },
            new ShiftAssignment { ShiftId = shifts[2].Id, EmployeeId = employees[3].Id },
            new ShiftAssignment { ShiftId = shifts[2].Id, EmployeeId = employees[5].Id },
            new ShiftAssignment { ShiftId = shifts[3].Id, EmployeeId = employees[0].Id },
            new ShiftAssignment { ShiftId = shifts[3].Id, EmployeeId = employees[2].Id },
            new ShiftAssignment { ShiftId = shifts[3].Id, EmployeeId = employees[5].Id }
        );

        db.ShiftRequirements.AddRange(
            new ShiftRequirement { ShiftId = shifts[0].Id, CompetenceId = competences[1].Id, MinimumCount = 1, MinimumLevel = "Intermediate" },
            new ShiftRequirement { ShiftId = shifts[0].Id, CompetenceId = competences[4].Id, MinimumCount = 1, MinimumLevel = "Advanced" },
            new ShiftRequirement { ShiftId = shifts[1].Id, CompetenceId = competences[0].Id, MinimumCount = 1, MinimumLevel = "Intermediate" },
            new ShiftRequirement { ShiftId = shifts[1].Id, CompetenceId = competences[2].Id, MinimumCount = 1, MinimumLevel = "Intermediate" },
            new ShiftRequirement { ShiftId = shifts[2].Id, CompetenceId = competences[0].Id, MinimumCount = 1, MinimumLevel = "Intermediate" },
            new ShiftRequirement { ShiftId = shifts[3].Id, CompetenceId = competences[5].Id, MinimumCount = 1, MinimumLevel = "Intermediate" }
        );

        var workTasks = new[]
        {
            new WorkTask { Name = "Legemiddelhåndtering", Description = "Sikker legemiddelhåndtering på vakten", RequiredRole = "Senior Specialist", CompetenceId = competences[1].Id, MinimumLevel = 2, RequiredCount = 1, IsCritical = true },
            new WorkTask { Name = "Teamledelse", Description = "Ansvar for koordinering og faglig ledelse", RequiredRole = "Team Lead", CompetenceId = competences[4].Id, MinimumLevel = 3, RequiredCount = 1, IsCritical = true },
            new WorkTask { Name = "Avansert vurdering", Description = "Klinisk vurdering og observasjon", RequiredRole = "Senior Specialist", CompetenceId = competences[0].Id, MinimumLevel = 2, RequiredCount = 1, IsCritical = false },
            new WorkTask { Name = "Førstehjelp", Description = "Akutt førstehjelpsberedskap", CompetenceId = competences[2].Id, MinimumLevel = 2, RequiredCount = 1, IsCritical = true }
        };
        db.WorkTasks.AddRange(workTasks);
        await db.SaveChangesAsync();

        var shiftTasks = new[]
        {
            new ShiftTask { ShiftId = shifts[0].Id, WorkTaskId = workTasks[0].Id, RequiredCount = 1, MinCompetenceLevel = 2, IsCritical = true },
            new ShiftTask { ShiftId = shifts[0].Id, WorkTaskId = workTasks[1].Id, RequiredCount = 1, MinCompetenceLevel = 3, IsCritical = true },
            new ShiftTask { ShiftId = shifts[1].Id, WorkTaskId = workTasks[2].Id, RequiredCount = 1, MinCompetenceLevel = 2, IsCritical = false },
            new ShiftTask { ShiftId = shifts[2].Id, WorkTaskId = workTasks[3].Id, RequiredCount = 1, MinCompetenceLevel = 2, IsCritical = true }
        };
        db.ShiftTasks.AddRange(shiftTasks);
        await db.SaveChangesAsync();

        var expiry = DateTime.UtcNow.AddYears(1);
        db.ShiftTaskCoverages.AddRange(
            new ShiftTaskCoverage { ShiftTaskId = shiftTasks[0].Id, EmployeeId = employees[0].Id, RequiredCount = 1, MinCompetenceLevel = 2, IsCritical = true, AssignedRole = employees[0].Role, AuthorizationExpiry = expiry, IsValid = true },
            new ShiftTaskCoverage { ShiftTaskId = shiftTasks[1].Id, EmployeeId = employees[5].Id, RequiredCount = 1, MinCompetenceLevel = 3, IsCritical = true, AssignedRole = employees[5].Role, AuthorizationExpiry = expiry, IsValid = true },
            new ShiftTaskCoverage { ShiftTaskId = shiftTasks[2].Id, EmployeeId = employees[3].Id, RequiredCount = 1, MinCompetenceLevel = 2, IsCritical = false, AssignedRole = employees[3].Role, AuthorizationExpiry = expiry, IsValid = true },
            new ShiftTaskCoverage { ShiftTaskId = shiftTasks[3].Id, EmployeeId = employees[3].Id, RequiredCount = 1, MinCompetenceLevel = 2, IsCritical = true, AssignedRole = employees[3].Role, AuthorizationExpiry = expiry, IsValid = true }
        );

        await db.SaveChangesAsync();
    }
}
