using System.Text.Json;
using Auth.Service.Data;
using Auth.Service.Messaging;
using Auth.Service.Models;

namespace Auth.Service.Application;

// The JSON contracts published on cropconnect.events for the account emails (camelCase).

// An email with a one-time link: verify the address, reset the password or, for an invitation, set the first one.
public sealed record AccountTokenMessage(
    Guid UserId,
    string Email,
    string Username,
    string Role,
    string Token,
    DateTime ExpiresAt,
    DateTime OccurredAt);

public sealed record AccountMessage(
    Guid UserId,
    string Email,
    string Username,
    string Role,
    DateTime OccurredAt);

// Adds the event to the outbox of the current unit of work, so it is saved with the change that raised it.
public static class AccountEvents
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void AddTokenEvent(AuthDbContext dbContext, string routingKey, User user, string token, DateTime expiresAt, DateTime now) =>
        Add(dbContext, routingKey, nameof(AccountTokenMessage),
            new AccountTokenMessage(user.Id, user.Email, user.Username, user.Role.ToString(), token, expiresAt, now), now);

    public static void AddEvent(AuthDbContext dbContext, string routingKey, User user, DateTime now) =>
        Add(dbContext, routingKey, nameof(AccountMessage),
            new AccountMessage(user.Id, user.Email, user.Username, user.Role.ToString(), now), now);

    private static void Add<T>(AuthDbContext dbContext, string routingKey, string eventType, T payload, DateTime now) =>
        dbContext.OutboxMessages.Add(OutboxMessage.Create(
            eventType, routingKey, JsonSerializer.Serialize(payload, Json), now, MessagingTelemetry.CurrentTraceParent()));
}
