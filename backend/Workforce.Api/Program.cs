using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;
using Workforce.Api.Models;
using Workforce.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDataProtection();
builder.Services.AddScoped<CoverageService>();
builder.Services.AddScoped<AutoStaffingService>();
builder.Services.AddScoped<ShiftViabilityService>();
builder.Services.AddScoped<AuditProtectionService>();
builder.Services.AddScoped<ShiftAccessService>();
builder.Services.AddScoped<EmployeeAccessService>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddAuthentication(DevelopmentAuthenticationHandler.Scheme)
        .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(DevelopmentAuthenticationHandler.Scheme, _ => { });
}
else
{
    var authority = builder.Configuration["Authentication:Authority"];
    var audience = builder.Configuration["Authentication:Audience"];
    if (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(audience))
        throw new InvalidOperationException("Authentication:Authority and Authentication:Audience must be configured in non-development environments.");
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = authority;
            options.Audience = audience;
            options.RequireHttpsMetadata = true;
        });
}

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("CoverageRead", policy => policy.RequireAuthenticatedUser().RequireRole("Employee", "Manager", "HR", "Admin"))
    .AddPolicy("CoverageManage", policy => policy.RequireAuthenticatedUser().RequireRole("Manager", "HR", "Admin"));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("coverage", o =>
    {
        o.PermitLimit = 60;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
    });
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length > 0) policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapOpenApi();
app.MapWorkforceExpansionEndpoints();

app.MapGet("/", () => Results.Ok(new { name = "Workforce & Competence Management API", version = "2.1.0", status = "running" })).AllowAnonymous();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow })).AllowAnonymous();

