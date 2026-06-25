using ShrimpCam.Infrastructure.Time;

#pragma warning disable CA2007

namespace ShrimpCam.Infrastructure.Tests.Time;

public sealed class SystemAsyncDelayTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Delay_async_honors_completed_delays()
    {
        var delay = new SystemAsyncDelay();

        var act = () => delay.DelayAsync(TimeSpan.Zero, CancellationToken.None);

        await act.Should().NotThrowAsync().ConfigureAwait(true);
    }
}

#pragma warning restore CA2007
