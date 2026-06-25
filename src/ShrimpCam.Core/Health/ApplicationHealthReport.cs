namespace ShrimpCam.Core.Health;

public sealed record ApplicationHealthReport(
    string Status,
    DateTimeOffset CheckedAtUtc,
    IReadOnlyDictionary<string, HealthComponentReport> Components);
