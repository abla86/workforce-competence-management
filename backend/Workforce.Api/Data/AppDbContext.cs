using Microsoft.EntityFrameworkCore;
using Workforce.Api.Models;

namespace Workforce.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Competence> Competences => Set<Competence>();
    public DbSet<EmployeeCompetence> EmployeeCompetences => Set<EmployeeCompetence>();
    public DbSet<EmployeeAvailability> EmployeeAvailability => Set<EmployeeAvailability>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();
    public DbSet<ShiftRequirement> ShiftRequirements => Set<ShiftRequirement>();
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<ShiftTask> ShiftTasks => Set<ShiftTask>();
    public DbSet<ShiftTaskCoverage> ShiftTaskCoverages => Set<ShiftTaskCoverage>();
    public DbSet<CoverageAuditEntry> CoverageAuditEntries => Set<CoverageAuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Employee>().Property(x => x.PositionPercent).HasPrecision(5, 2);
        modelBuilder.Entity<Shift>().Property(x => x.Hours).HasPrecision(4, 2);
        modelBuilder.Entity<EmployeeCompetence>().HasKey(x => new { x.EmployeeId, x.CompetenceId });
        modelBuilder.Entity<ShiftAssignment>().HasKey(x => new { x.ShiftId, x.EmployeeId });
        modelBuilder.Entity<ShiftRequirement>().HasKey(x => new { x.ShiftId, x.CompetenceId });
        modelBuilder.Entity<EmployeeAvailability>().HasKey(x => new { x.EmployeeId, x.Date });
        modelBuilder.Entity<Competence>().HasIndex(x => x.Name).IsUnique();

        modelBuilder.Entity<WorkTask>()
            .HasOne(x => x.Competence).WithMany().HasForeignKey(x => x.CompetenceId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<ShiftTask>()
            .HasOne(x => x.Shift).WithMany(x => x.ShiftTasks).HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ShiftTask>()
            .HasOne(x => x.WorkTask).WithMany(x => x.ShiftTasks).HasForeignKey(x => x.WorkTaskId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ShiftTaskCoverage>()
            .HasOne(x => x.ShiftTask).WithMany(x => x.ShiftTaskCoverages).HasForeignKey(x => x.ShiftTaskId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ShiftTaskCoverage>()
            .HasOne(x => x.Employee).WithMany(x => x.ShiftTaskCoverages).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CoverageAuditEntry>()
            .HasOne(x => x.Shift).WithMany(x => x.CoverageAudits).HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CoverageAuditEntry>().HasIndex(x => new { x.ShiftId, x.EvaluatedAt });
        modelBuilder.Entity<CoverageAuditEntry>().Property(x => x.Status).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<CoverageAuditEntry>().Property(x => x.TriggeredBy).HasMaxLength(200);
        modelBuilder.Entity<CoverageAuditEntry>().Property(x => x.DetailsJson).IsRequired();
    }
}
