namespace ShrimpCam.Core.Backups;

public sealed record BackupRestoreResult(
    bool Succeeded,
    string? FailureReason,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc)
{
    public static BackupRestoreResult Success(DateTimeOffset startedAtUtc, DateTimeOffset completedAtUtc) =>
        new(true, null, startedAtUtc, completedAtUtc);

    public static BackupRestoreResult Failure(string failureReason, DateTimeOffset startedAtUtc, DateTimeOffset completedAtUtc) =>
        new(false, failureReason, startedAtUtc, completedAtUtc);
}
