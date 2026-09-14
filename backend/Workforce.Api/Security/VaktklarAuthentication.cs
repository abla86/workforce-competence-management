using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Workforce.Api.Data;
using Workforce.Api.DTOs;
using Workforce.Api.Models;
using Workforce.Api.Services;

namespace Workforce.Api.Security;

public static class VaktklarAuthentication
{
    public const string CookieName = "vaktklar_access";

    public static void AddVaktklarAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var secret = configuration["Jwt:SecretKey"];
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
            throw new InvalidOperationException("Jwt:SecretKey must contain at least 32 UTF-8 bytes.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = configuration["Jwt:Issuer"] ?? "vaktklar",
                ValidateAudience = true, ValidAudience = configuration["Jwt:Audience"] ?? "vaktklar-web",
                ValidateLifetime = true, ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), ClockSkew = TimeSpan.FromSeconds(30)
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (context.Request.Cookies.TryGetValue(CookieName, out var token)) context.Token = token;
                    return Task.CompletedTask;
                }
            };
        });
        services.AddAuthorization(options =>
        {
            options.AddPolicy("Manager", p => p.RequireRole("Admin", "Manager"));
            options.AddPolicy("HR", p => p.RequireRole("Admin", "HR"));
            options.AddPolicy("Admin", p => p.RequireRole("Admin"));
        });
        services.AddScoped<MigrationService>();
        services.AddTransient<IStartupFilter, RoleGuardStartupFilter>();
    }

    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Authentication");
        group.MapPost("/login", async (LoginRequest request, AppDbContext db, IConfiguration config, HttpResponse response) =>
        {
            var normalized = request.Username.Trim().ToLowerInvariant();
            var user = await db.UserAccounts.SingleOrDefaultAsync(x => x.Username == normalized && x.IsActive);
            if (user is null) return Results.Unauthorized();
            if (user.LockedUntilUtc is { } locked && locked > DateTime.UtcNow) return Results.Json(new { message = "Kontoen er midlertidig låst. Prøv igjen senere." }, statusCode: 423);
            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= 5) { user.FailedLoginAttempts = 0; user.LockedUntilUtc = DateTime.UtcNow.AddMinutes(15); }
                await db.SaveChangesAsync(); return Results.Unauthorized();
            }
            user.FailedLoginAttempts = 0; user.LockedUntilUtc = null; user.LastLoginAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(); response.Cookies.Append(CookieName, CreateToken(user, config), CookieOptions(config));
            return Results.Ok(new { user = new { user.Id, user.Username, user.Role } });
        }).RequireRateLimiting("auth");

        group.MapPost("/logout", (HttpResponse response) => { response.Cookies.Delete(CookieName, CookieOptions(null)); return Results.NoContent(); });
        group.MapGet("/me", (ClaimsPrincipal principal) =>
        {
            if (principal.Identity?.IsAuthenticated != true) return Results.Unauthorized();
            return Results.Ok(new { id = principal.FindFirstValue(ClaimTypes.NameIdentifier), username = principal.FindFirstValue(ClaimTypes.Name), role = principal.FindFirstValue(ClaimTypes.Role) });
        }).RequireAuthorization();
        group.MapPost("/bootstrap", async (BootstrapRequest request, AppDbContext db, IConfiguration config) =>
        {
            var bootstrapKey = config["VAKTKLAR_BOOTSTRAP_KEY"];
            if (string.IsNullOrWhiteSpace(bootstrapKey)) return Results.StatusCode(503);
            var expected = Encoding.UTF8.GetBytes(bootstrapKey); var supplied = Encoding.UTF8.GetBytes(request.BootstrapKey);
            if (expected.Length != supplied.Length || !CryptographicOperations.FixedTimeEquals(expected, supplied)) return Results.Unauthorized();
            if (await db.UserAccounts.AnyAsync()) return Results.Conflict(new { message = "Initial setup is already completed." });
            if (request.Username.Length < 3 || request.Password.Length < 12) return Results.BadRequest(new { message = "Username must contain at least 3 characters and password at least 12 characters." });
            var user = new UserAccount { Username = request.Username.Trim().ToLowerInvariant(), PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, 12), Role = "Admin" };
            db.UserAccounts.Add(user); await db.SaveChangesAsync(); return Results.Created("/api/auth/me", new { user.Id, user.Username, user.Role });
        }).RequireRateLimiting("auth");
        return group;
    }

    private static string CreateToken(UserAccount user, IConfiguration config)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:SecretKey"]!));
        var token = new JwtSecurityToken(issuer: config["Jwt:Issuer"] ?? "vaktklar", audience: config["Jwt:Audience"] ?? "vaktklar-web",
            claims: [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.Username), new Claim(ClaimTypes.Role, user.Role)],
            expires: DateTime.UtcNow.AddMinutes(60), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static CookieOptions CookieOptions(IConfiguration? config) => new()
    {
        HttpOnly = true, Secure = config?["Security:CookieSecure"] == "true" || config is null, SameSite = SameSiteMode.Lax, Path = "/",
        MaxAge = config is null ? TimeSpan.Zero : TimeSpan.FromMinutes(60)
    };
}

