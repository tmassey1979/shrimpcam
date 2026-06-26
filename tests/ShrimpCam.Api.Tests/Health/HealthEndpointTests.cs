using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShrimpCam.Core.Health;

#nullable enable
#pragma warning disable CA2007

namespace ShrimpCam.Api.Tests.Health;

public sealed class HealthEndpointTests
{
    [Fact]
    [Trait("Category", "Api")]
    public async Task Health_endpoint_returns_healthy_status_when_all_dependencies_are_healthy()
    {
        var rootPath = CreateTempRoot();

        try
        {
            await using var factory = new HealthWebApplicationFactory(
                rootPath,
                new ApplicationHealthReport(
                    HealthStatusLevel.Healthy,
                    new DateTimeOffset(2026, 06, 25, 12, 00, 00, TimeSpan.Zero),
                    new Dictionary<string, HealthComponentReport>
                    {
                        ["app"] = new(HealthStatusLevel.Healthy, null),
                        ["database"] = new(HealthStatusLevel.Healthy, null),
                        ["storage"] = new(HealthStatusLevel.Healthy, null),
                        ["camera"] = new(HealthStatusLevel.Healthy, null),
                    }));
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/health").ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var payload = await response.Content.ReadFromJsonAsync<HealthResponseContract>().ConfigureAwait(true);
            payload.Should().NotBeNull();
            payload!.Status.Should().Be("Healthy");
            payload.Components["database"].Status.Should().Be("Healthy");
            payload.Components["storage"].Status.Should().Be("Healthy");
            payload.Components["camera"].Status.Should().Be("Healthy");
            payload.ApplicationVersion.Should().Be("0.0.1.0");
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Health_endpoint_returns_degraded_status_when_camera_is_unhealthy()
    {
        var rootPath = CreateTempRoot();

        try
        {
            await using var factory = new HealthWebApplicationFactory(
                rootPath,
                new ApplicationHealthReport(
                    HealthStatusLevel.Degraded,
                    new DateTimeOffset(2026, 06, 25, 12, 00, 00, TimeSpan.Zero),
                    new Dictionary<string, HealthComponentReport>
                    {
                        ["app"] = new(HealthStatusLevel.Healthy, null),
                        ["database"] = new(HealthStatusLevel.Healthy, null),
                        ["storage"] = new(HealthStatusLevel.Healthy, null),
                        ["camera"] = new(HealthStatusLevel.Unhealthy, "camera unavailable"),
                    }));
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/health").ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var payload = await response.Content.ReadFromJsonAsync<HealthResponseContract>().ConfigureAwait(true);
            payload.Should().NotBeNull();
            payload!.Status.Should().Be("Degraded");
            payload.Components["camera"].Status.Should().Be("Unhealthy");
            payload.Components["camera"].Detail.Should().Be("camera unavailable");
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Health_endpoint_returns_unhealthy_status_when_database_is_unavailable()
    {
        var rootPath = CreateTempRoot();

        try
        {
            await using var factory = new HealthWebApplicationFactory(
                rootPath,
                new ApplicationHealthReport(
                    HealthStatusLevel.Unhealthy,
                    new DateTimeOffset(2026, 06, 25, 12, 00, 00, TimeSpan.Zero),
                    new Dictionary<string, HealthComponentReport>
                    {
                        ["app"] = new(HealthStatusLevel.Healthy, null),
                        ["database"] = new(HealthStatusLevel.Unhealthy, "unable to open database file"),
                        ["storage"] = new(HealthStatusLevel.Healthy, null),
                        ["camera"] = new(HealthStatusLevel.Healthy, null),
                    }));
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/health").ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            var payload = await response.Content.ReadFromJsonAsync<HealthResponseContract>().ConfigureAwait(true);
            payload.Should().NotBeNull();
            payload!.Status.Should().Be("Unhealthy");
            payload.Components["database"].Status.Should().Be("Unhealthy");
            payload.Components["database"].Detail.Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    private static string CreateTempRoot() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

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

    private sealed record HealthResponseContract(
        string Status,
        DateTimeOffset CheckedAtUtc,
        Dictionary<string, HealthComponentContract> Components,
        string ApplicationVersion,
        string InformationalVersion,
        string SourceRevision,
        string BuildConfiguration);

    private sealed record HealthComponentContract(string Status, string? Detail);

    private sealed class HealthWebApplicationFactory(string rootPath, ApplicationHealthReport report) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration(
                (_, configBuilder) => configBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ShrimpCam:Storage:DatabasePath"] = Path.Combine(rootPath, "shrimpcam.db"),
                        ["ShrimpCam:Storage:ImageRootPath"] = Path.Combine(rootPath, "images"),
                        ["ShrimpCam:Storage:TimelapseRootPath"] = Path.Combine(rootPath, "timelapse"),
                    }));
            builder.ConfigureServices(
                services =>
                {
                    services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
                    services.AddSingleton<IApplicationHealthService>(new StubApplicationHealthService(report));
                });
        }
    }

    private sealed class StubApplicationHealthService(ApplicationHealthReport report) : IApplicationHealthService
    {
        public Task<ApplicationHealthReport> GetCurrentAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(report);
        }
    }
}

#pragma warning restore CA2007
