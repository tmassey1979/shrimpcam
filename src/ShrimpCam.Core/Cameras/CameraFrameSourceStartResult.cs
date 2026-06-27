namespace ShrimpCam.Core.Cameras;

public sealed record CameraFrameSourceStartResult(
    bool Succeeded,
    string? FailureReason,
    Task? RunningTask)
{
    public static CameraFrameSourceStartResult Success(Task runningTask) =>
        new(true, null, runningTask);

    public static CameraFrameSourceStartResult Failure(string failureReason) =>
        new(false, failureReason, null);
}
