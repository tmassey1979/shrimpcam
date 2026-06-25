using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Persistence;

namespace ShrimpCam.Core.Captures;

public sealed class ScheduledCaptureService(
    IAsyncDelay asyncDelay,
    ICameraCommandFactory cameraCommandFactory,
    ICameraResourceCoordinator cameraResourceCoordinator,
    ICameraStatusService cameraStatusService,
    ICaptureRecordRepository captureRecordRepository,
    ICaptureStorage captureStorage,
    IClock clock,
    IFileSystem fileSystem,
    IProcessRunner processRunner,
    IScheduledCaptureStateStore stateStore) : IScheduledCaptureService
{
    public async Task<ScheduledCaptureRunResult> RunDueCaptureAsync(
        ShrimpCamOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Capture.Enabled)
        {
            var disabledPlan = ScheduledCapturePlanner.Evaluate(options.Capture, clock.UtcNow, lastProcessedIntervalUtc: null);
            return ScheduledCaptureRunResult.Create(
                disabledPlan.Outcome,
                disabledPlan.IntervalStartUtc,
                disabledPlan.NextEligibleIntervalUtc);
        }

        var state = await stateStore.LoadAsync(options.Storage, cancellationToken).ConfigureAwait(false);
        var plan = ScheduledCapturePlanner.Evaluate(options.Capture, clock.UtcNow, state.LastProcessedIntervalUtc);

        switch (plan.Outcome)
        {
            case ScheduledCaptureOutcome.Disabled:
            case ScheduledCaptureOutcome.Waiting:
                return ScheduledCaptureRunResult.Create(
                    plan.Outcome,
                    plan.IntervalStartUtc,
                    plan.NextEligibleIntervalUtc);

            case ScheduledCaptureOutcome.SkippedBySchedule:
                await stateStore.SaveAsync(
                        options.Storage,
                        new ScheduledCaptureState(plan.IntervalStartUtc, plan.Outcome, null),
                        cancellationToken)
                    .ConfigureAwait(false);

                return ScheduledCaptureRunResult.Create(
                    plan.Outcome,
                    plan.IntervalStartUtc,
                    plan.NextEligibleIntervalUtc);

            case ScheduledCaptureOutcome.Captured:
                return await CaptureScheduledFrameAsync(options, plan, cancellationToken).ConfigureAwait(false);

            default:
                throw new InvalidOperationException($"Unexpected scheduled capture outcome '{plan.Outcome}'.");
        }
    }

    private async Task<ScheduledCaptureRunResult> CaptureScheduledFrameAsync(
        ShrimpCamOptions options,
        ScheduledCapturePlan plan,
        CancellationToken cancellationToken)
    {
        for (var failureCount = 0; ; failureCount++)
        {
            var stagedFilePath = fileSystem.GetTemporaryFilePath(".jpg");
            ProcessResult processResult;
            var cameraLease = await cameraResourceCoordinator
                .TryAcquireAsync(nameof(ScheduledCaptureService), cancellationToken)
                .ConfigureAwait(false);

            if (cameraLease is null)
            {
                DeleteIfPresent(stagedFilePath);
                return await PersistFailureAsync(
                        options,
                        plan,
                        ManualCaptureFailureReasons.CameraBusy,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            try
            {
                var command = cameraCommandFactory.BuildStillCaptureCommand(options.Camera, stagedFilePath);
                processResult = await processRunner.RunAsync(command, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                DeleteIfPresent(stagedFilePath);
                if (await TryDelayForRetryAsync(options.Camera, failureCount + 1, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                return await PersistFailureAsync(
                        options,
                        plan,
                        exception.Message,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                await cameraLease.DisposeAsync().ConfigureAwait(false);
            }

            if (processResult.ExitCode != 0)
            {
                DeleteIfPresent(stagedFilePath);
                if (await TryDelayForRetryAsync(options.Camera, failureCount + 1, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                return await PersistFailureAsync(
                        options,
                        plan,
                        ManualCaptureFailureReasons.CameraUnavailable,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            StoredCapture capture;
            try
            {
                capture = await captureStorage.StoreAsync(
                        options.Storage,
                        new CaptureStorageRequest(plan.IntervalStartUtc, CaptureSourceTypes.Scheduled, stagedFilePath),
                        cancellationToken)
                    .ConfigureAwait(false);

                await captureRecordRepository.CreateAsync(capture.ToCaptureRecord(), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                DeleteIfPresent(stagedFilePath);
                return await PersistFailureAsync(
                        options,
                        plan,
                        exception.Message,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            cameraStatusService.ReportOnline();

            await stateStore.SaveAsync(
                    options.Storage,
                    new ScheduledCaptureState(plan.IntervalStartUtc, ScheduledCaptureOutcome.Captured, null),
                    cancellationToken)
                .ConfigureAwait(false);

            return ScheduledCaptureRunResult.Create(
                ScheduledCaptureOutcome.Captured,
                plan.IntervalStartUtc,
                plan.NextEligibleIntervalUtc,
                capture);
        }
    }

    private async Task<ScheduledCaptureRunResult> PersistFailureAsync(
        ShrimpCamOptions options,
        ScheduledCapturePlan plan,
        string failureReason,
        CancellationToken cancellationToken)
    {
        cameraStatusService.ReportDegraded(failureReason);

        await stateStore.SaveAsync(
                options.Storage,
                new ScheduledCaptureState(plan.IntervalStartUtc, ScheduledCaptureOutcome.Failed, failureReason),
                cancellationToken)
            .ConfigureAwait(false);

        return ScheduledCaptureRunResult.Create(
            ScheduledCaptureOutcome.Failed,
            plan.IntervalStartUtc,
            plan.NextEligibleIntervalUtc,
            failureReason: failureReason);
    }

    private void DeleteIfPresent(string stagedFilePath)
    {
        if (fileSystem.FileExists(stagedFilePath))
        {
            fileSystem.DeleteFile(stagedFilePath);
        }
    }

    private async Task<bool> TryDelayForRetryAsync(
        CameraOptions options,
        int failureCount,
        CancellationToken cancellationToken)
    {
        if (!CameraRecoveryPlanner.ShouldRetry(options, failureCount))
        {
            return false;
        }

        await asyncDelay.DelayAsync(
                CameraRecoveryPlanner.GetBackoffDelay(options, failureCount),
                cancellationToken)
            .ConfigureAwait(false);

        return true;
    }
}
