using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Persistence;

namespace ShrimpCam.Core.Authentication;

public sealed class SessionAuthenticationService(
    IClock clock,
    ISessionRepository sessionRepository,
    IUserRepository userRepository,
    IUserRoleRepository userRoleRepository) : ISessionAuthenticationService
{
    public async Task<SessionAuthenticationResult> AuthenticateAsync(string token, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        cancellationToken.ThrowIfCancellationRequested();

        var tokenHash = SessionTokenHasher.ComputeHash(token);
        var session = await sessionRepository.GetByTokenHashAsync(tokenHash, cancellationToken).ConfigureAwait(false);
        if (session is null ||
            session.RevokedAtUtc is not null ||
            session.ExpiresAtUtc <= clock.UtcNow)
        {
            return SessionAuthenticationResult.Failure(SessionAuthenticationFailureReasons.InvalidSession);
        }

        var user = await userRepository.GetByIdAsync(session.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || !user.IsEnabled)
        {
            return SessionAuthenticationResult.Failure(SessionAuthenticationFailureReasons.InvalidSession);
        }

        var roles = await userRoleRepository.ListByUserIdAsync(user.Id, cancellationToken).ConfigureAwait(false);

        return SessionAuthenticationResult.Success(
            new SessionIdentity(
                session.Id,
                user.Id,
                user.UserName,
                roles.Select(role => role.RoleName).ToArray(),
                session.ExpiresAtUtc));
    }
}
