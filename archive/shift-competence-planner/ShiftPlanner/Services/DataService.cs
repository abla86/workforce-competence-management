using System.Text.Json;
using ShiftPlanner.Models;

namespace ShiftPlanner.Services;

public sealed class DataService
{
    private static readonly JsonSerializerOptions Options =
        new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

    public PlannerData Load(string path)
    {
        if (!File.Exists(path))
        {
            return new PlannerData();
        }

        var json = File.ReadAllText(path);

        return JsonSerializer.Deserialize<PlannerData>(
            json,
            Options
        ) ?? new PlannerData();
    }

    public void Save(string path, PlannerData data)
    {
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(data, Options);
        File.WriteAllText(path, json);
    }
}
