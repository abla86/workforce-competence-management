namespace Workforce.Api.Models;

public sealed class EmployeeStatus
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public EmployeeAvailabilityStatus Status { get; set; } = EmployeeAvailabilityStatus.Unknown;
    public string? StatusText { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsAutomatic { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum EmployeeAvailabilityStatus
{
    Unknown,
    Available,
    Busy,
    InMeeting,
    Away,
    Sick,
    OnVacation
}

public sealed class DailyPlan
{
    public int Id { get; set; }
    public int DepartmentId { get; set; }
    public DateTime PlanDate { get; set; }
    public string PlanTitle { get; set; } = "";
    public DailyPlanStatus Status { get; set; } = DailyPlanStatus.Draft;
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? PublishedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<DailyTaskItem> Tasks { get; set; } = [];
    public List<DailyPlanAssignment> Assignments { get; set; } = [];
}

public enum DailyPlanStatus
{
    Draft,
    Published,
    Archived
}

public sealed class DailyTaskItem
{
    public int Id { get; set; }
    public int DailyPlanId { get; set; }
    public DailyPlan DailyPlan { get; set; } = null!;
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool IsCompleted { get; set; }
    public int SortOrder { get; set; }
}

public sealed class DailyPlanAssignment
{
    public int Id { get; set; }
    public int DailyPlanId { get; set; }
    public DailyPlan DailyPlan { get; set; } = null!;
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int? RelatedShiftId { get; set; }
    public Shift? RelatedShift { get; set; }
}

public sealed class ShiftPlan
{
    public int Id { get; set; }
    public int DepartmentId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string PlanTitle { get; set; } = "";
    public ShiftPlanVisibility Visibility { get; set; } = ShiftPlanVisibility.AllEmployees;
    public bool IsPublished { get; set; }
    public int Version { get; set; } = 1;
    public DateTime? PublishedAt { get; set; }
    public string? PublishedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<Shift> Shifts { get; set; } = [];
}

public enum ShiftPlanVisibility
{
    ManagersOnly,
    Department,
    AllEmployees
}

public sealed class Notification
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public NotificationType Type { get; set; }
    public int? RelatedEmployeeId { get; set; }
    public int? RelatedShiftId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}

public enum NotificationType
{
    StatusChange,
    DailyPlanPublished,
    ShiftPlanPublished,
    General
}
