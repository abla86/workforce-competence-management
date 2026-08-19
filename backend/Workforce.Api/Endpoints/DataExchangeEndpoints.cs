using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;
using Workforce.Api.Models;

namespace Workforce.Api.Endpoints;

public static class DataExchangeEndpoints
{
    public static void MapDataExchangeEndpoints(this WebApplication app)
    {
        app.MapGet("/api/export/employees.csv", async (AppDbContext db) =>
        {
            var employees = await db.Employees.AsNoTracking().OrderBy(e => e.Name).ToListAsync();
            var sb = new StringBuilder();
            sb.AppendLine("Name,Role,Department,Authorization,PositionPercent,MaxWeeklyHours,IsActive");
            foreach (var e in employees)
            {
                sb.AppendLine(string.Join(',',
                    Csv(e.Name), Csv(e.Role), Csv(e.Department), Csv(e.Authorization),
                    e.PositionPercent.ToString(CultureInfo.InvariantCulture),
                    e.MaxWeeklyHours.ToString(CultureInfo.InvariantCulture),
                    e.IsActive ? "true" : "false"));
            }
            return Results.File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv; charset=utf-8", "vaktklar-ansatte.csv");
        }).WithTags("Datautveksling");

        app.MapPost("/api/import/employees.csv", async (HttpRequest request, AppDbContext db) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest(new { message = "Send CSV as multipart/form-data with field 'file'." });

            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { message = "CSV file is required." });
            if (file.Length > 5 * 1024 * 1024)
                return Results.BadRequest(new { message = "CSV file is too large (maximum 5 MB)." });

            using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, true);
            var text = await reader.ReadToEndAsync();
            var rows = ParseCsv(text).ToList();
            if (rows.Count < 2)
                return Results.BadRequest(new { message = "CSV must contain a header and at least one employee." });

            var headers = rows[0].Select(x => x.Trim()).ToArray();
            var required = new[] { "Name", "Role" };
            if (required.Any(r => Array.IndexOf(headers, r) < 0))
                return Results.BadRequest(new { message = "CSV must contain Name and Role columns." });

            var index = headers.Select((name, i) => (name, i)).ToDictionary(x => x.name, x => x.i, StringComparer.OrdinalIgnoreCase);
            var created = 0;
            var updated = 0;
            var errors = new List<object>();

            for (var rowNumber = 1; rowNumber < rows.Count; rowNumber++)
            {
                var row = rows[rowNumber];
                string Get(string name) => index.TryGetValue(name, out var i) && i < row.Count ? row[i].Trim() : "";
                var name = Get("Name");
                var role = Get("Role");
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(role))
                {
                    errors.Add(new { row = rowNumber + 1, message = "Name and Role are required." });
                    continue;
                }

                var department = Get("Department");
                var existing = await db.Employees.FirstOrDefaultAsync(e => e.Name == name && e.Role == role && e.Department == department);
                if (existing is null)
                {
                    existing = new Employee { Name = name, Role = role, Department = department };
                    db.Employees.Add(existing);
                    created++;
                }
                else updated++;

