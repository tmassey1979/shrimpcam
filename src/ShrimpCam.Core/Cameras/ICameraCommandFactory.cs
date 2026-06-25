using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Cameras;

public interface ICameraCommandFactory
{
    ProcessRequest BuildDiscoveryCommand(string platform);

    ProcessRequest BuildStillCaptureCommand(CameraOptions options, string outputPath);

    ProcessRequest BuildLiveStreamCommand(CameraOptions options);
}
