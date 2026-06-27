using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Cameras.Windows;

internal sealed class MediaFoundationFrameSourceAdapter(
    IMediaFoundationDeviceEnumerator deviceEnumerator,
    IMediaFoundationCamera camera,
    ICameraStatusService cameraStatusService,
    ILiveFrameSnapshotStore liveFrameSnapshotStore)
{
    public MediaFoundationFrameSourceStartResult Start(CameraOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(options.Source))
        {
            cameraStatusService.ReportDegraded(MediaFoundationFailureReasons.MissingDevice);
            return MediaFoundationFrameSourceStartResult.Failure(MediaFoundationFailureReasons.MissingDevice);
        }

        var recorder = liveFrameSnapshotStore.CreateRecorder();
        var runningTask = Task.Run(
            async () =>
            {
                using (recorder)
                {
                    try
                    {
                        var devices = await deviceEnumerator.EnumerateAsync(cancellationToken).ConfigureAwait(false);
                        var device = FindDevice(devices, options.Source);
                        if (device is null)
                        {
                            cameraStatusService.ReportDegraded(MediaFoundationFailureReasons.MissingDevice);
                            return;
                        }

                        var format = MediaFoundationFrameFormatSelector.SelectStreamFormat(device, options);
                        await camera
                            .RunAsync(
                                options,
                                device,
                                format,
                                (frame, _) =>
                                {
                                    recorder.Observe(frame);
                                    cameraStatusService.ReportOnline();
                                    return ValueTask.CompletedTask;
                                },
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (MediaFoundationUnsupportedFormatException exception)
                    {
                        cameraStatusService.ReportDegraded($"{MediaFoundationFailureReasons.UnsupportedFormat}: {exception.Message}");
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                    }
                    catch (Exception exception)
                    {
                        cameraStatusService.ReportDegraded($"{MediaFoundationFailureReasons.StartupFailed}: {exception.Message}");
                    }
                }
            },
            CancellationToken.None);

        return MediaFoundationFrameSourceStartResult.Success(runningTask);
    }

    private static MediaFoundationDeviceDescriptor? FindDevice(
        IEnumerable<MediaFoundationDeviceDescriptor> devices,
        string source) =>
        devices.FirstOrDefault(device =>
            string.Equals(device.SymbolicLink, source, StringComparison.OrdinalIgnoreCase)
            || string.Equals(device.DisplayName, source, StringComparison.OrdinalIgnoreCase));
}
