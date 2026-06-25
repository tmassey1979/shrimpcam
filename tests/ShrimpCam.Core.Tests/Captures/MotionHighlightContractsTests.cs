using ShrimpCam.Core.Captures;

namespace ShrimpCam.Core.Tests.Captures;

public sealed class MotionHighlightContractsTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Motion_highlight_contracts_are_public()
    {
        typeof(MotionHighlightEvent).IsPublic.Should().BeTrue();
        typeof(MotionHighlightOutcome).IsPublic.Should().BeTrue();
        typeof(MotionHighlightPlan).IsPublic.Should().BeTrue();
        typeof(MotionHighlightPlanner).IsPublic.Should().BeTrue();
        typeof(MotionHighlightResult).IsPublic.Should().BeTrue();
        typeof(MotionHighlightState).IsPublic.Should().BeTrue();
        typeof(IMotionHighlightService).IsPublic.Should().BeTrue();
        typeof(IMotionHighlightStateStore).IsPublic.Should().BeTrue();
        typeof(MotionHighlightService).IsPublic.Should().BeTrue();
    }
}
