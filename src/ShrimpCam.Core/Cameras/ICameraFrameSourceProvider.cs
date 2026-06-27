namespace ShrimpCam.Core.Cameras;

public interface ICameraFrameSourceProvider
{
    CameraFrameSourceProviderDescriptor Descriptor { get; }
}
