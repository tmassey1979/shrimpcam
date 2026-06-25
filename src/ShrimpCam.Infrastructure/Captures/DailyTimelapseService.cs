using System.ComponentModel.DataAnnotations;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Captures;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Captures;

internal sealed class DailyTimelapseService(IFileSystem fileSystem, IProcessRunner processRunner) : IDailyTimelapseService
{
    private const int MinimumFrameCount = 2;

    public async Task<DailyTimelapseGenerationResult> GenerateAsync(
        StorageOptions options,
        DateOnly day,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateOptions(options);

        var imageRoot = Path.GetFullPath(options.ImageRootPath);
        var timelapseRoot = Path.GetFullPath(options.TimelapseRootPath);
        var frameDirectory = GetFrameDirectory(imageRoot, day);
        var outputDirectory = fileSystem.Combine(
            timelapseRoot,
            day.Year.ToString("0000", System.Globalization.CultureInfo.InvariantCulture),
            day.Month.ToString("00", System.Globalization.CultureInfo.InvariantCulture));
        var outputPath = fileSystem.Combine(
            outputDirectory,
            $"{day:yyyyMMdd}_timelapse.mp4");

        var framePaths = GetOrderedFramePaths(frameDirectory).ToArray();
        if (fileSystem.FileExists(outputPath))
        {
            var relativeOutput = Path.GetRelativePath(timelapseRoot, outputPath).Replace('\\', '/');
            return new DailyTimelapseGenerationResult(
                DailyTimelapseGenerationStatus.AlreadyExists,
                day,
                framePaths.Length,
                outputPath,
                relativeOutput);
        }

        if (framePaths.Length < MinimumFrameCount)
        {
            return new DailyTimelapseGenerationResult(
                DailyTimelapseGenerationStatus.SkippedInsufficientFrames,
                day,
                framePaths.Length,
                VideoPath: null,
                RelativeVideoPath: null);
        }

        if (!fileSystem.DirectoryExists(outputDirectory))
        {
            fileSystem.CreateDirectory(outputDirectory);
        }

        var manifestPath = fileSystem.Combine(outputDirectory, $"{day:yyyyMMdd}_frames.txt");

        try
        {
            fileSystem.WriteAllLines(
                manifestPath,
                framePaths.Select(path => $"file '{EscapeForConcat(path)}'"));

            var command = new ProcessRequest(
                "ffmpeg",
                $"-hide_banner -loglevel error -y -f concat -safe 0 -i \"{EscapeForConcat(manifestPath)}\" -vf fps=30 -pix_fmt yuv420p \"{EscapeForConcat(outputPath)}\"");
            var result = await processRunner.RunAsync(command, cancellationToken).ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                throw new IOException(string.IsNullOrWhiteSpace(result.StandardError) ? "Timelapse generation failed." : result.StandardError);
            }

            var relativeOutput = Path.GetRelativePath(timelapseRoot, outputPath).Replace('\\', '/');
            return new DailyTimelapseGenerationResult(
                DailyTimelapseGenerationStatus.Generated,
                day,
                framePaths.Length,
                outputPath,
                relativeOutput);
        }
        finally
        {
            if (fileSystem.FileExists(manifestPath))
            {
                fileSystem.DeleteFile(manifestPath);
            }
        }
    }

    internal IEnumerable<string> GetOrderedFramePaths(string frameDirectory)
    {
        if (!fileSystem.DirectoryExists(frameDirectory))
        {
            return [];
        }

        return fileSystem.EnumerateFiles(frameDirectory, "*.jpg", SearchOption.TopDirectoryOnly)
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
    }

    internal string GetFrameDirectory(string imageRoot, DateOnly day) =>
        fileSystem.Combine(
            imageRoot,
            day.Year.ToString("0000", System.Globalization.CultureInfo.InvariantCulture),
            day.Month.ToString("00", System.Globalization.CultureInfo.InvariantCulture),
            day.Day.ToString("00", System.Globalization.CultureInfo.InvariantCulture));

    private static string EscapeForConcat(string path) =>
        path.Replace("'", "'\\''", StringComparison.Ordinal);

    private static void ValidateOptions(StorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ImageRootPath))
        {
            throw new ValidationException("Storage image root path is required.");
        }

        if (string.IsNullOrWhiteSpace(options.TimelapseRootPath))
        {
            throw new ValidationException("Timelapse output root path is required.");
        }
    }
}
