namespace ShrimpCam.Core.Captures;

public sealed record MotionHighlightResult(
    string Outcome,
    string? FailureReason,
    StoredCapture? Capture)
{
    public static MotionHighlightResult Captured(StoredCapture capture) =>
        new(MotionHighlightOutcome.Captured, null, capture);

    public static MotionHighlightResult Skipped(string outcome) =>
        new(outcome, null, null);

    public static MotionHighlightResult Failed(string failureReason) =>
        new(MotionHighlightOutcome.Failed, failureReason, null);
}
