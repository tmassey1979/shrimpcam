namespace ShrimpCam.Core.Cameras;

public sealed record CameraFrameSourceSelection(
    string ProviderKind,
    string BackendMode,
    string Platform,
    bool IsFallback);
