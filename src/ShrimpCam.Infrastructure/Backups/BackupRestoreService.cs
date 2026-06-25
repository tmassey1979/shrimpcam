using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Backups;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Infrastructure.Persistence;

namespace ShrimpCam.Infrastructure.Backups;

internal sealed class BackupRestoreService(
    IClock clock,
    IOptions<ShrimpCamOptions> options) : IBackupRestoreService
{
    private static readonly SemaphoreSlim RestoreLock = new(1, 1);

    public async Task<BackupRestoreResult> RestoreAsync(BackupRestoreRequest request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ArchivePath);
        cancellationToken.ThrowIfCancellationRequested();

        var startedAtUtc = clock.UtcNow;
        if (!await RestoreLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return BackupRestoreResult.Failure(BackupRestoreFailureReasons.RestoreFailed, startedAtUtc, clock.UtcNow);
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), $"shrimpcam-restore-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempRoot);
            var extractedRoot = Path.Combine(tempRoot, "extracted");
            var rollbackRoot = Path.Combine(tempRoot, "rollback");

            if (!TryValidateAndExtract(request.ArchivePath, extractedRoot, out var failureReason))
            {
                return BackupRestoreResult.Failure(failureReason, startedAtUtc, clock.UtcNow);
            }

            var extractedDatabasePath = Path.Combine(extractedRoot, "database", "shrimpcam.db");
            if (!ValidateDatabaseSchema(extractedDatabasePath))
            {
                return BackupRestoreResult.Failure(BackupRestoreFailureReasons.UnsupportedSchemaVersion, startedAtUtc, clock.UtcNow);
            }

            SnapshotCurrentState(rollbackRoot);

            try
            {
                ApplyExtractedState(extractedRoot);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                RestoreRollbackState(rollbackRoot);
                return BackupRestoreResult.Failure(BackupRestoreFailureReasons.RestoreFailed, startedAtUtc, clock.UtcNow);
            }

            return BackupRestoreResult.Success(startedAtUtc, clock.UtcNow);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
            _ = RestoreLock.Release();
        }
    }

    private static bool TryValidateAndExtract(string archivePath, string extractedRoot, out string failureReason)
    {
        failureReason = BackupRestoreFailureReasons.InvalidBackupPackage;

        if (!File.Exists(archivePath))
        {
            return false;
        }

        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var manifestEntry = archive.GetEntry("manifest.json");
            if (manifestEntry is null)
            {
                return false;
            }

            using (var manifestStream = manifestEntry.Open())
            using (var manifest = JsonDocument.Parse(manifestStream))
            {
                if (!manifest.RootElement.TryGetProperty("version", out var version) || version.GetInt32() != 1)
                {
                    failureReason = BackupRestoreFailureReasons.UnsupportedSchemaVersion;
                    return false;
                }
            }

            archive.ExtractToDirectory(extractedRoot);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool ValidateDatabaseSchema(string databasePath)
    {
        if (!File.Exists(databasePath))
        {
            return false;
        }

        try
        {
            using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadOnly,
                }.ToString());
            connection.Open();
            var version = SqliteApplicationDataInitializer.GetCurrentVersion(connection);
            return version <= SqliteApplicationDataInitializer.CurrentSchemaVersion;
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    private void SnapshotCurrentState(string rollbackRoot)
    {
        Directory.CreateDirectory(rollbackRoot);
        CopyFileIfExists(options.Value.Storage.DatabasePath, Path.Combine(rollbackRoot, "database", "shrimpcam.db"));
        CopyDirectoryIfExists(options.Value.Storage.ImageRootPath, Path.Combine(rollbackRoot, "images"));
        CopyDirectoryIfExists(options.Value.Storage.TimelapseRootPath, Path.Combine(rollbackRoot, "timelapse"));
    }

    private void RestoreRollbackState(string rollbackRoot)
    {
        SqliteConnection.ClearAllPools();
        RestoreFile(Path.Combine(rollbackRoot, "database", "shrimpcam.db"), options.Value.Storage.DatabasePath);
        RestoreDirectory(Path.Combine(rollbackRoot, "images"), options.Value.Storage.ImageRootPath);
        RestoreDirectory(Path.Combine(rollbackRoot, "timelapse"), options.Value.Storage.TimelapseRootPath);
    }

    private void ApplyExtractedState(string extractedRoot)
    {
        SqliteConnection.ClearAllPools();
        RestoreFile(Path.Combine(extractedRoot, "database", "shrimpcam.db"), options.Value.Storage.DatabasePath);
        RestoreDirectory(Path.Combine(extractedRoot, "captures", "images"), options.Value.Storage.ImageRootPath);
        RestoreDirectory(Path.Combine(extractedRoot, "captures", "timelapse"), options.Value.Storage.TimelapseRootPath);
    }

    private static void RestoreFile(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        var destinationDirectory = Path.GetDirectoryName(Path.GetFullPath(destinationPath));
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    private static void RestoreDirectory(string sourcePath, string destinationPath)
    {
        if (!Directory.Exists(sourcePath))
        {
            return;
        }

        if (Directory.Exists(destinationPath))
        {
            Directory.Delete(destinationPath, recursive: true);
        }

        CopyDirectoryIfExists(sourcePath, destinationPath);
    }

    private static void CopyFileIfExists(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        var destinationDirectory = Path.GetDirectoryName(Path.GetFullPath(destinationPath));
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    private static void CopyDirectoryIfExists(string sourcePath, string destinationPath)
    {
        if (!Directory.Exists(sourcePath))
        {
            return;
        }

        foreach (var directoryPath in Directory.EnumerateDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            var targetDirectory = Path.Combine(destinationPath, Path.GetRelativePath(sourcePath, directoryPath));
            Directory.CreateDirectory(targetDirectory);
        }

        Directory.CreateDirectory(destinationPath);
        foreach (var filePath in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            var targetFile = Path.Combine(destinationPath, Path.GetRelativePath(sourcePath, filePath));
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(filePath, targetFile, overwrite: true);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
