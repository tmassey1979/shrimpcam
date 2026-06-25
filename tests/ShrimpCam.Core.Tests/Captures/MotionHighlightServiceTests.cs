using NSubstitute;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Captures;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Tests.Captures;

public sealed class MotionHighlightServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Evaluate_captures_highlight_when_motion_qualifies()
    {
        var cameraCommandFactory = Substitute.For<ICameraCommandFactory>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var captureStorage = Substitute.For<ICaptureStorage>();
        var fileSystem = Substitute.For<IFileSystem>();
        var processRunner = Substitute.For<IProcessRunner>();
        var stateStore = Substitute.For<IMotionHighlightStateStore>();
        var motionEvent = new MotionHighlightEvent(new DateTimeOffset(2026, 06, 25, 00, 10, 00, TimeSpan.Zero), 0.88d, "event-10");
        var storedCapture = new StoredCapture(
            "data/images/2026/06/25/20260625T001000000Z_motionhighlight.jpg",
            "data/images/2026/06/25/20260625T001000000Z_motionhighlight.json",
            "2026/06/25/20260625T001000000Z_motionhighlight.jpg",
            "20260625T001000000Z_motionhighlight.jpg",
            motionEvent.OccurredAtUtc,
            CaptureSourceTypes.MotionHighlight);

        stateStore.LoadAsync(Arg.Any<StorageOptions>(), Arg.Any<CancellationToken>())
            .Returns(MotionHighlightState.Empty);
        fileSystem.GetTemporaryFilePath(".jpg").Returns("temp/highlight.jpg");
        cameraCommandFactory.BuildStillCaptureCommand(Arg.Any<CameraOptions>(), "temp/highlight.jpg")
            .Returns(new ProcessRequest("ffmpeg", "\"temp/highlight.jpg\""));
        processRunner.RunAsync(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, string.Empty, string.Empty));
        captureStorage.StoreAsync(
                Arg.Any<StorageOptions>(),
                Arg.Is<CaptureStorageRequest>(request =>
                    request.SourceType == CaptureSourceTypes.MotionHighlight
                    && request.CapturedAtUtc == motionEvent.OccurredAtUtc
                    && request.StagedFilePath == "temp/highlight.jpg"),
                Arg.Any<CancellationToken>())
            .Returns(storedCapture);

        var service = new MotionHighlightService(
            cameraCommandFactory,
            cameraStatusService,
            captureStorage,
            fileSystem,
            processRunner,
            stateStore);

        var result = await service.EvaluateAsync(CreateOptions(), motionEvent, CancellationToken.None).ConfigureAwait(true);

        result.Outcome.Should().Be(MotionHighlightOutcome.Captured);
        result.Capture.Should().BeSameAs(storedCapture);
        cameraStatusService.Received(1).ReportOnline();
        await stateStore.Received(1).SaveAsync(
                Arg.Any<StorageOptions>(),
                Arg.Is<MotionHighlightState>(state =>
                    state.LastHighlightCapturedAtUtc == motionEvent.OccurredAtUtc
                    && state.LastProcessedEventOccurredAtUtc == motionEvent.OccurredAtUtc
                    && state.LastProcessedEventFingerprint == "event-10"),
                Arg.Any<CancellationToken>())
            .ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Evaluate_suppresses_event_inside_cooldown_without_invoking_camera()
    {
        var cameraCommandFactory = Substitute.For<ICameraCommandFactory>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var captureStorage = Substitute.For<ICaptureStorage>();
        var fileSystem = Substitute.For<IFileSystem>();
        var processRunner = Substitute.For<IProcessRunner>();
        var stateStore = Substitute.For<IMotionHighlightStateStore>();
        var lastHighlightAt = new DateTimeOffset(2026, 06, 25, 00, 10, 00, TimeSpan.Zero);

        stateStore.LoadAsync(Arg.Any<StorageOptions>(), Arg.Any<CancellationToken>())
            .Returns(new MotionHighlightState(lastHighlightAt, "event-10", lastHighlightAt));

        var service = new MotionHighlightService(
            cameraCommandFactory,
            cameraStatusService,
            captureStorage,
            fileSystem,
            processRunner,
            stateStore);

        var result = await service.EvaluateAsync(
                CreateOptions(),
                new MotionHighlightEvent(new DateTimeOffset(2026, 06, 25, 00, 12, 00, TimeSpan.Zero), 0.9d, "event-11"),
                CancellationToken.None)
            .ConfigureAwait(true);

        result.Outcome.Should().Be(MotionHighlightOutcome.SuppressedByCooldown);
        cameraCommandFactory.DidNotReceiveWithAnyArgs().BuildStillCaptureCommand(default!, default!);
        await processRunner.DidNotReceiveWithAnyArgs().RunAsync(default!, default).ConfigureAwait(true);
        await captureStorage.DidNotReceiveWithAnyArgs().StoreAsync(default!, default!, default).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Evaluate_returns_disabled_when_motion_highlights_are_off()
    {
        var cameraCommandFactory = Substitute.For<ICameraCommandFactory>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var captureStorage = Substitute.For<ICaptureStorage>();
        var fileSystem = Substitute.For<IFileSystem>();
        var processRunner = Substitute.For<IProcessRunner>();
        var stateStore = Substitute.For<IMotionHighlightStateStore>();
        var motionEvent = new MotionHighlightEvent(new DateTimeOffset(2026, 06, 25, 00, 15, 00, TimeSpan.Zero), 0.9d, "event-disabled");

        stateStore.LoadAsync(Arg.Any<StorageOptions>(), Arg.Any<CancellationToken>())
            .Returns(MotionHighlightState.Empty);

        var service = new MotionHighlightService(
            cameraCommandFactory,
            cameraStatusService,
            captureStorage,
            fileSystem,
            processRunner,
            stateStore);

        var result = await service.EvaluateAsync(
                CreateOptions(new CaptureOptions
                {
                    MotionHighlightsEnabled = false,
                    MotionThreshold = 0.4d,
                    MotionCooldownSeconds = 300,
                }),
                motionEvent,
                CancellationToken.None)
            .ConfigureAwait(true);

        result.Outcome.Should().Be(MotionHighlightOutcome.Disabled);
        await stateStore.Received(1).SaveAsync(
                Arg.Any<StorageOptions>(),
                Arg.Is<MotionHighlightState>(state =>
                    state.LastHighlightCapturedAtUtc == null
                    && state.LastProcessedEventOccurredAtUtc == motionEvent.OccurredAtUtc
                    && state.LastProcessedEventFingerprint == "event-disabled"),
                Arg.Any<CancellationToken>())
            .ConfigureAwait(true);
        cameraCommandFactory.DidNotReceiveWithAnyArgs().BuildStillCaptureCommand(default!, default!);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Evaluate_returns_below_threshold_without_invoking_camera()
    {
        var cameraCommandFactory = Substitute.For<ICameraCommandFactory>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var captureStorage = Substitute.For<ICaptureStorage>();
        var fileSystem = Substitute.For<IFileSystem>();
        var processRunner = Substitute.For<IProcessRunner>();
        var stateStore = Substitute.For<IMotionHighlightStateStore>();
        var motionEvent = new MotionHighlightEvent(new DateTimeOffset(2026, 06, 25, 00, 16, 00, TimeSpan.Zero), 0.2d, "event-low");

        stateStore.LoadAsync(Arg.Any<StorageOptions>(), Arg.Any<CancellationToken>())
            .Returns(MotionHighlightState.Empty);

        var service = new MotionHighlightService(
            cameraCommandFactory,
            cameraStatusService,
            captureStorage,
            fileSystem,
            processRunner,
            stateStore);

        var result = await service.EvaluateAsync(CreateOptions(), motionEvent, CancellationToken.None).ConfigureAwait(true);

        result.Outcome.Should().Be(MotionHighlightOutcome.BelowThreshold);
        cameraCommandFactory.DidNotReceiveWithAnyArgs().BuildStillCaptureCommand(default!, default!);
        await processRunner.DidNotReceiveWithAnyArgs().RunAsync(default!, default).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Evaluate_suppresses_duplicate_event_without_invoking_camera()
    {
        var cameraCommandFactory = Substitute.For<ICameraCommandFactory>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var captureStorage = Substitute.For<ICaptureStorage>();
        var fileSystem = Substitute.For<IFileSystem>();
        var processRunner = Substitute.For<IProcessRunner>();
        var stateStore = Substitute.For<IMotionHighlightStateStore>();
        var occurredAtUtc = new DateTimeOffset(2026, 06, 25, 00, 10, 00, TimeSpan.Zero);

        stateStore.LoadAsync(Arg.Any<StorageOptions>(), Arg.Any<CancellationToken>())
            .Returns(new MotionHighlightState(occurredAtUtc, "event-12", occurredAtUtc));

        var service = new MotionHighlightService(
            cameraCommandFactory,
            cameraStatusService,
            captureStorage,
            fileSystem,
            processRunner,
            stateStore);

        var result = await service.EvaluateAsync(
                CreateOptions(),
                new MotionHighlightEvent(occurredAtUtc, 0.82d, "event-12"),
                CancellationToken.None)
            .ConfigureAwait(true);

        result.Outcome.Should().Be(MotionHighlightOutcome.SuppressedDuplicate);
        cameraCommandFactory.DidNotReceiveWithAnyArgs().BuildStillCaptureCommand(default!, default!);
        await processRunner.DidNotReceiveWithAnyArgs().RunAsync(default!, default).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Evaluate_returns_failure_when_camera_capture_fails()
    {
        var cameraCommandFactory = Substitute.For<ICameraCommandFactory>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var captureStorage = Substitute.For<ICaptureStorage>();
        var fileSystem = Substitute.For<IFileSystem>();
        var processRunner = Substitute.For<IProcessRunner>();
        var stateStore = Substitute.For<IMotionHighlightStateStore>();
        var motionEvent = new MotionHighlightEvent(new DateTimeOffset(2026, 06, 25, 00, 17, 00, TimeSpan.Zero), 0.91d, "event-failed");

        stateStore.LoadAsync(Arg.Any<StorageOptions>(), Arg.Any<CancellationToken>())
            .Returns(MotionHighlightState.Empty);
        fileSystem.GetTemporaryFilePath(".jpg").Returns("temp/failed-highlight.jpg");
        cameraCommandFactory.BuildStillCaptureCommand(Arg.Any<CameraOptions>(), "temp/failed-highlight.jpg")
            .Returns(new ProcessRequest("ffmpeg", "\"temp/failed-highlight.jpg\""));
        processRunner.RunAsync(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, string.Empty, "camera unavailable"));

        var service = new MotionHighlightService(
            cameraCommandFactory,
            cameraStatusService,
            captureStorage,
            fileSystem,
            processRunner,
            stateStore);

        var result = await service.EvaluateAsync(CreateOptions(), motionEvent, CancellationToken.None).ConfigureAwait(true);

        result.Outcome.Should().Be(MotionHighlightOutcome.Failed);
        result.FailureReason.Should().Be(ManualCaptureFailureReasons.CameraUnavailable);
        cameraStatusService.Received(1).ReportDegraded(ManualCaptureFailureReasons.CameraUnavailable);
        await captureStorage.DidNotReceiveWithAnyArgs().StoreAsync(default!, default!, default).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Evaluate_rejects_invalid_motion_scores()
    {
        var service = new MotionHighlightService(
            Substitute.For<ICameraCommandFactory>(),
            Substitute.For<ICameraStatusService>(),
            Substitute.For<ICaptureStorage>(),
            Substitute.For<IFileSystem>(),
            Substitute.For<IProcessRunner>(),
            Substitute.For<IMotionHighlightStateStore>());

        var act = () => service.EvaluateAsync(
            CreateOptions(),
            new MotionHighlightEvent(new DateTimeOffset(2026, 06, 25, 00, 18, 00, TimeSpan.Zero), 0d, "event-invalid"),
            CancellationToken.None);

        await act.Should().ThrowAsync<System.ComponentModel.DataAnnotations.ValidationException>().ConfigureAwait(true);
    }

    private static ShrimpCamOptions CreateOptions(CaptureOptions? capture = null)
    {
        return new ShrimpCamOptions
        {
            Camera = new CameraOptions
            {
                Platform = "Linux",
                Source = "/dev/video0",
            },
            Capture = capture ?? new CaptureOptions
            {
                MotionHighlightsEnabled = true,
                MotionThreshold = 0.4d,
                MotionCooldownSeconds = 300,
            },
            Storage = new StorageOptions
            {
                ImageRootPath = "data/images",
                TimelapseRootPath = "data/timelapse",
                RetentionDays = 30,
            },
        };
    }
}
