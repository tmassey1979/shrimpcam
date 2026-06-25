namespace ShrimpCam.Core.Health;

public interface IStorageHealthProbe
{
    Task<HealthComponentReport> CheckAsync(CancellationToken cancellationToken);
}
