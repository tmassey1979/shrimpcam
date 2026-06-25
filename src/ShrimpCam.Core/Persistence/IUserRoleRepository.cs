namespace ShrimpCam.Core.Persistence;

public interface IUserRoleRepository
{
    Task AssignAsync(UserRoleRecord roleAssignment, CancellationToken cancellationToken);

    Task<bool> AnyInRoleAsync(string roleName, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserRoleRecord>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken);
}
