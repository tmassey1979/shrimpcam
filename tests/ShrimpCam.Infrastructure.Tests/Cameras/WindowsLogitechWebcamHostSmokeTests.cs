using System.Diagnostics;
using System.Security.Cryptography;

namespace ShrimpCam.Infrastructure.Tests.Cameras;

public sealed class WindowsLogitechWebcamHostSmokeTests
{
    private const string EnableVariable = "SHRIMPCAM_HOST_SMOKE_WINDOWS_WEBCAM";
    private const string SourceVariable = "SHRIMPCAM_HOST_SMOKE_WINDOWS_WEBCAM_SOURCE";
    private const string FfmpegVariable = "SHRIMPCAM_HOST_SMOKE_FFMPEG";

    [Fact]
    [Trait("Category", "HostSmoke")]
    [Trait("Platform", "Windows")]
    public async Task Logitech_webcam_provides_multiple_frames_on_windows_host()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(EnableVariable), "1", StringComparison.Ordinal))
        {
            return;
        }

        OperatingSystem.IsWindows().Should().BeTrue(
            $"set {EnableVariable}=1 only on a Windows host with a Logitech webcam");

        var source = Environment.GetEnvironmentVariable(SourceVariable) ?? "Logi C270 HD WebCam";
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"shrimpcam-windows-webcam-smoke-{Guid.NewGuid():N}");
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
                "-hide_banner -loglevel error -f dshow -framerate 15 -video_size 640x480 " +
                $"-i video={QuoteWindowsSource(source)} -frames:v 8 {QuoteArgument(outputPattern)}",
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

    private static string QuoteWindowsSource(string value) =>
        "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static string QuoteArgument(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
