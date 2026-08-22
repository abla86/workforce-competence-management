using Workforce.Api.Models;
using Workforce.Api.Services;
using Xunit;

namespace Workforce.Api.Tests;

public sealed class PlanningAdvisorTests
{
    [Fact]
    public void CandidateWithApprovedAbsenceIsRejected()
    {
        var employee = new Employee { Id = 10, Name = "Candidate", IsActive = true, PositionPercent = 100m,
            Absences = [new Absence { EmployeeId = 10, From = DateOnly.FromDateTime(DateTime.UtcNow), To = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), Approved = true }] };
        var shift = new Shift { Id = 1, Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), Hours = 8, MinimumStaff = 1 };
        var result = new PlanningAdvisor().RankCandidates(shift, [employee], [shift]).Single();
        Assert.False(result.Eligible);
        Assert.Contains(result.HardFailures, x => x.Contains("Fravær"));
    }

    [Fact]
    public void CandidateWithOverlappingShiftIsRejected()
    {
        var employee = new Employee { Id = 10, Name = "Candidate", IsActive = true, PositionPercent = 100m };
        var target = new Shift { Id = 2, Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), StartTime = new TimeOnly(14, 0), Hours = 8, MinimumStaff = 1 };
        var existing = new Shift { Id = 1, Date = target.Date, StartTime = new TimeOnly(10, 0), Hours = 8, MinimumStaff = 1,
            Assignments = [new ShiftAssignment { EmployeeId = 10, Employee = employee }] };
        var result = new PlanningAdvisor().RankCandidates(target, [employee], [existing, target]).Single();
        Assert.False(result.Eligible);
        Assert.Contains(result.HardFailures, x => x.Contains("overlappende"));
    }

    [Fact]
    public void CandidateWithInsufficientRestBeforeShiftIsRejected()
    {
        var employee = new Employee { Id = 10, Name = "Candidate", IsActive = true, PositionPercent = 100m };
        var target = new Shift { Id = 2, Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), StartTime = new TimeOnly(7, 0), Hours = 8, MinimumStaff = 1 };
        var existing = new Shift { Id = 1, Date = target.Date.AddDays(-1), StartTime = new TimeOnly(18, 0), Hours = 8, MinimumStaff = 1,
            Assignments = [new ShiftAssignment { EmployeeId = 10, Employee = employee }] };
        var result = new PlanningAdvisor().RankCandidates(target, [employee], [existing, target]).Single();
        Assert.False(result.Eligible);
        Assert.Contains(result.HardFailures, x => x.Contains("11-timers hvile"));
    }

    [Fact]
    public void CandidateWithInsufficientRestAfterShiftIsRejected()
    {
        var employee = new Employee { Id = 10, Name = "Candidate", IsActive = true, PositionPercent = 100m };
        var target = new Shift { Id = 2, Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), StartTime = new TimeOnly(7, 0), Hours = 8, MinimumStaff = 1 };
        var existing = new Shift { Id = 3, Date = target.Date, StartTime = new TimeOnly(12, 0), Hours = 8, MinimumStaff = 1,
            Assignments = [new ShiftAssignment { EmployeeId = 10, Employee = employee }] };
        var result = new PlanningAdvisor().RankCandidates(target, [employee], [existing, target]).Single();
        Assert.False(result.Eligible);
        Assert.Contains(result.HardFailures, x => x.Contains("etter vakten"));
    }

    [Fact]
    public void CandidateExceedingWeeklyHoursIsRejected()
    {
        var employee = new Employee { Id = 10, Name = "Candidate", IsActive = true, PositionPercent = 100m, MaxWeeklyHours = 20m };
        var target = new Shift { Id = 2, Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), Hours = 8, MinimumStaff = 1 };
        var existing = new Shift { Id = 1, Date = target.Date.AddDays(-1), Hours = 16, MinimumStaff = 1,
            Assignments = [new ShiftAssignment { EmployeeId = 10, Employee = employee }] };
        var result = new PlanningAdvisor().RankCandidates(target, [employee], [existing, target]).Single();
        Assert.False(result.Eligible);
        Assert.Contains(result.HardFailures, x => x.Contains("Ukegrense"));
    }

    [Fact]
    public void InvalidRequirementLevelCannotBeRepresentedByPlanningModel()
    {
        var competence = new Competence { Id = 1, Name = "First aid" };
        var employee = new Employee { Id = 10, Name = "Candidate", IsActive = true, PositionPercent = 100m,
            Competences = [new EmployeeCompetence { CompetenceId = 1, Competence = competence, Level = CompetenceLevel.Advanced }] };
        var shift = new Shift { Id = 1, Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), Hours = 8, MinimumStaff = 1,
            Requirements = [new ShiftRequirement { CompetenceId = 1, Competence = competence, MinimumCount = 1, MinimumLevel = CompetenceLevel.Advanced }] };
        var result = new PlanningAdvisor().RankCandidates(shift, [employee], [shift]).Single();
        Assert.True(result.Eligible);
    }

    [Fact]
    public void InvalidEmployeeLevelCannotBeRepresentedByPlanningModel()
    {
        var competence = new Competence { Id = 1, Name = "First aid" };
        var employee = new Employee { Id = 10, Name = "Candidate", IsActive = true, PositionPercent = 100m,
            Competences = [new EmployeeCompetence { CompetenceId = 1, Competence = competence, Level = CompetenceLevel.Basic }] };
        var shift = new Shift { Id = 1, Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), Hours = 8, MinimumStaff = 1,
            Requirements = [new ShiftRequirement { CompetenceId = 1, Competence = competence, MinimumCount = 1, MinimumLevel = CompetenceLevel.Advanced }] };
        var result = new PlanningAdvisor().RankCandidates(shift, [employee], [shift]).Single();
        Assert.False(result.Eligible);
    }

    [Fact]
    public void ValidCandidateIsRankedEligible()
    {
        var competence = new Competence { Id = 1, Name = "First aid" };
        var employee = new Employee { Id = 10, Name = "Candidate", IsActive = true, PositionPercent = 100m,
            Competences = [new EmployeeCompetence { CompetenceId = 1, Competence = competence, Level = CompetenceLevel.Advanced }] };
        var shift = new Shift { Id = 1, Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), Hours = 8, MinimumStaff = 1,
            Requirements = [new ShiftRequirement { CompetenceId = 1, Competence = competence, MinimumCount = 1, MinimumLevel = CompetenceLevel.Intermediate }] };
        var result = new PlanningAdvisor().RankCandidates(shift, [employee], [shift]).Single();
        Assert.True(result.Eligible);
    }
}
