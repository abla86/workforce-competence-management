namespace Workforce.Api.Models;

public sealed record AutoStaffingRequest(
    int ShiftId,
    int? WorkTaskId = null,
    int? CompetenceId = null,
    int MinimumCompetenceLevel = 1);

public sealed class StaffingProposal
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public int MatchScore { get; set; }
    public double AddedHours { get; set; }
    public double ProjectedOvertimeHours { get; set; }
    public bool WillCauseOvertime { get; set; }
    public List<MatchingFactor> Factors { get; set; } = [];
    public List<StaffingWarning> Warnings { get; set; } = [];
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
    RestPeriodCompliant,
    UnderWeeklyLimit,
    PreferenceMatch
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

public sealed class SuggestedReplacement
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public string Role { get; set; } = "";
    public int CompetenceLevel { get; set; }
    public bool Available { get; set; }
    public List<string> MissingRequirements { get; set; } = [];
}

public sealed class ViabilityCheck
{
    public bool CanProceed { get; set; }
    public bool NeedsManualApproval { get; set; }
    public string Message { get; set; } = "";
    public List<RuleViolation> Violations { get; set; } = [];
    public List<DispensationNeed> DispensationsNeeded { get; set; } = [];
    public List<StaffingWarning> Warnings { get; set; } = [];
}

public sealed class RuleViolation
{
    public RuleType RuleType { get; set; }
    public RuleSeverity Severity { get; set; }
    public string Message { get; set; } = "";
}

public enum RuleSeverity
{
    Info,
    Warning,
    Critical
}

public sealed class DispensationNeed
{
    public RuleType RuleType { get; set; }
    public double HoursShortfall { get; set; }
    public bool RequiresApproval { get; set; }
    public bool CanBeAutoApproved { get; set; }
}
