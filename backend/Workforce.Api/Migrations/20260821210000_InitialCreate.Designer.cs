using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Workforce.Api.Data;

#nullable disable

namespace Workforce.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260821210000_InitialCreate")]
partial class InitialCreate
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasAnnotation("ProductVersion", "10.0.11").HasAnnotation("Relational:MaxIdentifierLength", 128);
        SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

        modelBuilder.Entity("Workforce.Api.Models.Absence", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("int"); SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));
            b.Property<bool>("Approved").HasColumnType("bit"); b.Property<int>("EmployeeId").HasColumnType("int"); b.Property<DateOnly>("From").HasColumnType("date"); b.Property<string>("Note").HasColumnType("nvarchar(max)"); b.Property<DateOnly>("To").HasColumnType("date"); b.Property<int>("Type").HasColumnType("int"); b.HasKey("Id"); b.HasIndex("EmployeeId", "From", "To"); b.ToTable("Absences");
        });
        modelBuilder.Entity("Workforce.Api.Models.AuditEvent", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint"); SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<long>("Id"));
            b.Property<string>("Action").IsRequired().HasColumnType("nvarchar(max)"); b.Property<string>("Actor").HasColumnType("nvarchar(max)"); b.Property<string>("DetailsJson").HasColumnType("nvarchar(max)"); b.Property<string>("EntityId").IsRequired().HasColumnType("nvarchar(max)"); b.Property<string>("EntityType").IsRequired().HasColumnType("nvarchar(max)"); b.Property<DateTime>("OccurredAtUtc").HasColumnType("datetime2"); b.Property<string>("Reason").HasColumnType("nvarchar(max)"); b.HasKey("Id"); b.HasIndex("OccurredAtUtc"); b.ToTable("AuditEvents");
        });
        modelBuilder.Entity("Workforce.Api.Models.Competence", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("int"); SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id")); b.Property<string>("Category").IsRequired().HasColumnType("nvarchar(max)"); b.Property<string>("Name").IsRequired().HasColumnType("nvarchar(max)"); b.HasKey("Id"); b.HasIndex("Name").IsUnique(); b.ToTable("Competences");
        });
        modelBuilder.Entity("Workforce.Api.Models.Employee", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("int"); SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id")); b.Property<string>("Authorization").HasColumnType("nvarchar(max)"); b.Property<string>("Department").IsRequired().HasColumnType("nvarchar(max)"); b.Property<bool>("IsActive").HasColumnType("bit"); b.Property<decimal>("MaxWeeklyHours").HasPrecision(5, 2).HasColumnType("decimal(5,2)"); b.Property<string>("Name").IsRequired().HasColumnType("nvarchar(max)"); b.Property<decimal>("PositionPercent").HasPrecision(5, 2).HasColumnType("decimal(5,2)"); b.Property<string>("Role").IsRequired().HasColumnType("nvarchar(max)"); b.HasKey("Id"); b.ToTable("Employees");
        });
        modelBuilder.Entity("Workforce.Api.Models.EmployeeCompetence", b =>
        {
            b.Property<int>("EmployeeId").HasColumnType("int"); b.Property<int>("CompetenceId").HasColumnType("int"); b.Property<int>("Level").HasColumnType("int"); b.Property<DateOnly?>("ValidUntil").HasColumnType("date"); b.HasKey("EmployeeId", "CompetenceId"); b.HasIndex("CompetenceId"); b.ToTable("EmployeeCompetences");
        });
        modelBuilder.Entity("Workforce.Api.Models.Shift", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("int"); SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id")); b.Property<DateOnly>("Date").HasColumnType("date"); b.Property<string>("Department").IsRequired().HasColumnType("nvarchar(max)"); b.Property<decimal>("Hours").HasPrecision(4, 2).HasColumnType("decimal(4,2)"); b.Property<bool>("IsCritical").HasColumnType("bit"); b.Property<bool>("IsPublished").HasColumnType("bit"); b.Property<int>("MinimumStaff").HasColumnType("int"); b.Property<TimeOnly?>("StartTime").HasColumnType("time"); b.Property<string>("ShiftType").IsRequired().HasColumnType("nvarchar(max)"); b.HasKey("Id"); b.ToTable("Shifts");
        });
        modelBuilder.Entity("Workforce.Api.Models.ShiftAssignment", b =>
        {
            b.Property<int>("ShiftId").HasColumnType("int"); b.Property<int>("EmployeeId").HasColumnType("int"); b.HasKey("ShiftId", "EmployeeId"); b.HasIndex("EmployeeId"); b.ToTable("ShiftAssignments");
        });
        modelBuilder.Entity("Workforce.Api.Models.ShiftRequirement", b =>
        {
            b.Property<int>("ShiftId").HasColumnType("int"); b.Property<int>("CompetenceId").HasColumnType("int"); b.Property<bool>("IsCritical").HasColumnType("bit"); b.Property<int>("MinimumCount").HasColumnType("int"); b.Property<int>("MinimumLevel").HasColumnType("int"); b.Property<string>("RequiredRole").HasColumnType("nvarchar(max)"); b.HasKey("ShiftId", "CompetenceId"); b.HasIndex("CompetenceId"); b.ToTable("ShiftRequirements");
        });
        modelBuilder.Entity("Workforce.Api.Models.UserAccount", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("int"); SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id")); b.Property<DateTime>("CreatedAtUtc").HasColumnType("datetime2"); b.Property<int>("FailedLoginAttempts").HasColumnType("int"); b.Property<string>("EmployeeId").HasColumnType("nvarchar(max)"); b.Property<bool>("IsActive").HasColumnType("bit"); b.Property<DateTime?>("LastLoginAtUtc").HasColumnType("datetime2"); b.Property<DateTime?>("LockedUntilUtc").HasColumnType("datetime2"); b.Property<string>("PasswordHash").IsRequired().HasColumnType("nvarchar(max)"); b.Property<string>("Role").IsRequired().HasMaxLength(40).HasColumnType("nvarchar(40)"); b.Property<string>("Username").IsRequired().HasMaxLength(100).HasColumnType("nvarchar(100)"); b.HasKey("Id"); b.HasIndex("Username").IsUnique(); b.ToTable("UserAccounts");
        });

        modelBuilder.Entity("Workforce.Api.Models.Absence", b => { b.HasOne("Workforce.Api.Models.Employee", "Employee").WithMany("Absences").HasForeignKey("EmployeeId").OnDelete(DeleteBehavior.Cascade).IsRequired(); b.Navigation("Employee"); });
        modelBuilder.Entity("Workforce.Api.Models.EmployeeCompetence", b => { b.HasOne("Workforce.Api.Models.Competence", "Competence").WithMany("Employees").HasForeignKey("CompetenceId").OnDelete(DeleteBehavior.Cascade).IsRequired(); b.HasOne("Workforce.Api.Models.Employee", "Employee").WithMany("Competences").HasForeignKey("EmployeeId").OnDelete(DeleteBehavior.Cascade).IsRequired(); b.Navigation("Competence"); b.Navigation("Employee"); });
        modelBuilder.Entity("Workforce.Api.Models.ShiftAssignment", b => { b.HasOne("Workforce.Api.Models.Employee", "Employee").WithMany("ShiftAssignments").HasForeignKey("EmployeeId").OnDelete(DeleteBehavior.Cascade).IsRequired(); b.HasOne("Workforce.Api.Models.Shift", "Shift").WithMany("Assignments").HasForeignKey("ShiftId").OnDelete(DeleteBehavior.Cascade).IsRequired(); b.Navigation("Employee"); b.Navigation("Shift"); });
        modelBuilder.Entity("Workforce.Api.Models.ShiftRequirement", b => { b.HasOne("Workforce.Api.Models.Competence", "Competence").WithMany("ShiftRequirements").HasForeignKey("CompetenceId").OnDelete(DeleteBehavior.Cascade).IsRequired(); b.HasOne("Workforce.Api.Models.Shift", "Shift").WithMany("Requirements").HasForeignKey("ShiftId").OnDelete(DeleteBehavior.Cascade).IsRequired(); b.Navigation("Competence"); b.Navigation("Shift"); });
        modelBuilder.Entity("Workforce.Api.Models.Employee", b => { b.Navigation("Absences"); b.Navigation("Competences"); b.Navigation("ShiftAssignments"); });
        modelBuilder.Entity("Workforce.Api.Models.Competence", b => { b.Navigation("Employees"); b.Navigation("ShiftRequirements"); });
        modelBuilder.Entity("Workforce.Api.Models.Shift", b => { b.Navigation("Assignments"); b.Navigation("Requirements"); });
#pragma warning restore 612, 618
    }
}
