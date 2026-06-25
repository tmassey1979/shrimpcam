using NSubstitute;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Captures;
using ShrimpCam.Core.Configuration;

#pragma warning disable CA2007

namespace ShrimpCam.Core.Tests.Captures;

public sealed class ScheduledCaptureServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Due_interval_captures_one_scheduled_frame_and_persists_state()
    {
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var captureStorage = Substitute.For<ICaptureStorage>();
        var clock = Substitute.For<IClock>();
        var fileSystem = Substitute.For<IFileSystem>();
        var processRunner = Substitute.For<IProcessRunner>();
        var stateStore = Substitute.For<IScheduledCaptureStateStore>();
        var options = CreateOptions();
        var stagedPath = "temp/scheduled.jpg";
        var dueInterval = new DateTimeOffset(2026, 06, 24, 12, 00, 00, TimeSpan.Zero);
        var command = new ProcessRequest("ffmpeg", "-args");
        var storedCapture = new StoredCapture(
            "data/images/2026/06/24/scheduled.jpg",
            "data/images/2026/06/24/scheduled.json",
            "2026/06/24/scheduled.jpg",
            "scheduled.jpg",
            dueInterval,
            CaptureSourceTypes.Scheduled);

        clock.UtcNow.Returns(new DateTimeOffset(2026, 06, 24, 12, 03, 00, TimeSpan.Zero));
        stateStore.LoadAsync(options.Storage, Arg.Any<CancellationToken>()).Returns(ScheduledCaptureState.Empty);
        fileSystem.GetTemporaryFilePath(".jpg").Returns(stagedPath);
        commandFactory.BuildStillCaptureCommand(options.Camera, stagedPath).Returns(command);
        processRunner.RunAsync(command, Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, string.Empty, string.Empty));
        captureStorage.StoreAsync(
                options.Storage,
                Arg.Is<CaptureStorageRequest>(request =>
                    request.SourceType == CaptureSourceTypes.Scheduled
                    && request.StagedFilePath == stagedPath
                    && request.CapturedAtUtc == dueInterval),
                Arg.Any<CancellationToken>())
            .Returns(storedCapture);

        var service = new ScheduledCaptureService(
            commandFactory,
            captureStorage,
            clock,
            fileSystem,
            processRunner,
            stateStore);

        var result = await service.RunDueCaptureAsync(options, CancellationToken.None).ConfigureAwait(true);

        result.Outcome.Should().Be(ScheduledCaptureOutcome.Captured);
        result.Capture.Should().Be(storedCapture);
        await stateStore.Received(1).SaveAsync(
                options.Storage,
                new ScheduledCaptureState(dueInterval, ScheduledCaptureOutcome.Captured, null),
                Arg.Any<CancellationToken>())
            .ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Disabled_schedule_returns_without_loading_or_saving_state()
    {
        var options = CreateOptions();
        options = new ShrimpCamOptions
        {
            Camera = options.Camera,
            Storage = options.Storage,
            Security = options.Security,
            Capture = new CaptureOptions
            {
                Enabled = false,
                IntervalMinutes = 5,
                ActiveStartHourUtc = 6,
                ActiveEndHourUtc = 22,
            },
        };
        var stateStore = Substitute.For<IScheduledCaptureStateStore>();
        var service = new ScheduledCaptureService(
            Substitute.For<ICameraCommandFactory>(),
            Substitute.For<ICaptureStorage>(),
            CreateClock(new DateTimeOffset(2026, 06, 24, 12, 03, 00, TimeSpan.Zero)),
            Substitute.For<IFileSystem>(),
            Substitute.For<IProcessRunner>(),
            stateStore);

        var result = await service.RunDueCaptureAsync(options, CancellationToken.None).ConfigureAwait(true);

        result.Outcome.Should().Be(ScheduledCaptureOutcome.Disabled);
        await stateStore.DidNotReceiveWithAnyArgs().LoadAsync(default!, default).ConfigureAwait(true);
        await stateStore.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!, default).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Outside_schedule_window_records_a_skip_for_the_interval()
    {
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var captureStorage = Substitute.For<ICaptureStorage>();
        var clock = Substitute.For<IClock>();
        var fileSystem = Substitute.For<IFileSystem>();
        var processRunner = Substitute.For<IProcessRunner>();
        var stateStore = Substitute.For<IScheduledCaptureStateStore>();
        var options = CreateOptions();
        var skippedInterval = new DateTimeOffset(2026, 06, 24, 23, 00, 00, TimeSpan.Zero);

        clock.UtcNow.Returns(new DateTimeOffset(2026, 06, 24, 23, 03, 00, TimeSpan.Zero));
        stateStore.LoadAsync(options.Storage, Arg.Any<CancellationToken>()).Returns(ScheduledCaptureState.Empty);

        var service = new ScheduledCaptureService(
            commandFactory,
            captureStorage,
            clock,
            fileSystem,
            processRunner,
            stateStore);

        var result = await service.RunDueCaptureAsync(options, CancellationToken.None).ConfigureAwait(true);

        result.Outcome.Should().Be(ScheduledCaptureOutcome.SkippedBySchedule);
        await captureStorage.DidNotReceiveWithAnyArgs().StoreAsync(default!, default!, default).ConfigureAwait(true);
        await stateStore.Received(1).SaveAsync(
                options.Storage,
                new ScheduledCaptureState(skippedInterval, ScheduledCaptureOutcome.SkippedBySchedule, null),
                Arg.Any<CancellationToken>())
            .ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Failed_camera_capture_marks_interval_failed_for_future_progress()
    {
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var captureStorage = Substitute.For<ICaptureStorage>();
        var clock = Substitute.For<IClock>();
        var fileSystem = Substitute.For<IFileSystem>();
        var processRunner = Substitute.For<IProcessRunner>();
        var stateStore = Substitute.For<IScheduledCaptureStateStore>();
        var options = CreateOptions();
        var stagedPath = "temp/scheduled.jpg";
        var dueInterval = new DateTimeOffset(2026, 06, 24, 12, 00, 00, TimeSpan.Zero);
        var command = new ProcessRequest("ffmpeg", "-args");

        clock.UtcNow.Returns(new DateTimeOffset(2026, 06, 24, 12, 03, 00, TimeSpan.Zero));
        stateStore.LoadAsync(options.Storage, Arg.Any<CancellationToken>()).Returns(ScheduledCaptureState.Empty);
        fileSystem.GetTemporaryFilePath(".jpg").Returns(stagedPath);
        fileSystem.FileExists(stagedPath).Returns(true);
        commandFactory.BuildStillCaptureCommand(options.Camera, stagedPath).Returns(command);
        processRunner.RunAsync(command, Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, string.Empty, "camera unavailable"));

        var service = new ScheduledCaptureService(
            commandFactory,
            captureStorage,
            clock,
            fileSystem,
            processRunner,
            stateStore);

        var result = await service.RunDueCaptureAsync(options, CancellationToken.None).ConfigureAwait(true);

        result.Outcome.Should().Be(ScheduledCaptureOutcome.Failed);
        result.FailureReason.Should().Be(ManualCaptureFailureReasons.CameraUnavailable);
        fileSystem.Received(1).DeleteFile(stagedPath);
        await stateStore.Received(1).SaveAsync(
                options.Storage,
                new ScheduledCaptureState(dueInterval, ScheduledCaptureOutcome.Failed, ManualCaptureFailureReasons.CameraUnavailable),
                Arg.Any<CancellationToken>())
            .ConfigureAwait(true);
    }

    private static ShrimpCamOptions CreateOptions() =>
        new()
        {
            Camera = new CameraOptions
            {
                Platform = CameraPlatforms.Windows,
                Source = "Logitech C920",
            },
            Capture = new CaptureOptions
            {
                Enabled = true,
                IntervalMinutes = 5,
                ActiveStartHourUtc = 6,
                ActiveEndHourUtc = 22,
            },
            Storage = new StorageOptions
            {
                ImageRootPath = "data/images",
                RetentionDays = 30,
            },
        };

    private static IClock CreateClock(DateTimeOffset utcNow)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(utcNow);
        return clock;
    }
}

#pragma warning restore CA2007
