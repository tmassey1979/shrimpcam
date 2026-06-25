namespace ShrimpCam.Core.Captures;

public sealed record ScheduledCaptureRunResult(
    ScheduledCaptureOutcome Outcome,
    DateTimeOffset IntervalStartUtc,
    DateTimeOffset? NextEligibleIntervalUtc,
    StoredCapture? Capture,
    string? FailureReason)
{
    public static ScheduledCaptureRunResult Create(
        ScheduledCaptureOutcome outcome,
        DateTimeOffset intervalStartUtc,
        DateTimeOffset? nextEligibleIntervalUtc,
        StoredCapture? capture = null,
        string? failureReason = null) =>
        new(outcome, intervalStartUtc, nextEligibleIntervalUtc, capture, failureReason);
}
