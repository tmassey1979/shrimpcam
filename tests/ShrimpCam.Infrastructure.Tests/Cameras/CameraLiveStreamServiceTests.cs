using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Infrastructure.Cameras;

#pragma warning disable CA2007

namespace ShrimpCam.Infrastructure.Tests.Cameras;

public sealed class CameraLiveStreamServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Multiple_subscribers_share_one_camera_process()
    {
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var processStreamRunner = Substitute.For<IProcessStreamRunner>();
        var stream = new BlockingAppendStream();
        var processStream = new StubProcessStream(stream, new ProcessResult(0, string.Empty, string.Empty));
        var options = CreateOptions();
        var command = new ProcessRequest("ffmpeg", "-stream");

        commandFactory.BuildLiveStreamCommand(Arg.Any<CameraOptions>()).Returns(command);
        processStreamRunner.StartAsync(command, Arg.Any<CancellationToken>()).Returns(processStream);

        var hub = CreateHub(commandFactory, cameraStatusService, processStreamRunner);
        var service = new CameraLiveStreamService(hub);

        var firstSubscriptionTask = service.StartAsync(options, CancellationToken.None);
        await stream.AppendFrameAsync("frame-01").ConfigureAwait(true);
        var firstSubscription = await firstSubscriptionTask.ConfigureAwait(true);

        var secondSubscriptionTask = service.StartAsync(options, CancellationToken.None);
        await Task.Delay(50).ConfigureAwait(true);
        await stream.AppendFrameAsync("frame-02").ConfigureAwait(true);
        var secondSubscription = await secondSubscriptionTask.ConfigureAwait(true);

        firstSubscription.Succeeded.Should().BeTrue();
        secondSubscription.Succeeded.Should().BeTrue();
        cameraStatusService.Received(1).ReportOnline();

        await firstSubscription.Session!.DisposeAsync().ConfigureAwait(true);
        await secondSubscription.Session!.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Shared_stream_feeds_latest_frame_cache_without_requiring_new_viewer_processes()
    {
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var frameStore = new LiveFrameSnapshotStore();
        var processStreamRunner = Substitute.For<IProcessStreamRunner>();
        var stream = new BlockingAppendStream();
        var processStream = new StubProcessStream(stream, new ProcessResult(0, string.Empty, string.Empty));
        var options = CreateOptions();
        var command = new ProcessRequest("ffmpeg", "-stream");
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jpg");
        var jpeg = new byte[] { 0xFF, 0xD8, 0x01, 0x02, 0xFF, 0xD9 };
        var hub = CreateHub(commandFactory, cameraStatusService, processStreamRunner, frameStore);

        try
        {
            commandFactory.BuildLiveStreamCommand(Arg.Any<CameraOptions>()).Returns(command);
            processStreamRunner.StartAsync(command, Arg.Any<CancellationToken>()).Returns(processStream);

            await hub.EnsureRunningAsync(options, CancellationToken.None).ConfigureAwait(true);
            await stream.AppendAsync(jpeg).ConfigureAwait(true);

            await EventuallyAsync(async () =>
            {
                (await frameStore.TryWriteLatestFrameAsync(outputPath, CancellationToken.None).ConfigureAwait(true))
                    .Should()
                    .BeTrue();
                File.Exists(outputPath).Should().BeTrue();
                File.ReadAllBytes(outputPath).Should().Equal(jpeg);
            }).ConfigureAwait(true);
            await processStreamRunner.Received(1).StartAsync(command, Arg.Any<CancellationToken>()).ConfigureAwait(true);
        }
        finally
        {
            await hub.DisposeAsync().ConfigureAwait(true);
            await DeleteIfExistsAsync(outputPath).ConfigureAwait(true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Subscriber_cancellation_token_is_not_used_to_control_shared_camera_process()
    {
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var processStreamRunner = Substitute.For<IProcessStreamRunner>();
        var stream = new BlockingAppendStream();
        var processStream = new StubProcessStream(stream, new ProcessResult(0, string.Empty, string.Empty));
        var options = CreateOptions();
        var command = new ProcessRequest("ffmpeg", "-stream");
        var processCancellationToken = CancellationToken.None;
        using var viewerCancellation = new CancellationTokenSource();

        commandFactory.BuildLiveStreamCommand(Arg.Any<CameraOptions>()).Returns(command);
        processStreamRunner
            .StartAsync(command, Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                processCancellationToken = call.ArgAt<CancellationToken>(1);
                return processStream;
            });

        var hub = CreateHub(commandFactory, cameraStatusService, processStreamRunner);
        var service = new CameraLiveStreamService(hub);

        var firstSubscriptionTask = service.StartAsync(options, viewerCancellation.Token);
        await stream.AppendFrameAsync("frame-01").ConfigureAwait(true);
        var firstSubscription = await firstSubscriptionTask.ConfigureAwait(true);

        viewerCancellation.Cancel();

        firstSubscription.Succeeded.Should().BeTrue();
        processCancellationToken.IsCancellationRequested.Should().BeFalse();
        await processStreamRunner.Received(1).StartAsync(command, Arg.Any<CancellationToken>()).ConfigureAwait(true);

        await firstSubscription.Session!.DisposeAsync().ConfigureAwait(true);
        await hub.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Subscriber_disconnect_does_not_stop_shared_camera_process()
    {
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var processStreamRunner = Substitute.For<IProcessStreamRunner>();
        var stream = new BlockingAppendStream();
        var processStream = new StubProcessStream(stream, new ProcessResult(0, string.Empty, string.Empty));
        var options = CreateOptions();
        var command = new ProcessRequest("ffmpeg", "-stream");

        commandFactory.BuildLiveStreamCommand(Arg.Any<CameraOptions>()).Returns(command);
        processStreamRunner.StartAsync(command, Arg.Any<CancellationToken>()).Returns(processStream);

        var hub = CreateHub(commandFactory, cameraStatusService, processStreamRunner);
        var service = new CameraLiveStreamService(hub);

        var subscriptionTask = service.StartAsync(options, CancellationToken.None);
        await stream.AppendFrameAsync("frame-01").ConfigureAwait(true);
        var subscription = await subscriptionTask.ConfigureAwait(true);
        subscription.Succeeded.Should().BeTrue();

        await subscription.Session!.DisposeAsync().ConfigureAwait(true);
        await stream.AppendFrameAsync("frame-02").ConfigureAwait(true);

        await processStreamRunner.Received(1).StartAsync(command, Arg.Any<CancellationToken>()).ConfigureAwait(true);
        cameraStatusService.Received(1).ReportOnline();

        await hub.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Shared_stream_reconnects_without_closing_existing_subscribers()
    {
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var processStreamRunner = Substitute.For<IProcessStreamRunner>();
        var firstStream = new BlockingAppendStream();
        var secondStream = new BlockingAppendStream();
        var processStreams = new Queue<IProcessStreamHandle>(
            [
                new StubProcessStream(firstStream, new ProcessResult(0, string.Empty, string.Empty)),
                new StubProcessStream(secondStream, new ProcessResult(0, string.Empty, string.Empty)),
            ]);
        var options = CreateOptions(reconnectBackoffSeconds: 0);
        var command = new ProcessRequest("ffmpeg", "-stream");

        commandFactory.BuildLiveStreamCommand(Arg.Any<CameraOptions>()).Returns(command);
        processStreamRunner
            .StartAsync(command, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(processStreams.Dequeue()));

        var hub = CreateHub(commandFactory, cameraStatusService, processStreamRunner);
        var service = new CameraLiveStreamService(hub);

        var subscriptionTask = service.StartAsync(options, CancellationToken.None);
        await firstStream.AppendFrameAsync("frame-01").ConfigureAwait(true);
        var subscription = await subscriptionTask.ConfigureAwait(true);
        subscription.Succeeded.Should().BeTrue();
        var buffer = new byte[512];
        var firstBytesRead = await subscription.Session!.Content.ReadAsync(buffer, CancellationToken.None).ConfigureAwait(true);
        Encoding.ASCII.GetString(buffer, 0, firstBytesRead).Should().Contain("frame-01");

        firstStream.Dispose();
        await EventuallyAsync(async () =>
        {
            await processStreamRunner.Received(2).StartAsync(command, Arg.Any<CancellationToken>()).ConfigureAwait(true);
        }).ConfigureAwait(true);
        await secondStream.AppendFrameAsync("frame-02").ConfigureAwait(true);

        var bytesRead = await subscription.Session!.Content.ReadAsync(buffer, CancellationToken.None).ConfigureAwait(true);
        var payload = Encoding.ASCII.GetString(buffer, 0, bytesRead);

        payload.Should().Contain("frame-02");

        await subscription.Session!.DisposeAsync().ConfigureAwait(true);
        await hub.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Busy_camera_returns_failure_without_starting_process()
    {
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var processStreamRunner = Substitute.For<IProcessStreamRunner>();
        var hub = CreateHub(
            commandFactory,
            cameraStatusService,
            processStreamRunner,
            resourceCoordinator: new BusyCameraResourceCoordinator());
        var service = new CameraLiveStreamService(hub);

        var result = await service.StartAsync(CreateOptions(), CancellationToken.None).ConfigureAwait(true);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be(CameraLiveStreamFailureReasons.CameraBusy);
        await processStreamRunner.DidNotReceiveWithAnyArgs().StartAsync(default!, default).ConfigureAwait(true);
        cameraStatusService.Received(1).ReportDegraded(CameraLiveStreamFailureReasons.CameraBusy);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Startup_read_timeout_reports_camera_unavailable_instead_of_cancellation_text()
    {
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var processStreamRunner = Substitute.For<IProcessStreamRunner>();
        var options = CreateOptions(reconnectRetryAttempts: 0);
        var command = new ProcessRequest("ffmpeg", "-stream");

        commandFactory.BuildLiveStreamCommand(Arg.Any<CameraOptions>()).Returns(command);
        processStreamRunner
            .StartAsync(command, Arg.Any<CancellationToken>())
            .Returns(new StubProcessStream(new CanceledReadStream(), new ProcessResult(1, string.Empty, "startup timed out")));

        var hub = CreateHub(commandFactory, cameraStatusService, processStreamRunner);
        var service = new CameraLiveStreamService(hub);

        var result = await service.StartAsync(options, CancellationToken.None).ConfigureAwait(true);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be(CameraLiveStreamFailureReasons.CameraUnavailable);
        cameraStatusService.Received(1).ReportDegraded(CameraLiveStreamFailureReasons.CameraUnavailable);
    }

    private static SharedCameraStreamHub CreateHub(
        ICameraCommandFactory commandFactory,
        ICameraStatusService cameraStatusService,
        IProcessStreamRunner processStreamRunner,
        ILiveFrameSnapshotStore? frameStore = null,
        ICameraResourceCoordinator? resourceCoordinator = null) =>
        new(
            commandFactory,
            resourceCoordinator ?? new AlwaysAvailableCameraResourceCoordinator(),
            cameraStatusService,
            frameStore ?? new LiveFrameSnapshotStore(),
            processStreamRunner,
            NullLogger<SharedCameraStreamHub>.Instance);

    private static CameraOptions CreateOptions(int reconnectBackoffSeconds = 1, int reconnectRetryAttempts = 2) =>
        new()
        {
            Platform = CameraPlatforms.Windows,
            Source = "Logi C270 HD WebCam",
            StreamWidth = 1280,
            StreamHeight = 720,
            StreamFramesPerSecond = 15,
            ReconnectRetryAttempts = reconnectRetryAttempts,
            ReconnectBackoffSeconds = reconnectBackoffSeconds,
        };

    private static async Task EventuallyAsync(Action assertion)
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                assertion();
                return;
            }
            catch (Exception exception)
            {
                lastException = exception;
                await Task.Delay(50).ConfigureAwait(true);
            }
        }

        throw lastException ?? new InvalidOperationException("Assertion did not complete.");
    }

    private static async Task EventuallyAsync(Func<Task> assertion)
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                await assertion().ConfigureAwait(true);
                return;
            }
            catch (Exception exception)
            {
                lastException = exception;
                await Task.Delay(50).ConfigureAwait(true);
            }
        }

        throw lastException ?? new InvalidOperationException("Assertion did not complete.");
    }

    private static async Task DeleteIfExistsAsync(string path)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return;
            }
            catch (IOException) when (attempt < 9)
            {
                await Task.Delay(50).ConfigureAwait(true);
            }
        }
    }

    private sealed class AlwaysAvailableCameraResourceCoordinator : ICameraResourceCoordinator
    {
        public ValueTask<CameraResourceLease?> TryAcquireAsync(string owner, CancellationToken cancellationToken) =>
            ValueTask.FromResult<CameraResourceLease?>(new CameraResourceLease(owner));
    }

    private sealed class BusyCameraResourceCoordinator : ICameraResourceCoordinator
    {
        public ValueTask<CameraResourceLease?> TryAcquireAsync(string owner, CancellationToken cancellationToken) =>
            ValueTask.FromResult<CameraResourceLease?>(null);
    }

    private sealed class StubProcessStream(Stream standardOutput, ProcessResult exitResult) : IProcessStreamHandle
    {
        public Stream StandardOutput { get; } = standardOutput;

        public Task<ProcessResult> WaitForExitAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(exitResult);
        }

        public ValueTask DisposeAsync() => StandardOutput.DisposeAsync();
    }

    private sealed class BlockingAppendStream : Stream
    {
        private readonly Queue<byte> _buffer = new();
        private readonly SemaphoreSlim _availableBytes = new(0);
        private bool _disposed;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public Task AppendAsync(string value) => AppendAsync(Encoding.ASCII.GetBytes(value));

        public Task AppendFrameAsync(string value)
        {
            var body = Encoding.ASCII.GetBytes(value);
            return AppendAsync([0xFF, 0xD8, .. body, 0xFF, 0xD9]);
        }

        public Task AppendAsync(byte[] bytes)
        {
            lock (_buffer)
            {
                foreach (var value in bytes)
                {
                    _buffer.Enqueue(value);
                    _availableBytes.Release();
                }
            }

            return Task.CompletedTask;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

        public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                return 0;
            }

            await _availableBytes.WaitAsync(cancellationToken).ConfigureAwait(false);

            var written = 0;
            lock (_buffer)
            {
                while (written < destination.Length && _buffer.Count > 0)
                {
                    destination.Span[written++] = _buffer.Dequeue();
                }
            }

            return written;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            _disposed = true;
            _availableBytes.Release();
            base.Dispose(disposing);
        }
    }

    private sealed class CanceledReadStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new OperationCanceledException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new OperationCanceledException());

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Task.FromException<int>(new OperationCanceledException());

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

#pragma warning restore CA2007
