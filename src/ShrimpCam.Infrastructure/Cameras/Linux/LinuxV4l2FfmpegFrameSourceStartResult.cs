namespace ShrimpCam.Infrastructure.Cameras.Linux;

internal sealed record LinuxV4l2FfmpegFrameSourceStartResult(
    bool Succeeded,
    string? FailureReason,
    Task? RunningTask)
{
    public static LinuxV4l2FfmpegFrameSourceStartResult Success(Task runningTask) =>
        new(true, null, runningTask);

    public static LinuxV4l2FfmpegFrameSourceStartResult Failure(string failureReason) =>
        new(false, failureReason, null);
}
