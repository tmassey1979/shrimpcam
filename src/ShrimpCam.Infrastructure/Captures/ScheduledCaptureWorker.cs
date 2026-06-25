using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShrimpCam.Core.Captures;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Settings;

namespace ShrimpCam.Infrastructure.Captures;

internal sealed class ScheduledCaptureWorker(
    IOptions<ShrimpCamOptions> defaults,
    IEditableSettingsService settingsService,
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

    private static readonly Action<ILogger, Exception?> UnexpectedScheduledCaptureIterationFailure =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2004, nameof(UnexpectedScheduledCaptureIterationFailure)),
            "Scheduled timelapse worker iteration failed unexpectedly. The worker will retry on the next poll.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await RunSingleIterationAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    internal async Task RunSingleIterationAsync(CancellationToken cancellationToken)
    {
        try
        {
            var settings = await settingsService.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
            var result = await scheduledCaptureService.RunDueCaptureAsync(ToOptions(settings, defaults.Value), cancellationToken).ConfigureAwait(false);

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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            UnexpectedScheduledCaptureIterationFailure(logger, exception);
        }
    }

    private static ShrimpCamOptions ToOptions(EditableSettings settings, ShrimpCamOptions defaults) =>
        new()
        {
            Camera = settings.Camera,
            Capture = settings.Capture,
            Storage = new StorageOptions
            {
                DatabasePath = defaults.Storage.DatabasePath,
                ImageRootPath = defaults.Storage.ImageRootPath,
                TimelapseRootPath = defaults.Storage.TimelapseRootPath,
                RetentionDays = settings.Storage.RetentionDays,
            },
            Security = settings.Security,
        };
}
