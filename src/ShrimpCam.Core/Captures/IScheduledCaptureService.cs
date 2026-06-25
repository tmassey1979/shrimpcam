using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Captures;

public interface IScheduledCaptureService
{
    Task<ScheduledCaptureRunResult> RunDueCaptureAsync(
        ShrimpCamOptions options,
        CancellationToken cancellationToken);
}
