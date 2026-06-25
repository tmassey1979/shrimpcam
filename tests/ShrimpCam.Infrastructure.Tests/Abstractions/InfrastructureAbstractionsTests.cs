using Microsoft.Extensions.DependencyInjection;
using ShrimpCam.Core.Abstractions;

#pragma warning disable CA2007

namespace ShrimpCam.Infrastructure.Tests.Abstractions;

public sealed class InfrastructureAbstractionsTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Infrastructure_registers_shared_abstractions()
    {
        var services = new ServiceCollection();

        Infrastructure.DependencyInjection.AddInfrastructure(services);

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IClock>().Should().NotBeNull();
        provider.GetRequiredService<IFileSystem>().Should().NotBeNull();
        provider.GetRequiredService<IProcessRunner>().Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Process_runner_executes_a_simple_command()
    {
        var services = new ServiceCollection();
        Infrastructure.DependencyInjection.AddInfrastructure(services);

        using var provider = services.BuildServiceProvider();

        var runner = provider.GetRequiredService<IProcessRunner>();
        var result = await runner.RunAsync(
                new ProcessRequest("dotnet", "--version"),
                CancellationToken.None)
            .ConfigureAwait(true);

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Process_runner_rejects_blank_file_names()
    {
        var services = new ServiceCollection();
        Infrastructure.DependencyInjection.AddInfrastructure(services);

        using var provider = services.BuildServiceProvider();

        var runner = provider.GetRequiredService<IProcessRunner>();
        var act = () => runner.RunAsync(new ProcessRequest(string.Empty, string.Empty), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Process_runner_kills_running_process_when_cancelled()
    {
        var services = new ServiceCollection();
        Infrastructure.DependencyInjection.AddInfrastructure(services);

        using var provider = services.BuildServiceProvider();
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        var markerPath = Path.Combine(Path.GetTempPath(), $"shrimpcam-cancelled-process-{Guid.NewGuid():N}.txt");
        var runner = provider.GetRequiredService<IProcessRunner>();
        var command = new ProcessRequest(
            "powershell",
            $"-NoProfile -ExecutionPolicy Bypass -Command \"Start-Sleep -Seconds 5; Set-Content -LiteralPath '{markerPath}' -Value survived\"");

        try
        {
            var act = () => runner.RunAsync(command, cancellationTokenSource.Token);

            await act.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(true);
            await Task.Delay(TimeSpan.FromSeconds(6)).ConfigureAwait(true);

            File.Exists(markerPath).Should().BeFalse("a cancelled capture process must not keep running after the caller gives up");
        }
        finally
        {
            if (File.Exists(markerPath))
            {
                File.Delete(markerPath);
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void File_system_combines_paths()
    {
        var services = new ServiceCollection();
        Infrastructure.DependencyInjection.AddInfrastructure(services);

        using var provider = services.BuildServiceProvider();

        var fileSystem = provider.GetRequiredService<IFileSystem>();
        var combined = fileSystem.Combine("data", "images");

        combined.Should().Contain("data");
        combined.Should().Contain("images");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void File_system_can_create_and_detect_directories()
    {
        var services = new ServiceCollection();
        Infrastructure.DependencyInjection.AddInfrastructure(services);

        using var provider = services.BuildServiceProvider();

        var fileSystem = provider.GetRequiredService<IFileSystem>();
        var directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            fileSystem.DirectoryExists(directoryPath).Should().BeFalse();

            fileSystem.CreateDirectory(directoryPath);

            fileSystem.DirectoryExists(directoryPath).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Clock_returns_a_recent_utc_timestamp()
    {
        var services = new ServiceCollection();
        Infrastructure.DependencyInjection.AddInfrastructure(services);

        using var provider = services.BuildServiceProvider();

        var clock = provider.GetRequiredService<IClock>();
        var before = DateTimeOffset.UtcNow;
        var now = clock.UtcNow;
        var after = DateTimeOffset.UtcNow;

        now.Should().BeOnOrAfter(before);
        now.Should().BeOnOrBefore(after);
    }
}

#pragma warning restore CA2007