app.MapGet("/api/shifts/{shiftId:int}/coverage", async (int shiftId, CoverageService coverage, ShiftAccessService access, HttpContext http, ILogger<CoverageService> logger) =>
{
    if (!await access.CanAccessShiftAsync(http.User, shiftId)) return Results.Forbid();
    try
    {
        var result = await coverage.EvaluateAsync(shiftId, http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", http);
        logger.LogInformation("Coverage evaluated for shift {ShiftId}: {Status}", shiftId, result.Status);
        return Results.Ok(result);
    }
    catch (ArgumentException ex) { return Results.NotFound(new { message = ex.Message }); }
    catch (Exception ex) { logger.LogError(ex, "Error evaluating coverage for shift {ShiftId}", shiftId); return Results.Problem("Internal server error"); }
})
.RequireAuthorization("CoverageRead")
.RequireRateLimiting("coverage")
.WithTags("coverage");

app.MapPost("/api/shifts/{shiftId:int}/coverage/scenario", async (int shiftId, RemoveEmployeeRequest request, AppDbContext db, CoverageService coverage, ShiftAccessService access, HttpContext http) =>
{
    if (!await access.CanAccessShiftAsync(http.User, shiftId)) return Results.Forbid();
    var shift = await db.Shifts
        .Include(s => s.ShiftTasks).ThenInclude(st => st.WorkTask)
        .Include(s => s.ShiftTasks).ThenInclude(st => st.ShiftTaskCoverages).ThenInclude(c => c.Employee).ThenInclude(e => e.Competences)
        .Include(s => s.Assignments).ThenInclude(a => a.Employee)
        .FirstOrDefaultAsync(s => s.Id == shiftId);
    if (shift is null) return Results.NotFound(new { message = $"Shift {shiftId} not found" });

    var removed = request.EmployeeIds.ToHashSet();
    var originalAssignments = shift.Assignments.ToList();
    var originalCoverages = shift.ShiftTasks.ToDictionary(t => t.Id, t => t.ShiftTaskCoverages.ToList());
    shift.Assignments = originalAssignments.Where(a => !removed.Contains(a.EmployeeId)).ToList();
    foreach (var task in shift.ShiftTasks) task.ShiftTaskCoverages = originalCoverages[task.Id].Where(c => !removed.Contains(c.EmployeeId)).ToList();
    var result = coverage.Evaluate(shift);
    shift.Assignments = originalAssignments;
    foreach (var task in shift.ShiftTasks) task.ShiftTaskCoverages = originalCoverages[task.Id];

    var candidates = await db.Employees.Where(e => e.IsActive && !removed.Contains(e.Id)).Select(e => new { e.Id, e.Name, e.Role }).ToListAsync();
    return Results.Ok(new { coverageWithoutEmployees = result, suggestedReplacements = candidates });
})
.RequireAuthorization("CoverageManage")
.RequireRateLimiting("coverage")
.WithTags("coverage");

app.MapPost("/api/shifts/{shiftId:int}/auto-staffing", async (int shiftId, AutoStaffingRequest request, AutoStaffingService staffing, ShiftAccessService access, HttpContext http) =>
{
    if (!await access.CanAccessShiftAsync(http.User, shiftId)) return Results.Forbid();
    request.ShiftId = shiftId;
    var proposals = await staffing.GenerateAsync(request, 10);
    return Results.Ok(new { shiftId, proposals });
})
.RequireAuthorization("CoverageManage")
.RequireRateLimiting("coverage")
.WithTags("staffing");

app.MapGet("/api/employees/{employeeId:int}/viability", async (int employeeId, DateTime start, DateTime end, ShiftViabilityService viability, EmployeeAccessService access, HttpContext http) =>
{
    if (!await access.CanAccessEmployeeAsync(http.User, employeeId)) return Results.Forbid();
    if (end <= start) return Results.BadRequest(new { message = "End must be after start." });
    var result = await viability.CheckAsync(employeeId, start, end);
    return Results.Ok(result);
})
.RequireAuthorization("CoverageManage")
.WithTags("staffing");

app.MapPost("/api/employees/{employeeId:int}/absences", async (int employeeId, AbsenceRequest request, AppDbContext db, EmployeeAccessService access, HttpContext http) =>
{
    if (!await access.CanAccessEmployeeAsync(http.User, employeeId)) return Results.Forbid();
    if (request.EndDate <= request.StartDate) return Results.BadRequest(new { message = "EndDate must be after StartDate." });
    var employee = await db.Employees.FindAsync(employeeId);
    if (employee is null) return Results.NotFound();
    var absence = new Absence { EmployeeId = employeeId, Type = request.Type, StartDate = request.StartDate, EndDate = request.EndDate, Description = request.Description, IsApproved = false };
    db.Absences.Add(absence);
    await db.SaveChangesAsync();
    return Results.Created($"/api/employees/{employeeId}/absences/{absence.Id}", absence);
})
.RequireAuthorization("CoverageManage")
.WithTags("absence");

app.MapGet("/api/employees/{employeeId:int}/absences", async (int employeeId, AppDbContext db, EmployeeAccessService access, HttpContext http) =>
{
    if (!await access.CanAccessEmployeeAsync(http.User, employeeId)) return Results.Forbid();
    return Results.Ok(await db.Absences.Where(a => a.EmployeeId == employeeId).OrderBy(a => a.StartDate).ToListAsync());
})
.RequireAuthorization("CoverageRead")
.WithTags("absence");

app.MapPost("/api/shift-rules", async (ShiftRule rule, AppDbContext db) =>
{
    if (rule.MinimumRestHours < 0 || rule.MaxDispensationHours < 0 || rule.MaxDispensationsPerMonth < 0) return Results.BadRequest();
    db.ShiftRules.Add(rule);
    await db.SaveChangesAsync();
    return Results.Created($"/api/shift-rules/{rule.Id}", rule);
})
.RequireAuthorization("CoverageManage")
.WithTags("rules");

app.MapGet("/api/shift-rules", async (AppDbContext db) => Results.Ok(await db.ShiftRules.Where(r => r.IsActive).OrderBy(r => r.RuleType).ToListAsync()))
.RequireAuthorization("CoverageRead")
.WithTags("rules");

app.MapPost("/api/shifts/{shiftId:int}/dispensations", async (int shiftId, CreateDispensationRequest request, AppDbContext db, ShiftAccessService access, HttpContext http) =>
{
    if (!await access.CanAccessShiftAsync(http.User, shiftId)) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest(new { message = "Reason is required." });
    var subject = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(subject)) return Results.Unauthorized();
    if (!await db.Shifts.AnyAsync(s => s.Id == shiftId) || !await db.Employees.AnyAsync(e => e.Id == request.EmployeeId)) return Results.NotFound();
    var item = new ShiftDispensation { ShiftId = shiftId, EmployeeId = request.EmployeeId, BreachedRule = request.BreachedRule, HoursGranted = request.HoursGranted, Reason = request.Reason, GrantedBySubject = subject, ExpiresAt = request.ExpiresAt, Status = DispensationStatus.Approved };
    db.ShiftDispensations.Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/api/shifts/{shiftId}/dispensations/{item.Id}", item);
})
.RequireAuthorization("CoverageManage")
.WithTags("rules");

