using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Infrastructure.Cameras;

namespace ShrimpCam.Infrastructure.Tests.Cameras;

public sealed class CameraStatusServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Status_service_starts_unknown_and_transitions_online_then_degraded()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(
            new DateTimeOffset(2026, 06, 24, 12, 00, 00, TimeSpan.Zero),
            new DateTimeOffset(2026, 06, 24, 12, 01, 00, TimeSpan.Zero),
            new DateTimeOffset(2026, 06, 24, 12, 02, 00, TimeSpan.Zero));
        var service = new CameraStatusService(clock);

        service.GetSnapshot().Status.Should().Be(CameraStatusLevel.Unknown);

        service.ReportOnline();
        var online = service.GetSnapshot();
        online.Status.Should().Be(CameraStatusLevel.Online);
        online.Reason.Should().BeNull();
        online.UpdatedAtUtc.Should().Be(new DateTimeOffset(2026, 06, 24, 12, 01, 00, TimeSpan.Zero));

        service.ReportDegraded("camera unavailable");
        var degraded = service.GetSnapshot();
        degraded.Status.Should().Be(CameraStatusLevel.Degraded);
        degraded.Reason.Should().Be("camera unavailable");
        degraded.UpdatedAtUtc.Should().Be(new DateTimeOffset(2026, 06, 24, 12, 02, 00, TimeSpan.Zero));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Infrastructure_registers_camera_status_service()
    {
        var services = new ServiceCollection();
        Infrastructure.DependencyInjection.AddInfrastructure(services);

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ICameraStatusService>().Should().NotBeNull();
    }
}
