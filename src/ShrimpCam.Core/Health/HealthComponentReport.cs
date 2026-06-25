namespace ShrimpCam.Core.Health;

public sealed record HealthComponentReport(
    string Status,
    string? Detail);
