using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;
using Workforce.Api.DTOs;
using Workforce.Api.Models;

namespace Workforce.Api.Services;

public sealed class CoverageService
{
    public ShiftCoverageResult AnalyzeShift(Shift shift)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var staffingCovered = shift.Assignments.Count >= shift.MinimumStaff;
        var warnings = new List<string>();
        var requirementResults = shift.Requirements.Select(requirement =>
        {
            var qualified = shift.Assignments.Count(a => IsQualified(a.Employee, requirement, shift.Date));
            var covered = qualified >= requirement.MinimumCount;
            if (!covered)
            {
                var prefix = requirement.IsCritical ? "Kritisk kompetanse mangler" : "Kompetanse mangler";
                warnings.Add($"{prefix}: {requirement.Competence.Name}");
            }
            return new RequirementCoverageResult(requirement.CompetenceId, requirement.Competence.Name,
                requirement.MinimumCount, requirement.MinimumLevel.ToString(), qualified, covered,
                covered ? "COVERED" : "MISSING", requirement.RequiredRole, requirement.IsCritical);
        }).ToList();

        if (!staffingCovered)
            warnings.Add($"Bemanning: mangler {shift.MinimumStaff - shift.Assignments.Count} person(er)");

        var coveredRequirements = requirementResults.Count(x => x.Covered);
        var competenceCoverage = requirementResults.Count == 0 ? 100 :
            (int)Math.Round((decimal)coveredRequirements / requirementResults.Count * 100);
        var criticalMissing = requirementResults.Any(x => x.IsCritical && !x.Covered);
        var overallCovered = staffingCovered && requirementResults.All(x => x.Covered);
        var status = overallCovered ? "GREEN" : criticalMissing || !staffingCovered ? "RED" : "YELLOW";

        if (shift.Date < today)
            warnings.Add("Vakten ligger tilbake i tid");

