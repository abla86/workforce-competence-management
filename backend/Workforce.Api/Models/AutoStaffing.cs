namespace Workforce.Api.Models;

public sealed class AutoStaffingRequest
{
    public int ShiftId { get; set; }
    public int? WorkTaskId { get; set; }
    public int RequiredCount { get; set; }
    public int MinimumCompetenceLevel { get; set; }
    public int? CompetenceId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

public sealed class StaffingProposal
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public int MatchScore { get; set; }
    public List<MatchingFactor> Factors { get; set; } = [];
    public List<StaffingWarning> Warnings { get; set; } = [];
    public double AddedHours { get; set; }
    public bool WillCauseOvertime { get; set; }
    public double ProjectedOvertimeHours { get; set; }
}

public sealed class MatchingFactor
{
    public FactorType Type { get; set; }
    public string Description { get; set; } = "";
    public int ScoreContribution { get; set; }
    public bool IsMandatory { get; set; }
}

public enum FactorType
{
    CompetenceMatch,
    CompetenceLevel,
    AvailableTimeSlot,
    UnderWeeklyLimit,
    RestPeriodCompliant,
    PreferenceMatch,
    FairDistribution
}

public sealed class StaffingWarning
{
    public StaffingWarningType Type { get; set; }
    public string Message { get; set; } = "";
}

public enum StaffingWarningType
{
    MissingCompetence,
    HasAbsence,
    DoubleBooked,
    RestPeriodViolation,
    OvertimeRisk
}
