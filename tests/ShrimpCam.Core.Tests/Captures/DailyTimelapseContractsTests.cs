using ShrimpCam.Core.Captures;

namespace ShrimpCam.Core.Tests.Captures;

public sealed class DailyTimelapseContractsTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Daily_timelapse_service_contract_is_public()
    {
        typeof(IDailyTimelapseService).IsPublic.Should().BeTrue();
    }
}