        return new ShiftCoverageResult(shift.Id, shift.Date, shift.ShiftType, shift.Hours,
            shift.MinimumStaff, shift.Assignments.Count, staffingCovered,
            staffingCovered ? "COVERED" : "UNDERSTAFFED",
            Math.Max(0, shift.MinimumStaff - shift.Assignments.Count), competenceCoverage,
            overallCovered, status,
            shift.Assignments.OrderBy(x => x.Employee.Name)
                .Select(x => new ShiftAssignmentResult(x.EmployeeId, x.Employee.Name, x.Employee.Role)).ToList(),
            requirementResults, warnings);
    }

    public async Task<ShiftCoverageResult> EvaluateShiftAsync(AppDbContext db, int shiftId,
        string? actor = "system", bool writeAudit = true)
    {
        var shift = await LoadShiftAsync(db, shiftId);
        if (shift is null) throw new ArgumentException($"Shift {shiftId} not found");
        return await EvaluateShiftAsync(db, shift, actor, writeAudit);
    }

    public async Task<ShiftCoverageResult> EvaluateShiftAsync(AppDbContext db, Shift shift,
        string? actor = "system", bool writeAudit = true)
    {
        var result = AnalyzeShift(shift);
        var warnings = result.Warnings?.ToList() ?? [];
        await AddAvailabilityWarningsAsync(db, shift, warnings);
        return FinalizeResult(db, result, warnings, actor, writeAudit);
    }

    // Used by list/dashboard endpoints: load the scheduling graph once, then evaluate in memory.
    public async Task<IReadOnlyList<ShiftCoverageResult>> EvaluateShiftsAsync(AppDbContext db)
    {
        var shifts = await db.Shifts
            .AsSplitQuery()
            .Include(x => x.Assignments).ThenInclude(x => x.Employee).ThenInclude(x => x.Competences)
            .Include(x => x.Assignments).ThenInclude(x => x.Employee).ThenInclude(x => x.Absences)
            .Include(x => x.Requirements).ThenInclude(x => x.Competence)
            .OrderBy(x => x.Date).ThenBy(x => x.StartTime)
            .ToListAsync();

        var results = new List<ShiftCoverageResult>(shifts.Count);
        foreach (var shift in shifts)
        {
            var result = AnalyzeShift(shift);
            var warnings = result.Warnings?.ToList() ?? [];
            AddAvailabilityWarningsFromLoadedShifts(shift, shifts, warnings);
            results.Add(FinalizeResultWithoutPersistence(result, warnings));
        }
        return results;
    }

    public async Task<CoverageScenarioResult> EvaluateScenarioAsync(AppDbContext db, int shiftId,
        IReadOnlyCollection<int> removeEmployeeIds, string? actor = "system")
    {
        var shift = await LoadShiftAsync(db, shiftId);
        if (shift is null) throw new ArgumentException($"Shift {shiftId} not found");
        var removed = new HashSet<int>(removeEmployeeIds);
        var simulated = new Shift
        {
            Id = shift.Id, Date = shift.Date, ShiftType = shift.ShiftType, Department = shift.Department,
            StartTime = shift.StartTime, Hours = shift.Hours, MinimumStaff = shift.MinimumStaff,
            IsCritical = shift.IsCritical, IsPublished = shift.IsPublished,
            Assignments = shift.Assignments.Where(a => !removed.Contains(a.EmployeeId)).ToList(),
            Requirements = shift.Requirements
        };

        var result = AnalyzeShift(simulated);
        var warnings = result.Warnings?.ToList() ?? [];
        await AddAvailabilityWarningsAsync(db, simulated, warnings);
        result = FinalizeResultWithoutPersistence(result, warnings);

        var employees = await db.Employees
            .Include(x => x.Competences).ThenInclude(x => x.Competence)
            .Include(x => x.Absences)
            .Where(x => x.IsActive && !removed.Contains(x.Id)).ToListAsync();
        var allShifts = await db.Shifts.Include(x => x.Assignments).ToListAsync();
        var candidates = new PlanningAdvisor().RankCandidates(shift, employees, allShifts)
            .Where(x => x.Eligible).Take(10).ToList();

        if (actor is not null)
        {
            db.AuditEvents.Add(new AuditEvent
            {
                Action = "shift.coverage.scenario", EntityType = "Shift", EntityId = shift.Id.ToString(),
                Actor = actor, Reason = $"Simulated removal of employee IDs: {string.Join(",", removed)}",
                DetailsJson = JsonSerializer.Serialize(new { result, candidates })
            });
            await db.SaveChangesAsync();
        }
        return new CoverageScenarioResult(shift.Id, removed.ToArray(), result, candidates);
    }

    private static ShiftCoverageResult FinalizeResultWithoutPersistence(ShiftCoverageResult result,
        IReadOnlyCollection<string> warnings)
    {
        var status = DetermineStatus(result, warnings);
        return result with { OverallCovered = status == "GREEN", OverallStatus = status, Warnings = warnings.ToList() };
    }

    private static ShiftCoverageResult FinalizeResult(AppDbContext db, ShiftCoverageResult result,
        IReadOnlyCollection<string> warnings, string? actor, bool writeAudit)
    {
        var finalized = FinalizeResultWithoutPersistence(result, warnings);
        if (writeAudit)
        {
            db.AuditEvents.Add(new AuditEvent
            {
                Action = "shift.coverage.evaluated", EntityType = "Shift",
                EntityId = finalized.ShiftId.ToString(), Actor = actor,
                DetailsJson = JsonSerializer.Serialize(finalized)
            });
            db.SaveChanges();
        }
        return finalized;
    }

    private async Task<Shift?> LoadShiftAsync(AppDbContext db, int shiftId)
    {
        return await db.Shifts.AsSplitQuery()
            .Include(x => x.Assignments).ThenInclude(x => x.Employee).ThenInclude(x => x.Competences)
            .Include(x => x.Assignments).ThenInclude(x => x.Employee).ThenInclude(x => x.Absences)
            .Include(x => x.Requirements).ThenInclude(x => x.Competence)
            .FirstOrDefaultAsync(x => x.Id == shiftId);
    }

    private async Task AddAvailabilityWarningsAsync(AppDbContext db, Shift shift, List<string> warnings)
    {
        if (shift.Assignments.Count == 0) return;
        var employeeIds = shift.Assignments.Select(x => x.EmployeeId).Distinct().ToArray();
        var otherShifts = await db.Shifts
            .Where(s => s.Id != shift.Id && s.Date >= shift.Date.AddDays(-1) && s.Date <= shift.Date.AddDays(1)
                && s.Assignments.Any(a => employeeIds.Contains(a.EmployeeId)))
            .Include(s => s.Assignments).ToListAsync();
        AddAvailabilityWarningsFromLoadedShifts(shift, otherShifts.Prepend(shift).ToList(), warnings);
    }

    private static void AddAvailabilityWarningsFromLoadedShifts(Shift shift,
        IReadOnlyCollection<Shift> allShifts, List<string> warnings)
    {
        if (shift.Assignments.Count == 0) return;
        var start = SchedulingRules.GetStart(shift);
        var end = SchedulingRules.GetEnd(shift);

        foreach (var assignment in shift.Assignments)
        {
            var employee = assignment.Employee;
            if (employee.Absences.Any(a => a.Approved && a.From <= shift.Date && a.To >= shift.Date))
                warnings.Add($"Tilgjengelighet: {employee.Name} har godkjent fravær på vaktdatoen");

            var employeeShifts = allShifts.Where(s => s.Id != shift.Id
                && s.Date >= shift.Date.AddDays(-1) && s.Date <= shift.Date.AddDays(1)
                && s.Assignments.Any(a => a.EmployeeId == employee.Id)).ToList();

            if (employeeShifts.Any(other => SchedulingRules.GetStart(other) < end && SchedulingRules.GetEnd(other) > start))
                warnings.Add($"Dobbeltbooking: {employee.Name} har overlappende vakt");

            foreach (var other in employeeShifts)
            {
                var otherStart = SchedulingRules.GetStart(other);
                var otherEnd = SchedulingRules.GetEnd(other);
                if (otherEnd <= start && (start - otherEnd).TotalHours < 11)
                {
                    warnings.Add($"Hviletid: {employee.Name} har under 11 timer mellom vakter før denne vakten");
                    break;
                }
                if (otherStart >= end && (otherStart - end).TotalHours < 11)
                {
                    warnings.Add($"Hviletid: {employee.Name} har under 11 timer mellom denne vakten og en senere vakt");
                    break;
                }
            }
        }
    }

    private static bool IsQualified(Employee employee, ShiftRequirement requirement, DateOnly shiftDate)
    {
        if (requirement.RequiredRole is not null && !string.Equals(employee.Role, requirement.RequiredRole, StringComparison.OrdinalIgnoreCase))
            return false;
        var ec = employee.Competences.FirstOrDefault(c => c.CompetenceId == requirement.CompetenceId);
        return ec is not null && ec.Level >= requirement.MinimumLevel && (!ec.ValidUntil.HasValue || ec.ValidUntil.Value >= shiftDate);
    }

    private static string DetermineStatus(ShiftCoverageResult result, IReadOnlyCollection<string> warnings)
    {
        if (result.Requirements.Any(x => x.IsCritical && !x.Covered) || !result.StaffingCovered) return "RED";
        if (warnings.Any(x => x.StartsWith("Tilgjengelighet:", StringComparison.Ordinal)
            || x.StartsWith("Hviletid:", StringComparison.Ordinal)
            || x.StartsWith("Dobbeltbooking:", StringComparison.Ordinal))
            || result.Requirements.Any(x => !x.Covered)) return "YELLOW";
        return "GREEN";
    }
}

public sealed record CoverageScenarioResult(int ShiftId, IReadOnlyCollection<int> RemovedEmployeeIds,
    ShiftCoverageResult CoverageWithoutEmployees, IReadOnlyList<CandidateResult> SuggestedReplacements);