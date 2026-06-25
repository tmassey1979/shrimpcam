using Microsoft.Extensions.Logging;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Cameras;

internal sealed class CameraLiveStreamService(
    IAsyncDelay asyncDelay,
    ICameraCommandFactory commandFactory,
    ICameraStatusService cameraStatusService,
    IProcessStreamRunner processStreamRunner,
    ILogger<CameraLiveStreamService> logger) : ICameraLiveStreamService
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(2);
    private static readonly Action<ILogger, string, Exception?> StreamStartupFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2101, nameof(StreamStartupFailed)),
            "Live stream startup failed: {FailureReason}");
    private static readonly Action<ILogger, int, Exception?> StreamReconnectAttempt =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(2102, nameof(StreamReconnectAttempt)),
            "Attempting to reconnect the live stream after camera disconnect. Attempt {Attempt}.");
    private static readonly Action<ILogger, string, Exception?> StreamReconnectExhausted =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2103, nameof(StreamReconnectExhausted)),
            "Live stream reconnect attempts were exhausted: {FailureReason}");

    public async Task<CameraLiveStreamStartResult> StartAsync(
        CameraOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var initialConnection = await TryOpenConnectionWithRetriesAsync(options, cancellationToken).ConfigureAwait(false);
        if (!initialConnection.Succeeded)
        {
            StreamStartupFailed(logger, initialConnection.FailureReason!, null);
            cameraStatusService.ReportDegraded(initialConnection.FailureReason!);
            return CameraLiveStreamStartResult.Failure(CameraLiveStreamFailureReasons.CameraUnavailable);
        }

        cameraStatusService.ReportOnline();

        var content = new ReconnectingCameraStream(
            initialConnection.Connection!,
            cameraStatusService,
            options,
            OpenConnectionAsync,
            asyncDelay,
            logger);

        return CameraLiveStreamStartResult.Success(new CameraLiveStreamSession(content));
    }

    private async Task<StreamOpenResult> TryOpenConnectionWithRetriesAsync(
        CameraOptions options,
        CancellationToken cancellationToken)
    {
        var failureReason = CameraLiveStreamFailureReasons.CameraUnavailable;

        for (var failureCount = 0; ; failureCount++)
        {
            var result = await OpenConnectionAsync(options, cancellationToken).ConfigureAwait(false);
            if (result.Succeeded)
            {
                return result;
            }

            failureReason = result.FailureReason ?? CameraLiveStreamFailureReasons.CameraUnavailable;
            if (!CameraRecoveryPlanner.ShouldRetry(options, failureCount + 1))
            {
                return new StreamOpenResult(false, null, failureReason);
            }

            StreamReconnectAttempt(logger, failureCount + 1, null);
            await asyncDelay.DelayAsync(
                    CameraRecoveryPlanner.GetBackoffDelay(options, failureCount + 1),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<StreamOpenResult> OpenConnectionAsync(
        CameraOptions options,
        CancellationToken cancellationToken)
    {
        var command = commandFactory.BuildLiveStreamCommand(options);
        var processStream = await processStreamRunner.StartAsync(command, cancellationToken).ConfigureAwait(false);

        var firstChunk = new byte[4096];
        using var startupCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        startupCancellation.CancelAfter(StartupTimeout);

        try
        {
            var bytesRead = await processStream.StandardOutput
                .ReadAsync(firstChunk.AsMemory(0, firstChunk.Length), startupCancellation.Token)
                .ConfigureAwait(false);

            if (bytesRead <= 0)
            {
                var failure = await processStream.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                await processStream.DisposeAsync().ConfigureAwait(false);
                return new StreamOpenResult(false, null, NormalizeFailureReason(failure.StandardError));
            }

            return new StreamOpenResult(
                true,
                new ActiveCameraStream(
                    processStream,
                    new PrefixedStream(processStream.StandardOutput, firstChunk, bytesRead)),
                null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await processStream.DisposeAsync().ConfigureAwait(false);
            return new StreamOpenResult(false, null, "Timed out waiting for live stream data.");
        }
        catch (Exception exception)
        {
            await processStream.DisposeAsync().ConfigureAwait(false);
            return new StreamOpenResult(false, null, NormalizeFailureReason(exception.Message));
        }
    }

    private static string NormalizeFailureReason(string? diagnostics) =>
        string.IsNullOrWhiteSpace(diagnostics)
            ? CameraLiveStreamFailureReasons.CameraUnavailable
            : diagnostics;

    private sealed class CameraLiveStreamSession(Stream content) : ICameraLiveStreamSession
    {
        public string ContentType => LiveStreamConstants.ContentType;

        public Stream Content { get; } = content;

        public ValueTask DisposeAsync() => Content.DisposeAsync();
    }

    private sealed record StreamOpenResult(
        bool Succeeded,
        ActiveCameraStream? Connection,
        string? FailureReason);

    private sealed class ActiveCameraStream(IProcessStreamHandle processStream, Stream content) : IAsyncDisposable
    {
        public IProcessStreamHandle ProcessStream { get; } = processStream;

        public Stream Content { get; } = content;

        public async ValueTask DisposeAsync()
        {
            await Content.DisposeAsync().ConfigureAwait(false);
            await ProcessStream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class ReconnectingCameraStream(
        ActiveCameraStream activeStream,
        ICameraStatusService cameraStatusService,
        CameraOptions options,
        Func<CameraOptions, CancellationToken, Task<StreamOpenResult>> openConnectionAsync,
        IAsyncDelay asyncDelay,
        ILogger logger) : Stream
    {
        private ActiveCameraStream _activeStream = activeStream;
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

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            while (true)
            {
                try
                {
                    var bytesRead = await _activeStream.Content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (bytesRead > 0)
                    {
                        return bytesRead;
                    }
                }
                catch (IOException) when (!cancellationToken.IsCancellationRequested)
                {
                }
                catch (ObjectDisposedException) when (!cancellationToken.IsCancellationRequested)
                {
                }

                if (!await TryReconnectAsync(cancellationToken).ConfigureAwait(false))
                {
                    return 0;
                }
            }
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _activeStream.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _disposed = true;
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            await _activeStream.DisposeAsync().ConfigureAwait(false);
            await base.DisposeAsync().ConfigureAwait(false);
        }

        private async Task<bool> TryReconnectAsync(CancellationToken cancellationToken)
        {
            string failureReason = CameraLiveStreamFailureReasons.CameraUnavailable;

            for (var failureCount = 1; ; failureCount++)
            {
                if (!CameraRecoveryPlanner.ShouldRetry(options, failureCount))
                {
                    cameraStatusService.ReportDegraded(failureReason);
                    StreamReconnectExhausted(logger, failureReason, null);
                    return false;
                }

                StreamReconnectAttempt(logger, failureCount, null);
                await asyncDelay.DelayAsync(
                        CameraRecoveryPlanner.GetBackoffDelay(options, failureCount),
                        cancellationToken)
                    .ConfigureAwait(false);

                var result = await openConnectionAsync(options, cancellationToken).ConfigureAwait(false);
                if (result.Succeeded)
                {
                    await _activeStream.DisposeAsync().ConfigureAwait(false);
                    _activeStream = result.Connection!;
                    cameraStatusService.ReportOnline();
                    return true;
                }

                failureReason = result.FailureReason ?? CameraLiveStreamFailureReasons.CameraUnavailable;
            }
        }
    }

    private sealed class PrefixedStream(Stream inner, byte[] prefixBuffer, int prefixLength) : Stream
    {
        private int _position;

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
            ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

        public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
        {
            if (_position < prefixLength)
            {
                var bytesToCopy = Math.Min(destination.Length, prefixLength - _position);
                prefixBuffer.AsMemory(_position, bytesToCopy).CopyTo(destination);
                _position += bytesToCopy;
                return bytesToCopy;
            }

            return await inner.ReadAsync(destination, cancellationToken).ConfigureAwait(false);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync().ConfigureAwait(false);
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }
}
