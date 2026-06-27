namespace ShrimpCam.Infrastructure.Cameras;

internal sealed class JpegFramePump(Action<ReadOnlyMemory<byte>> publishFrame) : IDisposable
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
                publishFrame(_currentFrame.ToArray());
                _currentFrame.SetLength(0);
                _capturing = false;
            }

            _previousByte = currentByte;
        }
    }

    public void Dispose() => _currentFrame.Dispose();
}
