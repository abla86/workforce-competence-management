using Microsoft.EntityFrameworkCore;
using Workforce.Api.Models;

namespace Workforce.Api.Data;

public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        await EnsureUserAccountSchemaAsync(db);

        if (await db.Employees.AnyAsync())
            return;

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
            new Shift { Date = today, ShiftType = "Day", Hours = 7.5m, MinimumStaff = 3 },
            new Shift { Date = today, ShiftType = "Evening", Hours = 7m, MinimumStaff = 4 },
            new Shift { Date = today, ShiftType = "Night", Hours = 10m, MinimumStaff = 2 },
            new Shift { Date = today.AddDays(1), ShiftType = "Day", Hours = 7.5m, MinimumStaff = 3 }
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
        await db.SaveChangesAsync();
    }

    private static async Task EnsureUserAccountSchemaAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'dbo.UserAccounts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserAccounts (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserAccounts PRIMARY KEY,
        Username nvarchar(100) NOT NULL,
        PasswordHash nvarchar(max) NOT NULL,
        Role nvarchar(40) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_UserAccounts_IsActive DEFAULT(1),
        FailedLoginAttempts int NOT NULL CONSTRAINT DF_UserAccounts_FailedLoginAttempts DEFAULT(0),
        LockedUntilUtc datetime2 NULL,
        CreatedAtUtc datetime2 NOT NULL CONSTRAINT DF_UserAccounts_CreatedAtUtc DEFAULT(SYSUTCDATETIME()),
        LastLoginAtUtc datetime2 NULL,
        EmployeeId nvarchar(100) NULL
    );
    CREATE UNIQUE INDEX IX_UserAccounts_Username ON dbo.UserAccounts(Username);
END");
    }
}
