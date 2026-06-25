using ShrimpCam.Core.Authentication;

namespace ShrimpCam.Core.Tests.Authentication;

public sealed class Pbkdf2PasswordHasherTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Hash_password_creates_a_verifiable_hash()
    {
        var hasher = new Pbkdf2PasswordHasher();

        var hash = hasher.HashPassword("shrimp-password");

        hash.Should().StartWith("pbkdf2-sha256$");
        hasher.VerifyPassword("shrimp-password", hash).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Verify_password_rejects_an_incorrect_password()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var hash = hasher.HashPassword("shrimp-password");

        hasher.VerifyPassword("wrong-password", hash).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Verify_password_rejects_malformed_hash_payloads()
    {
        var hasher = new Pbkdf2PasswordHasher();

        hasher.VerifyPassword("shrimp-password", "bad-format").Should().BeFalse();
        hasher.VerifyPassword("shrimp-password", "pbkdf2-sha256$abc$salt$hash").Should().BeFalse();
    }
}
