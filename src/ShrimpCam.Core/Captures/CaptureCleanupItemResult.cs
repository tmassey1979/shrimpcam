namespace ShrimpCam.Core.Captures;

public sealed record CaptureCleanupItemResult(
    string RelativePath,
    bool Deleted,
    string? FailureReason);
