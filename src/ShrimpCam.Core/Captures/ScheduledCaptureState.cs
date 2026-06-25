namespace ShrimpCam.Core.Captures;

public sealed record ScheduledCaptureState(
    DateTimeOffset? LastProcessedIntervalUtc,
    ScheduledCaptureOutcome LastOutcome,
    string? LastFailureReason)
{
    public static ScheduledCaptureState Empty { get; } =
        new(null, ScheduledCaptureOutcome.Waiting, null);
}
