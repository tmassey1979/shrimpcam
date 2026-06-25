using Microsoft.Extensions.Logging;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Cameras;

namespace ShrimpCam.Infrastructure.Cameras.Linux;

internal sealed class LinuxCameraDiscovery(
    IProcessRunner processRunner,
    ILogger<LinuxCameraDiscovery> logger) : ILinuxCameraDiscovery
{
    private static readonly ProcessRequest DiscoveryRequest = new("v4l2-ctl", "--list-devices");
    private static readonly Action<ILogger, Exception?> NoLinuxCamerasDetected =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(1001, nameof(NoLinuxCamerasDetected)),
            "No Linux cameras were detected by the discovery command.");

    public async Task<IReadOnlyList<CameraDescriptor>> DiscoverAsync(CancellationToken cancellationToken)
    {
        var result = await processRunner.RunAsync(DiscoveryRequest, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Linux camera discovery failed with exit code {result.ExitCode}: {result.StandardError}");
        }

        var cameras = ParseOutput(result.StandardOutput);

        if (cameras.Count == 0)
        {
            NoLinuxCamerasDetected(logger, null);
        }

        return cameras;
    }

    internal static IReadOnlyList<CameraDescriptor> ParseOutput(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var cameras = new List<CameraDescriptor>();
        var currentDisplayName = string.Empty;

        foreach (var rawLine in output.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
        {
            var line = rawLine.TrimEnd();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!char.IsWhiteSpace(rawLine, 0))
            {
                currentDisplayName = line.TrimEnd(':').Trim();
                continue;
            }

            if (string.IsNullOrWhiteSpace(currentDisplayName))
            {
                continue;
            }

            var devicePath = line.Trim();

            if (!devicePath.StartsWith("/dev/video", StringComparison.Ordinal))
            {
                continue;
            }

            cameras.Add(new CameraDescriptor(currentDisplayName, devicePath, CameraPlatforms.Linux));
        }

        return cameras;
    }
}
