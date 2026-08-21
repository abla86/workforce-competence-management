using Workforce.Api.Models;

namespace Workforce.Api.Services;

/// <summary>
/// Shared scheduling time rules used by coverage analysis and candidate ranking.
/// </summary>
public static class SchedulingRules
{
    public static DateTime GetStart(Shift shift)
    {
        var time = shift.StartTime ?? shift.ShiftType.ToLowerInvariant() switch
        {
            "night" => new TimeOnly(22, 0),
            "evening" => new TimeOnly(15, 0),
            _ => new TimeOnly(7, 30)
        };

        return shift.Date.ToDateTime(time);
    }

    public static DateTime GetEnd(Shift shift) => GetStart(shift).AddHours((double)shift.Hours);
}
