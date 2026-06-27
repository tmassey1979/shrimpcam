using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Cameras;

public interface ICameraFrameSourceProviderRegistry
{
    ICameraFrameSourceProvider GetProvider(CameraOptions options, string hostPlatform);

    IReadOnlyList<CameraFrameSourceProviderDescriptor> ListProviders();
}
