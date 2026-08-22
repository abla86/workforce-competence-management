using Microsoft.EntityFrameworkCore;
using Workforce.Api.Models;

namespace Workforce.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Competence> Competences => Set<Competence>();
    public DbSet<EmployeeCompetence> EmployeeCompetences => Set<EmployeeCompetence>();
    public DbSet<Absence> Absences => Set<Absence>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();
    public DbSet<ShiftRequirement> ShiftRequirements => Set<ShiftRequirement>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Absence>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("int");
            b.Property(x => x.EmployeeId).HasColumnType("int");
            b.Property(x => x.From).HasColumnType("date");
            b.Property(x => x.To).HasColumnType("date");
            b.Property(x => x.Type).HasColumnType("int");
            b.Property(x => x.Note).HasColumnType("nvarchar(max)");
            b.Property(x => x.Approved).HasColumnType("bit");
            b.HasIndex(x => new { x.EmployeeId, x.From, x.To });
            b.HasOne(x => x.Employee).WithMany(x => x.Absences).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        });

        modelBuilder.Entity<AuditEvent>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("bigint");
            b.Property(x => x.OccurredAtUtc).HasColumnType("datetime2");
            b.Property(x => x.Action).IsRequired().HasColumnType("nvarchar(max)");
            b.Property(x => x.EntityType).IsRequired().HasColumnType("nvarchar(max)");
            b.Property(x => x.EntityId).IsRequired().HasColumnType("nvarchar(max)");
            b.Property(x => x.Actor).HasColumnType("nvarchar(max)");
            b.Property(x => x.Reason).HasColumnType("nvarchar(max)");
            b.Property(x => x.DetailsJson).HasColumnType("nvarchar(max)");
            b.HasIndex(x => x.OccurredAtUtc);
        });

        modelBuilder.Entity<Competence>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("int");
            b.Property(x => x.Name).IsRequired().HasMaxLength(255).HasColumnType("nvarchar(255)");
            b.Property(x => x.Category).IsRequired().HasColumnType("nvarchar(max)");
            b.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Employee>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("int");
            b.Property(x => x.Name).IsRequired().HasColumnType("nvarchar(max)");
            b.Property(x => x.Role).IsRequired().HasColumnType("nvarchar(max)");
            b.Property(x => x.Department).IsRequired().HasColumnType("nvarchar(max)");
            b.Property(x => x.Authorization).HasColumnType("nvarchar(max)");
            b.Property(x => x.PositionPercent).HasPrecision(5, 2).HasColumnType("decimal(5,2)");
            b.Property(x => x.MaxWeeklyHours).HasPrecision(5, 2).HasColumnType("decimal(5,2)");
            b.Property(x => x.IsActive).HasColumnType("bit");
        });

        modelBuilder.Entity<EmployeeCompetence>(b =>
        {
            b.HasKey(x => new { x.EmployeeId, x.CompetenceId });
            b.Property(x => x.EmployeeId).HasColumnType("int");
            b.Property(x => x.CompetenceId).HasColumnType("int");
            b.Property(x => x.Level).HasColumnType("int");
            b.Property(x => x.ValidUntil).HasColumnType("date");
            b.HasIndex(x => x.CompetenceId);
            b.HasOne(x => x.Competence).WithMany(x => x.Employees).HasForeignKey(x => x.CompetenceId).OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.HasOne(x => x.Employee).WithMany(x => x.Competences).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        });

        modelBuilder.Entity<Shift>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("int");
            b.Property(x => x.Date).HasColumnType("date");
            b.Property(x => x.ShiftType).IsRequired().HasColumnType("nvarchar(max)");
            b.Property(x => x.Department).IsRequired().HasColumnType("nvarchar(max)");
            b.Property(x => x.StartTime).HasColumnType("time");
            b.Property(x => x.Hours).HasPrecision(4, 2).HasColumnType("decimal(4,2)");
            b.Property(x => x.MinimumStaff).HasColumnType("int");
            b.Property(x => x.IsPublished).HasColumnType("bit");
            b.Property(x => x.IsCritical).HasColumnType("bit");
        });

        modelBuilder.Entity<ShiftAssignment>(b =>
        {
            b.HasKey(x => new { x.ShiftId, x.EmployeeId });
            b.Property(x => x.ShiftId).HasColumnType("int");
            b.Property(x => x.EmployeeId).HasColumnType("int");
            b.HasIndex(x => x.EmployeeId);
            b.HasOne(x => x.Employee).WithMany(x => x.ShiftAssignments).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.HasOne(x => x.Shift).WithMany(x => x.Assignments).HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        });

        modelBuilder.Entity<ShiftRequirement>(b =>
        {
            b.HasKey(x => new { x.ShiftId, x.CompetenceId });
            b.Property(x => x.ShiftId).HasColumnType("int");
            b.Property(x => x.CompetenceId).HasColumnType("int");
            b.Property(x => x.MinimumCount).HasColumnType("int");
            b.Property(x => x.MinimumLevel).HasColumnType("int");
            b.Property(x => x.RequiredRole).HasColumnType("nvarchar(max)");
            b.Property(x => x.IsCritical).HasColumnType("bit");
            b.HasIndex(x => x.CompetenceId);
            b.HasOne(x => x.Competence).WithMany(x => x.ShiftRequirements).HasForeignKey(x => x.CompetenceId).OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.HasOne(x => x.Shift).WithMany(x => x.Requirements).HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        });

        modelBuilder.Entity<UserAccount>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("int");
            b.Property(x => x.Username).IsRequired().HasMaxLength(100).HasColumnType("nvarchar(100)");
            b.Property(x => x.PasswordHash).IsRequired().HasColumnType("nvarchar(max)");
            b.Property(x => x.Role).IsRequired().HasMaxLength(40).HasColumnType("nvarchar(40)");
            b.Property(x => x.IsActive).HasColumnType("bit");
            b.Property(x => x.FailedLoginAttempts).HasColumnType("int");
            b.Property(x => x.LockedUntilUtc).HasColumnType("datetime2");
            b.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
            b.Property(x => x.LastLoginAtUtc).HasColumnType("datetime2");
            b.Property(x => x.EmployeeId).HasColumnType("nvarchar(max)");
        });
    }
}
