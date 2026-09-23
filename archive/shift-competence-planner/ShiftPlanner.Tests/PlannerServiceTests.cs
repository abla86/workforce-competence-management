using Xunit;
using ShiftPlanner.Models;
using ShiftPlanner.Services;

namespace ShiftPlanner.Tests;

public sealed class PlannerServiceTests
{
    private readonly PlannerService _service = new();

    private static readonly List<Employee> Employees =
    [
        new(
            1,
            "Anne",
            "Sykepleier",
            100,
            ["Sykepleier", "Legemiddelhåndtering"]
        ),
        new(
            2,
            "Kari",
            "Helsefagarbeider",
            80,
            ["Helsefagarbeider"]
        )
    ];

    [Fact]
    public void ValidateShift_ReturnsSuccess_WhenRequirementsAreMet()
    {
        var shift = new Shift(
            1,
            new DateOnly(2026, 8, 14),
            "Dag",
            7.5m,
            2,
            ["Sykepleier"],
            [1, 2]
        );

        var result = _service.ValidateShift(shift, Employees);

        Assert.Single(result);
        Assert.Contains("oppfyller", result[0]);
    }

    [Fact]
    public void ValidateShift_ReportsMissingStaff()
    {
        var shift = new Shift(
            1,
            new DateOnly(2026, 8, 14),
            "Dag",
            7.5m,
            2,
            ["Sykepleier"],
            [1]
        );

        var result = _service.ValidateShift(shift, Employees);

        Assert.Contains(
            result,
            item => item.Contains("Mangler 1 ansatt")
        );
    }

    [Fact]
    public void ValidateShift_ReportsMissingCompetency()
    {
        var shift = new Shift(
            1,
            new DateOnly(2026, 8, 14),
            "Dag",
            7.5m,
            1,
            ["Sykepleier"],
            [2]
        );

        var result = _service.ValidateShift(shift, Employees);

        Assert.Contains(
            result,
            item => item.Contains("Mangler kompetanse")
        );
    }

    [Fact]
    public void CalculateHoursPerEmployee_SumsHoursCorrectly()
    {
        var shifts = new List<Shift>
        {
            new(
                1,
                new DateOnly(2026, 8, 14),
                "Dag",
                7.5m,
                1,
                [],
                [1]
            ),
            new(
                2,
                new DateOnly(2026, 8, 15),
                "Kveld",
                7.0m,
                1,
                [],
                [1, 2]
            )
        };

        var result = _service.CalculateHoursPerEmployee(shifts);

        Assert.Equal(14.5m, result[1]);
        Assert.Equal(7.0m, result[2]);
    }
}

