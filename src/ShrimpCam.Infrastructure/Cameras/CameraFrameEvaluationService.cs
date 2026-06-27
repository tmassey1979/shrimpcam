using Microsoft.Extensions.Logging;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Settings;

namespace ShrimpCam.Infrastructure.Cameras;

internal sealed class CameraFrameEvaluationService(
    IEditableSettingsService settingsService,
    ICameraFrameSourceProviderRegistry providerRegistry,
    ISharedCameraStreamHub streamHub,
    ILogger<CameraFrameEvaluationService> logger)
{
    private static readonly Action<ILogger, string, string, Exception?> CameraProviderResolved =
        LoggerMessage.Define<string, string>(
            LogLevel.Debug,
            new EventId(2204, nameof(CameraProviderResolved)),
            "Camera frame provider resolved to {ProviderKind} for {Platform}.");

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetCurrentAsync(cancellationToken).ConfigureAwait(false);

        var provider = providerRegistry.GetProvider(settings.Camera, settings.Camera.Platform);
        CameraProviderResolved(logger, provider.Descriptor.ProviderKind, provider.Descriptor.Platform, null);

        if (settings.Camera.AlwaysOnStreamEnabled)
        {
            await streamHub.EnsureRunningAsync(settings.Camera, cancellationToken).ConfigureAwait(false);
        }
    }
}
