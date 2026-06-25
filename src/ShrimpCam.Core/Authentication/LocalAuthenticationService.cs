using System.Security.Cryptography;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Persistence;

namespace ShrimpCam.Core.Authentication;

public sealed class LocalAuthenticationService(
    IClock clock,
    IPasswordHasher passwordHasher,
    ISessionRepository sessionRepository,
    IUserRepository userRepository) : IAuthenticationService
{
    private static readonly TimeSpan DefaultSessionLifetime = TimeSpan.FromHours(8);

    public async Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!CredentialValidator.IsValid(request))
        {
            return AuthenticationResult.Failure(AuthenticationFailureReasons.InvalidCredentials);
        }

        var normalizedUserName = request.UserName.Trim();
        var user = await userRepository.GetByUserNameAsync(normalizedUserName, cancellationToken).ConfigureAwait(false);
        if (user is null || !user.IsEnabled)
        {
            return AuthenticationResult.Failure(AuthenticationFailureReasons.InvalidCredentials);
        }

        if (!passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return AuthenticationResult.Failure(AuthenticationFailureReasons.InvalidCredentials);
        }

        var sessionId = Guid.NewGuid();
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var expiresAtUtc = clock.UtcNow.Add(DefaultSessionLifetime);

        await sessionRepository.CreateAsync(
                new SessionRecord(
                    sessionId,
                    user.Id,
                    SessionTokenHasher.ComputeHash(rawToken),
                    clock.UtcNow,
                    expiresAtUtc,
                    null),
                cancellationToken)
            .ConfigureAwait(false);

        return AuthenticationResult.Success(
            new AuthenticatedSession(sessionId, user.Id, user.UserName, rawToken, expiresAtUtc));
    }
}
