namespace ShrimpCam.Core.Cameras;

public sealed record CameraDescriptor(
    string DisplayName,
    string DevicePath,
    string Platform);
