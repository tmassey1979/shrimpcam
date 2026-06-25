using Microsoft.Extensions.Logging;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Cameras;

namespace ShrimpCam.Infrastructure.Cameras.Windows;

internal sealed class WindowsCameraDiscovery(
    IProcessRunner processRunner,
    ILogger<WindowsCameraDiscovery> logger) : IWindowsCameraDiscovery
{
    private static readonly ProcessRequest DiscoveryRequest = new(
        "ffmpeg",
        "-hide_banner -list_devices true -f dshow -i dummy");
    private static readonly Action<ILogger, Exception?> NoWindowsCamerasDetected =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(1002, nameof(NoWindowsCamerasDetected)),
            "No Windows cameras were detected by the discovery command.");

    public async Task<IReadOnlyList<CameraDescriptor>> DiscoverAsync(CancellationToken cancellationToken)
    {
        var result = await processRunner.RunAsync(DiscoveryRequest, cancellationToken).ConfigureAwait(false);
        var parserInput = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        var cameras = ParseOutput(parserInput);

        if (result.ExitCode != 0 && cameras.Count == 0)
        {
            throw new InvalidOperationException(
                $"Windows camera discovery failed with exit code {result.ExitCode}: {parserInput}");
        }

        if (cameras.Count == 0)
        {
            NoWindowsCamerasDetected(logger, null);
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
            var line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (TryGetQuotedValue(line, out var quotedValue))
            {
                if (line.Contains("Alternative name", StringComparison.Ordinal))
                {
                    if (!string.IsNullOrWhiteSpace(currentDisplayName))
                    {
                        cameras.Add(new CameraDescriptor(currentDisplayName, quotedValue, CameraPlatforms.Windows));
                    }

                    continue;
                }

                currentDisplayName = quotedValue;
            }
        }

        return cameras;
    }

    private static bool TryGetQuotedValue(string line, out string value)
    {
        var firstQuote = line.IndexOf('"');
        var lastQuote = line.LastIndexOf('"');

        if (firstQuote < 0 || lastQuote <= firstQuote)
        {
            value = string.Empty;
            return false;
        }

        value = line.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
        return !string.IsNullOrWhiteSpace(value);
    }
}
