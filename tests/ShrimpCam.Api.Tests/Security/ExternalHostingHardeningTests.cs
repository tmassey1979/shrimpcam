using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ShrimpCam.Api.Configuration;
using ShrimpCam.Core.Configuration;

#nullable enable
#pragma warning disable CA2007

namespace ShrimpCam.Api.Tests.Security;

public sealed class ExternalHostingHardeningTests
{
    [Fact]
    [Trait("Category", "Api")]
    public async Task Internet_exposed_responses_include_security_headers()
    {
        var rootPath = CreateTempRoot();

        try
        {
            await using var factory = new HardeningWebApplicationFactory(rootPath, "Production");
            using var client = factory.CreateClient();
            client.BaseAddress = new Uri("https://shrimp.example.test");

            var request = new HttpRequestMessage(HttpMethod.Get, "/health");
            request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");

            var response = await client.SendAsync(request).ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
            response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
            response.Headers.GetValues("Referrer-Policy").Should().Contain("no-referrer");
            response.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("frame-ancestors 'none'");
            response.Headers.GetValues("Strict-Transport-Security").Single().Should().Contain("max-age=");
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public void Internet_exposed_configuration_rejects_wildcard_allowed_hosts()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AllowedHosts"] = "*",
                    ["ShrimpCam:Security:HostMode"] = "InternetExposed",
                    ["ShrimpCam:Storage:DatabasePath"] = "data/shrimpcam.db",
                    ["ShrimpCam:Storage:ImageRootPath"] = "data/images",
                    ["ShrimpCam:Storage:TimelapseRootPath"] = "data/timelapse",
                })
            .Build();
        var services = new ServiceCollection();
        services.AddShrimpCamConfiguration(configuration, new TestHostEnvironment("Production"));

        using var provider = services.BuildServiceProvider();

        var action = () => provider.GetRequiredService<IOptions<ShrimpCamOptions>>().Value;

        action.Should()
            .Throw<OptionsValidationException>()
            .WithMessage("*AllowedHosts*explicit host names*");
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

    private sealed class HardeningWebApplicationFactory(string rootPath, string environmentName) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environmentName);
            builder.ConfigureAppConfiguration(
                (_, configBuilder) => configBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["AllowedHosts"] = "shrimp.example.test",
                        ["ShrimpCam:Storage:DatabasePath"] = Path.Combine(rootPath, "shrimpcam.db"),
                        ["ShrimpCam:Storage:ImageRootPath"] = Path.Combine(rootPath, "images"),
                        ["ShrimpCam:Storage:TimelapseRootPath"] = Path.Combine(rootPath, "timelapse"),
                        ["ShrimpCam:Security:InitialAdministrator:Password"] = "StrongShrimp123",
                    }));
        }
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "ShrimpCam.Api.Tests";

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

#pragma warning restore CA2007
