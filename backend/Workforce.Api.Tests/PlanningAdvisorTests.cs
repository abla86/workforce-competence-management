using Workforce.Api.Models;
using Workforce.Api.Services;
using Xunit;

namespace Workforce.Api.Tests;

public sealed class PlanningAdvisorTests
{
    [Fact]
    public void CandidateWithoutRequiredCompetenceIsNotEligible()
    {
        var competence = new Competence { Id = 1, Name = "Medication" };
        var shift = new Shift
        {
            Id = 1, Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), Hours = 8, MinimumStaff = 1,
            Requirements = [new ShiftRequirement { CompetenceId = 1, Competence = competence, MinimumCount = 1, MinimumLevel = "Advanced" }]
        };
        var employee = new Employee { Id = 10, Name = "Candidate", IsActive = true };
        var result = new PlanningAdvisor().RankCandidates(shift, [employee], [shift]).Single();
        Assert.False(result.Eligible);
        Assert.Contains(result.HardFailures, x => x.Contains("Medication"));
    }

    [Fact]
    public void CandidateWithOverlapIsRejected()
    {
        var shift = new Shift { Id = 2, Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), StartTime = new TimeOnly(15, 0), Hours = 7, MinimumStaff = 1 };
        var existing = new Shift { Id = 1, Date = shift.Date, StartTime = new TimeOnly(8, 0), Hours = 8, MinimumStaff = 1,
            Assignments = [new ShiftAssignment { EmployeeId = 10, Employee = new Employee { Id = 10, Name = "Candidate", IsActive = true } }] };
        var employee = existing.Assignments[0].Employee;
        var result = new PlanningAdvisor().RankCandidates(shift, [employee], [existing, shift]).Single();
        Assert.False(result.Eligible);
        Assert.Contains(result.HardFailures, x => x.Contains("overlappende"));
    }

    [Fact]
    public void ValidCandidateIsRankedEligible()
    {
        var competence = new Competence { Id = 1, Name = "First aid" };
        var employee = new Employee { Id = 10, Name = "Candidate", IsActive = true,
            Competences = [new EmployeeCompetence { CompetenceId = 1, Competence = competence, Level = "Advanced" }] };
        var shift = new Shift { Id = 1, Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), Hours = 8, MinimumStaff = 1,
            Requirements = [new ShiftRequirement { CompetenceId = 1, Competence = competence, MinimumCount = 1, MinimumLevel = "Intermediate" }] };
        var result = new PlanningAdvisor().RankCandidates(shift, [employee], [shift]).Single();
        Assert.True(result.Eligible);
    }
}
