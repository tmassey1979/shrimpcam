namespace ShrimpCam.Core.Captures;

public sealed record ScheduledCapturePlan(
    ScheduledCaptureOutcome Outcome,
    DateTimeOffset IntervalStartUtc,
    DateTimeOffset? NextEligibleIntervalUtc);
