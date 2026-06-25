namespace ShrimpCam.Core.Captures;

public sealed record MotionHighlightState(
    DateTimeOffset? LastHighlightCapturedAtUtc,
    string? LastProcessedEventFingerprint,
    DateTimeOffset? LastProcessedEventOccurredAtUtc)
{
    public static MotionHighlightState Empty { get; } = new(null, null, null);
}
