using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Cameras;

internal sealed class CameraStartupProbe(
    IOptions<ShrimpCamOptions> options,
    ILinuxCameraDiscovery linuxCameraDiscovery,
    IWindowsCameraDiscovery windowsCameraDiscovery,
    ICameraStatusService cameraStatusService,
    ILogger<CameraStartupProbe> logger) : ICameraStartupProbe
{
    private static readonly Action<ILogger, string, Exception?> CameraStartupDegraded =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1003, nameof(CameraStartupDegraded)),
            "Camera startup probe reported degraded availability: {Reason}");

    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var cameraOptions = options.Value.Camera;
            var cameras = await DiscoverForPlatformAsync(cameraOptions.Platform, cancellationToken).ConfigureAwait(false);

            if (cameras.Any(camera => MatchesConfiguredSource(camera, cameraOptions.Source)))
            {
                cameraStatusService.ReportOnline();
                return;
            }

            ReportDegraded($"Configured camera source '{cameraOptions.Source}' was not discovered.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ReportDegraded(ex.Message);
        }
    }

    private Task<IReadOnlyList<CameraDescriptor>> DiscoverForPlatformAsync(
        string platform,
        CancellationToken cancellationToken) =>
        platform switch
        {
            CameraPlatforms.Linux => linuxCameraDiscovery.DiscoverAsync(cancellationToken),
            CameraPlatforms.Windows => windowsCameraDiscovery.DiscoverAsync(cancellationToken),
            _ => Task.FromResult<IReadOnlyList<CameraDescriptor>>([]),
        };

    private static bool MatchesConfiguredSource(CameraDescriptor camera, string source) =>
        string.Equals(camera.DevicePath, source, StringComparison.OrdinalIgnoreCase)
        || string.Equals(camera.DisplayName, source, StringComparison.OrdinalIgnoreCase);

    private void ReportDegraded(string reason)
    {
        cameraStatusService.ReportDegraded(reason);
        CameraStartupDegraded(logger, reason, null);
    }
}
