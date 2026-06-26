using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Cameras;

internal sealed class SharedCameraStreamHub(
    ICameraCommandFactory commandFactory,
    ICameraResourceCoordinator cameraResourceCoordinator,
    ICameraStatusService cameraStatusService,
    ILiveFrameSnapshotStore liveFrameSnapshotStore,
    IProcessStreamRunner processStreamRunner,
    ILogger<SharedCameraStreamHub> logger) : ISharedCameraStreamHub, IAsyncDisposable
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SubscriberFirstChunkTimeout = TimeSpan.FromSeconds(5);
    private static readonly Action<ILogger, Exception?> SharedPumpStopped =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(2202, nameof(SharedPumpStopped)),
            "Shared live stream pump stopped unexpectedly.");
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Channel<byte[]>> _subscribers = [];
    private CancellationTokenSource? _pumpCancellation;
    private Task? _pumpTask;
    private CameraOptions? _currentOptions;
    private string? _lastFailureReason;
    private byte[]? _lastChunk;

    public async Task<CameraLiveStreamStartResult> SubscribeAsync(CameraOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        await EnsureRunningAsync(options, cancellationToken).ConfigureAwait(false);

        Channel<byte[]> channel;
        Guid subscriptionId;
        lock (_gate)
        {
            if (_lastFailureReason is not null)
            {
                return CameraLiveStreamStartResult.Failure(_lastFailureReason);
            }

            subscriptionId = Guid.NewGuid();
            channel = Channel.CreateBounded<byte[]>(
                new BoundedChannelOptions(8)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = true,
                    SingleWriter = false,
            });
            _subscribers[subscriptionId] = channel;
            if (_lastChunk is not null)
            {
                channel.Writer.TryWrite(_lastChunk);
            }
        }

        using var firstChunkCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        firstChunkCancellation.CancelAfter(SubscriberFirstChunkTimeout);

        try
        {
            var firstChunk = await channel.Reader.ReadAsync(firstChunkCancellation.Token).ConfigureAwait(false);
            return CameraLiveStreamStartResult.Success(
                new CameraLiveStreamSession(new SubscriberStream(this, subscriptionId, channel.Reader, firstChunk)));
        }
        catch (ChannelClosedException)
        {
            RemoveSubscriber(subscriptionId);
            return CameraLiveStreamStartResult.Failure(GetFailureReason() ?? CameraLiveStreamFailureReasons.CameraUnavailable);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            RemoveSubscriber(subscriptionId);
            var failureReason = GetFailureReason() ?? "Timed out waiting for shared live stream data.";
            return CameraLiveStreamStartResult.Failure(failureReason);
        }
        catch
        {
            RemoveSubscriber(subscriptionId);
            throw;
        }
    }

    public Task EnsureRunningAsync(CameraOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_pumpTask is { IsCompleted: false } && IsSameCamera(_currentOptions, options))
            {
                return Task.CompletedTask;
            }

            StopPumpLocked();
            _currentOptions = CloneOptions(options);
            _lastFailureReason = null;
            _pumpCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _pumpTask = Task.Run(() => RunPumpAsync(_currentOptions, _pumpCancellation.Token), CancellationToken.None);
            return Task.CompletedTask;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task? pumpTask;
        lock (_gate)
        {
            StopPumpLocked();
            pumpTask = _pumpTask;
        }

        if (pumpTask is not null)
        {
            await pumpTask.ConfigureAwait(false);
        }
    }

    private async Task RunPumpAsync(CameraOptions options, CancellationToken cancellationToken)
    {
        using var recorder = liveFrameSnapshotStore.CreateRecorder();
        CameraResourceLease? cameraLease = null;

        try
        {
            cameraLease = await cameraResourceCoordinator
                .TryAcquireAsync(nameof(SharedCameraStreamHub), cancellationToken)
                .ConfigureAwait(false);

            if (cameraLease is null)
            {
                SetFailure(CameraLiveStreamFailureReasons.CameraBusy);
                cameraStatusService.ReportDegraded(CameraLiveStreamFailureReasons.CameraBusy);
                CompleteSubscribers();
                return;
            }

            var command = commandFactory.BuildLiveStreamCommand(options);
            var processStream = await processStreamRunner.StartAsync(command, cancellationToken).ConfigureAwait(false);
            await using var configuredProcessStream = processStream.ConfigureAwait(false);
            var buffer = new byte[16384];

            using var startupCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            startupCancellation.CancelAfter(StartupTimeout);
            var firstRead = await processStream.StandardOutput
                .ReadAsync(buffer.AsMemory(0, buffer.Length), startupCancellation.Token)
                .ConfigureAwait(false);

            if (firstRead <= 0)
            {
                var failure = await processStream.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                SetFailure(NormalizeFailureReason(failure.StandardError));
                cameraStatusService.ReportDegraded(GetFailureReason()!);
                CompleteSubscribers();
                return;
            }

            cameraStatusService.ReportOnline();
            Broadcast(buffer.AsMemory(0, firstRead));
            recorder.Observe(buffer.AsMemory(0, firstRead));

            while (!cancellationToken.IsCancellationRequested)
            {
                var bytesRead = await processStream.StandardOutput
                    .ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                    .ConfigureAwait(false);

                if (bytesRead <= 0)
                {
                    break;
                }

                var chunk = buffer.AsMemory(0, bytesRead);
                recorder.Observe(chunk);
                Broadcast(chunk);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            SetFailure(NormalizeFailureReason(exception.Message));
            cameraStatusService.ReportDegraded(GetFailureReason()!);
            CompleteSubscribers();
            SharedPumpStopped(logger, exception);
        }
        finally
        {
            if (cameraLease is not null)
            {
                await cameraLease.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private void Broadcast(ReadOnlyMemory<byte> chunk)
    {
        var payload = chunk.ToArray();
        List<Channel<byte[]>> subscribers;
        lock (_gate)
        {
            _lastChunk = payload;
            subscribers = _subscribers.Values.ToList();
        }

        foreach (var subscriber in subscribers)
        {
            subscriber.Writer.TryWrite(payload);
        }
    }

    private void RemoveSubscriber(Guid subscriptionId)
    {
        lock (_gate)
        {
            if (_subscribers.Remove(subscriptionId, out var subscriber))
            {
                subscriber.Writer.TryComplete();
            }
        }
    }

    private void CompleteSubscribers()
    {
        lock (_gate)
        {
            foreach (var subscriber in _subscribers.Values)
            {
                subscriber.Writer.TryComplete();
            }

            _subscribers.Clear();
        }
    }

    private void StopPumpLocked()
    {
        _pumpCancellation?.Cancel();
        _pumpCancellation?.Dispose();
        _pumpCancellation = null;
        CompleteSubscribers();
    }

    private void SetFailure(string failureReason)
    {
        lock (_gate)
        {
            _lastFailureReason = failureReason;
        }
    }

    private string? GetFailureReason()
    {
        lock (_gate)
        {
            return _lastFailureReason;
        }
    }

    private static string NormalizeFailureReason(string? diagnostics) =>
        string.IsNullOrWhiteSpace(diagnostics)
            ? CameraLiveStreamFailureReasons.CameraUnavailable
            : diagnostics;

    private static bool IsSameCamera(CameraOptions? current, CameraOptions next) =>
        current is not null
        && string.Equals(current.Platform, next.Platform, StringComparison.Ordinal)
        && string.Equals(current.Source, next.Source, StringComparison.Ordinal)
        && current.StreamWidth == next.StreamWidth
        && current.StreamHeight == next.StreamHeight
        && current.StreamFramesPerSecond == next.StreamFramesPerSecond
        && current.AlwaysOnStreamEnabled == next.AlwaysOnStreamEnabled;

    private static CameraOptions CloneOptions(CameraOptions options) =>
        new()
        {
            Platform = options.Platform,
            Source = options.Source,
            CaptureWidth = options.CaptureWidth,
            CaptureHeight = options.CaptureHeight,
            StreamWidth = options.StreamWidth,
            StreamHeight = options.StreamHeight,
            StreamFramesPerSecond = options.StreamFramesPerSecond,
            ReconnectRetryAttempts = options.ReconnectRetryAttempts,
            ReconnectBackoffSeconds = options.ReconnectBackoffSeconds,
            AlwaysOnStreamEnabled = options.AlwaysOnStreamEnabled,
        };

    private sealed class CameraLiveStreamSession(Stream content) : ICameraLiveStreamSession
    {
        public string ContentType => LiveStreamConstants.ContentType;

        public Stream Content { get; } = content;

        public ValueTask DisposeAsync() => Content.DisposeAsync();
    }

    private sealed class SubscriberStream(
        SharedCameraStreamHub hub,
        Guid subscriptionId,
        ChannelReader<byte[]> reader,
        byte[] firstChunk) : Stream
    {
        private byte[]? _currentChunk = firstChunk;
        private int _currentOffset;
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
                if (_currentChunk is not null && _currentOffset < _currentChunk.Length)
                {
                    var bytesToCopy = Math.Min(buffer.Length, _currentChunk.Length - _currentOffset);
                    _currentChunk.AsMemory(_currentOffset, bytesToCopy).CopyTo(buffer);
                    _currentOffset += bytesToCopy;
                    return bytesToCopy;
                }

                if (!await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return 0;
                }

                if (reader.TryRead(out var nextChunk))
                {
                    _currentChunk = nextChunk;
                    _currentOffset = 0;
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
            if (disposing && !_disposed)
            {
                _disposed = true;
                hub.RemoveSubscriber(subscriptionId);
            }

            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            Dispose(disposing: true);
            return base.DisposeAsync();
        }
    }

}
