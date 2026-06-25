namespace ShrimpCam.Core.Authentication;

public sealed record AuthenticatedSession(
    Guid SessionId,
    Guid UserId,
    string UserName,
    string Token,
    DateTimeOffset ExpiresAtUtc);
