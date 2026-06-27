namespace ShrimpCam.Infrastructure.Cameras.Windows;

internal sealed record MediaFoundationFrameSourceStartResult(
    bool Succeeded,
    string? FailureReason,
    Task? RunningTask)
{
    public static MediaFoundationFrameSourceStartResult Success(Task runningTask) =>
        new(true, null, runningTask);

    public static MediaFoundationFrameSourceStartResult Failure(string failureReason) =>
        new(false, failureReason, null);
}
