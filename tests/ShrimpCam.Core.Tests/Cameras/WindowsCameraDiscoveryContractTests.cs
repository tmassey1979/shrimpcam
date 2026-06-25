using ShrimpCam.Core.Cameras;

namespace ShrimpCam.Core.Tests.Cameras;

public sealed class WindowsCameraDiscoveryContractTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Windows_camera_discovery_contract_is_public()
    {
        typeof(IWindowsCameraDiscovery).IsPublic.Should().BeTrue();
    }
}
