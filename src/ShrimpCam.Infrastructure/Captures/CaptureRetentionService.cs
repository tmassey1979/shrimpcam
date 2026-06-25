using System.ComponentModel.DataAnnotations;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Captures;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Captures;

internal sealed class CaptureRetentionService(IClock clock, IFileSystem fileSystem) : ICaptureRetentionService
{
    private static readonly string[] ManagedExtensions = [".jpg", ".json"];

    public Task<CaptureCleanupResult> CleanupExpiredCapturesAsync(
        StorageOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateOptions(options);

        var rootPath = Path.GetFullPath(options.ImageRootPath);
        var cutoffUtc = clock.UtcNow.AddDays(-options.RetentionDays);

        if (!fileSystem.DirectoryExists(rootPath))
        {
            return Task.FromResult(new CaptureCleanupResult(cutoffUtc, 0, 0, []));
        }

        var expiredGroups = FindExpiredCaptureGroups(rootPath, cutoffUtc);
        var itemResults = new List<CaptureCleanupItemResult>();
        var deletedCount = 0;
        var failedCount = 0;

        foreach (var group in expiredGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var deleteFailed = false;
            string? failureReason = null;

            foreach (var path in group.Value)
            {
                try
                {
                    fileSystem.DeleteFile(path);
                }
                catch (Exception exception)
                {
                    deleteFailed = true;
                    failureReason = exception.Message;
                }
            }

            if (deleteFailed)
            {
                failedCount++;
                itemResults.Add(new CaptureCleanupItemResult(group.Key, false, failureReason));
            }
            else
            {
                deletedCount++;
                itemResults.Add(new CaptureCleanupItemResult(group.Key, true, null));
            }
        }

        return Task.FromResult(new CaptureCleanupResult(cutoffUtc, deletedCount, failedCount, itemResults));
    }

    internal Dictionary<string, List<string>> FindExpiredCaptureGroups(string rootPath, DateTimeOffset cutoffUtc)
    {
        var expiredGroups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in fileSystem.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories))
        {
            if (!IsManagedCaptureFile(rootPath, filePath))
            {
                continue;
            }

            if (fileSystem.GetLastWriteTimeUtc(filePath) >= cutoffUtc)
            {
                continue;
            }

            var extension = Path.GetExtension(filePath);
            var groupKey = Path.ChangeExtension(Path.GetRelativePath(rootPath, filePath), ".jpg").Replace('\\', '/');
            if (!expiredGroups.TryGetValue(groupKey, out var paths))
            {
                paths = [];
                expiredGroups[groupKey] = paths;
            }

            var canonicalPath = string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase)
                ? Path.ChangeExtension(Path.Combine(rootPath, groupKey.Replace('/', Path.DirectorySeparatorChar)), ".json")
                : Path.Combine(rootPath, groupKey.Replace('/', Path.DirectorySeparatorChar));

            if (!paths.Contains(canonicalPath, StringComparer.OrdinalIgnoreCase) && fileSystem.FileExists(canonicalPath))
            {
                paths.Add(canonicalPath);
            }
        }

        return expiredGroups;
    }

    internal static bool IsManagedCaptureFile(string rootPath, string filePath)
    {
        var normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(rootPath));
        var normalizedFile = Path.GetFullPath(filePath);

        if (!normalizedFile.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relativePath = Path.GetRelativePath(normalizedRoot, normalizedFile);
        if (relativePath.StartsWith(".shrimpcam", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var extension = Path.GetExtension(normalizedFile);
        return ManagedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

    private static void ValidateOptions(StorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ImageRootPath))
        {
            throw new ValidationException("Storage image root path is required.");
        }
    }
}
