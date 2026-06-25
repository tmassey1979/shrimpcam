namespace ShrimpCam.Core.Authentication;

public sealed record SessionAuthenticationResult(
    bool Succeeded,
    string? FailureReason,
    SessionIdentity? Identity)
{
    public static SessionAuthenticationResult Success(SessionIdentity identity) =>
        new(true, null, identity);

    public static SessionAuthenticationResult Failure(string failureReason) =>
        new(false, failureReason, null);
}
