using ShrimpCam.Core.Cameras;

namespace ShrimpCam.Core.Tests.Cameras;

public sealed class LiveStreamContractsTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Camera_live_stream_service_contract_is_public()
    {
        typeof(ICameraLiveStreamService).IsPublic.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Camera_live_stream_session_contract_is_public()
    {
        typeof(ICameraLiveStreamSession).IsPublic.Should().BeTrue();
    }
}
