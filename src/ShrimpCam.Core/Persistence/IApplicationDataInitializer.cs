using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Persistence;

public interface IApplicationDataInitializer
{
    Task InitializeAsync(
        StorageOptions options,
        CancellationToken cancellationToken);
}
