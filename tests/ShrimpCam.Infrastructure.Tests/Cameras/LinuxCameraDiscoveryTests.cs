using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Infrastructure.Cameras.Linux;

#pragma warning disable CA2007

namespace ShrimpCam.Infrastructure.Tests.Cameras;

public sealed class LinuxCameraDiscoveryTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Discovery_service_maps_linux_devices_to_shared_camera_descriptors()
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.RunAsync(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new ProcessResult(
                    0,
                    """
                    HD Pro Webcam C920 (usb-0000:01:00.0-1):
                    	/dev/video0
                    	/dev/video1

                    Raspberry Pi Camera:
                    	/dev/video2
                    """,
                    string.Empty));

        using var provider = BuildProvider(processRunner, new CapturingLogger());
        var discovery = provider.GetRequiredService<ILinuxCameraDiscovery>();

        var cameras = await discovery.DiscoverAsync(CancellationToken.None).ConfigureAwait(true);

        cameras.Should().BeEquivalentTo(
            [
                new CameraDescriptor("HD Pro Webcam C920 (usb-0000:01:00.0-1)", "/dev/video0", CameraPlatforms.Linux),
                new CameraDescriptor("HD Pro Webcam C920 (usb-0000:01:00.0-1)", "/dev/video1", CameraPlatforms.Linux),
                new CameraDescriptor("Raspberry Pi Camera", "/dev/video2", CameraPlatforms.Linux),
            ]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Parser_returns_empty_when_output_is_blank()
    {
        var cameras = LinuxCameraDiscovery.ParseOutput(string.Empty);

        cameras.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Parser_ignores_malformed_output_without_device_headers()
    {
        var cameras = LinuxCameraDiscovery.ParseOutput(
            """
            	/dev/video0
            not-a-device-path
            	card0
            """);

        cameras.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Discovery_logs_a_warning_when_no_linux_cameras_are_detected()
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.RunAsync(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, string.Empty, string.Empty));

        var logger = new CapturingLogger();

        using var provider = BuildProvider(processRunner, logger);
        var discovery = provider.GetRequiredService<ILinuxCameraDiscovery>();

        var cameras = await discovery.DiscoverAsync(CancellationToken.None).ConfigureAwait(true);

        cameras.Should().BeEmpty();
        logger.Messages.Should().ContainSingle(
            message => message.Level == LogLevel.Warning
                && message.Message.Contains("No Linux cameras were detected", StringComparison.Ordinal));
    }

    private static ServiceProvider BuildProvider(
        IProcessRunner processRunner,
        ILogger<LinuxCameraDiscovery> logger)
    {
        var services = new ServiceCollection();
        services.AddInfrastructure();
        services.AddSingleton(processRunner);
        services.AddSingleton(logger);

        return services.BuildServiceProvider();
    }

    private sealed class CapturingLogger : ILogger<LinuxCameraDiscovery>
    {
        public List<LogMessage> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(new LogMessage(logLevel, formatter(state, exception)));
        }
    }

    private sealed record LogMessage(LogLevel Level, string Message);

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}

#pragma warning restore CA2007
