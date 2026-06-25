namespace ShrimpCam.Core.Health;

public interface IApplicationHealthService
{
    Task<ApplicationHealthReport> GetCurrentAsync(CancellationToken cancellationToken);
}
