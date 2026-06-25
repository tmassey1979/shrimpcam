using Microsoft.Extensions.Options;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Health;

namespace ShrimpCam.Infrastructure.Health;

public sealed class StorageHealthProbe(
    IOptions<ShrimpCamOptions> optionsAccessor,
    IFileSystem fileSystem) : IStorageHealthProbe
{
    private readonly StorageOptions options = optionsAccessor.Value.Storage;

    public Task<HealthComponentReport> CheckAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            EnsureDirectory(options.ImageRootPath);
            EnsureDirectory(options.TimelapseRootPath);

            return Task.FromResult(new HealthComponentReport(HealthStatusLevel.Healthy, null));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Task.FromResult(new HealthComponentReport(HealthStatusLevel.Unhealthy, ex.Message));
        }
    }

    private void EnsureDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!fileSystem.DirectoryExists(path))
        {
            fileSystem.CreateDirectory(path);
        }
    }
}
