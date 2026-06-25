namespace ShrimpCam.Core.Captures;

public sealed record ManualCaptureResult(
    bool Succeeded,
    string? FailureReason,
    StoredCapture? Capture)
{
    public static ManualCaptureResult Success(StoredCapture capture) => new(true, null, capture);

    public static ManualCaptureResult Failure(string failureReason) => new(false, failureReason, null);
}
