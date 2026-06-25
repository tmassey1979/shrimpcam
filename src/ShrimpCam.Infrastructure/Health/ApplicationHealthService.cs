using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Health;

namespace ShrimpCam.Infrastructure.Health;

public sealed class ApplicationHealthService(
    IClock clock,
    ICameraStatusService cameraStatusService,
    IDatabaseHealthProbe databaseHealthProbe,
    IStorageHealthProbe storageHealthProbe) : IApplicationHealthService
{
    public async Task<ApplicationHealthReport> GetCurrentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var database = await databaseHealthProbe.CheckAsync(cancellationToken).ConfigureAwait(false);
        var storage = await storageHealthProbe.CheckAsync(cancellationToken).ConfigureAwait(false);
        var camera = MapCameraComponent(cameraStatusService.GetSnapshot());

        var components = new Dictionary<string, HealthComponentReport>
        {
            ["app"] = new(HealthStatusLevel.Healthy, null),
            ["database"] = database,
            ["storage"] = storage,
            ["camera"] = camera,
        };

        var status = DetermineOverallStatus(database, storage, camera);
        return new ApplicationHealthReport(status, clock.UtcNow, components);
    }

    private static string DetermineOverallStatus(
        HealthComponentReport database,
        HealthComponentReport storage,
        HealthComponentReport camera)
    {
        if (database.Status == HealthStatusLevel.Unhealthy || storage.Status == HealthStatusLevel.Unhealthy)
        {
            return HealthStatusLevel.Unhealthy;
        }

        if (camera.Status == HealthStatusLevel.Unhealthy)
        {
            return HealthStatusLevel.Degraded;
        }

        return HealthStatusLevel.Healthy;
    }

    private static HealthComponentReport MapCameraComponent(CameraStatusSnapshot snapshot) =>
        snapshot.Status == CameraStatusLevel.Degraded
            ? new HealthComponentReport(HealthStatusLevel.Unhealthy, snapshot.Reason ?? "Camera is unavailable.")
            : new HealthComponentReport(HealthStatusLevel.Healthy, null);
}
