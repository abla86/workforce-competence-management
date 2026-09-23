using System.Text;
using ShiftPlanner.Models;

namespace ShiftPlanner.Services;

public sealed class CsvExportService
{
    public void Export(
        string path,
        IReadOnlyList<Shift> shifts,
        IReadOnlyList<Employee> employees)
    {
        var employeeMap = employees.ToDictionary(e => e.Id);

        var builder = new StringBuilder();

        builder.AppendLine(
            "Date,ShiftType,Hours,MinimumStaff,AssignedStaff,Employees"
        );

        foreach (var shift in shifts.OrderBy(s => s.Date))
        {
            var names = shift.EmployeeIds
                .Where(employeeMap.ContainsKey)
                .Select(id => employeeMap[id].Name);

            builder.AppendLine(
                $"{shift.Date:yyyy-MM-dd}," +
                $"{Escape(shift.ShiftType)}," +
                $"{shift.Hours}," +
                $"{shift.MinimumStaff}," +
                $"{shift.EmployeeIds.Count}," +
                $"{Escape(string.Join(" | ", names))}"
            );
        }

        File.WriteAllText(
            path,
            builder.ToString(),
            Encoding.UTF8
        );
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"'))
        {
            var escaped = value.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }

        return value;
    }
}
