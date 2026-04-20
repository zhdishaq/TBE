using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TBE.BackofficeService.Infrastructure;

/// <summary>
/// Design-time factory for EF Core migrations. Used only by dotnet-ef tooling.
/// </summary>
public class BackofficeDbContextFactory : IDesignTimeDbContextFactory<BackofficeDbContext>
{
    public BackofficeDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BackofficeDbContext>()
            .UseSqlServer("Server=localhost;Database=BackofficeDb;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;
        return new BackofficeDbContext(options);
    }
}
