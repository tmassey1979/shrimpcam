using ShrimpCam.Core.Captures;

namespace ShrimpCam.Core.Tests.Captures;

public sealed class CaptureStorageContractTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Capture_storage_contracts_are_public()
    {
        typeof(ICaptureStorage).IsPublic.Should().BeTrue();
        typeof(CaptureStorageRequest).IsPublic.Should().BeTrue();
        typeof(StoredCapture).IsPublic.Should().BeTrue();
        typeof(CaptureSourceTypes).IsPublic.Should().BeTrue();
    }
}
