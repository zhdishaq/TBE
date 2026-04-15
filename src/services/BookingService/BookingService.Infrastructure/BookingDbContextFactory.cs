using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TBE.BookingService.Infrastructure;

/// <summary>
/// Design-time factory for EF Core migrations. Used only by dotnet-ef tooling.
/// </summary>
public class BookingDbContextFactory : IDesignTimeDbContextFactory<BookingDbContext>
{
    public BookingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseSqlServer("Server=localhost;Database=BookingDb;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;
        return new BookingDbContext(options);
    }
}
