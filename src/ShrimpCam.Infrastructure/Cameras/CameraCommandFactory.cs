using System.ComponentModel.DataAnnotations;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Cameras;

internal sealed class CameraCommandFactory : ICameraCommandFactory
{
    public ProcessRequest BuildDiscoveryCommand(string platform)
    {
        var normalizedPlatform = NormalizePlatform(platform);

        return normalizedPlatform switch
        {
            CameraPlatforms.Linux => new ProcessRequest("v4l2-ctl", "--list-devices"),
            CameraPlatforms.Windows => new ProcessRequest("ffmpeg", "-hide_banner -list_devices true -f dshow -i dummy"),
            _ => throw new ValidationException($"Unsupported camera platform '{platform}'."),
        };
    }

    public ProcessRequest BuildStillCaptureCommand(CameraOptions options, string outputPath)
    {
        ValidateOptions(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        return NormalizePlatform(options.Platform) switch
        {
            CameraPlatforms.Linux => new ProcessRequest(
                "ffmpeg",
                $"-hide_banner -loglevel error -f video4linux2 -video_size {options.CaptureWidth}x{options.CaptureHeight} -i {QuoteLinuxSource(options.Source)} -frames:v 1 {QuotePath(outputPath)}"),
            CameraPlatforms.Windows => new ProcessRequest(
                "ffmpeg",
                $"-hide_banner -loglevel error -f dshow -video_size {options.CaptureWidth}x{options.CaptureHeight} -i video={QuoteWindowsSource(options.Source)} -frames:v 1 {QuotePath(outputPath)}"),
            _ => throw new ValidationException($"Unsupported camera platform '{options.Platform}'."),
        };
    }

    public ProcessRequest BuildLiveStreamCommand(CameraOptions options)
    {
        ValidateOptions(options);

        return NormalizePlatform(options.Platform) switch
        {
            CameraPlatforms.Linux => new ProcessRequest(
                "ffmpeg",
                $"-hide_banner -loglevel error -f video4linux2 -framerate {options.StreamFramesPerSecond} -video_size {options.StreamWidth}x{options.StreamHeight} -i {QuoteLinuxSource(options.Source)} -f mpjpeg -boundary_tag {LiveStreamConstants.Boundary} -q:v 5 pipe:1"),
            CameraPlatforms.Windows => new ProcessRequest(
                "ffmpeg",
                $"-hide_banner -loglevel error -f dshow -framerate {options.StreamFramesPerSecond} -video_size {options.StreamWidth}x{options.StreamHeight} -i video={QuoteWindowsSource(options.Source)} -f mpjpeg -boundary_tag {LiveStreamConstants.Boundary} -q:v 5 pipe:1"),
            _ => throw new ValidationException($"Unsupported camera platform '{options.Platform}'."),
        };
    }

    private static void ValidateOptions(CameraOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);

        if (!isValid)
        {
            throw new ValidationException(results[0].ErrorMessage);
        }
    }

    private static string NormalizePlatform(string platform)
    {
        if (string.Equals(platform, CameraPlatforms.Linux, StringComparison.OrdinalIgnoreCase))
        {
            return CameraPlatforms.Linux;
        }

        if (string.Equals(platform, CameraPlatforms.Windows, StringComparison.OrdinalIgnoreCase))
        {
            return CameraPlatforms.Windows;
        }

        return platform;
    }

    private static string QuoteLinuxSource(string source) => QuotePath(source);

    private static string QuoteWindowsSource(string source) =>
        "\"" + source.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static string QuotePath(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
