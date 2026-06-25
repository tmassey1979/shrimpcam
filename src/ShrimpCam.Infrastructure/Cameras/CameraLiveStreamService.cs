using Microsoft.Extensions.Logging;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Cameras;

internal sealed class CameraLiveStreamService(
    ICameraCommandFactory commandFactory,
    IProcessStreamRunner processStreamRunner,
    ILogger<CameraLiveStreamService> logger) : ICameraLiveStreamService
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(2);
    private static readonly Action<ILogger, string, Exception?> StreamStartupFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2101, nameof(StreamStartupFailed)),
            "Live stream startup failed: {FailureReason}");

    public async Task<CameraLiveStreamStartResult> StartAsync(
        CameraOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

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
                return Failure(failure.StandardError);
            }

            var session = new CameraLiveStreamSession(
                new PrefixedStream(processStream.StandardOutput, firstChunk, bytesRead),
                processStream);

            return CameraLiveStreamStartResult.Success(session);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await processStream.DisposeAsync().ConfigureAwait(false);
            return Failure("Timed out waiting for live stream data.");
        }
        catch
        {
            await processStream.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        CameraLiveStreamStartResult Failure(string diagnostics)
        {
            StreamStartupFailed(logger, diagnostics, null);
            return CameraLiveStreamStartResult.Failure(CameraLiveStreamFailureReasons.CameraUnavailable);
        }
    }

    private sealed class CameraLiveStreamSession(Stream content, IProcessStreamHandle processStream) : ICameraLiveStreamSession
    {
        public string ContentType => LiveStreamConstants.ContentType;

        public Stream Content { get; } = content;

        public async ValueTask DisposeAsync()
        {
            await Content.DisposeAsync().ConfigureAwait(false);
            await processStream.DisposeAsync().ConfigureAwait(false);
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
