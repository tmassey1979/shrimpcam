using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Cameras;

public static class CameraRecoveryPlanner
{
    public static bool ShouldRetry(CameraOptions options, int failureCount)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegative(failureCount);

        return failureCount <= options.ReconnectRetryAttempts;
    }

    public static TimeSpan GetBackoffDelay(CameraOptions options, int failureCount)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(failureCount, 1);

        var multiplier = Math.Pow(2, failureCount - 1);
        return TimeSpan.FromSeconds(options.ReconnectBackoffSeconds * multiplier);
    }
}
