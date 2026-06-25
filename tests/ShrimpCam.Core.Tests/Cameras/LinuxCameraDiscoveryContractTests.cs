using ShrimpCam.Core.Cameras;

namespace ShrimpCam.Core.Tests.Cameras;

public sealed class LinuxCameraDiscoveryContractTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Linux_camera_discovery_contract_and_shared_descriptor_are_public()
    {
        typeof(ILinuxCameraDiscovery).IsPublic.Should().BeTrue();
        typeof(CameraDescriptor).IsPublic.Should().BeTrue();
        typeof(CameraPlatforms).IsPublic.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Camera_descriptor_exposes_constructor_values()
    {
        var descriptor = new CameraDescriptor("Logitech C920", "/dev/video0", CameraPlatforms.Linux);

        descriptor.DisplayName.Should().Be("Logitech C920");
        descriptor.DevicePath.Should().Be("/dev/video0");
        descriptor.Platform.Should().Be(CameraPlatforms.Linux);
    }
}
