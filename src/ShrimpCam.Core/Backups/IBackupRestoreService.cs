namespace ShrimpCam.Core.Backups;

public interface IBackupRestoreService
{
    Task<BackupRestoreResult> RestoreAsync(BackupRestoreRequest request, CancellationToken cancellationToken);
}
