namespace ShrimpCam.Core.Captures;

public sealed record MotionHighlightEvent(
    DateTimeOffset OccurredAtUtc,
    double Score,
    string? EventId = null);
