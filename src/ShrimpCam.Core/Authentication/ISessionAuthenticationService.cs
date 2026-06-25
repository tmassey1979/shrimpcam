namespace ShrimpCam.Core.Authentication;

public interface ISessionAuthenticationService
{
    Task<SessionAuthenticationResult> AuthenticateAsync(string token, CancellationToken cancellationToken);
}
