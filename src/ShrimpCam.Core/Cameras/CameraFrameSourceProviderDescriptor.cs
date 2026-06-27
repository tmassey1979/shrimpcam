namespace ShrimpCam.Core.Cameras;

public sealed record CameraFrameSourceProviderDescriptor(
    string ProviderKind,
    string DisplayName,
    string Platform,
    bool IsPrimary,
    bool RequiresExternalProcess,
    string DiagnosticsName);
