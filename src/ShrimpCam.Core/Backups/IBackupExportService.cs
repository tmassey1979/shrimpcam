namespace ShrimpCam.Core.Backups;

public interface IBackupExportService
{
    Task<BackupExportResult> ExportAsync(CancellationToken cancellationToken);
}
