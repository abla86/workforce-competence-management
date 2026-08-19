using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;
using Workforce.Api.Models;

namespace Workforce.Api.Services;

public interface IEmployeeAvailabilityService
{
    Task<List<EmployeeStatus>> GetTeamStatusAsync(int departmentId, DateTime date);
    Task<EmployeeStatus> GetEmployeeStatusAsync(int employeeId, DateTime date);
    Task SetEmployeeStatusAsync(int employeeId, EmployeeAvailabilityStatus status, string? statusText, DateTime? endTime, string changedByUserId);
}

public interface IDailyPlanService
{
    Task<DailyPlan?> GetTodayPlanAsync(int departmentId);
    Task<DailyPlan> CreateDailyPlanAsync(int departmentId, string userId);
    Task<DailyPlan> PublishDailyPlanAsync(int planId, string userId);
    Task<List<DailyPlan>> GetRecentDailyPlansAsync(int departmentId, int days = 7);
    Task AddTaskToDailyPlanAsync(int planId, DailyTaskItem task, string userId);
}

public interface IShiftPlanService
{
    Task<ShiftPlan> CreateShiftPlanAsync(int departmentId, DateTime start, DateTime end, string userId);
    Task<ShiftPlan> PublishShiftPlanAsync(int planId, string userId);
    Task<ShiftPlan?> GetCurrentPublishedShiftPlanAsync(int departmentId);
    Task<List<ShiftPlan>> GetShiftPlanHistoryAsync(int departmentId, int count = 5);
    Task<List<Employee>> GetWhoIsWorkingTodayAsync(int departmentId, DateTime date);
}

public sealed class EmployeeAvailabilityService : IEmployeeAvailabilityService
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notifications;

    public EmployeeAvailabilityService(AppDbContext db, NotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task<List<EmployeeStatus>> GetTeamStatusAsync(int departmentId, DateTime date)
    {
        var employees = await _db.Employees.Where(e => e.DepartmentId == departmentId && e.IsActive).ToListAsync();
        var result = new List<EmployeeStatus>();
        foreach (var employee in employees)
            result.Add(await GetEmployeeStatusAsync(employee.Id, date));
        return result;
    }

    public async Task<EmployeeStatus> GetEmployeeStatusAsync(int employeeId, DateTime date)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId);
        if (employee is null) throw new ArgumentException($"Employee {employeeId} not found");

        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);
        var now = DateTime.Now;

        var absence = await _db.Absences
            .Where(a => a.EmployeeId == employeeId && a.IsApproved && a.StartDate < dayEnd && a.EndDate > dayStart)
            .OrderByDescending(a => a.StartDate)
            .FirstOrDefaultAsync();

        if (absence is not null)
        {
            return new EmployeeStatus
            {
                EmployeeId = employeeId,
                EmployeeName = employee.Name,
                Status = absence.Type switch
                {
                    AbsenceType.SickLeave => EmployeeAvailabilityStatus.Sick,
                    AbsenceType.Vacation => EmployeeAvailabilityStatus.OnVacation,
                    _ => EmployeeAvailabilityStatus.Away
                },
                StatusText = GetAbsenceLabel(absence),
                StartTime = dayStart,
                EndTime = dayEnd,
                IsAutomatic = true
            };
        }

        var shift = await _db.Shifts
            .Include(s => s.Assignments)
            .Include(s => s.ShiftTasks).ThenInclude(st => st.ShiftTaskCoverages)
            .Where(s => s.StartTime < dayEnd && s.EndTime > dayStart &&
                (s.Assignments.Any(a => a.EmployeeId == employeeId) || s.ShiftTasks.Any(st => st.ShiftTaskCoverages.Any(sc => sc.EmployeeId == employeeId))))
            .OrderBy(s => s.StartTime)
            .FirstOrDefaultAsync();

        if (shift is not null)
        {
            if (now >= shift.StartTime && now <= shift.EndTime)
                return new EmployeeStatus { EmployeeId = employeeId, EmployeeName = employee.Name, Status = EmployeeAvailabilityStatus.Busy, StatusText = $"På vakt {shift.ShiftType}", StartTime = shift.StartTime, EndTime = shift.EndTime, IsAutomatic = true };
            if (now < shift.StartTime)
                return new EmployeeStatus { EmployeeId = employeeId, EmployeeName = employee.Name, Status = EmployeeAvailabilityStatus.Unknown, StatusText = $"Vakt kl. {shift.StartTime:HH:mm}", StartTime = dayStart, EndTime = shift.StartTime, IsAutomatic = true };
        }

        var manual = await _db.EmployeeStatuses.AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeId == employeeId && !x.IsAutomatic && x.EndTime > DateTime.UtcNow);
        if (manual is not null)
            return manual;

        return new EmployeeStatus { EmployeeId = employeeId, EmployeeName = employee.Name, Status = EmployeeAvailabilityStatus.Available, StatusText = "Fri", StartTime = dayStart, EndTime = dayEnd, IsAutomatic = true };
    }

    public async Task SetEmployeeStatusAsync(int employeeId, EmployeeAvailabilityStatus status, string? statusText, DateTime? endTime, string changedByUserId)
    {
        var employee = await _db.Employees.FindAsync(employeeId) ?? throw new ArgumentException($"Employee {employeeId} not found");
        var item = await _db.EmployeeStatuses.FindAsync(employeeId);
        if (item is null)
        {
            item = new EmployeeStatus { EmployeeId = employeeId };
            _db.EmployeeStatuses.Add(item);
        }
        item.Status = status;
        item.StatusText = statusText;
        item.StartTime = DateTime.UtcNow;
        item.EndTime = endTime ?? DateTime.UtcNow.AddHours(8);
        item.IsAutomatic = false;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _notifications.NotifyStatusChangeAsync(employeeId, status, changedByUserId);
    }

    private static string GetAbsenceLabel(Absence absence) => absence.Type switch
    {
        AbsenceType.SickLeave => absence.Description ?? "Sykemeldt",
        AbsenceType.Vacation => absence.Description ?? "Ferie",
        AbsenceType.ParentalLeave => absence.Description ?? "Foreldrepermisjon",
        AbsenceType.Education => absence.Description ?? "Utdanning",
        _ => absence.Description ?? "Fravær"
    };
}

