using System.ComponentModel.DataAnnotations;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Cameras;

internal sealed class CameraFrameSourceSelector : ICameraFrameSourceSelector
{
    public CameraFrameSourceSelection ChooseFrameSource(CameraOptions options, string hostPlatform)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostPlatform);

        var platform = NormalizePlatform(string.IsNullOrWhiteSpace(options.Platform) ? hostPlatform : options.Platform);
        var backendMode = NormalizeBackendMode(options.BackendMode);

        return backendMode switch
        {
            CameraBackendModes.Automatic => SelectAutomatic(platform),
            CameraBackendModes.WindowsMediaFoundation => RequirePlatform(
                platform,
                CameraPlatforms.Windows,
                new CameraFrameSourceSelection(
                    CameraFrameProviderKinds.WindowsMediaFoundation,
                    backendMode,
                    platform,
                    IsFallback: false)),
            CameraBackendModes.WindowsFfmpegFallback => RequirePlatform(
                platform,
                CameraPlatforms.Windows,
                new CameraFrameSourceSelection(
                    CameraFrameProviderKinds.WindowsFfmpegDirectShow,
                    backendMode,
                    platform,
                    IsFallback: true)),
            CameraBackendModes.LinuxV4l2Ffmpeg => RequirePlatform(
                platform,
                CameraPlatforms.Linux,
                new CameraFrameSourceSelection(
                    CameraFrameProviderKinds.LinuxV4l2Ffmpeg,
                    backendMode,
                    platform,
                    IsFallback: false)),
            _ => throw new ValidationException($"Unsupported camera backend mode '{options.BackendMode}'."),
        };
    }

    private static CameraFrameSourceSelection SelectAutomatic(string platform) =>
        platform switch
        {
            CameraPlatforms.Windows => new CameraFrameSourceSelection(
                CameraFrameProviderKinds.WindowsMediaFoundation,
                CameraBackendModes.Automatic,
                platform,
                IsFallback: false),
            CameraPlatforms.Linux => new CameraFrameSourceSelection(
                CameraFrameProviderKinds.LinuxV4l2Ffmpeg,
                CameraBackendModes.Automatic,
                platform,
                IsFallback: false),
            _ => throw new ValidationException($"Unsupported camera platform '{platform}'."),
        };

    private static CameraFrameSourceSelection RequirePlatform(
        string actualPlatform,
        string expectedPlatform,
        CameraFrameSourceSelection selection)
    {
        if (!string.Equals(actualPlatform, expectedPlatform, StringComparison.Ordinal))
        {
            throw new ValidationException(
                $"Camera backend mode '{selection.BackendMode}' requires platform '{expectedPlatform}', but platform '{actualPlatform}' was selected.");
        }

        return selection;
    }

    private static string NormalizePlatform(string platform)
    {
        if (string.Equals(platform, CameraPlatforms.Windows, StringComparison.OrdinalIgnoreCase))
        {
            return CameraPlatforms.Windows;
        }

        if (string.Equals(platform, CameraPlatforms.Linux, StringComparison.OrdinalIgnoreCase))
        {
            return CameraPlatforms.Linux;
        }

        return platform;
    }

    private static string NormalizeBackendMode(string backendMode)
    {
        if (string.Equals(backendMode, CameraBackendModes.Automatic, StringComparison.OrdinalIgnoreCase))
        {
            return CameraBackendModes.Automatic;
        }

        if (string.Equals(backendMode, CameraBackendModes.WindowsMediaFoundation, StringComparison.OrdinalIgnoreCase))
        {
            return CameraBackendModes.WindowsMediaFoundation;
        }

        if (string.Equals(backendMode, CameraBackendModes.WindowsFfmpegFallback, StringComparison.OrdinalIgnoreCase))
        {
            return CameraBackendModes.WindowsFfmpegFallback;
        }

        if (string.Equals(backendMode, CameraBackendModes.LinuxV4l2Ffmpeg, StringComparison.OrdinalIgnoreCase))
        {
            return CameraBackendModes.LinuxV4l2Ffmpeg;
        }

        return backendMode;
    }
}
