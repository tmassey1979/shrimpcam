using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ShrimpCam.Infrastructure.Cameras;

internal sealed class CameraFrameEvaluationWorker(
    CameraFrameEvaluationService evaluationService,
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
                await evaluationService.RunOnceAsync(stoppingToken).ConfigureAwait(false);
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
