using System.Text.Json;
using Workforce.Api.DTOs;
using Workforce.Api.Models;
using Workforce.Api.Services;
using Xunit;

namespace Workforce.Api.Tests;

public sealed class CoverageServiceTests
{
    [Fact]
    public void CoveredShift_ReturnsGreen()
    {
        var competence = new Competence { Id = 1, Name = "First aid" };
        var employee = new Employee { Id = 1, Name = "Test", Competences = [new EmployeeCompetence { CompetenceId = 1, Competence = competence, Level = CompetenceLevel.Advanced }] };
        var shift = new Shift { Id = 1, MinimumStaff = 1, Assignments = [new ShiftAssignment { Employee = employee, EmployeeId = 1 }], Requirements = [new ShiftRequirement { CompetenceId = 1, Competence = competence, MinimumCount = 1, MinimumLevel = CompetenceLevel.Intermediate }] };
        var result = new CoverageService().AnalyzeShift(shift);
        Assert.True(result.OverallCovered);
        Assert.Equal("GREEN", result.OverallStatus);
    }

    [Fact]
    public void MissingStaff_ReturnsRedAndExplainsGap()
    {
        var result = new CoverageService().AnalyzeShift(new Shift { Id = 1, MinimumStaff = 2 });
        Assert.False(result.StaffingCovered);
        Assert.Equal(2, result.MissingStaff);
        Assert.Equal("RED", result.OverallStatus);
        Assert.Contains(result.Warnings!, x => x.Contains("mangler 2"));
    }

    [Fact]
    public void MissingNonCriticalCompetence_ReturnsYellow()
    {
        var competence = new Competence { Id = 1, Name = "Team leadership" };
        var employee = new Employee { Id = 1, Name = "Test" };
        var shift = new Shift { Id = 1, MinimumStaff = 1, Assignments = [new ShiftAssignment { Employee = employee, EmployeeId = 1 }], Requirements = [new ShiftRequirement { CompetenceId = 1, Competence = competence, MinimumCount = 1, MinimumLevel = CompetenceLevel.Advanced }] };
        var result = new CoverageService().AnalyzeShift(shift);
        Assert.False(result.OverallCovered);
        Assert.Equal("YELLOW", result.OverallStatus);
    }

    [Fact]
    public void CriticalRequirementIsMarkedAsRed()
    {
        var competence = new Competence { Id = 1, Name = "Medication" };
        var shift = new Shift { Id = 1, MinimumStaff = 1, Assignments = [new ShiftAssignment { Employee = new Employee { Id = 1, Name = "Test" }, EmployeeId = 1 }], Requirements = [new ShiftRequirement { CompetenceId = 1, Competence = competence, MinimumCount = 1, MinimumLevel = CompetenceLevel.Advanced, IsCritical = true }] };
        var result = new CoverageService().AnalyzeShift(shift);
        Assert.Contains(result.Warnings!, x => x.Contains("Kritisk kompetanse"));
        Assert.Equal("RED", result.OverallStatus);
    }

    [Fact]
    public void ExpiredCompetenceDoesNotCountAsQualified()
    {
        var competence = new Competence { Id = 1, Name = "First aid" };
        var employee = new Employee { Id = 1, Name = "Test", Competences = [new EmployeeCompetence { CompetenceId = 1, Competence = competence, Level = CompetenceLevel.Advanced, ValidUntil = new DateOnly(2020, 1, 1) }] };
        var shift = new Shift { Id = 1, Date = new DateOnly(2026, 8, 20), MinimumStaff = 1, Assignments = [new ShiftAssignment { Employee = employee, EmployeeId = 1 }], Requirements = [new ShiftRequirement { CompetenceId = 1, Competence = competence, MinimumCount = 1, MinimumLevel = CompetenceLevel.Basic }] };
        var result = new CoverageService().AnalyzeShift(shift);
        Assert.Equal(0, result.Requirements[0].QualifiedCount);
        Assert.False(result.Requirements[0].Covered);
    }

    [Fact]
    public void RequiredRoleIsRespected()
    {
        var competence = new Competence { Id = 1, Name = "Leadership" };
        var employee = new Employee { Id = 1, Name = "Test", Role = "Nurse", Competences = [new EmployeeCompetence { CompetenceId = 1, Competence = competence, Level = CompetenceLevel.Advanced }] };
        var shift = new Shift { Id = 1, MinimumStaff = 1, Assignments = [new ShiftAssignment { Employee = employee, EmployeeId = 1 }], Requirements = [new ShiftRequirement { CompetenceId = 1, Competence = competence, MinimumCount = 1, MinimumLevel = CompetenceLevel.Basic, RequiredRole = "Manager" }] };
        var result = new CoverageService().AnalyzeShift(shift);
        Assert.Equal(0, result.Requirements[0].QualifiedCount);
        Assert.False(result.Requirements[0].Covered);
    }

    [Fact]
    public void InvalidRequirementLevelIsRejectedByJsonDeserialization()
    {
        var json = "{\"CompetenceId\":1,\"MinimumCount\":1,\"MinimumLevel\":\"Advcanced\"}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AddRequirementRequest>(json));
    }

    [Fact]
    public void InvalidEmployeeLevelIsRejectedByJsonDeserialization()
    {
        var json = "{\"CompetenceId\":1,\"Level\":\"Advcanced\",\"ValidUntil\":null}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AddCompetenceRequest>(json));
    }
}
