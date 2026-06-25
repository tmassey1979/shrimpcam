using ShrimpCam.Core.Backups;

namespace ShrimpCam.Infrastructure.Backups;

internal sealed class DriveInfoBackupStorageCapacityProbe : IBackupStorageCapacityProbe
{
    public bool HasAvailableSpace(string directoryPath, long requiredBytes)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(directoryPath));
        if (string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        var driveInfo = new DriveInfo(root);
        return driveInfo.AvailableFreeSpace >= requiredBytes;
    }
}
