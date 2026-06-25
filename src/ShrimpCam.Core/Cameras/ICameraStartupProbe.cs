namespace ShrimpCam.Core.Cameras;

public interface ICameraStartupProbe
{
    Task CheckAsync(CancellationToken cancellationToken);
}
