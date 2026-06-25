using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Captures;

public interface IDailyTimelapseService
{
    Task<DailyTimelapseGenerationResult> GenerateAsync(
        StorageOptions options,
        DateOnly day,
        CancellationToken cancellationToken);
}
