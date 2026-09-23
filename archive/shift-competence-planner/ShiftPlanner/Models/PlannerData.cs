namespace ShiftPlanner.Models;

public sealed class PlannerData
{
    public List<Employee> Employees { get; init; } = [];
    public List<Shift> Shifts { get; init; } = [];
}
