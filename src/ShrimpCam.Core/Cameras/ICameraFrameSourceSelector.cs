using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Cameras;

public interface ICameraFrameSourceSelector
{
    CameraFrameSourceSelection ChooseFrameSource(CameraOptions options, string hostPlatform);
}