public sealed class NotificationService
{
    private readonly AppDbContext _db;
    public NotificationService(AppDbContext db) => _db = db;

    public async Task NotifyStatusChangeAsync(int employeeId, EmployeeAvailabilityStatus status, string changedByUserId)
    {
        var employee = await _db.Employees.FindAsync(employeeId);
        if (employee is null) return;
        var managerIds = await _db.Employees.Where(e => e.DepartmentId == employee.DepartmentId && (e.Role == "Manager" || e.Role == "HR") && e.IsActive).Select(e => e.Id).ToListAsync();
        foreach (var managerId in managerIds)
            _db.Notifications.Add(new Notification { EmployeeId = managerId, Title = $"Status endret: {employee.Name}", Message = $"{employee.Name} er nå: {GetStatusLabel(status)}", Type = NotificationType.StatusChange, RelatedEmployeeId = employeeId, CreatedAt = DateTime.UtcNow, IsRead = false });
        await _db.SaveChangesAsync();
    }

    public async Task NotifyDepartmentAsync(int departmentId, string title, string message, NotificationType type)
    {
        var employees = await _db.Employees.Where(e => e.DepartmentId == departmentId && e.IsActive).Select(e => e.Id).ToListAsync();
        foreach (var id in employees)
            _db.Notifications.Add(new Notification { EmployeeId = id, Title = title, Message = message, Type = type, CreatedAt = DateTime.UtcNow, IsRead = false });
        await _db.SaveChangesAsync();
    }

    private static string GetStatusLabel(EmployeeAvailabilityStatus status) => status switch
    {
        EmployeeAvailabilityStatus.Available => "Tilgjengelig",
        EmployeeAvailabilityStatus.Busy => "Opptatt",
        EmployeeAvailabilityStatus.InMeeting => "På møte",
        EmployeeAvailabilityStatus.Away => "Borte",
        EmployeeAvailabilityStatus.Sick => "Syk",
        EmployeeAvailabilityStatus.OnVacation => "Ferie",
        _ => "Ukjent"
    };
}

