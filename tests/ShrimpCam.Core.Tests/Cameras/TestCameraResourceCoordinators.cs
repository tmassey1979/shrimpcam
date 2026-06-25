using ShrimpCam.Core.Cameras;

namespace ShrimpCam.Core.Tests.Cameras;

internal sealed class AlwaysAvailableCameraResourceCoordinator : ICameraResourceCoordinator
{
    public ValueTask<CameraResourceLease?> TryAcquireAsync(string owner, CancellationToken cancellationToken) =>
        ValueTask.FromResult<CameraResourceLease?>(new CameraResourceLease(owner));
}

internal sealed class BusyCameraResourceCoordinator : ICameraResourceCoordinator
{
    public ValueTask<CameraResourceLease?> TryAcquireAsync(string owner, CancellationToken cancellationToken) =>
        ValueTask.FromResult<CameraResourceLease?>(null);
}
