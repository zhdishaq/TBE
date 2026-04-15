using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TBE.PaymentService.Infrastructure;

/// <summary>
/// Design-time factory for EF Core migrations. Used only by dotnet-ef tooling.
/// </summary>
public class PaymentDbContextFactory : IDesignTimeDbContextFactory<PaymentDbContext>
{
    public PaymentDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer("Server=localhost;Database=PaymentDb;Trusted_Connection=True;")
            .Options;
        return new PaymentDbContext(options);
    }
}
