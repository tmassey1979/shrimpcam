using Microsoft.Extensions.DependencyInjection;
using ShrimpCam.Core.Captures;
using ShrimpCam.Core.Configuration;

#pragma warning disable CA2007

namespace ShrimpCam.Infrastructure.Tests.Captures;

public sealed class MotionHighlightStateStoreTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task State_store_round_trips_motion_highlight_state_on_disk()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var options = new StorageOptions
        {
            ImageRootPath = rootPath,
            TimelapseRootPath = Path.Combine(rootPath, "timelapse"),
            RetentionDays = 30,
        };
        var expected = new MotionHighlightState(
            new DateTimeOffset(2026, 06, 25, 00, 10, 00, TimeSpan.Zero),
            "event-100",
            new DateTimeOffset(2026, 06, 25, 00, 10, 00, TimeSpan.Zero));

        try
        {
            var services = new ServiceCollection();
            Infrastructure.DependencyInjection.AddInfrastructure(services);

            using var provider = services.BuildServiceProvider();
            var store = provider.GetRequiredService<IMotionHighlightStateStore>();

            await store.SaveAsync(options, expected, CancellationToken.None).ConfigureAwait(true);
            var actual = await store.LoadAsync(options, CancellationToken.None).ConfigureAwait(true);

            actual.Should().Be(expected);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Infrastructure_registers_motion_highlight_services()
    {
        var services = new ServiceCollection();
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        Infrastructure.DependencyInjection.AddInfrastructure(services);
        services.AddSingleton(
            Microsoft.Extensions.Options.Options.Create(
                new ShrimpCamOptions
                {
                    Storage = new StorageOptions
                    {
                        DatabasePath = Path.Combine(rootPath, "shrimpcam.db"),
                        ImageRootPath = Path.Combine(rootPath, "images"),
                        TimelapseRootPath = Path.Combine(rootPath, "timelapse"),
                        RetentionDays = 30,
                    },
                }));

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IMotionHighlightStateStore>().Should().NotBeNull();
        provider.GetRequiredService<IMotionHighlightService>().Should().NotBeNull();

        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }
}

#pragma warning restore CA2007
