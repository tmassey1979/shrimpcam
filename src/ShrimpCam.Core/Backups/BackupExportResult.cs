namespace ShrimpCam.Core.Backups;

public sealed record BackupExportResult(
    bool Succeeded,
    string? FailureReason,
    string? ArchivePath,
    string? FileName,
    long ArchiveSizeBytes,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc)
{
    public static BackupExportResult Success(
        string archivePath,
        string fileName,
        long archiveSizeBytes,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc) =>
        new(true, null, archivePath, fileName, archiveSizeBytes, startedAtUtc, completedAtUtc);

    public static BackupExportResult Failure(
        string failureReason,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc) =>
        new(false, failureReason, null, null, 0, startedAtUtc, completedAtUtc);
}
