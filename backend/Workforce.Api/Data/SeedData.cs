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
            new Employee { Name = "Anne Andersen", Role = "Senior Specialist", PositionPercent = 100, IdentitySubject = "dev-manager" },
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
            new Shift { Date = today, ShiftType = "Evening", Hours = 7m, MinimumStaff = 4, StartTime = today.ToDateTime(new TimeOnly(14, 30)), EndTime = today.ToDateTime(new TimeOnly(21, 30)) },
            new Shift { Date = today, ShiftType = "Night", Hours = 10m, MinimumStaff = 2, StartTime = today.ToDateTime(new TimeOnly(21, 30)), EndTime = today.AddDays(1).ToDateTime(new TimeOnly(07, 30)) },
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

        var tasks = new[]
        {
            new WorkTask { Name = "Legemiddelutdeling", Description = "Legemiddelhåndtering på vakt", CompetenceId = competences[1].Id, MinimumLevel = 2, RequiredCount = 1, IsCritical = true },
            new WorkTask { Name = "Tilsyn og fallforebygging", Description = "Tilsyn og risikoreduserende tiltak", CompetenceId = competences[2].Id, MinimumLevel = 2, RequiredCount = 1, IsCritical = false },
            new WorkTask { Name = "Dokumentasjon", Description = "Dokumentasjon i journalsystem", CompetenceId = competences[3].Id, MinimumLevel = 1, RequiredCount = 1, IsCritical = false },
            new WorkTask { Name = "Teamledelse", Description = "Koordinering av vakten", CompetenceId = competences[4].Id, MinimumLevel = 3, RequiredCount = 1, RequiredRole = "Team Lead", IsCritical = true }
        };
        db.WorkTasks.AddRange(tasks);
        await db.SaveChangesAsync();

        db.ShiftTasks.AddRange(
            new ShiftTask { ShiftId = shifts[0].Id, WorkTaskId = tasks[0].Id, RequiredCount = 1, MinCompetenceLevel = 2, IsCritical = true },
            new ShiftTask { ShiftId = shifts[0].Id, WorkTaskId = tasks[3].Id, RequiredCount = 1, MinCompetenceLevel = 3, IsCritical = true },
            new ShiftTask { ShiftId = shifts[1].Id, WorkTaskId = tasks[1].Id, RequiredCount = 1, MinCompetenceLevel = 2, IsCritical = false },
            new ShiftTask { ShiftId = shifts[2].Id, WorkTaskId = tasks[0].Id, RequiredCount = 1, MinCompetenceLevel = 2, IsCritical = true },
            new ShiftTask { ShiftId = shifts[3].Id, WorkTaskId = tasks[2].Id, RequiredCount = 1, MinCompetenceLevel = 1, IsCritical = false }
        );
        await db.SaveChangesAsync();

        var shiftTasks = await db.ShiftTasks.OrderBy(x => x.Id).ToListAsync();
        db.ShiftTaskCoverages.AddRange(
            new ShiftTaskCoverage { ShiftTaskId = shiftTasks[0].Id, EmployeeId = employees[0].Id, RequiredCount = 1, MinCompetenceLevel = 2 },
            new ShiftTaskCoverage { ShiftTaskId = shiftTasks[1].Id, EmployeeId = employees[5].Id, RequiredCount = 1, MinCompetenceLevel = 3 },
            new ShiftTaskCoverage { ShiftTaskId = shiftTasks[2].Id, EmployeeId = employees[1].Id, RequiredCount = 1, MinCompetenceLevel = 2 },
            new ShiftTaskCoverage { ShiftTaskId = shiftTasks[3].Id, EmployeeId = employees[3].Id, RequiredCount = 1, MinCompetenceLevel = 2 },
            new ShiftTaskCoverage { ShiftTaskId = shiftTasks[4].Id, EmployeeId = employees[0].Id, RequiredCount = 1, MinCompetenceLevel = 1 }
        );

        await db.SaveChangesAsync();
    }
}
