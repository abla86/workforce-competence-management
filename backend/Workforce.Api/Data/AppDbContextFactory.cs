using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Workforce.Api.Data;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD")
            ?? Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD");

        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("DB_PASSWORD or MSSQL_SA_PASSWORD must be set for EF Core design-time operations.");

        var connectionString =
            $"Server=localhost,1433;Database=WorkforceCompetenceDb;User Id=sa;Password={password};TrustServerCertificate=True;Encrypt=False";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
