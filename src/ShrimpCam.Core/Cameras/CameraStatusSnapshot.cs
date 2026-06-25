namespace ShrimpCam.Core.Cameras;

public sealed record CameraStatusSnapshot(
    string Status,
    string? Reason,
    DateTimeOffset UpdatedAtUtc);
