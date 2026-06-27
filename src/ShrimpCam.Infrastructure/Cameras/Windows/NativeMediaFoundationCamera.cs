using ShrimpCam.Core.Configuration;
using OpenCvSharp;

namespace ShrimpCam.Infrastructure.Cameras.Windows;

internal sealed class NativeMediaFoundationCamera : IMediaFoundationCamera
{
    private const int FrameReadDelayMilliseconds = 5;
    private const int FirstFrameTimeoutSeconds = 8;
    private const int FrameReadTimeoutSeconds = 3;

    public async Task RunAsync(
        CameraOptions options,
        MediaFoundationDeviceDescriptor device,
        MediaFoundationFrameFormat format,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> onFrame,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(onFrame);

        if (device.NativeIndex is null)
        {
            throw new InvalidOperationException(
                $"Media Foundation device '{device.DisplayName}' does not include a native camera index. Refresh camera discovery and select a Windows Media Foundation device.");
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows Media Foundation capture requires a Windows host.");
        }

        // OpenCV's MSMF backend can block indefinitely on some Logitech devices.
        // Use the Windows-native DirectShow backend here while the isolated MF helper is completed.
        using var capture = new VideoCapture(device.NativeIndex.Value, VideoCaptureAPIs.DSHOW);
        ConfigureCapture(capture, format);

        if (!capture.IsOpened())
        {
            throw new InvalidOperationException($"Media Foundation could not open device '{device.DisplayName}' at native index {device.NativeIndex.Value}.");
        }

        using var frame = new Mat();
        using var firstFrameDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        firstFrameDeadline.CancelAfter(TimeSpan.FromSeconds(FirstFrameTimeoutSeconds));
        var hasPublishedFrame = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await ReadFrameWithTimeoutAsync(capture, frame, device, cancellationToken).ConfigureAwait(false);
            if (!read || frame.Empty())
            {
                if (!hasPublishedFrame)
                {
                    firstFrameDeadline.Token.ThrowIfCancellationRequested();
                }

                await Task.Delay(FrameReadDelayMilliseconds, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (!Cv2.ImEncode(".jpg", frame, out var jpegBytes))
            {
                throw new InvalidOperationException($"Media Foundation frame from '{device.DisplayName}' could not be encoded as JPEG.");
            }

            hasPublishedFrame = true;
            await onFrame(jpegBytes, cancellationToken).ConfigureAwait(false);
            await Task.Delay(FrameReadDelayMilliseconds, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<bool> ReadFrameWithTimeoutAsync(
        VideoCapture capture,
        Mat frame,
        MediaFoundationDeviceDescriptor device,
        CancellationToken cancellationToken)
    {
        try
        {
            return await Task
                .Run(() => capture.Read(frame), cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(FrameReadTimeoutSeconds), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            throw new InvalidOperationException(
                $"Windows camera read timed out after {FrameReadTimeoutSeconds} seconds for '{device.DisplayName}'. Try the FFmpeg DirectShow fallback or lower the stream resolution.",
                exception);
        }
    }

    private static void ConfigureCapture(VideoCapture capture, MediaFoundationFrameFormat format)
    {
        capture.Set(VideoCaptureProperties.FrameWidth, format.Width);
        capture.Set(VideoCaptureProperties.FrameHeight, format.Height);
        capture.Set(VideoCaptureProperties.Fps, format.FramesPerSecond);

        if (format.IsJpegLike)
        {
            capture.Set(VideoCaptureProperties.FourCC, VideoWriter.FourCC('M', 'J', 'P', 'G'));
        }
    }
}
