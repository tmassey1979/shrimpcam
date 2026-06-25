using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Persistence;

namespace ShrimpCam.Core.Authentication;

public sealed class SessionRevocationService(
    IClock clock,
    ISessionRepository sessionRepository) : ISessionRevocationService
{
    public async Task<SessionRevocationResult> RevokeAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existingSession = await sessionRepository.GetByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (existingSession is null)
        {
            return SessionRevocationResult.Failure(SessionRevocationFailureReasons.SessionNotFound);
        }

        var revokedSession = existingSession with { RevokedAtUtc = clock.UtcNow };

        await sessionRepository.UpdateAsync(revokedSession, cancellationToken).ConfigureAwait(false);

        return SessionRevocationResult.Success(revokedSession);
    }
}
