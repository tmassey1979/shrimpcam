namespace ShrimpCam.Core.Persistence;

public sealed record SessionRecord(
    Guid Id,
    Guid UserId,
    string TokenHash,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? RevokedAtUtc);
