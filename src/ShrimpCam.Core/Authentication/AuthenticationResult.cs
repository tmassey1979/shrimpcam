namespace ShrimpCam.Core.Authentication;

public sealed record AuthenticationResult(
    bool Succeeded,
    string? FailureReason,
    AuthenticatedSession? Session)
{
    public static AuthenticationResult Success(AuthenticatedSession session) => new(true, null, session);

    public static AuthenticationResult Failure(string failureReason) => new(false, failureReason, null);
}
