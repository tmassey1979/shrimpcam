using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Cameras.Linux;

internal sealed class LinuxV4l2FfmpegFrameSourceAdapter(
    ICameraCommandFactory commandFactory,
    IProcessStreamRunner processStreamRunner,
    ICameraStatusService cameraStatusService,
    ILiveFrameSnapshotStore liveFrameSnapshotStore)
{
    public LinuxV4l2FfmpegFrameSourceStartResult Start(
        CameraOptions options,
        CancellationToken cancellationToken,
        Action<ReadOnlyMemory<byte>>? publishFrame = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(options.Source) ||
            !options.Source.StartsWith("/dev/video", StringComparison.Ordinal))
        {
            cameraStatusService.ReportDegraded(LinuxV4l2FfmpegFailureReasons.MissingDevice);
            return LinuxV4l2FfmpegFrameSourceStartResult.Failure(LinuxV4l2FfmpegFailureReasons.MissingDevice);
        }

        var runningTask = Task.Run(() => RunAsync(options, publishFrame, cancellationToken), CancellationToken.None);
        return LinuxV4l2FfmpegFrameSourceStartResult.Success(runningTask);
    }

    private async Task RunAsync(
        CameraOptions options,
        Action<ReadOnlyMemory<byte>>? publishFrame,
        CancellationToken cancellationToken)
    {
        using var recorder = liveFrameSnapshotStore.CreateRecorder();

        for (var failureCount = 0; !cancellationToken.IsCancellationRequested;)
        {
            try
            {
                var command = commandFactory.BuildLiveStreamCommand(options);
                var processStream = await processStreamRunner
                    .StartAsync(command, cancellationToken)
                    .ConfigureAwait(false);
                await using var configuredProcessStream = processStream.ConfigureAwait(false);
                var streamedFrames = await PumpFramesAsync(processStream.StandardOutput, recorder, publishFrame, cancellationToken)
                    .ConfigureAwait(false);
                var processResult = await processStream.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                cameraStatusService.ReportDegraded(
                    $"{LinuxV4l2FfmpegFailureReasons.ProcessExited}: exit {processResult.ExitCode}; frames {streamedFrames}; {processResult.StandardError}");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                cameraStatusService.ReportDegraded($"{LinuxV4l2FfmpegFailureReasons.StartupFailed}: {exception.Message}");
            }

            failureCount++;
            if (!CameraRecoveryPlanner.ShouldRetry(options, failureCount))
            {
                return;
            }

            await Task.Delay(CameraRecoveryPlanner.GetBackoffDelay(options, failureCount), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<bool> PumpFramesAsync(
        Stream standardOutput,
        ILiveFrameSnapshotRecorder recorder,
        Action<ReadOnlyMemory<byte>>? publishFrame,
        CancellationToken cancellationToken)
    {
        var streamedFrames = false;
        var buffer = new byte[16 * 1024];
        using var framePump = new JpegFramePump(
            frame =>
            {
                streamedFrames = true;
                recorder.Observe(frame);
                publishFrame?.Invoke(frame);
                cameraStatusService.ReportOnline();
            });

        while (!cancellationToken.IsCancellationRequested)
        {
            var bytesRead = await standardOutput
                .ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                .ConfigureAwait(false);

            if (bytesRead <= 0)
            {
                return streamedFrames;
            }

            framePump.Observe(buffer.AsMemory(0, bytesRead));
        }

        return streamedFrames;
    }
}
