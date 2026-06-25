using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShrimpCam.Core.Captures;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Captures;

internal sealed class ScheduledCaptureWorker(
    IOptions<ShrimpCamOptions> options,
    IScheduledCaptureService scheduledCaptureService,
    ILogger<ScheduledCaptureWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
    private static readonly Action<ILogger, DateTimeOffset, Exception?> CapturedScheduledFrame =
        LoggerMessage.Define<DateTimeOffset>(
            LogLevel.Information,
            new EventId(2001, nameof(CapturedScheduledFrame)),
            "Captured scheduled timelapse frame for interval {IntervalStartUtc}.");

    private static readonly Action<ILogger, DateTimeOffset, Exception?> SkippedScheduledFrame =
        LoggerMessage.Define<DateTimeOffset>(
            LogLevel.Information,
            new EventId(2002, nameof(SkippedScheduledFrame)),
            "Skipped scheduled timelapse frame for interval {IntervalStartUtc} because it was outside the active window.");

    private static readonly Action<ILogger, DateTimeOffset, string?, Exception?> FailedScheduledFrame =
        LoggerMessage.Define<DateTimeOffset, string?>(
            LogLevel.Warning,
            new EventId(2003, nameof(FailedScheduledFrame)),
            "Scheduled timelapse capture failed for interval {IntervalStartUtc}: {FailureReason}");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunSingleIterationAsync(stoppingToken).ConfigureAwait(false);

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

    internal async Task RunSingleIterationAsync(CancellationToken cancellationToken)
    {
        var result = await scheduledCaptureService.RunDueCaptureAsync(options.Value, cancellationToken).ConfigureAwait(false);

        switch (result.Outcome)
        {
            case ScheduledCaptureOutcome.Captured:
                CapturedScheduledFrame(logger, result.IntervalStartUtc, null);
                break;

            case ScheduledCaptureOutcome.SkippedBySchedule:
                SkippedScheduledFrame(logger, result.IntervalStartUtc, null);
                break;

            case ScheduledCaptureOutcome.Failed:
                FailedScheduledFrame(logger, result.IntervalStartUtc, result.FailureReason, null);
                break;
        }
    }
}
