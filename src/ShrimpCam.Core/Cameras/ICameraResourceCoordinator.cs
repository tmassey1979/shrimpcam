namespace ShrimpCam.Core.Cameras;

public interface ICameraResourceCoordinator
{
    ValueTask<CameraResourceLease?> TryAcquireAsync(string owner, CancellationToken cancellationToken);
}
