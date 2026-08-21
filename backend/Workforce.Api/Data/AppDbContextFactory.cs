using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Workforce.Api.Data;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=WorkforceCompetenceDb;User Id=sa;Password=LocalDesignTimeOnlyPassword_123!;TrustServerCertificate=True;Encrypt=False")
            .Options;

        return new AppDbContext(options);
    }
}
