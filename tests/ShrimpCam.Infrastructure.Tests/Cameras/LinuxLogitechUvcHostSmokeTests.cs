using System.Diagnostics;
using System.Security.Cryptography;

namespace ShrimpCam.Infrastructure.Tests.Cameras;

public sealed class LinuxLogitechUvcHostSmokeTests
{
    private const string EnableVariable = "SHRIMPCAM_HOST_SMOKE_LINUX_UVC";
    private const string SourceVariable = "SHRIMPCAM_HOST_SMOKE_LINUX_UVC_SOURCE";
    private const string FfmpegVariable = "SHRIMPCAM_HOST_SMOKE_FFMPEG";

    [Fact]
    [Trait("Category", "HostSmoke")]
    [Trait("Platform", "Linux")]
    public async Task Logitech_uvc_camera_provides_multiple_frames_on_linux_host()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(EnableVariable), "1", StringComparison.Ordinal))
        {
            return;
        }

        OperatingSystem.IsLinux().Should().BeTrue(
            $"set {EnableVariable}=1 only on a Raspberry Pi OS Lite or Linux host with a Logitech UVC webcam");

        var source = Environment.GetEnvironmentVariable(SourceVariable) ?? "/dev/video0";
        File.Exists(source).Should().BeTrue($"the configured V4L2 source '{source}' must exist");

        var outputDirectory = Path.Combine(Path.GetTempPath(), $"shrimpcam-linux-uvc-smoke-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var stderr = await RunFfmpegFrameCaptureAsync(
                    Environment.GetEnvironmentVariable(FfmpegVariable) ?? "ffmpeg",
                    source,
                    Path.Combine(outputDirectory, "frame-%02d.jpg"))
                .ConfigureAwait(true);

            var frames = Directory.GetFiles(outputDirectory, "frame-*.jpg");
            frames.Should().HaveCountGreaterThanOrEqualTo(2, stderr);
            frames.Select(ReadHash).Distinct(StringComparer.Ordinal).Should().HaveCountGreaterThanOrEqualTo(1);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static async Task<string> RunFfmpegFrameCaptureAsync(
        string ffmpegPath,
        string source,
        string outputPattern)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments =
                "-hide_banner -loglevel error -f video4linux2 -framerate 15 -video_size 640x480 " +
                $"-i {QuoteArgument(source)} -frames:v 8 {QuoteArgument(outputPattern)}",
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        process.Start().Should().BeTrue();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        process.ExitCode.Should().Be(0, stderr);
        return stderr;
    }

    private static string ReadHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string QuoteArgument(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
