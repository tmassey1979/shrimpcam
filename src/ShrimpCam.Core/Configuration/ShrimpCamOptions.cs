namespace ShrimpCam.Core.Configuration;

public sealed class ShrimpCamOptions
{
    public CameraOptions Camera { get; init; } = new();

    public CaptureOptions Capture { get; init; } = new();

    public StorageOptions Storage { get; init; } = new();

    public SecurityOptions Security { get; init; } = new();
}
