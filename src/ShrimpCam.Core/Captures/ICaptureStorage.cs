using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Captures;

public interface ICaptureStorage
{
    Task<StoredCapture> StoreAsync(
        StorageOptions options,
        CaptureStorageRequest request,
        CancellationToken cancellationToken);
}
