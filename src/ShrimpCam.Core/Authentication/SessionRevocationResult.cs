using ShrimpCam.Core.Persistence;

namespace ShrimpCam.Core.Authentication;

public sealed record SessionRevocationResult(
    bool Succeeded,
    string? FailureReason,
    SessionRecord? RevokedSession)
{
    public static SessionRevocationResult Success(SessionRecord revokedSession) =>
        new(true, null, revokedSession);

    public static SessionRevocationResult Failure(string failureReason) =>
        new(false, failureReason, null);
}
