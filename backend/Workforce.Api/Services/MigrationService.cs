using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;
using Workforce.Api.Models;

namespace Workforce.Api.Services;

public sealed class MigrationService
{
    private readonly AppDbContext _db;
    public MigrationService(AppDbContext db) => _db = db;

    public async Task<MigrationImportResult> ImportAsync(MigrationImportRequest request, string actor, CancellationToken cancellationToken = default)
    {
        if (request.Employees.Count > 5000 || request.Competences.Count > 5000 || request.Shifts.Count > 10000) throw new ArgumentException("Import exceeds the supported limits.");
        var created = 0; var updated = 0; var skipped = 0; var conflicts = new List<MigrationConflict>();
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var competenceMap = (await _db.Competences.ToListAsync(cancellationToken)).ToDictionary(x => Normalize(x.Name));
            var employeeMap = (await _db.Employees.Include(x => x.Competences).ToListAsync(cancellationToken)).ToDictionary(x => EmployeeKey(x.Name, x.Role));
            var shiftMap = (await _db.Shifts.Include(x => x.Assignments).Include(x => x.Requirements).ToListAsync(cancellationToken)).ToDictionary(x => ShiftKey(x.Date, x.StartTime, x.ShiftType, x.Department));

            foreach (var source in request.Competences)
            {
                var key = Normalize(source.Name);
                if (string.IsNullOrWhiteSpace(key)) { conflicts.Add(new("Competence", source.Name, "Mangler navn")); continue; }
                if (competenceMap.TryGetValue(key, out var existing))
                {
                    if (request.Mode == MigrationConflictMode.Skip) { skipped++; continue; }
                    if (request.Mode == MigrationConflictMode.Create) { conflicts.Add(new("Competence", source.Name, "Finnes allerede")); continue; }
                    existing.Category = string.IsNullOrWhiteSpace(source.Category) ? existing.Category : source.Category.Trim(); updated++;
                }
                else { var item = new Competence { Name = source.Name.Trim(), Category = string.IsNullOrWhiteSpace(source.Category) ? "General" : source.Category.Trim() }; _db.Competences.Add(item); competenceMap[key] = item; created++; }
            }
            await _db.SaveChangesAsync(cancellationToken);
            competenceMap = (await _db.Competences.ToListAsync(cancellationToken)).ToDictionary(x => Normalize(x.Name));

            foreach (var source in request.Employees)
            {
                var key = EmployeeKey(source.Name, source.Role);
                if (string.IsNullOrWhiteSpace(Normalize(source.Name)) || string.IsNullOrWhiteSpace(Normalize(source.Role))) { conflicts.Add(new("Employee", source.Name, "Navn og rolle er påkrevd")); continue; }
                if (employeeMap.TryGetValue(key, out var existing))
                {
                    if (request.Mode == MigrationConflictMode.Skip) { skipped++; continue; }
                    if (request.Mode == MigrationConflictMode.Create) { conflicts.Add(new("Employee", source.Name, "Finnes allerede")); continue; }
                    existing.Department = source.Department?.Trim() ?? existing.Department; existing.Authorization = source.Authorization?.Trim();
                    if (source.PositionPercent is > 0 and <= 100) existing.PositionPercent = source.PositionPercent.Value;
                    if (source.MaxWeeklyHours is > 0 and <= 80) existing.MaxWeeklyHours = source.MaxWeeklyHours.Value;
                    existing.IsActive = source.IsActive; updated++;
                }
                else
                {
                    var item = new Employee { Name = source.Name.Trim(), Role = source.Role.Trim(), Department = source.Department?.Trim() ?? "", Authorization = source.Authorization?.Trim(), PositionPercent = source.PositionPercent is > 0 and <= 100 ? source.PositionPercent.Value : 100m, MaxWeeklyHours = source.MaxWeeklyHours is > 0 and <= 80 ? source.MaxWeeklyHours.Value : 37.5m, IsActive = source.IsActive };
                    _db.Employees.Add(item); employeeMap[key] = item; created++;
                }
            }
            await _db.SaveChangesAsync(cancellationToken);
            employeeMap = (await _db.Employees.Include(x => x.Competences).ToListAsync(cancellationToken)).ToDictionary(x => EmployeeKey(x.Name, x.Role));

            foreach (var source in request.Employees)
            {
                if (!employeeMap.TryGetValue(EmployeeKey(source.Name, source.Role), out var employee)) continue;
                foreach (var ec in source.Competences ?? [])
                {
                    if (!competenceMap.TryGetValue(Normalize(ec.Name), out var competence)) { conflicts.Add(new("EmployeeCompetence", $"{source.Name}/{ec.Name}", "Kompetansen finnes ikke")); continue; }
                    var existing = await _db.EmployeeCompetences.FindAsync([employee.Id, competence.Id], cancellationToken);
                    if (existing is null) _db.EmployeeCompetences.Add(new EmployeeCompetence { EmployeeId = employee.Id, CompetenceId = competence.Id, Level = ec.Level, ValidUntil = ec.ValidUntil });
                    else if (request.Mode == MigrationConflictMode.Update) { existing.Level = ec.Level; existing.ValidUntil = ec.ValidUntil; }
                }
            }

            foreach (var source in request.Shifts)
            {
                var key = ShiftKey(source.Date, source.StartTime, source.ShiftType, source.Department);
                if (shiftMap.TryGetValue(key, out var existing))
                {
                    if (request.Mode == MigrationConflictMode.Skip) { skipped++; continue; }
                    if (request.Mode == MigrationConflictMode.Create) { conflicts.Add(new("Shift", key, "Finnes allerede")); continue; }
                    existing.Hours = source.Hours; existing.MinimumStaff = source.MinimumStaff; existing.IsCritical = source.IsCritical; existing.IsPublished = source.IsPublished; updated++;
                    await ApplyShiftRelations(existing, source, employeeMap, competenceMap, cancellationToken);
                }
                else
                {
                    var item = new Shift { Date = source.Date, StartTime = source.StartTime, ShiftType = source.ShiftType.Trim(), Department = source.Department?.Trim() ?? "", Hours = source.Hours, MinimumStaff = source.MinimumStaff, IsCritical = source.IsCritical, IsPublished = source.IsPublished };
                    _db.Shifts.Add(item); await _db.SaveChangesAsync(cancellationToken); shiftMap[key] = item; created++;
                    await ApplyShiftRelations(item, source, employeeMap, competenceMap, cancellationToken);
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            var details = JsonSerializer.Serialize(new { created, updated, skipped, conflicts = conflicts.Count });
            _db.AuditEvents.Add(new AuditEvent { OccurredAtUtc = DateTime.UtcNow, Action = "migration.import.completed", EntityType = "Migration", EntityId = Guid.NewGuid().ToString("N"), Reason = request.SourceFileName, Actor = actor, DetailsJson = details });
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new MigrationImportResult(created, updated, skipped, conflicts, true);
        }
        catch { await transaction.RollbackAsync(cancellationToken); throw; }
    }

    private async Task ApplyShiftRelations(Shift shift, MigrationShift source, Dictionary<string, Employee> employees, Dictionary<string, Competence> competences, CancellationToken cancellationToken)
    {
        foreach (var assignment in source.Assignments ?? [])
        {
            var key = Normalize(assignment);
            var employee = employees.Values.FirstOrDefault(x => Normalize(x.Name) == key || EmployeeKey(x.Name, x.Role) == key);
            if (employee is not null && !shift.Assignments.Any(x => x.EmployeeId == employee.Id)) _db.ShiftAssignments.Add(new ShiftAssignment { ShiftId = shift.Id, EmployeeId = employee.Id });
        }
        foreach (var requirement in source.Requirements ?? [])
        {
            if (!competences.TryGetValue(Normalize(requirement.CompetenceName), out var competence)) continue;
            var existing = await _db.ShiftRequirements.FindAsync([shift.Id, competence.Id], cancellationToken);
            if (existing is null) _db.ShiftRequirements.Add(new ShiftRequirement { ShiftId = shift.Id, CompetenceId = competence.Id, MinimumCount = requirement.MinimumCount, MinimumLevel = requirement.MinimumLevel, RequiredRole = requirement.RequiredRole, IsCritical = requirement.IsCritical });
            else { existing.MinimumCount = requirement.MinimumCount; existing.MinimumLevel = requirement.MinimumLevel; existing.RequiredRole = requirement.RequiredRole; existing.IsCritical = requirement.IsCritical; }
        }
    }

    private static string EmployeeKey(string name, string role) => $"{Normalize(name)}|{Normalize(role)}";
    private static string ShiftKey(DateOnly date, TimeOnly? start, string type, string? department) => $"{date:yyyy-MM-dd}|{(start.HasValue ? start.Value.ToString("HH:mm") : "")}|{Normalize(type)}|{Normalize(department ?? "")}";
    private static string Normalize(string? value) => (value ?? "").Trim().ToLowerInvariant();

    public static IReadOnlyList<IReadOnlyList<string>> ReadExcel(Stream stream, CancellationToken cancellationToken = default)
    {
        using var document = SpreadsheetDocument.Open(stream, false);
        var workbook = document.WorkbookPart ?? throw new InvalidDataException("Excel workbook has no workbook part.");
        var sharedStrings = workbook.SharedStringTablePart?.SharedStringTable;
        var result = new List<IReadOnlyList<string>>();
        foreach (var sheet in workbook.Workbook.Sheets!.Elements<Sheet>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var part = (WorksheetPart)workbook.GetPartById(sheet.Id!);
            var rows = part.Worksheet.GetFirstChild<SheetData>()?.Elements<Row>() ?? [];
            foreach (var row in rows)
            {
                var values = row.Elements<Cell>().Select(cell => GetCellValue(cell, sharedStrings)).ToList();
                result.Add(new[] { $"__SHEET__:{sheet.Name}" }.Concat(values).ToList());
            }
        }
        return result;
    }

    private static string GetCellValue(Cell cell, SharedStringTable? sharedStrings)
    {
        var value = cell.CellValue?.Text ?? cell.InnerText ?? "";
        if (cell.DataType?.Value == CellValues.SharedString && int.TryParse(value, out var index) && sharedStrings is not null) return sharedStrings.Elements<SharedStringItem>().ElementAtOrDefault(index)?.InnerText ?? "";
        return value;
    }
}

public enum MigrationConflictMode { Skip, Update, Create }
public sealed record MigrationImportRequest(List<MigrationEmployee> Employees, List<MigrationCompetence> Competences, List<MigrationShift> Shifts, MigrationConflictMode Mode = MigrationConflictMode.Skip, string? SourceFileName = null);
public sealed record MigrationEmployee(string Name, string Role, string? Department, string? Authorization, decimal? PositionPercent, decimal? MaxWeeklyHours, bool IsActive = true, List<MigrationEmployeeCompetence>? Competences = null);
public sealed record MigrationEmployeeCompetence(string Name, CompetenceLevel Level, DateOnly? ValidUntil);
public sealed record MigrationCompetence(string Name, string? Category);
public sealed record MigrationShift(DateOnly Date, TimeOnly? StartTime, string ShiftType, string? Department, decimal Hours, int MinimumStaff, bool IsCritical, bool IsPublished, List<string>? Assignments = null, List<MigrationRequirement>? Requirements = null);
public sealed record MigrationRequirement(string CompetenceName, int MinimumCount, CompetenceLevel MinimumLevel, string? RequiredRole, bool IsCritical);
public sealed record MigrationConflict(string Type, string Key, string Reason);
public sealed record MigrationImportResult(int Created, int Updated, int Skipped, IReadOnlyList<MigrationConflict> Conflicts, bool Committed);
