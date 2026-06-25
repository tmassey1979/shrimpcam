namespace ShrimpCam.Core.Cameras;

public sealed record CameraLiveStreamStartResult(
    bool Succeeded,
    string? FailureReason,
    ICameraLiveStreamSession? Session)
{
    public static CameraLiveStreamStartResult Success(ICameraLiveStreamSession session) => new(true, null, session);

    public static CameraLiveStreamStartResult Failure(string failureReason) => new(false, failureReason, null);
}
