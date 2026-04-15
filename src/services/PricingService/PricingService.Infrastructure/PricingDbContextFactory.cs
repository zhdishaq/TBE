using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TBE.PricingService.Infrastructure;

/// <summary>
/// Design-time factory for EF Core migrations. Used only by dotnet-ef tooling.
/// </summary>
public class PricingDbContextFactory : IDesignTimeDbContextFactory<PricingDbContext>
{
    public PricingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PricingDbContext>()
            .UseSqlServer("Server=localhost;Database=PricingDb;Trusted_Connection=True;")
            .Options;
        return new PricingDbContext(options);
    }
}
