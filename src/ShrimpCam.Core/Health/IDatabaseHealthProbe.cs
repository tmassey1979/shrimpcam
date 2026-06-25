namespace ShrimpCam.Core.Health;

public interface IDatabaseHealthProbe
{
    Task<HealthComponentReport> CheckAsync(CancellationToken cancellationToken);
}
