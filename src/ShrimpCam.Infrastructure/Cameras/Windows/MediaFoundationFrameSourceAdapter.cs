using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Cameras.Windows;

internal sealed class MediaFoundationFrameSourceAdapter(
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
                        await camera
                            .RunAsync(
                                options,
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
}
