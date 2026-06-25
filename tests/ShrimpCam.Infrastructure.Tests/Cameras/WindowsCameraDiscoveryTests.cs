using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Infrastructure.Cameras.Windows;

#pragma warning disable CA2007

namespace ShrimpCam.Infrastructure.Tests.Cameras;

public sealed class WindowsCameraDiscoveryTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Discovery_service_maps_directshow_devices_to_shared_camera_descriptors()
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.RunAsync(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new ProcessResult(
                    1,
                    string.Empty,
                    """
                    [dshow @ 000001] DirectShow video devices
                    [dshow @ 000001]  "Logitech BRIO"
                    [dshow @ 000001]     Alternative name "@device_pnp_\\?\usb#vid_046d&pid_085e&mi_00#7&111111&0&0000#{abc}"
                    [dshow @ 000001]  "OBS Virtual Camera"
                    [dshow @ 000001]     Alternative name "@device_sw_{11111111-1111-1111-1111-111111111111}\{22222222-2222-2222-2222-222222222222}"
                    """));

        using var provider = BuildProvider(processRunner, new CapturingLogger());
        var discovery = provider.GetRequiredService<IWindowsCameraDiscovery>();

        var cameras = await discovery.DiscoverAsync(CancellationToken.None).ConfigureAwait(true);

        cameras.Should().BeEquivalentTo(
            [
                new CameraDescriptor(
                    "Logitech BRIO",
                    "@device_pnp_\\\\?\\usb#vid_046d&pid_085e&mi_00#7&111111&0&0000#{abc}",
                    CameraPlatforms.Windows),
                new CameraDescriptor(
                    "OBS Virtual Camera",
                    "@device_sw_{11111111-1111-1111-1111-111111111111}\\{22222222-2222-2222-2222-222222222222}",
                    CameraPlatforms.Windows),
            ]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Parser_returns_empty_when_output_is_blank()
    {
        var cameras = WindowsCameraDiscovery.ParseOutput(string.Empty);

        cameras.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Parser_ignores_lines_without_quoted_devices()
    {
        var cameras = WindowsCameraDiscovery.ParseOutput(
            """
            [dshow @ 000001] DirectShow video devices
            [dshow @ 000001] device without quotes
            [dshow @ 000001] Alternative name without pairing
            """);

        cameras.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Discovery_throws_actionable_error_when_command_fails_without_devices()
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.RunAsync(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, string.Empty, "ffmpeg is not recognized"));

        using var provider = BuildProvider(processRunner, new CapturingLogger());
        var discovery = provider.GetRequiredService<IWindowsCameraDiscovery>();

        var act = () => discovery.DiscoverAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Windows camera discovery failed*ffmpeg is not recognized*")
            .ConfigureAwait(true);
    }

    private static ServiceProvider BuildProvider(
        IProcessRunner processRunner,
        ILogger<WindowsCameraDiscovery> logger)
    {
        var services = new ServiceCollection();
        services.AddInfrastructure();
        services.AddSingleton(processRunner);
        services.AddSingleton(logger);

        return services.BuildServiceProvider();
    }

    private sealed class CapturingLogger : ILogger<WindowsCameraDiscovery>
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
