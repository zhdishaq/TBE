using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace TBE.NotificationService.Application.Consumers;

internal static class IdempotencyHelpers
{
    // SQL Server unique-index violation codes.
    // 2601 = cannot insert duplicate key row in object with unique index
    // 2627 = violation of UNIQUE KEY constraint
    public static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627);
}
