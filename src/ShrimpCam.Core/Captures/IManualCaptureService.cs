using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Captures;

public interface IManualCaptureService
{
    Task<ManualCaptureResult> CaptureAsync(
        ShrimpCamOptions options,
        CancellationToken cancellationToken);
}
