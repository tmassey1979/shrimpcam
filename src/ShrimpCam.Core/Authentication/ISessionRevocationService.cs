namespace ShrimpCam.Core.Authentication;

public interface ISessionRevocationService
{
    Task<SessionRevocationResult> RevokeAsync(Guid sessionId, CancellationToken cancellationToken);
}
