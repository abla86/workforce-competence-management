namespace Workforce.Api.Models;

public sealed class CoverageResult
{
    public CoverageStatus Status { get; set; }
    public List<TaskCoverageDetail> Tasks { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class TaskCoverageDetail
{
    public string TaskName { get; set; } = "";
    public int Required { get; set; }
    public int Actual { get; set; }
    public bool Critical { get; set; }
    public List<CoverageGap> Gaps { get; set; } = [];
}

public sealed class CoverageGap
{
    public GapType Type { get; set; }
    public string Description { get; set; } = "";
    public int? EmployeeId { get; set; }
}

public enum GapType
{
    InsufficientStaff,
    MissingCompetence,
    CompetenceExpired,
    AuthorizationExpired,
    MissingRole,
    UnauthorizedRole,
    DoubleBooked,
    RestPeriodViolation,
    EmployeeUnavailable
}

public enum CoverageStatus
{
    Green,
    Yellow,
    Red
}
