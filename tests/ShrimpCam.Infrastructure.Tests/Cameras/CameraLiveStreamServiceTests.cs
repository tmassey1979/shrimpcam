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
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var processStreamRunner = Substitute.For<IProcessStreamRunner>();
        var processStream = new StubProcessStream(
            new MemoryStream(Encoding.ASCII.GetBytes($"--{LiveStreamConstants.Boundary}\r\nContent-Type: image/jpeg\r\n\r\nframe-01\r\n")),
            new ProcessResult(0, string.Empty, string.Empty));
        var options = new CameraOptions
        {
            Platform = CameraPlatforms.Linux,
            Source = "/dev/video0",
        };

        commandFactory.BuildLiveStreamCommand(options).Returns(new ProcessRequest("ffmpeg", "-stream"));
        processStreamRunner.StartAsync(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(processStream);

        var service = new CameraLiveStreamService(
            commandFactory,
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

        await result.Session.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Start_async_returns_failure_and_logs_when_process_exits_without_stream_data()
    {
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var processStreamRunner = Substitute.For<IProcessStreamRunner>();
        var logger = new ListLogger<CameraLiveStreamService>();
        var processStream = new StubProcessStream(
            new MemoryStream(Array.Empty<byte>()),
            new ProcessResult(1, string.Empty, "camera unavailable"));
        var options = new CameraOptions
        {
            Platform = CameraPlatforms.Linux,
            Source = "/dev/video0",
        };

        commandFactory.BuildLiveStreamCommand(options).Returns(new ProcessRequest("ffmpeg", "-stream"));
        processStreamRunner.StartAsync(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(processStream);

        var service = new CameraLiveStreamService(commandFactory, processStreamRunner, logger);

        var result = await service.StartAsync(options, CancellationToken.None).ConfigureAwait(true);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be(CameraLiveStreamFailureReasons.CameraUnavailable);
        logger.Entries.Should().ContainSingle(entry =>
            entry.LogLevel == LogLevel.Warning
            && entry.Message.Contains("camera unavailable", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Start_async_returns_failure_when_startup_times_out()
    {
        var commandFactory = Substitute.For<ICameraCommandFactory>();
        var processStreamRunner = Substitute.For<IProcessStreamRunner>();
        var logger = new ListLogger<CameraLiveStreamService>();
        var processStream = new TimeoutProcessStream();
        var options = new CameraOptions
        {
            Platform = CameraPlatforms.Linux,
            Source = "/dev/video0",
        };

        commandFactory.BuildLiveStreamCommand(options).Returns(new ProcessRequest("ffmpeg", "-stream"));
        processStreamRunner.StartAsync(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(processStream);

        var service = new CameraLiveStreamService(commandFactory, processStreamRunner, logger);

        var result = await service.StartAsync(options, CancellationToken.None).ConfigureAwait(true);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be(CameraLiveStreamFailureReasons.CameraUnavailable);
        processStream.Disposed.Should().BeTrue();
        logger.Entries.Should().ContainSingle(entry =>
            entry.LogLevel == LogLevel.Warning
            && entry.Message.Contains("Timed out", StringComparison.Ordinal));
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