                existing.Authorization = NullIfEmpty(Get("Authorization"));
                existing.PositionPercent = ParseDecimal(Get("PositionPercent"), 100m);
                existing.MaxWeeklyHours = ParseDecimal(Get("MaxWeeklyHours"), 37.5m);
                existing.IsActive = !bool.TryParse(Get("IsActive"), out var active) || active;
            }

            if (errors.Count > 0 && created == 0 && updated == 0)
                return Results.BadRequest(new { message = "No valid employee rows found.", errors });

            await db.SaveChangesAsync();
            return Results.Ok(new { created, updated, errors });
        }).WithTags("Datautveksling");

        app.MapGet("/api/export/competences.csv", async (AppDbContext db) =>
        {
            var rows = await db.EmployeeCompetences.AsNoTracking()
                .Include(x => x.Employee).Include(x => x.Competence)
                .OrderBy(x => x.Employee.Name).ThenBy(x => x.Competence.Name).ToListAsync();
            var sb = new StringBuilder("EmployeeName,CompetenceName,Level,ValidUntil\n");
            foreach (var x in rows)
                sb.AppendLine(string.Join(',', Csv(x.Employee.Name), Csv(x.Competence.Name), x.Level.ToString(CultureInfo.InvariantCulture), x.ValidUntil?.ToString("yyyy-MM-dd") ?? ""));
            return Results.File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv; charset=utf-8", "vaktklar-kompetanse.csv");
        }).WithTags("Datautveksling");

        app.MapGet("/api/export/shifts.xls", async (AppDbContext db, CoverageService coverage) =>
        {
            var shifts = await db.Shifts.AsNoTracking()
                .Include(s => s.Assignments).ThenInclude(a => a.Employee)
                .Include(s => s.Requirements).ThenInclude(r => r.Competence)
                .OrderBy(s => s.Date).ThenBy(s => s.StartTime).ToListAsync();

            var sb = new StringBuilder();
            sb.Append("<html><head><meta charset='utf-8'><style>table{border-collapse:collapse}th,td{border:1px solid #999;padding:6px} .green{background:#d9ead3}.yellow{background:#fff2cc}.red{background:#f4cccc}</style></head><body>");
            sb.Append("<h1>Vaktklar – vaktplan</h1><table><tr><th>Dato</th><th>Vakt</th><th>Avdeling</th><th>Start</th><th>Slutt</th><th>Minimum</th><th>Bemannet</th><th>Status</th><th>Kommentar</th></tr>");
            foreach (var shift in shifts)
            {
                var result = coverage.AnalyzeShift(shift);
                var status = result.Status?.ToString() ?? "UNKNOWN";
                var css = status.Contains("Green", StringComparison.OrdinalIgnoreCase) ? "green" : status.Contains("Yellow", StringComparison.OrdinalIgnoreCase) ? "yellow" : "red";
                var comment = string.Join(" | ", result.Warnings ?? []);
                sb.Append($"<tr class='{css}'><td>{H(shift.Date.ToString("yyyy-MM-dd"))}</td><td>{H(shift.ShiftType)}</td><td>{H(shift.Department)}</td><td>{H(shift.StartTime.ToString("HH:mm"))}</td><td>{H(shift.StartTime.AddHours((double)shift.Hours).ToString("HH:mm"))}</td><td>{shift.MinimumStaff}</td><td>{shift.Assignments.Count}</td><td>{H(status)}</td><td>{H(comment)}</td></tr>");
            }
            sb.Append("</table></body></html>");
            return Results.File(Encoding.UTF8.GetBytes(sb.ToString()), "application/vnd.ms-excel", "vaktklar-vaktplan.xls");
        }).WithTags("Datautveksling");

        app.MapGet("/api/export/backup.json", async (AppDbContext db) =>
        {
            var employees = await db.Employees.AsNoTracking().Include(e => e.Competences).ThenInclude(c => c.Competence).ToListAsync();
            var competences = await db.Competences.AsNoTracking().ToListAsync();
            var shifts = await db.Shifts.AsNoTracking().Include(s => s.Assignments).Include(s => s.Requirements).ToListAsync();
            var payload = new { exportedAtUtc = DateTime.UtcNow, version = "vaktklar-backup-1", employees, competences, shifts };
            return Results.File(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true })), "application/json", "vaktklar-backup.json");
        }).WithTags("Datautveksling");
    }

    private static string Csv(string? value) => $"\"{(value ?? "").Replace("\"", "\"\"")}\"";
    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static decimal ParseDecimal(string value, decimal fallback) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("nb-NO"), out n) ? n : fallback;
    private static string H(string value) => System.Net.WebUtility.HtmlEncode(value);

    private static IEnumerable<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (ch == ',' && !quoted) { row.Add(cell.ToString()); cell.Clear(); }
            else if ((ch == '\n' || ch == '\r') && !quoted)
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Clear();
                if (row.Any(x => !string.IsNullOrWhiteSpace(x))) rows.Add(row);
                row = new List<string>();
            }
            else cell.Append(ch);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
        return rows;
    }
}
