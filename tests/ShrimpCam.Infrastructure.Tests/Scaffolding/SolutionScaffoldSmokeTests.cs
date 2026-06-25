namespace ShrimpCam.Infrastructure.Tests.Scaffolding;

public sealed class SolutionScaffoldSmokeTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Infrastructure_test_project_scaffold_is_ready()
    {
        true.Should().BeTrue();
    }
}
