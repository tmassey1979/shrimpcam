using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Health;

#nullable enable
#pragma warning disable CA2007

namespace ShrimpCam.Api.Tests.Configuration;

public sealed class StartupConfigurationValidationTests
{
    [Fact]
    [Trait("Category", "Api")]
    public async Task Health_endpoint_exposes_runtime_health_and_build_metadata_from_valid_configuration()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            var overrides = new Dictionary<string, string>
            {
                ["ShrimpCam:Camera:Platform"] = "Linux",
                ["ShrimpCam:Camera:Source"] = "/dev/video0",
                ["ShrimpCam:Storage:DatabasePath"] = Path.Combine(rootPath, "shrimpcam.db"),
                ["ShrimpCam:Storage:ImageRootPath"] = Path.Combine(rootPath, "images"),
                ["ShrimpCam:Storage:TimelapseRootPath"] = Path.Combine(rootPath, "timelapse"),
            };

            await using var factory = new ConfigurationWebApplicationFactory(overrides, cameraAvailable: true);
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/health").ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var payload = await response.Content.ReadFromJsonAsync<HealthResponseContract>().ConfigureAwait(true);

            payload.Should().NotBeNull();
            payload!.Status.Should().Be(HealthStatusLevel.Healthy);
            payload.Components.Should().ContainKey("database");
            payload.Components.Should().ContainKey("storage");
            payload.Components.Should().ContainKey("camera");
            payload.Components["database"].Status.Should().Be(HealthStatusLevel.Healthy);
            payload.ApplicationVersion.Should().Be("0.1.0.0");
            payload.InformationalVersion.Should().Be("0.1.0+sha.local");
            payload.SourceRevision.Should().Be("local");
            payload.BuildConfiguration.Should().Be("Debug");
            File.Exists(Path.Combine(rootPath, "shrimpcam.db")).Should().BeTrue();
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Missing_camera_at_startup_keeps_api_available_with_degraded_health()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            var overrides = new Dictionary<string, string>
            {
                ["ShrimpCam:Camera:Platform"] = "Linux",
                ["ShrimpCam:Camera:Source"] = "/dev/video404",
                ["ShrimpCam:Storage:DatabasePath"] = Path.Combine(rootPath, "shrimpcam.db"),
                ["ShrimpCam:Storage:ImageRootPath"] = Path.Combine(rootPath, "images"),
                ["ShrimpCam:Storage:TimelapseRootPath"] = Path.Combine(rootPath, "timelapse"),
            };

            await using var factory = new ConfigurationWebApplicationFactory(overrides, cameraAvailable: false);
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/health").ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var payload = await response.Content.ReadFromJsonAsync<HealthResponseContract>().ConfigureAwait(true);

