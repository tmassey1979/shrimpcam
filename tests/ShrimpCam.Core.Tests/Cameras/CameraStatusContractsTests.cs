using ShrimpCam.Core.Cameras;

namespace ShrimpCam.Core.Tests.Cameras;

public sealed class CameraStatusContractsTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Camera_status_service_contract_is_public()
    {
        typeof(ICameraStatusService).IsPublic.Should().BeTrue();
    }
}
