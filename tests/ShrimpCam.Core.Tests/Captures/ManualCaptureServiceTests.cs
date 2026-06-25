using NSubstitute;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Captures;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Persistence;
using ShrimpCam.Core.Tests.Cameras;

#pragma warning disable CA2007

namespace ShrimpCam.Core.Tests.Captures;

public sealed class ManualCaptureServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Successful_manual_capture_stores_image_as_manual_source()
    {
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var captureRecordRepository = Substitute.For<ICaptureRecordRepository>();
        var captureStorage = Substitute.For<ICaptureStorage>();
        var clock = Substitute.For<IClock>();
        var fileSystem = Substitute.For<IFileSystem>();
        var processRunner = Substitute.For<IProcessRunner>();
        var options = CreateOptions();
        var stagedPath = "temp/capture.jpg";
        var captureTime = new DateTimeOffset(2026, 06, 24, 23, 15, 00, TimeSpan.Zero);
        var command = new ProcessRequest("ffmpeg", "-args");
        var storedCapture = new StoredCapture(
            "data/images/2026/06/24/manual.jpg",
            "data/images/2026/06/24/manual.json",
            "2026/06/24/manual.jpg",
            "2026/06/24/manual.json",
            "manual.jpg",
            captureTime,
            CaptureSourceTypes.Manual);

        fileSystem.GetTemporaryFilePath(".jpg").Returns(stagedPath);
        commandFactory.BuildStillCaptureCommand(options.Camera, stagedPath).Returns(command);
        processRunner.RunAsync(command, Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, string.Empty, string.Empty));
        clock.UtcNow.Returns(captureTime);
        captureStorage.StoreAsync(
                options.Storage,
                Arg.Is<CaptureStorageRequest>(request =>
                    request.SourceType == CaptureSourceTypes.Manual
                    && request.StagedFilePath == stagedPath
                    && request.CapturedAtUtc == captureTime),
                Arg.Any<CancellationToken>())
            .Returns(storedCapture);

        var service = new ManualCaptureService(commandFactory, new AlwaysAvailableCameraResourceCoordinator(), captureRecordRepository, captureStorage, clock, fileSystem, processRunner);

        var result = await service.CaptureAsync(options, CancellationToken.None).ConfigureAwait(true);

        result.Succeeded.Should().BeTrue();
        result.Capture.Should().Be(storedCapture);
        await captureRecordRepository.Received(1)
            .CreateAsync(
                Arg.Is<CaptureRecord>(record =>
                    record.RelativeImagePath == storedCapture.RelativeImagePath
                    && record.RelativeMetadataPath == storedCapture.RelativeMetadataPath
                    && record.FileName == storedCapture.FileName
                    && record.SourceType == CaptureSourceTypes.Manual
                    && record.CapturedAtUtc == captureTime),
                Arg.Any<CancellationToken>())
            .ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Failed_manual_capture_returns_camera_unavailable_without_persisting_metadata()
    {
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var captureRecordRepository = Substitute.For<ICaptureRecordRepository>();
        var captureStorage = Substitute.For<ICaptureStorage>();
        var clock = Substitute.For<IClock>();
        var fileSystem = Substitute.For<IFileSystem>();
        var processRunner = Substitute.For<IProcessRunner>();
        var options = CreateOptions();
        var stagedPath = "temp/capture.jpg";
        var command = new ProcessRequest("ffmpeg", "-args");

        fileSystem.GetTemporaryFilePath(".jpg").Returns(stagedPath);
        fileSystem.FileExists(stagedPath).Returns(true);
        commandFactory.BuildStillCaptureCommand(options.Camera, stagedPath).Returns(command);
        processRunner.RunAsync(command, Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, string.Empty, "camera unavailable"));

        var service = new ManualCaptureService(commandFactory, new AlwaysAvailableCameraResourceCoordinator(), captureRecordRepository, captureStorage, clock, fileSystem, processRunner);

        var result = await service.CaptureAsync(options, CancellationToken.None).ConfigureAwait(true);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be(ManualCaptureFailureReasons.CameraUnavailable);
        await captureStorage.DidNotReceiveWithAnyArgs()
            .StoreAsync(default!, default!, default)
            .ConfigureAwait(true);
        await captureRecordRepository.DidNotReceiveWithAnyArgs()
            .CreateAsync(default!, default)
            .ConfigureAwait(true);
        fileSystem.Received(1).DeleteFile(stagedPath);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Busy_manual_capture_returns_camera_busy_without_starting_process()
    {
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var captureRecordRepository = Substitute.For<ICaptureRecordRepository>();
        var captureStorage = Substitute.For<ICaptureStorage>();
        var clock = Substitute.For<IClock>();
        var fileSystem = Substitute.For<IFileSystem>();
        var processRunner = Substitute.For<IProcessRunner>();
        var options = CreateOptions();
        var stagedPath = "temp/capture.jpg";

        fileSystem.GetTemporaryFilePath(".jpg").Returns(stagedPath);
        fileSystem.FileExists(stagedPath).Returns(true);

        var service = new ManualCaptureService(commandFactory, new BusyCameraResourceCoordinator(), captureRecordRepository, captureStorage, clock, fileSystem, processRunner);

        var result = await service.CaptureAsync(options, CancellationToken.None).ConfigureAwait(true);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be(ManualCaptureFailureReasons.CameraBusy);
        commandFactory.DidNotReceiveWithAnyArgs().BuildStillCaptureCommand(default!, default!);
        await processRunner.DidNotReceiveWithAnyArgs().RunAsync(default!, default).ConfigureAwait(true);
        await captureStorage.DidNotReceiveWithAnyArgs()
            .StoreAsync(default!, default!, default)
            .ConfigureAwait(true);
        await captureRecordRepository.DidNotReceiveWithAnyArgs()
            .CreateAsync(default!, default)
            .ConfigureAwait(true);
        fileSystem.Received(1).DeleteFile(stagedPath);
    }

    private static ShrimpCamOptions CreateOptions() =>
        new()
        {
            Camera = new CameraOptions
            {
                Platform = CameraPlatforms.Windows,
                Source = "Logitech C920",
            },
            Storage = new StorageOptions
            {
                ImageRootPath = "data/images",
                RetentionDays = 30,
            },
        };
}

#pragma warning restore CA2007
