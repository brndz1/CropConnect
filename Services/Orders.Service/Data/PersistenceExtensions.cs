using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orders.Service.Models;

namespace Orders.Service.Data;

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
