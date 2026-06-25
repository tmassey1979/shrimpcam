using Microsoft.Extensions.Options;
using NSubstitute;
using ShrimpCam.Core.Captures;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Settings;
using ShrimpCam.Infrastructure.Captures;

#pragma warning disable CA2007

namespace ShrimpCam.Infrastructure.Tests.Captures;

public sealed class ScheduledCaptureWorkerTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Run_single_iteration_logs_capture_and_skip_outcomes()
    {
        var scheduledCaptureService = Substitute.For<IScheduledCaptureService>();
        var settingsService = Substitute.For<IEditableSettingsService>();
        settingsService.GetCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(CreateSettings(CreateOptions()));
        var logger = new ListLogger<ScheduledCaptureWorker>();
        var worker = new ScheduledCaptureWorker(
            Options.Create(CreateOptions()),
            settingsService,
            scheduledCaptureService,
            logger);

        scheduledCaptureService.RunDueCaptureAsync(Arg.Any<ShrimpCamOptions>(), Arg.Any<CancellationToken>())
            .Returns(
                ScheduledCaptureRunResult.Create(
                    ScheduledCaptureOutcome.Captured,
                    new DateTimeOffset(2026, 06, 24, 12, 00, 00, TimeSpan.Zero),
                    new DateTimeOffset(2026, 06, 24, 12, 05, 00, TimeSpan.Zero)),
                ScheduledCaptureRunResult.Create(
                    ScheduledCaptureOutcome.SkippedBySchedule,
                    new DateTimeOffset(2026, 06, 24, 23, 00, 00, TimeSpan.Zero),
                    new DateTimeOffset(2026, 06, 25, 06, 00, 00, TimeSpan.Zero)));

        await worker.RunSingleIterationAsync(CancellationToken.None).ConfigureAwait(true);
        await worker.RunSingleIterationAsync(CancellationToken.None).ConfigureAwait(true);

        logger.Entries.Should().Contain(entry => entry.Message.Contains("Captured scheduled timelapse frame", StringComparison.Ordinal));
        logger.Entries.Should().Contain(entry => entry.Message.Contains("Skipped scheduled timelapse frame", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Background_worker_stops_before_first_poll_when_host_is_cancelled()
    {
        var scheduledCaptureService = Substitute.For<IScheduledCaptureService>();
        var settingsService = Substitute.For<IEditableSettingsService>();
        settingsService.GetCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(CreateSettings(CreateOptions()));
        var logger = new ListLogger<ScheduledCaptureWorker>();
        var worker = new ScheduledCaptureWorker(
            Options.Create(CreateOptions()),
            settingsService,
            scheduledCaptureService,
            logger);

        scheduledCaptureService.RunDueCaptureAsync(Arg.Any<ShrimpCamOptions>(), Arg.Any<CancellationToken>())
            .Returns(ScheduledCaptureRunResult.Create(
                ScheduledCaptureOutcome.Waiting,
                new DateTimeOffset(2026, 06, 24, 12, 00, 00, TimeSpan.Zero),
                new DateTimeOffset(2026, 06, 24, 12, 05, 00, TimeSpan.Zero)));

        using var cancellationTokenSource = new CancellationTokenSource();

        await worker.StartAsync(cancellationTokenSource.Token).ConfigureAwait(true);
        cancellationTokenSource.Cancel();
        await worker.StopAsync(CancellationToken.None).ConfigureAwait(true);

        await scheduledCaptureService.DidNotReceiveWithAnyArgs()
            .RunDueCaptureAsync(default!, default)
            .ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Run_single_iteration_uses_current_editable_settings()
    {
        var defaults = CreateOptions(
            enabled: false,
            intervalMinutes: 30,
            imageRootPath: "configured/images");

        var current = CreateOptions(
            enabled: true,
            intervalMinutes: 2,
            retentionDays: 12);

        var settingsService = Substitute.For<IEditableSettingsService>();
        settingsService.GetCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(CreateSettings(current));

        var scheduledCaptureService = Substitute.For<IScheduledCaptureService>();
        scheduledCaptureService.RunDueCaptureAsync(Arg.Any<ShrimpCamOptions>(), Arg.Any<CancellationToken>())
            .Returns(ScheduledCaptureRunResult.Create(
                ScheduledCaptureOutcome.Waiting,
                new DateTimeOffset(2026, 06, 24, 12, 00, 00, TimeSpan.Zero),
                new DateTimeOffset(2026, 06, 24, 12, 02, 00, TimeSpan.Zero)));

        var worker = new ScheduledCaptureWorker(
            Options.Create(defaults),
            settingsService,
            scheduledCaptureService,
            new ListLogger<ScheduledCaptureWorker>());

        await worker.RunSingleIterationAsync(CancellationToken.None).ConfigureAwait(true);

        await scheduledCaptureService.Received(1)
            .RunDueCaptureAsync(
                Arg.Is<ShrimpCamOptions>(options =>
                    options.Capture.Enabled
                    && options.Capture.IntervalMinutes == 2
                    && options.Storage.RetentionDays == 12
                    && options.Storage.ImageRootPath == "configured/images"),
                Arg.Any<CancellationToken>())
            .ConfigureAwait(true);
    }

    private static ShrimpCamOptions CreateOptions(
        bool enabled = true,
        int intervalMinutes = 5,
        string imageRootPath = "data/images",
        int retentionDays = 30) =>
        new()
        {
            Camera = new CameraOptions
            {
                Platform = "Linux",
                Source = "/dev/video0",
            },
            Capture = new CaptureOptions
            {
                Enabled = enabled,
                IntervalMinutes = intervalMinutes,
                ActiveStartHourUtc = 6,
                ActiveEndHourUtc = 22,
            },
            Storage = new StorageOptions
            {
                ImageRootPath = imageRootPath,
                RetentionDays = retentionDays,
            },
        };

    private static EditableSettings CreateSettings(ShrimpCamOptions options) =>
        new(
            options.Camera,
            options.Capture,
            new StorageEditableSettings(options.Storage.RetentionDays),
            options.Security);

    private sealed class ListLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public List<(Microsoft.Extensions.Logging.LogLevel LogLevel, string Message)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull =>
            NullScope.Instance;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}

#pragma warning restore CA2007
