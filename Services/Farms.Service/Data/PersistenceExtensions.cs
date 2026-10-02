using Farms.Service.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;

namespace Farms.Service.Data;

public static class PersistenceExtensions
{
    #region Errors

    public static bool IsUniqueViolation(this DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    public static async Task SaveChangesOrConflictAsync(
        this DbContext dbContext,
        VersionedEntity entity,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException(entity.EntityName);
        }
    }

    #endregion

    #region Locking

    public static async Task AcquireAdvisoryLockAsync(
        this DatabaseFacade database,
        Guid resourceId,
        CancellationToken cancellationToken)
    {
        if (database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Advisory locks require an active transaction.");
        }

        var key = BitConverter.ToInt64(resourceId.ToByteArray(), 0);
        await database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
    }

    #endregion

    #region Search

    public const string LikeEscapeCharacter = "\\";

    public static string ToContainsPattern(this string search)
    {
        var escaped = search.Trim()
            .Replace(LikeEscapeCharacter, LikeEscapeCharacter + LikeEscapeCharacter)
            .Replace("%", LikeEscapeCharacter + "%")
            .Replace("_", LikeEscapeCharacter + "_");

        return $"%{escaped}%";
    }

    #endregion
}
