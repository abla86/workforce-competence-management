using Workforce.Api.Models;
using Workforce.Api.Services;
using Xunit;

namespace Workforce.Api.Tests;

public sealed class CoverageServiceTests
{
    [Fact]
    public void CoveredShift_ReturnsGood()
    {
        var competence = new Competence { Id = 1, Name = "First aid" };
        var employee = new Employee
        {
            Id = 1,
            Name = "Test",
            Competences =
            [
                new EmployeeCompetence
                {
                    CompetenceId = 1,
                    Competence = competence,
                    Level = "Advanced"
                }
            ]
        };

        var shift = new Shift
        {
            Id = 1,
            MinimumStaff = 1,
            Assignments =
            [
                new ShiftAssignment { Employee = employee, EmployeeId = 1 }
            ],
            Requirements =
            [
                new ShiftRequirement
                {
                    CompetenceId = 1,
                    Competence = competence,
                    MinimumCount = 1,
                    MinimumLevel = "Intermediate"
                }
            ]
        };

        var result = new CoverageService().AnalyzeShift(shift);

        Assert.True((bool)result.OverallCovered);
        Assert.Equal("GOOD", (string)result.OverallStatus);
    }

    [Fact]
    public void MissingStaff_ReturnsUnderstaffed()
    {
        var shift = new Shift { Id = 1, MinimumStaff = 2 };
        var result = new CoverageService().AnalyzeShift(shift);

        Assert.False((bool)result.StaffingCovered);
        Assert.Equal(2, (int)result.MissingStaff);
    }

    [Fact]
    public void MissingCompetence_ReturnsActionRequired()
    {
        var competence = new Competence { Id = 1, Name = "Team leadership" };
        var employee = new Employee { Id = 1, Name = "Test" };

        var shift = new Shift
        {
            MinimumStaff = 1,
            Assignments = [new ShiftAssignment { Employee = employee, EmployeeId = 1 }],
            Requirements =
            [
                new ShiftRequirement
                {
                    CompetenceId = 1,
                    Competence = competence,
                    MinimumCount = 1,
                    MinimumLevel = "Advanced"
                }
            ]
        };

        var result = new CoverageService().AnalyzeShift(shift);

        Assert.False((bool)result.OverallCovered);
        Assert.Equal("ACTION_REQUIRED", (string)result.OverallStatus);
    }
}


