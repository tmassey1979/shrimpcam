using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Persistence;

namespace ShrimpCam.Core.Authentication;

public sealed class BootstrapAdministratorService(
    IClock clock,
    IPasswordHasher passwordHasher,
    IPasswordPolicy passwordPolicy,
    IUserRepository userRepository,
    IUserRoleRepository userRoleRepository) : IBootstrapAdministratorService
{
    public async Task<BootstrapAdministratorResult> BootstrapAsync(
        BootstrapAdministratorRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (await userRoleRepository.AnyInRoleAsync("Administrator", cancellationToken).ConfigureAwait(false))
        {
            return BootstrapAdministratorResult.Failure(BootstrapAdministratorFailureReasons.AlreadyConfigured);
        }

        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            return BootstrapAdministratorResult.Failure(BootstrapAdministratorFailureReasons.InvalidUserName);
        }

        if (!passwordPolicy.IsSatisfiedBy(request.Password))
        {
            return BootstrapAdministratorResult.Failure(BootstrapAdministratorFailureReasons.WeakPassword);
        }

        var normalizedUserName = request.UserName.Trim();
        if (await userRepository.GetByUserNameAsync(normalizedUserName, cancellationToken).ConfigureAwait(false) is not null)
        {
            return BootstrapAdministratorResult.Failure(BootstrapAdministratorFailureReasons.UserNameUnavailable);
        }

        var userId = Guid.NewGuid();
        var createdAtUtc = clock.UtcNow;

        await userRepository.CreateAsync(
                new UserRecord(
                    userId,
                    normalizedUserName,
                    passwordHasher.HashPassword(request.Password),
                    true,
                    createdAtUtc),
                cancellationToken)
            .ConfigureAwait(false);

        await userRoleRepository.AssignAsync(
                new UserRoleRecord(userId, "Administrator", createdAtUtc),
                cancellationToken)
            .ConfigureAwait(false);

        return BootstrapAdministratorResult.Success(
            new BootstrapAdministratorUser(userId, normalizedUserName, "Administrator"));
    }
}
