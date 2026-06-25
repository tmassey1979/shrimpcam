using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Persistence;

namespace ShrimpCam.Core.Captures;

public sealed class ManualCaptureService(
    ICameraCommandFactory cameraCommandFactory,
    ICameraResourceCoordinator cameraResourceCoordinator,
    ICameraStatusService cameraStatusService,
    ICaptureRecordRepository captureRecordRepository,
    ICaptureStorage captureStorage,
    IClock clock,
    IFileSystem fileSystem,
    IProcessRunner processRunner) : IManualCaptureService
{
    public async Task<ManualCaptureResult> CaptureAsync(
        ShrimpCamOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var stagedFilePath = fileSystem.GetTemporaryFilePath(".jpg");
        var cameraLease = await cameraResourceCoordinator
            .TryAcquireAsync(nameof(ManualCaptureService), cancellationToken)
            .ConfigureAwait(false);

        if (cameraLease is null)
        {
            DeleteIfPresent(stagedFilePath);
            cameraStatusService.ReportDegraded(ManualCaptureFailureReasons.CameraBusy);
            return ManualCaptureResult.Failure(ManualCaptureFailureReasons.CameraBusy);
        }

        try
        {
            var command = cameraCommandFactory.BuildStillCaptureCommand(options.Camera, stagedFilePath);
            var processResult = await processRunner.RunAsync(command, cancellationToken).ConfigureAwait(false);

            if (processResult.ExitCode != 0)
            {
                DeleteIfPresent(stagedFilePath);
                cameraStatusService.ReportDegraded(ManualCaptureFailureReasons.CameraUnavailable);
                return ManualCaptureResult.Failure(ManualCaptureFailureReasons.CameraUnavailable);
            }

            var storedCapture = await captureStorage.StoreAsync(
                    options.Storage,
                    new CaptureStorageRequest(clock.UtcNow, CaptureSourceTypes.Manual, stagedFilePath),
                    cancellationToken)
                .ConfigureAwait(false);

            await captureRecordRepository.CreateAsync(storedCapture.ToCaptureRecord(), cancellationToken).ConfigureAwait(false);

            cameraStatusService.ReportOnline();

            return ManualCaptureResult.Success(storedCapture);
        }
        catch
        {
            DeleteIfPresent(stagedFilePath);
            throw;
        }
        finally
        {
            await cameraLease.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void DeleteIfPresent(string stagedFilePath)
    {
        if (fileSystem.FileExists(stagedFilePath))
        {
            fileSystem.DeleteFile(stagedFilePath);
        }
    }
}

internal static class StoredCapturePersistenceMapping
{
    public static CaptureRecord ToCaptureRecord(this StoredCapture capture) =>
        new(
            Guid.NewGuid(),
            capture.RelativeImagePath,
            capture.RelativeMetadataPath,
            capture.FileName,
            capture.SourceType,
            capture.CapturedAtUtc);
}
