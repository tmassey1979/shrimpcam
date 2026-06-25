using ShrimpCam.Core.Cameras;

namespace ShrimpCam.Core.Tests.Cameras;

public sealed class CameraCommandFactoryContractTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Camera_command_factory_contract_is_public()
    {
        typeof(ICameraCommandFactory).IsPublic.Should().BeTrue();
    }
}
