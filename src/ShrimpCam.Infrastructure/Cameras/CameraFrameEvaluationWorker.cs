using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShrimpCam.Core.Settings;

namespace ShrimpCam.Infrastructure.Cameras;

internal sealed class CameraFrameEvaluationWorker(
    IEditableSettingsService settingsService,
    ISharedCameraStreamHub streamHub,
    ILogger<CameraFrameEvaluationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private static readonly Action<ILogger, Exception?> FrameEvaluationFailed =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(2201, nameof(FrameEvaluationFailed)),
            "Camera frame evaluation worker could not keep the shared stream running.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var settings = await settingsService.GetCurrentAsync(stoppingToken).ConfigureAwait(false);
                if (settings.Camera.AlwaysOnStreamEnabled)
                {
                    await streamHub.EnsureRunningAsync(settings.Camera, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                FrameEvaluationFailed(logger, exception);
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
