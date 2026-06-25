namespace ShrimpCam.Core.Backups;

public interface IBackupStorageCapacityProbe
{
    bool HasAvailableSpace(string directoryPath, long requiredBytes);
}
