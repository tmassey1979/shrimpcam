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
    public async Task Shared_stream_can_publish_frames_from_selected_frame_source_provider()
    {
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var processStreamRunner = Substitute.For<IProcessStreamRunner>();
        var provider = new PushFrameSourceProvider();
        var providerRegistry = new StaticFrameSourceProviderRegistry(provider);
        var hub = CreateHub(
            commandFactory,
            cameraStatusService,
            processStreamRunner,
            providerRegistry: providerRegistry);
        var service = new CameraLiveStreamService(hub);

        var subscriptionTask = service.StartAsync(CreateOptions(), CancellationToken.None);
        await EventuallyAsync(() => provider.IsStarted.Should().BeTrue()).ConfigureAwait(true);
        provider.Publish([0xFF, 0xD8, 0xCA, 0xFE, 0xFF, 0xD9]);
        var subscription = await subscriptionTask.ConfigureAwait(true);

        subscription.Succeeded.Should().BeTrue();
        await processStreamRunner.DidNotReceiveWithAnyArgs().StartAsync(default!, default).ConfigureAwait(true);

        var buffer = new byte[512];
        var bytesRead = await subscription.Session!.Content.ReadAsync(buffer, CancellationToken.None).ConfigureAwait(true);
        buffer.AsSpan(0, bytesRead).ToArray().Should().ContainInOrder([0xCA, 0xFE]);

        await subscription.Session!.DisposeAsync().ConfigureAwait(true);
        await hub.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Shared_stream_records_provider_frames_for_timelapse_consumers()
    {
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var processStreamRunner = Substitute.For<IProcessStreamRunner>();
        var frameStore = new LiveFrameSnapshotStore();
        var provider = new PushFrameSourceProvider();
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jpg");
        var providerRegistry = new StaticFrameSourceProviderRegistry(provider);
        var hub = CreateHub(
            commandFactory,
            cameraStatusService,
            processStreamRunner,
            frameStore,
            providerRegistry: providerRegistry);

        try
        {
            var subscriptionTask = hub.SubscribeAsync(CreateOptions(), CancellationToken.None);
            await EventuallyAsync(() => provider.IsStarted.Should().BeTrue()).ConfigureAwait(true);
            provider.Publish([0xFF, 0xD8, 0x41, 0x42, 0xFF, 0xD9]);
            var subscription = await subscriptionTask.ConfigureAwait(true);

            subscription.Succeeded.Should().BeTrue();
            (await frameStore.TryWriteLatestFrameAsync(outputPath, CancellationToken.None).ConfigureAwait(true))
                .Should()
                .BeTrue();
            File.ReadAllBytes(outputPath).Should().Equal([0xFF, 0xD8, 0x41, 0x42, 0xFF, 0xD9]);
            await processStreamRunner.DidNotReceiveWithAnyArgs().StartAsync(default!, default).ConfigureAwait(true);

            await subscription.Session!.DisposeAsync().ConfigureAwait(true);
        }
        finally
        {
            await hub.DisposeAsync().ConfigureAwait(true);
            await DeleteIfExistsAsync(outputPath).ConfigureAwait(true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Shared_stream_restarts_provider_when_camera_settings_change()
    {
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var processStreamRunner = Substitute.For<IProcessStreamRunner>();
        var provider = new RestartTrackingFrameSourceProvider();
        var providerRegistry = new StaticFrameSourceProviderRegistry(provider);
        var hub = CreateHub(
            commandFactory,
            cameraStatusService,
            processStreamRunner,
            providerRegistry: providerRegistry);
        var initialOptions = CreateOptions(source: "Camera A", streamWidth: 1280, streamHeight: 720);
        var updatedOptions = CreateOptions(source: "Camera B", streamWidth: 640, streamHeight: 480);

        await hub.EnsureRunningAsync(initialOptions, CancellationToken.None).ConfigureAwait(true);
        await EventuallyAsync(() => provider.StartedOptions.Should().ContainSingle()).ConfigureAwait(true);
        var firstRun = provider.Runs.Single();

        await hub.EnsureRunningAsync(updatedOptions, CancellationToken.None).ConfigureAwait(true);
        await EventuallyAsync(() => provider.StartedOptions.Should().HaveCount(2)).ConfigureAwait(true);

        firstRun.IsCancellationRequested.Should().BeTrue();
        provider.StartedOptions[0].Source.Should().Be("Camera A");
        provider.StartedOptions[1].Source.Should().Be("Camera B");
        provider.StartedOptions[1].StreamWidth.Should().Be(640);
        provider.StartedOptions[1].StreamHeight.Should().Be(480);
        await processStreamRunner.DidNotReceiveWithAnyArgs().StartAsync(default!, default).ConfigureAwait(true);

        await hub.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Shared_stream_reports_online_when_provider_fails_but_process_fallback_streams()
    {
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var processStreamRunner = Substitute.For<IProcessStreamRunner>();
        var providerRegistry = new StaticFrameSourceProviderRegistry(
            new DegradingFrameSourceProvider(cameraStatusService, requiresExternalProcess: true));
        var stream = new BlockingAppendStream();
        var processStream = new StubProcessStream(stream, new ProcessResult(0, string.Empty, string.Empty));
        var command = new ProcessRequest("ffmpeg", "-stream");

        commandFactory.BuildLiveStreamCommand(Arg.Any<CameraOptions>()).Returns(command);
        processStreamRunner.StartAsync(command, Arg.Any<CancellationToken>()).Returns(processStream);

        var hub = CreateHub(
            commandFactory,
            cameraStatusService,
            processStreamRunner,
            providerRegistry: providerRegistry);
        var service = new CameraLiveStreamService(hub);

        var subscriptionTask = service.StartAsync(CreateOptions(), CancellationToken.None);
        await stream.AppendFrameAsync("fallback-frame").ConfigureAwait(true);
        var subscription = await subscriptionTask.ConfigureAwait(true);

        subscription.Succeeded.Should().BeTrue();
        await processStreamRunner.Received(1).StartAsync(command, Arg.Any<CancellationToken>()).ConfigureAwait(true);
        cameraStatusService.Received(1).ReportDegraded("provider-startup-failed");
        cameraStatusService.Received(1).ReportOnline();

        await subscription.Session!.DisposeAsync().ConfigureAwait(true);
        await hub.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Shared_stream_does_not_launch_process_fallback_for_native_provider_failure()
    {
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var processStreamRunner = Substitute.For<IProcessStreamRunner>();
        var providerRegistry = new StaticFrameSourceProviderRegistry(new DegradingFrameSourceProvider(cameraStatusService));
        var hub = CreateHub(
            commandFactory,
            cameraStatusService,
            processStreamRunner,
            providerRegistry: providerRegistry);
        var service = new CameraLiveStreamService(hub);

        var subscription = await service.StartAsync(CreateOptions(), CancellationToken.None).ConfigureAwait(true);

        subscription.Succeeded.Should().BeFalse();
        await processStreamRunner.DidNotReceiveWithAnyArgs().StartAsync(default!, default).ConfigureAwait(true);
        cameraStatusService.Received().ReportDegraded("provider-startup-failed");

        await hub.DisposeAsync().ConfigureAwait(true);
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
        ICameraResourceCoordinator? resourceCoordinator = null,
        ICameraFrameSourceProviderRegistry? providerRegistry = null) =>
        new(
            providerRegistry ?? new LegacyFallbackFrameSourceProviderRegistry(),
            commandFactory,
            resourceCoordinator ?? new AlwaysAvailableCameraResourceCoordinator(),
            cameraStatusService,
            frameStore ?? new LiveFrameSnapshotStore(),
            processStreamRunner,
            NullLogger<SharedCameraStreamHub>.Instance);

    private static CameraOptions CreateOptions(
        int reconnectBackoffSeconds = 1,
        int reconnectRetryAttempts = 2,
        string source = "Logi C270 HD WebCam",
        int streamWidth = 1280,
        int streamHeight = 720) =>
        new()
        {
            Platform = CameraPlatforms.Windows,
            Source = source,
            StreamWidth = streamWidth,
            StreamHeight = streamHeight,
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

    private sealed class LegacyFallbackFrameSourceProviderRegistry : ICameraFrameSourceProviderRegistry
    {
        private readonly ICameraFrameSourceProvider _provider = new LegacyFallbackFrameSourceProvider();

        public ICameraFrameSourceProvider GetProvider(CameraOptions options, string hostPlatform) => _provider;

        public IReadOnlyList<CameraFrameSourceProviderDescriptor> ListProviders() => [_provider.Descriptor];
    }

    private sealed class LegacyFallbackFrameSourceProvider : ICameraFrameSourceProvider
    {
        public CameraFrameSourceProviderDescriptor Descriptor { get; } = new(
            "legacy-test-fallback",
            "Legacy test fallback",
            CameraPlatforms.Windows,
            IsPrimary: false,
            RequiresExternalProcess: true,
            "legacy-test-fallback");

        public CameraFrameSourceStartResult Start(
            CameraOptions options,
            Action<ReadOnlyMemory<byte>> publishFrame,
            CancellationToken cancellationToken) =>
            CameraFrameSourceStartResult.Failure("legacyFallbackRequested");
    }

    private sealed class StaticFrameSourceProviderRegistry(ICameraFrameSourceProvider provider) : ICameraFrameSourceProviderRegistry
    {
        public ICameraFrameSourceProvider GetProvider(CameraOptions options, string hostPlatform) => provider;

        public IReadOnlyList<CameraFrameSourceProviderDescriptor> ListProviders() => [provider.Descriptor];
    }

    private sealed class PushFrameSourceProvider : ICameraFrameSourceProvider
    {
        private Action<ReadOnlyMemory<byte>>? _publishFrame;
        private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsStarted => _publishFrame is not null;

        public CameraFrameSourceProviderDescriptor Descriptor { get; } = new(
            "push-test-provider",
            "Push test provider",
            CameraPlatforms.Windows,
            IsPrimary: true,
            RequiresExternalProcess: false,
            "push-test-provider");

        public CameraFrameSourceStartResult Start(
            CameraOptions options,
            Action<ReadOnlyMemory<byte>> publishFrame,
            CancellationToken cancellationToken)
        {
            _publishFrame = publishFrame;
            cancellationToken.Register(() => _stopped.TrySetResult());
            return CameraFrameSourceStartResult.Success(_stopped.Task);
        }

        public void Publish(byte[] frame) => _publishFrame?.Invoke(frame);
    }

    private sealed class RestartTrackingFrameSourceProvider : ICameraFrameSourceProvider
    {
        public List<CameraOptions> StartedOptions { get; } = [];

        public List<ProviderRun> Runs { get; } = [];

        public CameraFrameSourceProviderDescriptor Descriptor { get; } = new(
            "restart-tracking-provider",
            "Restart tracking provider",
            CameraPlatforms.Windows,
            IsPrimary: true,
            RequiresExternalProcess: false,
            "restart-tracking-provider");

        public CameraFrameSourceStartResult Start(
            CameraOptions options,
            Action<ReadOnlyMemory<byte>> publishFrame,
            CancellationToken cancellationToken)
        {
            var run = new ProviderRun();
            StartedOptions.Add(options);
            Runs.Add(run);
            cancellationToken.Register(() => run.IsCancellationRequested = true);
            return CameraFrameSourceStartResult.Success(Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));
        }
    }

    private sealed class ProviderRun
    {
        public bool IsCancellationRequested { get; set; }
    }

    private sealed class DegradingFrameSourceProvider(
        ICameraStatusService cameraStatusService,
        bool requiresExternalProcess = false) : ICameraFrameSourceProvider
    {
        public CameraFrameSourceProviderDescriptor Descriptor { get; } = new(
            "degrading-test-provider",
            "Degrading test provider",
            CameraPlatforms.Windows,
            IsPrimary: true,
            RequiresExternalProcess: requiresExternalProcess,
            "degrading-test-provider");

        public CameraFrameSourceStartResult Start(
            CameraOptions options,
            Action<ReadOnlyMemory<byte>> publishFrame,
            CancellationToken cancellationToken)
        {
            cameraStatusService.ReportDegraded("provider-startup-failed");
            return CameraFrameSourceStartResult.Failure("provider-startup-failed");
        }
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
