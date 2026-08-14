using Microsoft.EntityFrameworkCore;
using Workforce.Api.Data;
using Workforce.Api.DTOs;
using Workforce.Api.Models;
using Workforce.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<CoverageService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseCors();
app.MapOpenApi();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await SeedData.InitializeAsync(db);
}

app.MapGet("/", () => Results.Ok(new
{
    name = "Workforce & Competence Management API",
    version = "1.0.0",
    status = "running"
}));

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapGet("/api/employees", async (AppDbContext db, string? search, string? role) =>
{
    var query = db.Employees
        .Include(x => x.Competences)
        .ThenInclude(x => x.Competence)
        .AsQueryable();

    if (!string.IsNullOrWhiteSpace(search))
        query = query.Where(x => x.Name.Contains(search));

    if (!string.IsNullOrWhiteSpace(role))
        query = query.Where(x => x.Role == role);

    var employees = await query.OrderBy(x => x.Name).ToListAsync();

    return Results.Ok(employees.Select(e => new
    {
        e.Id,
        e.Name,
        e.Role,
        e.PositionPercent,
        e.IsActive,
        Competences = e.Competences.Select(c => new
        {
            c.CompetenceId,
            c.Competence.Name,
            c.Competence.Category,
            c.Level,
            c.ValidUntil,
            Status = c.ValidUntil.HasValue && c.ValidUntil.Value < DateOnly.FromDateTime(DateTime.Today)
                ? "EXPIRED"
                : c.ValidUntil.HasValue && c.ValidUntil.Value <= DateOnly.FromDateTime(DateTime.Today.AddDays(45))
                    ? "REVIEW_DUE"
                    : "ACTIVE"
        })
    }));
});

app.MapPost("/api/employees", async (CreateEmployeeRequest request, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Role))
        return Results.BadRequest(new { message = "Name and role are required." });

    if (request.PositionPercent <= 0 || request.PositionPercent > 100)
        return Results.BadRequest(new { message = "Position percent must be between 1 and 100." });

    var employee = new Employee
    {
        Name = request.Name.Trim(),
        Role = request.Role.Trim(),
        PositionPercent = request.PositionPercent
    };

    db.Employees.Add(employee);
    await db.SaveChangesAsync();
    return Results.Created($"/api/employees/{employee.Id}", employee);
});

app.MapPut("/api/employees/{id:int}", async (int id, UpdateEmployeeRequest request, AppDbContext db) =>
{
    var employee = await db.Employees.FindAsync(id);
    if (employee is null) return Results.NotFound();

    employee.Name = request.Name.Trim();
    employee.Role = request.Role.Trim();
    employee.PositionPercent = request.PositionPercent;
    employee.IsActive = request.IsActive;

    await db.SaveChangesAsync();
    return Results.Ok(employee);
});

