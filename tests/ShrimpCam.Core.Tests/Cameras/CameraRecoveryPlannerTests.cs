using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Tests.Cameras;

public sealed class CameraRecoveryPlannerTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Retry_policy_allows_failures_within_configured_threshold()
    {
        var options = new CameraOptions
        {
            Platform = "Linux",
            Source = "/dev/video0",
            ReconnectRetryAttempts = 2,
            ReconnectBackoffSeconds = 1,
        };

        CameraRecoveryPlanner.ShouldRetry(options, 1).Should().BeTrue();
        CameraRecoveryPlanner.ShouldRetry(options, 2).Should().BeTrue();
        CameraRecoveryPlanner.ShouldRetry(options, 3).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Retry_backoff_uses_exponential_growth()
    {
        var options = new CameraOptions
        {
            Platform = "Linux",
            Source = "/dev/video0",
            ReconnectRetryAttempts = 3,
            ReconnectBackoffSeconds = 2,
        };

        CameraRecoveryPlanner.GetBackoffDelay(options, 1).Should().Be(TimeSpan.FromSeconds(2));
        CameraRecoveryPlanner.GetBackoffDelay(options, 2).Should().Be(TimeSpan.FromSeconds(4));
        CameraRecoveryPlanner.GetBackoffDelay(options, 3).Should().Be(TimeSpan.FromSeconds(8));
    }
}
