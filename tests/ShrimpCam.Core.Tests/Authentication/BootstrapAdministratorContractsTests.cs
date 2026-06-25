using ShrimpCam.Core.Authentication;

namespace ShrimpCam.Core.Tests.Authentication;

public sealed class BootstrapAdministratorContractsTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Bootstrap_administrator_contracts_are_public()
    {
        typeof(BootstrapAdministratorFailureReasons).IsPublic.Should().BeTrue();
        typeof(BootstrapAdministratorRequest).IsPublic.Should().BeTrue();
        typeof(BootstrapAdministratorResult).IsPublic.Should().BeTrue();
        typeof(BootstrapAdministratorUser).IsPublic.Should().BeTrue();
        typeof(IBootstrapAdministratorService).IsPublic.Should().BeTrue();
        typeof(IPasswordPolicy).IsPublic.Should().BeTrue();
        typeof(BootstrapAdministratorService).IsPublic.Should().BeTrue();
        typeof(DefaultPasswordPolicy).IsPublic.Should().BeTrue();
    }
}
