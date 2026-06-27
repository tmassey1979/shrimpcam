namespace ShrimpCam.Infrastructure.Cameras.Windows;

internal sealed record MediaFoundationDeviceDescriptor(
    string DisplayName,
    string SymbolicLink,
    IReadOnlyList<MediaFoundationFrameFormat> Formats,
    int? NativeIndex = null);
