using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShrimpCam.Core.Abstractions;

#pragma warning disable CA2007

namespace ShrimpCam.Infrastructure.Tests.Abstractions;

public sealed class InfrastructureAbstractionsTests
{
    [Fact]
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
}

#pragma warning restore CA2007
