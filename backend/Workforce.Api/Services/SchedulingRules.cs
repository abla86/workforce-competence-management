using Workforce.Api.Models;

namespace Workforce.Api.Services;

public static class SchedulingRules
{
    private static readonly IReadOnlyDictionary<string, int> LevelRank =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Basic"] = 1,
            ["Intermediate"] = 2,
            ["Advanced"] = 3
        };

    public static bool TryGetLevelRank(string? level, out int rank)
    {
        if (string.IsNullOrWhiteSpace(level))
        {
            rank = 0;
            return false;
        }

        return LevelRank.TryGetValue(level.Trim(), out rank);
    }

    public static bool IsValidLevel(string? level) => TryGetLevelRank(level, out _);

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
