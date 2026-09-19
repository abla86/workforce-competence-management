using ShiftPlanner.Models;

namespace ShiftPlanner.Services;

public sealed class PlannerService
{
    public IReadOnlyList<string> ValidateShift(
        Shift shift,
        IReadOnlyList<Employee> employees)
    {
        var messages = new List<string>();

        var assigned = employees
            .Where(e => shift.EmployeeIds.Contains(e.Id))
            .ToList();

        if (assigned.Count < shift.MinimumStaff)
        {
            messages.Add(
                $"Mangler {shift.MinimumStaff - assigned.Count} ansatt(e) " +
                $"for å nå minimumsbemanning på {shift.MinimumStaff}."
            );
        }

        foreach (var required in shift.RequiredCompetencies)
        {
            var covered = assigned.Any(e =>
                e.Competencies.Any(c =>
                    string.Equals(
                        c,
                        required,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            );

            if (!covered)
            {
                messages.Add(
                    $"Mangler kompetanse: {required}."
                );
            }
        }

        if (messages.Count == 0)
        {
            messages.Add("Vakten oppfyller bemanning og kompetansekrav.");
        }

        return messages;
    }

    public Dictionary<int, decimal> CalculateHoursPerEmployee(
        IReadOnlyList<Shift> shifts)
    {
        var result = new Dictionary<int, decimal>();

        foreach (var shift in shifts)
        {
            foreach (var employeeId in shift.EmployeeIds)
            {
                result[employeeId] =
                    result.GetValueOrDefault(employeeId) + shift.Hours;
            }
        }

        return result;
    }

    public IReadOnlyList<Employee> GetAvailableEmployees(
        Shift shift,
        IReadOnlyList<Employee> employees)
    {
        return employees
            .Where(e => !shift.EmployeeIds.Contains(e.Id))
            .OrderBy(e => e.Name)
            .ToList();
    }

    public bool EmployeeHasCompetency(
        Employee employee,
        string competency)
    {
        return employee.Competencies.Any(c =>
            string.Equals(
                c,
                competency,
                StringComparison.OrdinalIgnoreCase
            )
        );
    }
}
