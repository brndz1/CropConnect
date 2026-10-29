namespace Notifications.Service.Application.Clients;

public sealed record UserContact(Guid UserId, string Username, string Email, bool IsActive);

public interface IUserDirectoryClient
{
    Task<UserContact> GetContactAsync(Guid userId, CancellationToken cancellationToken);
}
