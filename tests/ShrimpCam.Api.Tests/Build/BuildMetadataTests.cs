using System.Diagnostics;
using System.Reflection;

#nullable enable
#pragma warning disable CA2007

namespace ShrimpCam.Api.Tests.Build;

public sealed class BuildMetadataTests
{
    [Fact]
    [Trait("Category", "Api")]
    public void Api_assembly_exposes_deterministic_build_metadata()
    {
        var assembly = typeof(Program).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        informationalVersion.Should().Be("0.1.0+sha.local");
        assembly.GetName().Version?.ToString().Should().Be("0.1.0.0");

        var assemblyMetadata = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value, StringComparer.OrdinalIgnoreCase);

        assemblyMetadata["SourceRevision"].Should().Be("local");
        assemblyMetadata["BuildConfiguration"].Should().Be("Debug");
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Build_validation_rejects_invalid_source_revision_values()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments =
                "msbuild src/ShrimpCam.Api/ShrimpCam.Api.csproj -nologo -t:ValidateShrimpCamBuildMetadata /p:ShrimpCamSourceRevision=invalid/revision",
            WorkingDirectory = ResolveRepositoryRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = startInfo };

        process.Start();

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync().ConfigureAwait(true);

        var standardOutput = await standardOutputTask.ConfigureAwait(true);
        var standardError = await standardErrorTask.ConfigureAwait(true);
        var combinedOutput = string.Join(Environment.NewLine, new[] { standardOutput, standardError });

        process.ExitCode.Should().NotBe(0);
        combinedOutput.Should().Contain("ShrimpCamSourceRevision");
        combinedOutput.Should().Contain("invalid");
    }

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ShrimpCam.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the repository root for build metadata tests.");
    }
}

#pragma warning restore CA2007
