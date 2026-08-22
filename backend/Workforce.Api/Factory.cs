using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Workforce.Api.Data;

namespace Workforce.Api;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost,1433;Database=WorkforceCompetenceDesignTime;User Id=sa;Password=CiOnly-Password-2026!;TrustServerCertificate=True;Encrypt=False");
        return new AppDbContext(optionsBuilder.Options);
    }
}