            payload.Should().NotBeNull();
            payload!.Status.Should().Be(HealthStatusLevel.Degraded);
            payload.Components["database"].Status.Should().Be(HealthStatusLevel.Healthy);
            payload.Components["camera"].Status.Should().Be(HealthStatusLevel.Unhealthy);
            payload.Components["camera"].Detail.Should().Contain("/dev/video404");
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Invalid_configuration_fails_fast_on_startup()
    {
        var overrides = new Dictionary<string, string>
        {
            ["ShrimpCam:Camera:Platform"] = "Other",
            ["ShrimpCam:Capture:ActiveStartHourUtc"] = "6",
            ["ShrimpCam:Capture:ActiveEndHourUtc"] = "2",
        };

        await using var factory = new ConfigurationWebApplicationFactory(overrides);

        var act = () => Task.Run(() => factory.CreateClient());

        await act.Should().ThrowAsync<OptionsValidationException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Weak_initial_administrator_password_fails_fast_on_startup()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            var overrides = new Dictionary<string, string>
            {
                ["ShrimpCam:Camera:Platform"] = "Linux",
                ["ShrimpCam:Camera:Source"] = "/dev/video0",
                ["ShrimpCam:Storage:DatabasePath"] = Path.Combine(rootPath, "shrimpcam.db"),
                ["ShrimpCam:Storage:ImageRootPath"] = Path.Combine(rootPath, "images"),
                ["ShrimpCam:Storage:TimelapseRootPath"] = Path.Combine(rootPath, "timelapse"),
                ["ShrimpCam:Security:InitialAdministrator:Password"] = "weak",
            };

            await using var factory = new ConfigurationWebApplicationFactory(overrides);

            var act = () => Task.Run(() => factory.CreateClient());

            await act.Should().ThrowAsync<OptionsValidationException>()
                .WithMessage("*Initial administrator credentials*")
                .ConfigureAwait(true);
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Internet_exposed_production_rejects_committed_initial_administrator_password()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            var overrides = CreateValidPathOverrides(rootPath);

            await using var factory = new ConfigurationWebApplicationFactory(overrides, environmentName: "Production");

            var act = () => Task.Run(() => factory.CreateClient());

            await act.Should().ThrowAsync<OptionsValidationException>()
                .WithMessage("*committed initial administrator password*")
                .ConfigureAwait(true);
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Internet_exposed_production_accepts_deployment_provided_initial_administrator_password()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            var overrides = CreateValidPathOverrides(rootPath);
            overrides["ShrimpCam:Security:InitialAdministrator:Password"] = "StrongShrimp123";

            await using var factory = new ConfigurationWebApplicationFactory(overrides, environmentName: "Production");
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/health").ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Newer_database_schema_version_blocks_startup_with_clear_error()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(rootPath, "shrimpcam.db");
        Directory.CreateDirectory(rootPath);

        try
        {
            using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    CREATE TABLE schema_version (
                        version INTEGER NOT NULL,
                        applied_at_utc TEXT NOT NULL
                    );

                    INSERT INTO schema_version (version, applied_at_utc)
                    VALUES (99, '2026-06-25T00:00:00.0000000Z');
                    """;
                _ = command.ExecuteNonQuery();
            }

            var overrides = new Dictionary<string, string>
            {
                ["ShrimpCam:Storage:DatabasePath"] = databasePath,
                ["ShrimpCam:Storage:ImageRootPath"] = Path.Combine(rootPath, "images"),
                ["ShrimpCam:Storage:TimelapseRootPath"] = Path.Combine(rootPath, "timelapse"),
            };

            await using var factory = new ConfigurationWebApplicationFactory(overrides);
            var act = () => Task.Run(() => factory.CreateClient());

            await act.Should()
                .ThrowAsync<InvalidOperationException>()
                .WithMessage("*newer than this application supports*")
                .ConfigureAwait(true);
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Unopenable_database_path_blocks_startup_before_ready_state()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(rootPath, "database-directory");
        Directory.CreateDirectory(databasePath);

        try
        {
            var overrides = new Dictionary<string, string>
            {
                ["ShrimpCam:Camera:Platform"] = "Linux",
                ["ShrimpCam:Camera:Source"] = "/dev/video0",
                ["ShrimpCam:Storage:DatabasePath"] = databasePath,
                ["ShrimpCam:Storage:ImageRootPath"] = Path.Combine(rootPath, "images"),
                ["ShrimpCam:Storage:TimelapseRootPath"] = Path.Combine(rootPath, "timelapse"),
            };

            await using var factory = new ConfigurationWebApplicationFactory(overrides, cameraAvailable: true);
            var act = () => Task.Run(() => factory.CreateClient());

            await act.Should()
                .ThrowAsync<SqliteException>()
                .WithMessage("*unable to open database file*")
                .ConfigureAwait(true);
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    private sealed record HealthResponseContract(
        string Status,
        DateTimeOffset CheckedAtUtc,
        Dictionary<string, HealthComponentContract> Components,
        string ApplicationVersion,
        string InformationalVersion,
        string SourceRevision,
        string BuildConfiguration);

    private sealed record HealthComponentContract(string Status, string? Detail);

    private sealed class ConfigurationWebApplicationFactory(
        IReadOnlyDictionary<string, string>? overrides = null,
        bool cameraAvailable = true,
        string environmentName = "Development") : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environmentName);

            if (overrides is null || overrides.Count == 0)
            {
                return;
            }

            builder.ConfigureAppConfiguration(
                (_, configBuilder) => configBuilder.AddInMemoryCollection(
                    overrides.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value))));

            builder.ConfigureServices(
                services =>
                {
                    services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
                    services.AddSingleton<IProcessRunner>(new StubProcessRunner(cameraAvailable));
                });
        }
    }

    private static Dictionary<string, string> CreateValidPathOverrides(string rootPath) =>
        new()
        {
            ["ShrimpCam:Camera:Platform"] = "Linux",
            ["ShrimpCam:Camera:Source"] = "/dev/video0",
            ["ShrimpCam:Storage:DatabasePath"] = Path.Combine(rootPath, "shrimpcam.db"),
            ["ShrimpCam:Storage:ImageRootPath"] = Path.Combine(rootPath, "images"),
            ["ShrimpCam:Storage:TimelapseRootPath"] = Path.Combine(rootPath, "timelapse"),
        };

    private sealed class StubProcessRunner(bool cameraAvailable) : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var standardOutput = cameraAvailable
                ? """
                  Logitech USB Camera:
                      /dev/video0
                  """
                : string.Empty;

            return Task.FromResult(new ProcessResult(0, standardOutput, string.Empty));
        }
    }

    private static void DeleteDirectory(string rootPath)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (!Directory.Exists(rootPath))
            {
                return;
            }

            try
            {
                Directory.Delete(rootPath, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(100);
            }
            catch (IOException)
            {
                return;
            }
        }
    }
}

#pragma warning restore CA2007
