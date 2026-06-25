namespace ShrimpCam.Core.Authentication;

public sealed record SessionIdentity(
    Guid SessionId,
    Guid UserId,
    string UserName,
    IReadOnlyList<string> Roles,
    DateTimeOffset ExpiresAtUtc);