public sealed class DailyPlanService : IDailyPlanService
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notifications;
    public DailyPlanService(AppDbContext db, NotificationService notifications) { _db = db; _notifications = notifications; }

    public Task<DailyPlan?> GetTodayPlanAsync(int departmentId) => _db.DailyPlans.Include(p => p.Tasks).Include(p => p.Assignments).ThenInclude(a => a.Employee).FirstOrDefaultAsync(p => p.DepartmentId == departmentId && p.PlanDate.Date == DateTime.Today);

    public async Task<DailyPlan> CreateDailyPlanAsync(int departmentId, string userId)
    {
        var existing = await GetTodayPlanAsync(departmentId);
        if (existing is not null) return existing;
        var plan = new DailyPlan { DepartmentId = departmentId, PlanDate = DateTime.Today, PlanTitle = $"Plan for {DateTime.Today:dd.MM.yyyy}", Status = DailyPlanStatus.Draft, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _db.DailyPlans.Add(plan);
        await _db.SaveChangesAsync();
        return plan;
    }

    public async Task<DailyPlan> PublishDailyPlanAsync(int planId, string userId)
    {
        var plan = await _db.DailyPlans.FindAsync(planId) ?? throw new ArgumentException($"Daily plan {planId} not found");
        plan.Status = DailyPlanStatus.Published; plan.IsPublished = true; plan.PublishedAt = DateTime.UtcNow; plan.PublishedByUserId = userId; plan.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _notifications.NotifyDepartmentAsync(plan.DepartmentId, "Ny dagsplan", $"Ny dagsplan er publisert for {plan.PlanDate:dd.MM.yyyy}", NotificationType.DailyPlanPublished);
        return plan;
    }

    public Task<List<DailyPlan>> GetRecentDailyPlansAsync(int departmentId, int days = 7) => _db.DailyPlans.Where(p => p.DepartmentId == departmentId && p.PlanDate >= DateTime.Today.AddDays(-days) && p.PlanDate <= DateTime.Today.AddDays(days)).OrderByDescending(p => p.PlanDate).ToListAsync();

    public async Task AddTaskToDailyPlanAsync(int planId, DailyTaskItem task, string userId)
    {
        if (!await _db.DailyPlans.AnyAsync(p => p.Id == planId)) throw new ArgumentException($"Daily plan {planId} not found");
        task.DailyPlanId = planId; _db.DailyTaskItems.Add(task); await _db.SaveChangesAsync();
    }
}

public sealed class ShiftPlanService : IShiftPlanService
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notifications;
    public ShiftPlanService(AppDbContext db, NotificationService notifications) { _db = db; _notifications = notifications; }

    public async Task<ShiftPlan> CreateShiftPlanAsync(int departmentId, DateTime start, DateTime end, string userId)
    {
        if (end <= start) throw new ArgumentException("End date must be after start date.");
        var plan = new ShiftPlan { DepartmentId = departmentId, StartDate = start, EndDate = end, PlanTitle = $"Vaktplan {start:dd.MM.yyyy} - {end:dd.MM.yyyy}", Visibility = ShiftPlanVisibility.AllEmployees, CreatedAt = DateTime.UtcNow };
        _db.ShiftPlans.Add(plan); await _db.SaveChangesAsync(); return plan;
    }

    public async Task<ShiftPlan> PublishShiftPlanAsync(int planId, string userId)
    {
        var plan = await _db.ShiftPlans.FindAsync(planId) ?? throw new ArgumentException($"Shift plan {planId} not found");
        plan.IsPublished = true; plan.PublishedAt = DateTime.UtcNow; plan.PublishedByUserId = userId; plan.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _notifications.NotifyDepartmentAsync(plan.DepartmentId, "Ny vaktplan publisert", "En ny vaktplan er publisert. Sjekk vakten din.", NotificationType.ShiftPlanPublished);
        return plan;
    }

    public Task<ShiftPlan?> GetCurrentPublishedShiftPlanAsync(int departmentId) => _db.ShiftPlans.Include(p => p.Shifts).FirstOrDefaultAsync(p => p.DepartmentId == departmentId && p.IsPublished && p.StartDate <= DateTime.Today && p.EndDate >= DateTime.Today);

    public Task<List<ShiftPlan>> GetShiftPlanHistoryAsync(int departmentId, int count = 5) => _db.ShiftPlans.Where(p => p.DepartmentId == departmentId && p.IsPublished).OrderByDescending(p => p.PublishedAt).Take(Math.Clamp(count, 1, 50)).ToListAsync();

    public Task<List<Employee>> GetWhoIsWorkingTodayAsync(int departmentId, DateTime date) => _db.Employees.Where(e => e.DepartmentId == departmentId && e.IsActive && (e.ShiftAssignments.Any(a => a.Shift.StartTime.Date == date.Date) || e.ShiftTaskCoverages.Any(sc => sc.ShiftTask.Shift.StartTime.Date == date.Date))).Include(e => e.ShiftAssignments).ThenInclude(a => a.Shift).ToListAsync();
}
