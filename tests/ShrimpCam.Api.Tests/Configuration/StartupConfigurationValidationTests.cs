using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

#nullable enable
#pragma warning disable CA2007

namespace ShrimpCam.Api.Tests.Configuration;

public sealed class StartupConfigurationValidationTests
{
    [Fact]
    public async Task Health_endpoint_exposes_values_from_valid_bound_configuration()
    {
        await using var factory = new ConfigurationWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health").ConfigureAwait(true);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<HealthResponseContract>().ConfigureAwait(true);

        payload.Should().NotBeNull();
        payload!.CameraPlatform.Should().Be("Windows");
        payload.CaptureIntervalMinutes.Should().Be(5);
        payload.HostMode.Should().Be("InternetExposed");
    }

    [Fact]
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

    private sealed record HealthResponseContract(
        string Status,
        string CameraPlatform,
        int CaptureIntervalMinutes,
        string HostMode);

    private sealed class ConfigurationWebApplicationFactory(
        IReadOnlyDictionary<string, string>? overrides = null) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            if (overrides is null || overrides.Count == 0)
            {
                return;
            }

            builder.ConfigureAppConfiguration(
                (_, configBuilder) => configBuilder.AddInMemoryCollection(
                    overrides.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value))));
        }
    }
}

#pragma warning restore CA2007
