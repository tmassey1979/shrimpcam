using ShrimpCam.Core.Abstractions;

namespace ShrimpCam.Core.Tests.Abstractions;

public sealed class SharedAbstractionsSmokeTests
{
    [Fact]
    public void Shared_abstractions_are_public_and_available_for_application_services()
    {
        typeof(IClock).IsPublic.Should().BeTrue();
        typeof(IFileSystem).IsPublic.Should().BeTrue();
        typeof(IProcessRunner).IsPublic.Should().BeTrue();
        typeof(ProcessRequest).IsPublic.Should().BeTrue();
        typeof(ProcessResult).IsPublic.Should().BeTrue();
    }
}
