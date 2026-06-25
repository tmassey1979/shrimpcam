using ShrimpCam.Core.Captures;

namespace ShrimpCam.Core.Tests.Captures;

public sealed class ScheduledCaptureContractsTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Scheduled_capture_service_contract_is_public()
    {
        typeof(IScheduledCaptureService).IsPublic.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Scheduled_capture_state_store_contract_is_public()
    {
        typeof(IScheduledCaptureStateStore).IsPublic.Should().BeTrue();
    }
}
