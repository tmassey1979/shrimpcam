using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using ShrimpCam.Core.Captures;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Tests.Captures;

public sealed class ScheduledCaptureStateStoreTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task State_store_round_trips_scheduler_progress_to_disk()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            var services = new ServiceCollection();
            Infrastructure.DependencyInjection.AddInfrastructure(services);

            using var provider = services.BuildServiceProvider();
            var store = provider.GetRequiredService<IScheduledCaptureStateStore>();
            var expected = new ScheduledCaptureState(
                new DateTimeOffset(2026, 06, 24, 12, 00, 00, TimeSpan.Zero),
                ScheduledCaptureOutcome.SkippedBySchedule,
                null);

            await store.SaveAsync(
                    new StorageOptions { ImageRootPath = rootPath, RetentionDays = 30 },
                    expected,
                    CancellationToken.None)
                .ConfigureAwait(true);

            var restored = await store.LoadAsync(
                    new StorageOptions { ImageRootPath = rootPath, RetentionDays = 30 },
                    CancellationToken.None)
                .ConfigureAwait(true);

            restored.Should().Be(expected);
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
    [Trait("Category", "Unit")]
    public async Task Missing_storage_root_is_rejected()
    {
        var services = new ServiceCollection();
        Infrastructure.DependencyInjection.AddInfrastructure(services);

        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IScheduledCaptureStateStore>();

        var act = () => store.LoadAsync(new StorageOptions { ImageRootPath = string.Empty, RetentionDays = 30 }, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Infrastructure_registers_scheduled_capture_state_store()
    {
        var services = new ServiceCollection();
        Infrastructure.DependencyInjection.AddInfrastructure(services);

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IScheduledCaptureStateStore>().Should().NotBeNull();
    }
}