internal sealed class RoleGuardStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextRequest) =>
        {
            if (await TryHandleDataExchangeAsync(context)) return;

            if (context.Request.Path.StartsWithSegments("/api") && !context.Request.Path.StartsWithSegments("/api/auth") && context.Request.Method is "POST" or "PUT" or "DELETE" or "PATCH")
            {
                var auth = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
                var role = auth.Principal?.FindFirstValue(ClaimTypes.Role);
                if (role is not ("Admin" or "Manager" or "HR"))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { message = "Manager-, HR- eller administratorrolle kreves for å endre planleggingsdata." });
                    return;
                }
            }
            await nextRequest();
        });
        next(app);
    };

    private static async Task<bool> TryHandleDataExchangeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        if (!path.StartsWith("/api/export/", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("/api/import/", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("/api/share/", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("/api/migration/", StringComparison.OrdinalIgnoreCase))
            return false;

        var auth = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        if (!auth.Succeeded)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return true;
        }

        var role = auth.Principal?.FindFirstValue(ClaimTypes.Role);
        if (path.StartsWith("/api/migration/", StringComparison.OrdinalIgnoreCase) && role is not ("Admin" or "Manager" or "HR"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = "Manager-, HR- eller administratorrolle kreves for migrering." });
            return true;
        }

        if (path.Equals("/api/migration/inspect", StringComparison.OrdinalIgnoreCase) && HttpMethods.IsPost(context.Request.Method))
        {
            if (!context.Request.HasFormContentType) { await WriteBadRequestAsync(context, new { message = "Send filen som multipart/form-data med feltet 'file'." }); return true; }
            var form = await context.Request.ReadFormAsync(); var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0 || file.Length > 20 * 1024 * 1024) { await WriteBadRequestAsync(context, new { message = "Filen mangler eller er større enn 20 MB." }); return true; }
            try
            {
                if (Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(file.FileName).Equals(".xlsm", StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = file.OpenReadStream();
                    var rows = MigrationService.ReadExcel(stream);
                    await context.Response.WriteAsJsonAsync(new { format = "Excel", fileName = file.FileName, rows, rowCount = rows.Count });
                    return true;
                }
                using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, true);
                var text = await reader.ReadToEndAsync();
                var format = Path.GetExtension(file.FileName).Equals(".json", StringComparison.OrdinalIgnoreCase) ? "JSON" : Path.GetExtension(file.FileName).Equals(".ics", StringComparison.OrdinalIgnoreCase) ? "ICS" : "CSV";
                var preview = format == "JSON" ? (object?)JsonDocument.Parse(text).RootElement : text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).Take(101).ToArray();
                await context.Response.WriteAsJsonAsync(new { format, fileName = file.FileName, preview });
                return true;
            }
            catch (Exception ex) { await WriteBadRequestAsync(context, new { message = $"Kunne ikke lese filen: {ex.Message}" }); return true; }
        }

        if (path.Equals("/api/migration/import", StringComparison.OrdinalIgnoreCase) && HttpMethods.IsPost(context.Request.Method))
        {
            try
            {
                var request = await JsonSerializer.DeserializeAsync<MigrationImportRequest>(context.Request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (request is null) { await WriteBadRequestAsync(context, new { message = "Import payload is empty." }); return true; }
                var service = context.RequestServices.GetRequiredService<MigrationService>();
                var result = await service.ImportAsync(request, auth.Principal?.FindFirstValue(ClaimTypes.Name) ?? "system", context.RequestAborted);
                await context.Response.WriteAsJsonAsync(result);
                return true;
            }
            catch (ArgumentException ex) { await WriteBadRequestAsync(context, new { message = ex.Message }); return true; }
            catch (Exception ex) { context.Response.StatusCode = StatusCodes.Status500InternalServerError; await context.Response.WriteAsJsonAsync(new { message = "Migration failed and the database transaction was rolled back.", detail = ex.Message }); return true; }
        }

        var db = context.RequestServices.GetRequiredService<AppDbContext>();
        var coverage = context.RequestServices.GetRequiredService<CoverageService>();

        if (HttpMethods.IsGet(context.Request.Method) && path.Equals("/api/export/employees.csv", StringComparison.OrdinalIgnoreCase))
        {
            var employees = await db.Employees.AsNoTracking().OrderBy(e => e.Name).ToListAsync();
            var sb = new StringBuilder("Name,Role,Department,Authorization,PositionPercent,MaxWeeklyHours,IsActive\n");
            foreach (var e in employees) sb.AppendLine(string.Join(',', Csv(e.Name), Csv(e.Role), Csv(e.Department), Csv(e.Authorization), e.PositionPercent.ToString(CultureInfo.InvariantCulture), e.MaxWeeklyHours.ToString(CultureInfo.InvariantCulture), e.IsActive ? "true" : "false"));
            await FileResponse(context, sb.ToString(), "text/csv; charset=utf-8", "vaktklar-ansatte.csv"); return true;
        }
        if (HttpMethods.IsGet(context.Request.Method) && path.Equals("/api/export/competences.csv", StringComparison.OrdinalIgnoreCase))
        {
            var rows = await db.EmployeeCompetences.AsNoTracking().Include(x => x.Employee).Include(x => x.Competence).OrderBy(x => x.Employee.Name).ThenBy(x => x.Competence.Name).ToListAsync();
            var sb = new StringBuilder("EmployeeName,CompetenceName,Level,ValidUntil\n");
            foreach (var x in rows) sb.AppendLine(string.Join(',', Csv(x.Employee.Name), Csv(x.Competence.Name), Csv(x.Level.ToString()), x.ValidUntil?.ToString("yyyy-MM-dd") ?? ""));
            await FileResponse(context, sb.ToString(), "text/csv; charset=utf-8", "vaktklar-kompetanse.csv"); return true;
        }
        if (HttpMethods.IsGet(context.Request.Method) && path.Equals("/api/export/shifts.xls", StringComparison.OrdinalIgnoreCase))
        {
            var shifts = await db.Shifts.AsNoTracking().Include(s => s.Assignments).ThenInclude(a => a.Employee).Include(s => s.Requirements).ThenInclude(r => r.Competence).OrderBy(s => s.Date).ThenBy(s => s.StartTime).ToListAsync();
            await FileResponse(context, BuildShiftHtml(shifts, coverage), "application/vnd.ms-excel", "vaktklar-vaktplan.xls"); return true;
        }
        if (HttpMethods.IsGet(context.Request.Method) && path.Equals("/api/export/backup.json", StringComparison.OrdinalIgnoreCase))
        {
            var employees = await db.Employees.AsNoTracking().Include(e => e.Competences).ThenInclude(c => c.Competence).ToListAsync();
            var competences = await db.Competences.AsNoTracking().ToListAsync(); var shifts = await db.Shifts.AsNoTracking().Include(s => s.Assignments).Include(s => s.Requirements).ToListAsync();
            var payload = JsonSerializer.Serialize(new { exportedAtUtc = DateTime.UtcNow, version = "vaktklar-backup-1", employees, competences, shifts }, new JsonSerializerOptions { WriteIndented = true });
            await FileResponse(context, payload, "application/json", "vaktklar-backup.json"); return true;
        }
        if (HttpMethods.IsGet(context.Request.Method) && path.Equals("/api/share/shiftplan", StringComparison.OrdinalIgnoreCase))
        {
            var shifts = await db.Shifts.AsNoTracking().Include(s => s.Assignments).ThenInclude(a => a.Employee).Include(s => s.Requirements).ThenInclude(r => r.Competence).OrderBy(s => s.Date).ThenBy(s => s.StartTime).ToListAsync();
            context.Response.ContentType = "text/html; charset=utf-8"; context.Response.Headers.ContentDisposition = "inline; filename=\"vaktklar-vaktplan.html\""; await context.Response.WriteAsync(BuildShiftHtml(shifts, coverage)); return true;
        }
        if (HttpMethods.IsPost(context.Request.Method) && path.Equals("/api/import/employees.csv", StringComparison.OrdinalIgnoreCase)) { await ImportEmployeesAsync(context, db); return true; }
        if (HttpMethods.IsPost(context.Request.Method) && path.Equals("/api/import/competences.csv", StringComparison.OrdinalIgnoreCase)) { await ImportCompetencesAsync(context, db); return true; }
        context.Response.StatusCode = StatusCodes.Status404NotFound; return true;
    }

    private static Task WriteBadRequestAsync(HttpContext context, object payload) { context.Response.StatusCode = StatusCodes.Status400BadRequest; return context.Response.WriteAsJsonAsync(payload); }

    private static async Task ImportEmployeesAsync(HttpContext context, AppDbContext db)
    {
        if (!context.Request.HasFormContentType) { await WriteBadRequestAsync(context, new { message = "Send CSV as multipart/form-data with field 'file'." }); return; }
        var form = await context.Request.ReadFormAsync(); var file = form.Files.GetFile("file"); if (file is null || file.Length == 0 || file.Length > 5 * 1024 * 1024) { await WriteBadRequestAsync(context, new { message = "CSV file is required and must be <= 5 MB." }); return; }
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, true); var rows = ParseCsv(await reader.ReadToEndAsync()).ToList(); if (rows.Count < 2) { await WriteBadRequestAsync(context, new { message = "CSV must contain a header and data." }); return; }
        var headers = rows[0].Select(x => x.Trim()).ToArray(); var index = headers.Select((name, i) => (name, i)).ToDictionary(x => x.name, x => x.i, StringComparer.OrdinalIgnoreCase); if (!index.ContainsKey("Name") || !index.ContainsKey("Role")) { await WriteBadRequestAsync(context, new { message = "CSV must contain Name and Role columns." }); return; }
        var created = 0; var updated = 0; var errors = new List<object>();
        for (var r = 1; r < rows.Count; r++) { var row = rows[r]; string Get(string n) => index.TryGetValue(n, out var i) && i < row.Count ? row[i].Trim() : ""; var name = Get("Name"); var role = Get("Role"); var department = Get("Department"); if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(role)) { errors.Add(new { row = r + 1, message = "Name and Role are required." }); continue; } var employee = await db.Employees.FirstOrDefaultAsync(e => e.Name == name && e.Role == role && e.Department == department); if (employee is null) { employee = new Employee { Name = name, Role = role, Department = department }; db.Employees.Add(employee); created++; } else updated++; employee.Authorization = NullIfEmpty(Get("Authorization")); employee.PositionPercent = ParseDecimal(Get("PositionPercent"), 100m); employee.MaxWeeklyHours = ParseDecimal(Get("MaxWeeklyHours"), 37.5m); employee.IsActive = !bool.TryParse(Get("IsActive"), out var active) || active; }
        await db.SaveChangesAsync(); await context.Response.WriteAsJsonAsync(new { created, updated, errors });
    }

    private static async Task ImportCompetencesAsync(HttpContext context, AppDbContext db)
    {
        if (!context.Request.HasFormContentType) { await WriteBadRequestAsync(context, new { message = "Send CSV as multipart/form-data with field 'file'." }); return; }
        var form = await context.Request.ReadFormAsync(); var file = form.Files.GetFile("file"); if (file is null || file.Length == 0 || file.Length > 5 * 1024 * 1024) { await WriteBadRequestAsync(context, new { message = "CSV file is required and must be <= 5 MB." }); return; }
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, true); var rows = ParseCsv(await reader.ReadToEndAsync()).ToList(); if (rows.Count < 2) { await WriteBadRequestAsync(context, new { message = "CSV must contain a header and data." }); return; }
        var headers = rows[0].Select(x => x.Trim()).ToArray(); var index = headers.Select((name, i) => (name, i)).ToDictionary(x => x.name, x => x.i, StringComparer.OrdinalIgnoreCase); if (!index.ContainsKey("EmployeeName") || !index.ContainsKey("CompetenceName")) { await WriteBadRequestAsync(context, new { message = "CSV must contain EmployeeName and CompetenceName columns." }); return; }
        var created = 0; var updated = 0; var errors = new List<object>();
        for (var r = 1; r < rows.Count; r++) { var row = rows[r]; string Get(string n) => index.TryGetValue(n, out var i) && i < row.Count ? row[i].Trim() : ""; var employeeName = Get("EmployeeName"); var competenceName = Get("CompetenceName"); var employee = await db.Employees.FirstOrDefaultAsync(e => e.Name == employeeName); var competence = await db.Competences.FirstOrDefaultAsync(c => c.Name == competenceName); if (employee is null || competence is null) { errors.Add(new { row = r + 1, message = "EmployeeName or CompetenceName not found." }); continue; } var levelText = Get("Level"); if (string.IsNullOrWhiteSpace(levelText)) levelText = nameof(CompetenceLevel.Basic); if (!Enum.TryParse<CompetenceLevel>(levelText, true, out var level)) { errors.Add(new { row = r + 1, message = $"Invalid competence level '{levelText}'. Valid values: Basic, Intermediate, Advanced." }); continue; } var validUntil = DateOnly.TryParse(Get("ValidUntil"), out var parsedDate) ? parsedDate : (DateOnly?)null; var item = await db.EmployeeCompetences.FindAsync(employee.Id, competence.Id); if (item is null) { db.EmployeeCompetences.Add(new EmployeeCompetence { EmployeeId = employee.Id, CompetenceId = competence.Id, Level = level, ValidUntil = validUntil }); created++; } else { item.Level = level; item.ValidUntil = validUntil; updated++; } }
        await db.SaveChangesAsync(); await context.Response.WriteAsJsonAsync(new { created, updated, errors });
    }

    private static async Task FileResponse(HttpContext context, string content, string contentType, string fileName)
    { context.Response.ContentType = contentType; context.Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\""; await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(content)); }

    private static string BuildShiftHtml(IEnumerable<Shift> shifts, CoverageService coverage)
    {
        var sb = new StringBuilder("<html><head><meta charset='utf-8'><style>table{border-collapse:collapse}th,td{border:1px solid #999;padding:6px} .green{background:#d9ead3}.yellow{background:#fff2cc}.red{background:#f4cccc}</style></head><body><h1>Vaktklar – vaktplan</h1><table><tr><th>Dato</th><th>Vakt</th><th>Avdeling</th><th>Start</th><th>Slutt</th><th>Minimum</th><th>Bemannet</th><th>Status</th><th>Kommentar</th></tr>");
        foreach (var shift in shifts) { var result = coverage.AnalyzeShift(shift); var status = result.OverallStatus ?? "UNKNOWN"; sb.Append("<tr class='").Append(status.ToLowerInvariant()).Append("'>").Append("<td>").Append(System.Net.WebUtility.HtmlEncode(shift.Date.ToString("yyyy-MM-dd"))).Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(shift.ShiftType)).Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(shift.Department)).Append("</td><td>").Append(shift.StartTime?.ToString("HH:mm") ?? "-").Append("</td><td>").Append(shift.StartTime.HasValue ? SchedulingRules.GetEnd(shift).ToString("HH:mm") : "-").Append("</td><td>").Append(shift.MinimumStaff).Append("</td><td>").Append(shift.Assignments.Count).Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(result.OverallStatus)).Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(string.Join("; ", result.Warnings ?? []))).Append("</td></tr>"); }
        sb.Append("</table></body></html>"); return sb.ToString();
    }

    private static string Csv(string? value) { var text = value ?? ""; return $"\"{text.Replace("\"", "\"\"")}\""; }
    private static string NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? "" : value;
    private static decimal ParseDecimal(string value, decimal fallback) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;

    private static List<List<string>> ParseCsv(string csv)
    {
        var result = new List<List<string>>(); var row = new List<string>(); var cell = new StringBuilder(); var quoted = false;
        for (var i = 0; i < csv.Length; i++) { var ch = csv[i]; if (ch == '"') { if (quoted && i + 1 < csv.Length && csv[i + 1] == '"') { cell.Append('"'); i++; } else quoted = !quoted; continue; } if (ch == ',' && !quoted) { row.Add(cell.ToString()); cell.Clear(); continue; } if ((ch == '\n' || ch == '\r') && !quoted) { if (ch == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++; row.Add(cell.ToString()); cell.Clear(); if (row.Any(x => !string.IsNullOrWhiteSpace(x))) result.Add(row); row = new List<string>(); continue; } cell.Append(ch); }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); if (row.Any(x => !string.IsNullOrWhiteSpace(x))) result.Add(row); }
        return result;
    }
}
