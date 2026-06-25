using System.ComponentModel.DataAnnotations;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Captures;

public sealed class MotionHighlightService(
    ICameraCommandFactory cameraCommandFactory,
    ICameraStatusService cameraStatusService,
    ICaptureStorage captureStorage,
    IFileSystem fileSystem,
    IProcessRunner processRunner,
    IMotionHighlightStateStore stateStore) : IMotionHighlightService
{
    public async Task<MotionHighlightResult> EvaluateAsync(
        ShrimpCamOptions options,
        MotionHighlightEvent motionEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateMotionEvent(motionEvent);

        var state = await stateStore.LoadAsync(options.Storage, cancellationToken).ConfigureAwait(false);
        var plan = MotionHighlightPlanner.Evaluate(options.Capture, motionEvent, state);

        switch (plan.Outcome)
        {
            case MotionHighlightOutcome.Disabled:
            case MotionHighlightOutcome.BelowThreshold:
            case MotionHighlightOutcome.SuppressedByCooldown:
                await stateStore.SaveAsync(
                        options.Storage,
                        CreateUpdatedState(state, motionEvent, plan.EventFingerprint, captured: false),
                        cancellationToken)
                    .ConfigureAwait(false);
                return MotionHighlightResult.Skipped(plan.Outcome);

            case MotionHighlightOutcome.SuppressedDuplicate:
                return MotionHighlightResult.Skipped(plan.Outcome);

            case MotionHighlightOutcome.Captured:
                return await CaptureHighlightAsync(options, motionEvent, plan.EventFingerprint, state, cancellationToken).ConfigureAwait(false);

            default:
                throw new InvalidOperationException($"Unexpected motion highlight outcome '{plan.Outcome}'.");
        }
    }

    private async Task<MotionHighlightResult> CaptureHighlightAsync(
        ShrimpCamOptions options,
        MotionHighlightEvent motionEvent,
        string eventFingerprint,
        MotionHighlightState state,
        CancellationToken cancellationToken)
    {
        var stagedFilePath = fileSystem.GetTemporaryFilePath(".jpg");

        try
        {
            var command = cameraCommandFactory.BuildStillCaptureCommand(options.Camera, stagedFilePath);
            var processResult = await processRunner.RunAsync(command, cancellationToken).ConfigureAwait(false);

            if (processResult.ExitCode != 0)
            {
                DeleteIfPresent(stagedFilePath);
                cameraStatusService.ReportDegraded(ManualCaptureFailureReasons.CameraUnavailable);
                await stateStore.SaveAsync(
                        options.Storage,
                        CreateUpdatedState(state, motionEvent, eventFingerprint, captured: false),
                        cancellationToken)
                    .ConfigureAwait(false);
                return MotionHighlightResult.Failed(ManualCaptureFailureReasons.CameraUnavailable);
            }

            var storedCapture = await captureStorage.StoreAsync(
                    options.Storage,
                    new CaptureStorageRequest(motionEvent.OccurredAtUtc, CaptureSourceTypes.MotionHighlight, stagedFilePath),
                    cancellationToken)
                .ConfigureAwait(false);

            cameraStatusService.ReportOnline();

            await stateStore.SaveAsync(
                    options.Storage,
                    CreateUpdatedState(state, motionEvent, eventFingerprint, captured: true),
                    cancellationToken)
                .ConfigureAwait(false);

            return MotionHighlightResult.Captured(storedCapture);
        }
        catch
        {
            DeleteIfPresent(stagedFilePath);
            throw;
        }
    }

    private static MotionHighlightState CreateUpdatedState(
        MotionHighlightState state,
        MotionHighlightEvent motionEvent,
        string eventFingerprint,
        bool captured) =>
        new(
            captured ? motionEvent.OccurredAtUtc : state.LastHighlightCapturedAtUtc,
            eventFingerprint,
            motionEvent.OccurredAtUtc);

    private static void ValidateMotionEvent(MotionHighlightEvent motionEvent)
    {
        ArgumentNullException.ThrowIfNull(motionEvent);

        if (motionEvent.OccurredAtUtc == default)
        {
            throw new ValidationException("Motion event timestamp is required.");
        }

        if (motionEvent.Score <= 0d || motionEvent.Score > 1d)
        {
            throw new ValidationException("Motion score must be greater than 0 and less than or equal to 1.");
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
