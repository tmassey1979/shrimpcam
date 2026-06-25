using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Backups;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Diagnostics;

namespace ShrimpCam.Infrastructure.Backups;

internal sealed class BackupExportService(
    IClock clock,
    IOptions<ShrimpCamOptions> options,
    IDiagnosticsBundleService diagnosticsBundleService,
    IBackupStorageCapacityProbe capacityProbe) : IBackupExportService
{
    private const int ManifestVersion = 1;
    private const long MinimumRequiredFreeBytes = 10L * 1024L * 1024L;
    private static readonly SemaphoreSlim ExportLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public async Task<BackupExportResult> ExportAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startedAtUtc = clock.UtcNow;
        if (!await ExportLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return BackupExportResult.Failure(BackupExportFailureReasons.ExportAlreadyRunning, startedAtUtc, clock.UtcNow);
        }

        try
        {
            return await ExportCoreAsync(startedAtUtc, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = ExportLock.Release();
        }
    }

    private async Task<BackupExportResult> ExportCoreAsync(DateTimeOffset startedAtUtc, CancellationToken cancellationToken)
    {
        var storageOptions = options.Value.Storage;
        var backupRoot = GetBackupRoot(storageOptions);

        try
        {
            Directory.CreateDirectory(backupRoot);

            if (!capacityProbe.HasAvailableSpace(backupRoot, MinimumRequiredFreeBytes))
            {
                return BackupExportResult.Failure(BackupExportFailureReasons.InsufficientStorage, startedAtUtc, clock.UtcNow);
            }

            var fileName = $"shrimpcam-backup-{startedAtUtc:yyyyMMddTHHmmssfffZ}.zip";
            var archivePath = Path.Combine(backupRoot, fileName);
            var diagnostics = await diagnosticsBundleService.GenerateAsync(cancellationToken).ConfigureAwait(false);
            var includedFiles = GetIncludedFiles(storageOptions);
            var manifest = new BackupManifest(
                ManifestVersion,
                startedAtUtc,
                diagnostics.Health.Status,
                includedFiles.Select(file => file.ArchivePath).Prepend("manifest.json").Prepend("diagnostics.json").ToArray());

            using (var archiveStream = File.Create(archivePath))
            using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create))
            {
                await WriteJsonEntryAsync(archive, "manifest.json", manifest, cancellationToken).ConfigureAwait(false);
                await WriteJsonEntryAsync(archive, "diagnostics.json", diagnostics, cancellationToken).ConfigureAwait(false);

                foreach (var includedFile in includedFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await CopyFileToArchiveAsync(archive, includedFile, cancellationToken).ConfigureAwait(false);
                }
            }

            var completedAtUtc = clock.UtcNow;
            return BackupExportResult.Success(
                archivePath,
                fileName,
                new FileInfo(archivePath).Length,
                startedAtUtc,
                completedAtUtc);
        }
        catch (IOException)
        {
            return BackupExportResult.Failure(BackupExportFailureReasons.StorageUnavailable, startedAtUtc, clock.UtcNow);
        }
        catch (UnauthorizedAccessException)
        {
            return BackupExportResult.Failure(BackupExportFailureReasons.StorageUnavailable, startedAtUtc, clock.UtcNow);
        }
    }

    private static string GetBackupRoot(StorageOptions storageOptions)
    {
        var databaseDirectory = Path.GetDirectoryName(Path.GetFullPath(storageOptions.DatabasePath));
        return Path.Combine(databaseDirectory ?? AppContext.BaseDirectory, "backups");
    }

    private static List<IncludedFile> GetIncludedFiles(StorageOptions storageOptions)
    {
        var includedFiles = new List<IncludedFile>();
        AddFileIfExists(includedFiles, storageOptions.DatabasePath, "database/shrimpcam.db");
        AddDirectoryFiles(includedFiles, storageOptions.ImageRootPath, "captures/images");
        AddDirectoryFiles(includedFiles, storageOptions.TimelapseRootPath, "captures/timelapse");
        return includedFiles;
    }

    private static void AddFileIfExists(List<IncludedFile> includedFiles, string sourcePath, string archivePath)
    {
        if (File.Exists(sourcePath))
        {
            includedFiles.Add(new IncludedFile(sourcePath, archivePath));
        }
    }

    private static void AddDirectoryFiles(List<IncludedFile> includedFiles, string directoryPath, string archiveRoot)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(directoryPath, filePath).Replace('\\', '/');
            includedFiles.Add(new IncludedFile(filePath, FormattableString.Invariant($"{archiveRoot}/{relativePath}")));
        }
    }

    private static async Task WriteJsonEntryAsync<T>(
        ZipArchive archive,
        string entryName,
        T value,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        using var entryStream = entry.Open();
        await JsonSerializer.SerializeAsync(entryStream, value, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private static async Task CopyFileToArchiveAsync(
        ZipArchive archive,
        IncludedFile includedFile,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(includedFile.ArchivePath, CompressionLevel.Fastest);
        using var sourceStream = new FileStream(
            includedFile.SourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var destinationStream = entry.Open();
        await sourceStream.CopyToAsync(destinationStream, cancellationToken).ConfigureAwait(false);
    }

    private sealed record IncludedFile(string SourcePath, string ArchivePath);

    private sealed record BackupManifest(
        int Version,
        DateTimeOffset GeneratedAtUtc,
        string HealthStatus,
        IReadOnlyList<string> Entries);
}
