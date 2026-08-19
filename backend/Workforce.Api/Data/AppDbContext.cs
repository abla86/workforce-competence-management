using Microsoft.EntityFrameworkCore;
using Workforce.Api.Models;

namespace Workforce.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Competence> Competences => Set<Competence>();
    public DbSet<EmployeeCompetence> EmployeeCompetences => Set<EmployeeCompetence>();
    public DbSet<EmployeeAvailability> EmployeeAvailability => Set<EmployeeAvailability>();
    public DbSet<Absence> Absences => Set<Absence>();
    public DbSet<EmployeeTimeRecord> EmployeeTimeRecords => Set<EmployeeTimeRecord>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();
    public DbSet<ShiftRequirement> ShiftRequirements => Set<ShiftRequirement>();
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<ShiftTask> ShiftTasks => Set<ShiftTask>();
    public DbSet<ShiftTaskCoverage> ShiftTaskCoverages => Set<ShiftTaskCoverage>();
    public DbSet<ShiftRule> ShiftRules => Set<ShiftRule>();
    public DbSet<ShiftDispensation> ShiftDispensations => Set<ShiftDispensation>();
    public DbSet<CoverageAuditEntry> CoverageAuditEntries => Set<CoverageAuditEntry>();
    public DbSet<PrivacyRequest> PrivacyRequests => Set<PrivacyRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Employee>().Property(x => x.PositionPercent).HasPrecision(5, 2);
        modelBuilder.Entity<Employee>().HasIndex(x => x.IdentitySubject).IsUnique().HasFilter("[IdentitySubject] IS NOT NULL");
        modelBuilder.Entity<Shift>().Property(x => x.Hours).HasPrecision(4, 2);

        modelBuilder.Entity<EmployeeCompetence>().HasKey(x => new { x.EmployeeId, x.CompetenceId });
        modelBuilder.Entity<ShiftAssignment>().HasKey(x => new { x.ShiftId, x.EmployeeId });
        modelBuilder.Entity<ShiftRequirement>().HasKey(x => new { x.ShiftId, x.CompetenceId });
        modelBuilder.Entity<EmployeeAvailability>().HasKey(x => new { x.EmployeeId, x.Date });
        modelBuilder.Entity<Competence>().HasIndex(x => x.Name).IsUnique();

        modelBuilder.Entity<WorkTask>().HasOne(x => x.Competence).WithMany().HasForeignKey(x => x.CompetenceId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<ShiftTask>().HasOne(x => x.Shift).WithMany(x => x.ShiftTasks).HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ShiftTask>().HasOne(x => x.WorkTask).WithMany(x => x.ShiftTasks).HasForeignKey(x => x.WorkTaskId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ShiftTaskCoverage>().HasOne(x => x.ShiftTask).WithMany(x => x.ShiftTaskCoverages).HasForeignKey(x => x.ShiftTaskId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ShiftTaskCoverage>().HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Absence>().HasOne(x => x.Employee).WithMany(x => x.Absences).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<EmployeeTimeRecord>().HasIndex(x => new { x.EmployeeId, x.Year, x.Month }).IsUnique();
        modelBuilder.Entity<EmployeeTimeRecord>().HasOne(x => x.Employee).WithMany(x => x.TimeRecords).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ShiftDispensation>().HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ShiftDispensation>().HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CoverageAuditEntry>().HasOne(x => x.Shift).WithMany(x => x.CoverageAudits).HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CoverageAuditEntry>().HasIndex(x => new { x.ShiftId, x.EvaluatedAt });
        modelBuilder.Entity<CoverageAuditEntry>().Property(x => x.Status).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<CoverageAuditEntry>().Property(x => x.TriggeredBy).HasMaxLength(200);
        modelBuilder.Entity<CoverageAuditEntry>().Property(x => x.ClientIp).HasMaxLength(64);
        modelBuilder.Entity<CoverageAuditEntry>().Property(x => x.UserAgent).HasMaxLength(512);
        modelBuilder.Entity<CoverageAuditEntry>().Property(x => x.AnonymizedSummary).IsRequired();
        modelBuilder.Entity<CoverageAuditEntry>().Property(x => x.EncryptedDetails).IsRequired(false);
        modelBuilder.Entity<CoverageAuditEntry>().Property(x => x.DetailsJson).IsRequired();

        modelBuilder.Entity<PrivacyRequest>().HasIndex(x => new { x.IdentitySubject, x.Type, x.RequestedAt });
        modelBuilder.Entity<PrivacyRequest>().Property(x => x.IdentitySubject).HasMaxLength(200).IsRequired();
        modelBuilder.Entity<PrivacyRequest>().Property(x => x.Type).HasMaxLength(40).IsRequired();
        modelBuilder.Entity<PrivacyRequest>().Property(x => x.Status).HasMaxLength(30).IsRequired();
    }
}
