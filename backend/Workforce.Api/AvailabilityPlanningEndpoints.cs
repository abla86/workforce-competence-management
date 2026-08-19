using System.Security.Claims;
using Workforce.Api.Models;
using Workforce.Api.Services;

namespace Workforce.Api;

public static class AvailabilityPlanningEndpoints
{
    public static void MapAvailabilityPlanningEndpoints(this WebApplication app)
    {
        app.MapGet("/api/availability/team/{departmentId:int}", async (
            int departmentId,
            DateTime? date,
            IEmployeeAvailabilityService service) =>
        {
            var requestedDate = date?.Date ?? DateTime.UtcNow.Date;
            var statuses = await service.GetTeamStatusAsync(departmentId, requestedDate);
            var grouped = statuses
                .GroupBy(s => s.Status)
                .ToDictionary(g => g.Key.ToString(), g => g.ToList());

            return Results.Ok(new
            {
                date = requestedDate.ToString("yyyy-MM-dd"),
                total = statuses.Count,
                available = statuses.Count(s => s.Status == EmployeeAvailabilityStatus.Available),
                busy = statuses.Count(s => s.Status == EmployeeAvailabilityStatus.Busy),
                absent = statuses.Count(s =>
                    s.Status == EmployeeAvailabilityStatus.Sick ||
                    s.Status == EmployeeAvailabilityStatus.OnVacation ||
                    s.Status == EmployeeAvailabilityStatus.Away),
                byStatus = grouped
            });
        })
        .WithName("GetTeamAvailability")
        .WithTags("Tilgjengelighet")
        .RequireAuthorization("CoverageRead");

        app.MapPut("/api/availability/me/status", async (
            EmployeeStatusRequest request,
            ClaimsPrincipal user,
            IEmployeeAvailabilityService service) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userId, out var employeeId))
                return Results.Unauthorized();

            if (request.EndTime.HasValue && request.EndTime.Value <= DateTime.UtcNow)
                return Results.BadRequest(new { message = "EndTime must be in the future." });

            await service.SetEmployeeStatusAsync(
                employeeId,
                request.Status,
                request.StatusText,
                request.EndTime,
                userId!);

            return Results.Ok(new { employeeId, request.Status, request.StatusText, request.EndTime });
        })
        .WithName("UpdateMyStatus")
        .WithTags("Tilgjengelighet")
        .RequireAuthorization();

        app.MapGet("/api/who-is-working/today/{departmentId:int}", async (
            int departmentId,
            DateTime? date,
            IShiftPlanService shiftPlanService,
            IEmployeeAvailabilityService availabilityService) =>
        {
            var requestedDate = date?.Date ?? DateTime.Today;
            var employees = await shiftPlanService.GetWhoIsWorkingTodayAsync(departmentId, requestedDate);
            var result = new List<object>();

            foreach (var employee in employees)
            {
                var status = await availabilityService.GetEmployeeStatusAsync(employee.Id, requestedDate);
                var shift = employee.ShiftAssignments
                    .Select(a => a.Shift)
                    .Where(s => s.Date == DateOnly.FromDateTime(requestedDate))
                    .OrderBy(s => s.StartTime)
                    .FirstOrDefault();

                result.Add(new
                {
                    id = employee.Id,
                    name = employee.Name,
                    role = employee.Role,
                    status = status.Status,
                    shiftType = shift?.ShiftType,
                    startTime = shift?.StartTime,
                    endTime = shift?.EndTime,
                    statusText = status.StatusText
                });
            }

            return Results.Ok(result);
        })
        .WithName("GetWhoIsWorkingToday")
        .WithTags("Tilgjengelighet")
        .RequireAuthorization("CoverageRead");

        app.MapGet("/api/dailyplans/today/{departmentId:int}", async (
            int departmentId,
            ClaimsPrincipal user,
            IDailyPlanService service) =>
        {
            var plan = await service.GetTodayPlanAsync(departmentId);
            if (plan is null)
            {
                var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
                plan = await service.CreateDailyPlanAsync(departmentId, userId);
            }

            return Results.Ok(plan);
        })
        .WithName("GetTodayDailyPlan")
        .WithTags("Dagsplan")
        .RequireAuthorization("CoverageRead");

        app.MapPost("/api/dailyplans/today/publish/{departmentId:int}", async (
            int departmentId,
            ClaimsPrincipal user,
            IDailyPlanService service) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Results.Unauthorized();

            var plan = await service.GetTodayPlanAsync(departmentId)
                ?? await service.CreateDailyPlanAsync(departmentId, userId);

            await service.PublishDailyPlanAsync(plan.Id, userId);
            return Results.Ok(new { planId = plan.Id });
        })
        .WithName("PublishTodayDailyPlan")
        .WithTags("Dagsplan")
        .RequireAuthorization("CoverageManage");

        app.MapGet("/api/shiftplans/current/{departmentId:int}", async (
            int departmentId,
            IShiftPlanService service) =>
        {
            var plan = await service.GetCurrentPublishedShiftPlanAsync(departmentId);
            return plan is null ? Results.NotFound() : Results.Ok(plan);
        })
        .WithName("GetCurrentShiftPlan")
        .WithTags("Skiftplan")
        .RequireAuthorization("CoverageRead");

        app.MapGet("/api/shiftplans/history/{departmentId:int}", async (
            int departmentId,
            int? count,
            IShiftPlanService service) =>
        {
            var plans = await service.GetShiftPlanHistoryAsync(departmentId, count ?? 5);
            return Results.Ok(plans);
        })
        .WithName("GetShiftPlanHistory")
        .WithTags("Skiftplan")
        .RequireAuthorization("CoverageRead");
    }

    public sealed record EmployeeStatusRequest(
        EmployeeAvailabilityStatus Status,
        string? StatusText,
        DateTime? EndTime);
}
