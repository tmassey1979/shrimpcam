using ShrimpCam.Core.Cameras;

namespace ShrimpCam.Infrastructure.Cameras;

internal sealed class LiveFrameSnapshotStore : ILiveFrameSnapshotStore
{
    private static readonly TimeSpan MaximumFrameAge = TimeSpan.FromSeconds(10);
    private readonly object _gate = new();
    private byte[]? _latestFrame;
    private DateTimeOffset _latestFrameCapturedAtUtc;

    public ILiveFrameSnapshotRecorder CreateRecorder() => new Recorder(this);

    public async Task<bool> TryWriteLatestFrameAsync(string outputPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        byte[]? frame;
        lock (_gate)
        {
            if (_latestFrame is null || DateTimeOffset.UtcNow - _latestFrameCapturedAtUtc > MaximumFrameAge)
            {
                return false;
            }

            frame = _latestFrame.ToArray();
        }

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(outputPath, frame, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private void StoreFrame(byte[] frame)
    {
        lock (_gate)
        {
            _latestFrame = frame;
            _latestFrameCapturedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    private sealed class Recorder(LiveFrameSnapshotStore store) : ILiveFrameSnapshotRecorder
    {
        private const byte JpegStartPrefix = 0xFF;
        private const byte JpegStartMarker = 0xD8;
        private const byte JpegEndMarker = 0xD9;
        private readonly MemoryStream _currentFrame = new();
        private bool _capturing;
        private byte? _previousByte;

        public void Observe(ReadOnlyMemory<byte> buffer)
        {
            foreach (var currentByte in buffer.Span)
            {
                if (!_capturing)
                {
                    if (_previousByte == JpegStartPrefix && currentByte == JpegStartMarker)
                    {
                        _capturing = true;
                        _currentFrame.SetLength(0);
                        _currentFrame.WriteByte(JpegStartPrefix);
                        _currentFrame.WriteByte(JpegStartMarker);
                    }

                    _previousByte = currentByte;
                    continue;
                }

                _currentFrame.WriteByte(currentByte);

                if (_previousByte == JpegStartPrefix && currentByte == JpegEndMarker)
                {
                    store.StoreFrame(_currentFrame.ToArray());
                    _currentFrame.SetLength(0);
                    _capturing = false;
                }

                _previousByte = currentByte;
            }
        }

        public void Dispose() => _currentFrame.Dispose();
    }
}
