using ShrimpCam.Core.Authentication;

namespace ShrimpCam.Core.Tests.Authentication;

public sealed class DefaultPasswordPolicyTests
{
    [Theory]
    [InlineData("StrongShrimp123", true)]
    [InlineData("short1A", false)]
    [InlineData("alllowercase123", false)]
    [InlineData("ALLUPPERCASE123", false)]
    [InlineData("NoDigitsInThisPass", false)]
    [Trait("Category", "Unit")]
    public void Password_policy_enforces_expected_strength_rules(string password, bool expected)
    {
        var policy = new DefaultPasswordPolicy();

        policy.IsSatisfiedBy(password).Should().Be(expected);
    }
}
