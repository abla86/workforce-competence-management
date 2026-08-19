namespace Workforce.Api.Models;

public sealed class ViabilityCheck
{
    public bool CanProceed { get; set; }
    public bool NeedsManualApproval { get; set; }
    public List<RuleViolation> Violations { get; set; } = [];
    public List<StaffingWarning> Warnings { get; set; } = [];
    public List<DispensationNeed> DispensationsNeeded { get; set; } = [];
    public string Message { get; set; } = "";
}

public sealed class RuleViolation
{
    public RuleType RuleType { get; set; }
    public RuleSeverity Severity { get; set; }
    public string Message { get; set; } = "";
}

public enum RuleSeverity { Info, Warning, Critical }

public sealed class DispensationNeed
{
    public RuleType RuleType { get; set; }
    public double HoursShortfall { get; set; }
    public bool RequiresApproval { get; set; }
    public bool CanBeAutoApproved { get; set; }
}
