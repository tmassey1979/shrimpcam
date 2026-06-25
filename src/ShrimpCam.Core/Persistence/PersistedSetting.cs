namespace ShrimpCam.Core.Persistence;

public sealed record PersistedSetting(
    string Key,
    string Value,
    string? Description,
    DateTimeOffset UpdatedAtUtc);
