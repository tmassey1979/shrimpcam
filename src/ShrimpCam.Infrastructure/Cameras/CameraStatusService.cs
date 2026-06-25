using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Cameras;

namespace ShrimpCam.Infrastructure.Cameras;

internal sealed class CameraStatusService(IClock clock) : ICameraStatusService
{
    private readonly object _sync = new();
    private CameraStatusSnapshot _snapshot = new(CameraStatusLevel.Unknown, null, clock.UtcNow);

    public CameraStatusSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return _snapshot;
        }
    }

    public void ReportOnline()
    {
        lock (_sync)
        {
            _snapshot = new CameraStatusSnapshot(CameraStatusLevel.Online, null, clock.UtcNow);
        }
    }

    public void ReportDegraded(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        lock (_sync)
        {
            _snapshot = new CameraStatusSnapshot(CameraStatusLevel.Degraded, reason, clock.UtcNow);
        }
    }
}
