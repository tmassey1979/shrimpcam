namespace ShrimpCam.Core.Persistence;

public sealed record UserRecord(
    Guid Id,
    string UserName,
    string PasswordHash,
    bool IsEnabled,
    DateTimeOffset CreatedAtUtc);
