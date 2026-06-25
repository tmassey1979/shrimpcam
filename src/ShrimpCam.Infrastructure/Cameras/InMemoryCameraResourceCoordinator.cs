using ShrimpCam.Core.Cameras;

namespace ShrimpCam.Infrastructure.Cameras;

internal sealed class InMemoryCameraResourceCoordinator : ICameraResourceCoordinator, IDisposable
{
    private readonly SemaphoreSlim _cameraGate = new(1, 1);

    public async ValueTask<CameraResourceLease?> TryAcquireAsync(string owner, CancellationToken cancellationToken)
    {
        var acquired = await _cameraGate.WaitAsync(TimeSpan.Zero, cancellationToken).ConfigureAwait(false);
        return acquired
            ? new CameraResourceLease(owner, ReleaseAsync)
            : null;
    }

    private ValueTask ReleaseAsync()
    {
        _cameraGate.Release();
        return ValueTask.CompletedTask;
    }

    public void Dispose() => _cameraGate.Dispose();
}
