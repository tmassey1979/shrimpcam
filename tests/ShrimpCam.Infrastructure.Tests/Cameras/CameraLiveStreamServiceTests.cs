using System.Text;
using Microsoft.Extensions.Logging;
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
    public async Task Start_async_returns_session_when_process_produces_stream_bytes()
    {
        var asyncDelay = Substitute.For<IAsyncDelay>();
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var processStreamRunner = Substitute.For<IProcessStreamRunner>();
        var processStream = new StubProcessStream(
            new MemoryStream(Encoding.ASCII.GetBytes($"--{LiveStreamConstants.Boundary}\r\nContent-Type: image/jpeg\r\n\r\nframe-01\r\n")),
            new ProcessResult(0, string.Empty, string.Empty));
        var options = CreateOptions();

        commandFactory.BuildLiveStreamCommand(options).Returns(new ProcessRequest("ffmpeg", "-stream"));
        processStreamRunner.StartAsync(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(processStream);

        var service = new CameraLiveStreamService(
            asyncDelay,
            commandFactory,
            cameraStatusService,
            processStreamRunner,
            new ListLogger<CameraLiveStreamService>());

        var result = await service.StartAsync(options, CancellationToken.None).ConfigureAwait(true);

        result.Succeeded.Should().BeTrue();
        result.Session.Should().NotBeNull();
        result.Session!.ContentType.Should().Be(LiveStreamConstants.ContentType);

        var buffer = new byte[128];
        var bytesRead = result.Session.Content.Read(buffer, 0, buffer.Length);
        var payload = Encoding.ASCII.GetString(buffer, 0, bytesRead);
        payload.Should().Contain("frame-01");
        cameraStatusService.Received(1).ReportOnline();

        await result.Session.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Start_async_returns_failure_and_logs_when_process_exits_without_stream_data()
    {
        var asyncDelay = Substitute.For<IAsyncDelay>();
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var processStreamRunner = Substitute.For<IProcessStreamRunner>();
        var logger = new ListLogger<CameraLiveStreamService>();
        var processStream = new StubProcessStream(
            new MemoryStream(Array.Empty<byte>()),
            new ProcessResult(1, string.Empty, "camera unavailable"));
        var options = CreateOptions(reconnectRetryAttempts: 0);

        commandFactory.BuildLiveStreamCommand(options).Returns(new ProcessRequest("ffmpeg", "-stream"));
        processStreamRunner.StartAsync(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(processStream);

        var service = new CameraLiveStreamService(asyncDelay, commandFactory, cameraStatusService, processStreamRunner, logger);

        var result = await service.StartAsync(options, CancellationToken.None).ConfigureAwait(true);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be(CameraLiveStreamFailureReasons.CameraUnavailable);
        cameraStatusService.Received(1).ReportDegraded("camera unavailable");
        logger.Entries.Should().ContainSingle(entry =>
            entry.LogLevel == LogLevel.Warning
            && entry.Message.Contains("camera unavailable", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Start_async_returns_failure_when_startup_times_out()
    {
        var asyncDelay = Substitute.For<IAsyncDelay>();
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var processStreamRunner = Substitute.For<IProcessStreamRunner>();
        var logger = new ListLogger<CameraLiveStreamService>();
        var processStream = new TimeoutProcessStream();
        var options = CreateOptions();

        commandFactory.BuildLiveStreamCommand(options).Returns(new ProcessRequest("ffmpeg", "-stream"));
        processStreamRunner.StartAsync(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(processStream);

        var service = new CameraLiveStreamService(asyncDelay, commandFactory, cameraStatusService, processStreamRunner, logger);

        var result = await service.StartAsync(options, CancellationToken.None).ConfigureAwait(true);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be(CameraLiveStreamFailureReasons.CameraUnavailable);
        processStream.Disposed.Should().BeTrue();
        cameraStatusService.Received(1).ReportDegraded("Timed out waiting for live stream data.");
        logger.Entries.Should().ContainSingle(entry =>
            entry.LogLevel == LogLevel.Warning
            && entry.Message.Contains("Timed out", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Dropped_stream_reconnects_when_camera_returns_within_retry_window()
    {
        var asyncDelay = Substitute.For<IAsyncDelay>();
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var processStreamRunner = Substitute.For<IProcessStreamRunner>();
        var logger = new ListLogger<CameraLiveStreamService>();
        var options = CreateOptions();
        var firstStream = new StubProcessStream(
            new MemoryStream(Encoding.ASCII.GetBytes($"--{LiveStreamConstants.Boundary}\r\nContent-Type: image/jpeg\r\n\r\nframe-01\r\n")),
            new ProcessResult(0, string.Empty, string.Empty));
        var secondStream = new StubProcessStream(
            new MemoryStream(Encoding.ASCII.GetBytes($"--{LiveStreamConstants.Boundary}\r\nContent-Type: image/jpeg\r\n\r\nframe-02\r\n")),
            new ProcessResult(0, string.Empty, string.Empty));

        commandFactory.BuildLiveStreamCommand(options).Returns(new ProcessRequest("ffmpeg", "-stream"));
        processStreamRunner.StartAsync(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(firstStream, secondStream);

        var service = new CameraLiveStreamService(asyncDelay, commandFactory, cameraStatusService, processStreamRunner, logger);

        var result = await service.StartAsync(options, CancellationToken.None).ConfigureAwait(true);
        var payload = await ReadUntilAsync(result.Session!.Content, "frame-02").ConfigureAwait(true);

        payload.Should().Contain("frame-01");
        payload.Should().Contain("frame-02");
        await asyncDelay.Received(1)
            .DelayAsync(TimeSpan.FromSeconds(1), Arg.Any<CancellationToken>())
            .ConfigureAwait(true);
        cameraStatusService.Received(2).ReportOnline();

        await result.Session.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Repeated_reconnect_failures_mark_camera_degraded_without_runaway_retries()
    {
        var asyncDelay = Substitute.For<IAsyncDelay>();
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var cameraStatusService = Substitute.For<ICameraStatusService>();
        var processStreamRunner = Substitute.For<IProcessStreamRunner>();
        var logger = new ListLogger<CameraLiveStreamService>();
        var options = CreateOptions();
        var firstStream = new StubProcessStream(
            new MemoryStream(Encoding.ASCII.GetBytes($"--{LiveStreamConstants.Boundary}\r\nContent-Type: image/jpeg\r\n\r\nframe-01\r\n")),
            new ProcessResult(0, string.Empty, string.Empty));
        var failedReconnect1 = new StubProcessStream(new MemoryStream(Array.Empty<byte>()), new ProcessResult(1, string.Empty, "camera unavailable"));
        var failedReconnect2 = new StubProcessStream(new MemoryStream(Array.Empty<byte>()), new ProcessResult(1, string.Empty, "camera unavailable"));

        commandFactory.BuildLiveStreamCommand(options).Returns(new ProcessRequest("ffmpeg", "-stream"));
        processStreamRunner.StartAsync(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(firstStream, failedReconnect1, failedReconnect2);

        var service = new CameraLiveStreamService(asyncDelay, commandFactory, cameraStatusService, processStreamRunner, logger);

        var result = await service.StartAsync(options, CancellationToken.None).ConfigureAwait(true);
        using var reader = new StreamReader(result.Session!.Content, Encoding.ASCII, leaveOpen: true);
        var payload = await reader.ReadToEndAsync().ConfigureAwait(true);

        payload.Should().Contain("frame-01");
        await asyncDelay.Received(2).DelayAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).ConfigureAwait(true);
        cameraStatusService.Received(1).ReportDegraded("camera unavailable");
        logger.Entries.Count(entry => entry.Message.Contains("Attempting to reconnect", StringComparison.Ordinal)).Should().Be(2);
        logger.Entries.Count(entry => entry.Message.Contains("reconnect attempts were exhausted", StringComparison.OrdinalIgnoreCase)).Should().Be(1);

        await result.Session.DisposeAsync().ConfigureAwait(true);
    }

    private static CameraOptions CreateOptions(int reconnectRetryAttempts = 2) =>
        new()
        {
            Platform = CameraPlatforms.Linux,
            Source = "/dev/video0",
            ReconnectRetryAttempts = reconnectRetryAttempts,
            ReconnectBackoffSeconds = 1,
        };

    private static async Task<string> ReadUntilAsync(Stream stream, string marker)
    {
        var buffer = new byte[128];
        var builder = new StringBuilder();

        while (!builder.ToString().Contains(marker, StringComparison.Ordinal))
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), CancellationToken.None).ConfigureAwait(true);
            if (bytesRead == 0)
            {
                break;
            }

            builder.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));
        }

        return builder.ToString();
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

    private sealed class TimeoutProcessStream : IProcessStreamHandle
    {
        public bool Disposed { get; private set; }

        public Stream StandardOutput { get; } = new TimeoutStream();

        public Task<ProcessResult> WaitForExitAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ProcessResult(1, string.Empty, "timed out"));

        public async ValueTask DisposeAsync()
        {
            Disposed = true;
            await StandardOutput.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class TimeoutStream : Stream
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

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return 0;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return 0;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<(LogLevel LogLevel, string Message)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull =>
            NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
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