app.MapDelete("/api/employees/{id:int}", async (int id, AppDbContext db) =>
{
    var employee = await db.Employees.FindAsync(id);
    if (employee is null) return Results.NotFound();

    db.Employees.Remove(employee);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapPost("/api/employees/{id:int}/competences", async (int id, AddCompetenceRequest request, AppDbContext db) =>
{
    var employeeExists = await db.Employees.AnyAsync(x => x.Id == id);
    var competenceExists = await db.Competences.AnyAsync(x => x.Id == request.CompetenceId);

    if (!employeeExists || !competenceExists) return Results.NotFound();

    var existing = await db.EmployeeCompetences.FindAsync(id, request.CompetenceId);
    if (existing is null)
    {
        db.EmployeeCompetences.Add(new EmployeeCompetence
        {
            EmployeeId = id,
            CompetenceId = request.CompetenceId,
            Level = request.Level,
            ValidUntil = request.ValidUntil
        });
    }
    else
    {
        existing.Level = request.Level;
        existing.ValidUntil = request.ValidUntil;
    }

    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapGet("/api/competences", async (AppDbContext db) =>
    Results.Ok(await db.Competences.OrderBy(x => x.Category).ThenBy(x => x.Name).ToListAsync()));

app.MapPost("/api/competences", async (CreateCompetenceRequest request, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest();
    var item = new Competence { Name = request.Name.Trim(), Category = request.Category.Trim() };
    db.Competences.Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/api/competences/{item.Id}", item);
});

app.MapGet("/api/shifts", async (AppDbContext db, CoverageService coverage) =>
{
    var shifts = await db.Shifts
        .Include(x => x.Assignments)
            .ThenInclude(x => x.Employee)
                .ThenInclude(x => x.Competences)
        .Include(x => x.Requirements)
            .ThenInclude(x => x.Competence)
        .OrderBy(x => x.Date)
        .ThenBy(x => x.ShiftType)
        .ToListAsync();

    return Results.Ok(shifts.Select(coverage.AnalyzeShift));
});

app.MapPost("/api/shifts", async (CreateShiftRequest request, AppDbContext db) =>
{
    var shift = new Shift
    {
        Date = request.Date,
        ShiftType = request.ShiftType,
        Hours = request.Hours,
        MinimumStaff = request.MinimumStaff
    };

    db.Shifts.Add(shift);
    await db.SaveChangesAsync();
    return Results.Created($"/api/shifts/{shift.Id}", shift);
});

app.MapPost("/api/shifts/{id:int}/assignments", async (int id, AssignEmployeeRequest request, AppDbContext db) =>
{
    if (!await db.Shifts.AnyAsync(x => x.Id == id) || !await db.Employees.AnyAsync(x => x.Id == request.EmployeeId))
        return Results.NotFound();

    if (!await db.ShiftAssignments.AnyAsync(x => x.ShiftId == id && x.EmployeeId == request.EmployeeId))
    {
        db.ShiftAssignments.Add(new ShiftAssignment { ShiftId = id, EmployeeId = request.EmployeeId });
        await db.SaveChangesAsync();
    }

    return Results.NoContent();
});

app.MapPost("/api/shifts/{id:int}/requirements", async (int id, AddRequirementRequest request, AppDbContext db) =>
{
    if (!await db.Shifts.AnyAsync(x => x.Id == id) || !await db.Competences.AnyAsync(x => x.Id == request.CompetenceId))
        return Results.NotFound();

    var existing = await db.ShiftRequirements.FindAsync(id, request.CompetenceId);
    if (existing is null)
    {
        db.ShiftRequirements.Add(new ShiftRequirement
        {
            ShiftId = id,
            CompetenceId = request.CompetenceId,
            MinimumCount = request.MinimumCount,
            MinimumLevel = request.MinimumLevel
        });
    }
    else
    {
        existing.MinimumCount = request.MinimumCount;
        existing.MinimumLevel = request.MinimumLevel;
    }

    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapGet("/api/dashboard", async (AppDbContext db, CoverageService coverage) =>
{
    var employees = await db.Employees
        .Include(x => x.Competences)
        .ToListAsync();

    var shifts = await db.Shifts
        .Include(x => x.Assignments)
            .ThenInclude(x => x.Employee)
                .ThenInclude(x => x.Competences)
        .Include(x => x.Requirements)
            .ThenInclude(x => x.Competence)
        .ToListAsync();

    var analyses = shifts
        .Select(coverage.AnalyzeShift)
        .ToList();

    var actionRequired =
        analyses.Count(x => !x.OverallCovered);

    var coverageValues =
        analyses.Select(x => x.CompetenceCoverage).ToList();

    var activeCompetences = await db.Competences.CountAsync();

    return Results.Ok(new
    {
        TotalEmployees = employees.Count(x => x.IsActive),
        ActiveCompetences = activeCompetences,
        ActionRequiredShifts = actionRequired,
        CompetenceCoverage = coverageValues.Count == 0 ? 100 : (int)Math.Round(coverageValues.Average()),
        UpcomingShifts = analyses
    });
});

app.Run();

public partial class Program { }


