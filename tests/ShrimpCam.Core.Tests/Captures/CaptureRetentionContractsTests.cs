using ShrimpCam.Core.Captures;

namespace ShrimpCam.Core.Tests.Captures;

public sealed class CaptureRetentionContractsTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Capture_retention_service_contract_is_public()
    {
        typeof(ICaptureRetentionService).IsPublic.Should().BeTrue();
    }
}
