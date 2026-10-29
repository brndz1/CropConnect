using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Notifications.Service.Data;

public static class PersistenceExtensions
{
    public static bool IsUniqueViolation(this DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
