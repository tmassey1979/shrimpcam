using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Captures;

public interface ICaptureRetentionService
{
    Task<CaptureCleanupResult> CleanupExpiredCapturesAsync(
        StorageOptions options,
        CancellationToken cancellationToken);
}
