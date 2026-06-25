using ShrimpCam.Core.Authentication;

namespace ShrimpCam.Core.Tests.Authentication;

public sealed class AuthenticationContractsTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Authentication_contracts_are_public()
    {
        typeof(AuthenticationFailureReasons).IsPublic.Should().BeTrue();
        typeof(AuthenticationRequest).IsPublic.Should().BeTrue();
        typeof(AuthenticationResult).IsPublic.Should().BeTrue();
        typeof(AuthenticatedSession).IsPublic.Should().BeTrue();
        typeof(CredentialValidator).IsPublic.Should().BeTrue();
        typeof(IAuthenticationService).IsPublic.Should().BeTrue();
        typeof(IPasswordHasher).IsPublic.Should().BeTrue();
        typeof(LocalAuthenticationService).IsPublic.Should().BeTrue();
        typeof(Pbkdf2PasswordHasher).IsPublic.Should().BeTrue();
    }
}
