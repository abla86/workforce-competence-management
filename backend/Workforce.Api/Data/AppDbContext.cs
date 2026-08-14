using Microsoft.EntityFrameworkCore;
using Workforce.Api.Models;

namespace Workforce.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Competence> Competences => Set<Competence>();
    public DbSet<EmployeeCompetence> EmployeeCompetences => Set<EmployeeCompetence>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();
    public DbSet<ShiftRequirement> ShiftRequirements => Set<ShiftRequirement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>().Property(x => x.PositionPercent).HasPrecision(5, 2);
        modelBuilder.Entity<Shift>().Property(x => x.Hours).HasPrecision(4, 2);

        modelBuilder.Entity<EmployeeCompetence>().HasKey(x => new { x.EmployeeId, x.CompetenceId });
        modelBuilder.Entity<ShiftAssignment>().HasKey(x => new { x.ShiftId, x.EmployeeId });
        modelBuilder.Entity<ShiftRequirement>().HasKey(x => new { x.ShiftId, x.CompetenceId });

        modelBuilder.Entity<Competence>().HasIndex(x => x.Name).IsUnique();
    }
}