app.MapGet("/api/shifts/{shiftId:int}/coverage/history", async (int shiftId, AppDbContext db, ShiftAccessService access, HttpContext http) =>
{
    if (!await access.CanAccessShiftAsync(http.User, shiftId)) return Results.Forbid();
    var audits = await db.CoverageAuditEntries.Where(a => a.ShiftId == shiftId).OrderByDescending(a => a.EvaluatedAt).Take(20)
        .Select(a => new { a.Id, a.ShiftId, a.EvaluatedAt, a.Status, a.AnonymizedSummary, a.TriggeredBy }).ToListAsync();
    return Results.Ok(audits);
})
.RequireAuthorization("CoverageRead")
.RequireRateLimiting("coverage")
.WithTags("coverage");

app.MapGet("/api/me/data-export", async (HttpContext http, AppDbContext db) =>
{
    var subject = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(subject)) return Results.Unauthorized();
    var employee = await db.Employees.Include(e => e.Competences).ThenInclude(ec => ec.Competence).Include(e => e.Availability).Include(e => e.ShiftAssignments).ThenInclude(a => a.Shift).SingleOrDefaultAsync(e => e.IdentitySubject == subject);
    if (employee is null) return Results.NotFound();
    var export = new
    {
        employee = new { employee.Id, employee.Name, employee.Role, employee.PositionPercent, employee.IsActive },
        competences = employee.Competences.Select(c => new { c.CompetenceId, Name = c.Competence.Name, c.Level, c.ValidUntil }),
        availability = employee.Availability.Select(a => new { a.Date, a.IsAvailable, a.Reason }),
        shifts = employee.ShiftAssignments.Select(a => new { a.ShiftId, a.Shift.Date, a.Shift.ShiftType, a.Shift.Hours })
    };
    return Results.Ok(export);
})
.RequireAuthorization("CoverageRead")
.WithTags("privacy");

app.MapPost("/api/me/privacy-requests", async (PrivacyRequestType request, HttpContext http, AppDbContext db) =>
{
    var subject = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(subject)) return Results.Unauthorized();
    var type = request.Type.Trim().ToLowerInvariant();
    var allowed = new[] { "access", "rectification", "erasure", "portability" };
    if (!allowed.Contains(type)) return Results.BadRequest(new { message = "Unsupported privacy request type." });
    var item = new PrivacyRequest { IdentitySubject = subject, Type = type };
    db.PrivacyRequests.Add(item);
    await db.SaveChangesAsync();
    return Results.Accepted($"/api/me/privacy-requests/{item.Id}", new { item.Id, item.Type, item.Status, item.RequestedAt });
})
.RequireAuthorization("CoverageRead")
.WithTags("privacy");

app.MapGet("/api/me/privacy-requests", async (HttpContext http, AppDbContext db) =>
{
    var subject = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(subject)) return Results.Unauthorized();
    return Results.Ok(await db.PrivacyRequests.Where(x => x.IdentitySubject == subject).OrderByDescending(x => x.RequestedAt)
        .Select(x => new { x.Id, x.Type, x.RequestedAt, x.Status, x.CompletedAt }).ToListAsync());
})
.RequireAuthorization("CoverageRead")
.WithTags("privacy");

app.Run();

public sealed record RemoveEmployeeRequest(List<int> EmployeeIds);
public sealed record PrivacyRequestType(string Type);
public sealed record AbsenceRequest(AbsenceType Type, DateTime StartDate, DateTime EndDate, string? Description);
public sealed record CreateDispensationRequest(int EmployeeId, RuleType BreachedRule, int HoursGranted, string Reason, DateTime? ExpiresAt);