using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Cameras;

public interface ICameraFrameSourceProvider
{
    CameraFrameSourceProviderDescriptor Descriptor { get; }

    CameraFrameSourceStartResult Start(
        CameraOptions options,
        Action<ReadOnlyMemory<byte>> publishFrame,
        CancellationToken cancellationToken);
}
