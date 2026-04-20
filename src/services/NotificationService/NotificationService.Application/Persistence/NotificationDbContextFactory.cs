using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TBE.NotificationService.Application.Persistence;

/// <summary>
/// Design-time factory for EF Core migrations. Used only by dotnet-ef tooling.
/// </summary>
public class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseSqlServer("Server=localhost;Database=NotificationDb;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;
        return new NotificationDbContext(options);
    }
}
