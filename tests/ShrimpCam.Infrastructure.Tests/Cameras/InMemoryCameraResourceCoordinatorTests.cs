using ShrimpCam.Infrastructure.Cameras;

#pragma warning disable CA2007

namespace ShrimpCam.Infrastructure.Tests.Cameras;

public sealed class InMemoryCameraResourceCoordinatorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Try_acquire_allows_one_owner_until_lease_is_released()
    {
        using var coordinator = new InMemoryCameraResourceCoordinator();

        var firstLease = await coordinator.TryAcquireAsync("live-stream", CancellationToken.None).ConfigureAwait(true);
        var blockedLease = await coordinator.TryAcquireAsync("scheduled-capture", CancellationToken.None).ConfigureAwait(true);

        firstLease.Should().NotBeNull();
        firstLease!.Owner.Should().Be("live-stream");
        blockedLease.Should().BeNull();

        await firstLease.DisposeAsync().ConfigureAwait(true);

        var nextLease = await coordinator.TryAcquireAsync("scheduled-capture", CancellationToken.None).ConfigureAwait(true);

        nextLease.Should().NotBeNull();
        nextLease!.Owner.Should().Be("scheduled-capture");

        await nextLease.DisposeAsync().ConfigureAwait(true);
    }
}

#pragma warning restore CA2007
