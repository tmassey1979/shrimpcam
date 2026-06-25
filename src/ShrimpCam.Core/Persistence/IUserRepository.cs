namespace ShrimpCam.Core.Persistence;

public interface IUserRepository
{
    Task CreateAsync(UserRecord user, CancellationToken cancellationToken);

    Task<UserRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<UserRecord?> GetByUserNameAsync(string userName, CancellationToken cancellationToken);
}
