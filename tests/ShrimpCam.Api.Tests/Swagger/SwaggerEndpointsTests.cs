using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

#pragma warning disable CA2007

namespace ShrimpCam.Api.Tests.Swagger;

public sealed class SwaggerEndpointsTests
{
    [Fact]
    [Trait("Category", "Api")]
    public async Task Development_environment_serves_openapi_document()
    {
        await using var factory = new SwaggerWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json").ConfigureAwait(true);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(true);

        payload.Should().Contain("\"openapi\"");
        payload.Should().Contain("/health");
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Development_environment_serves_swagger_ui()
    {
        await using var factory = new SwaggerWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/index.html").ConfigureAwait(true);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Production_environment_does_not_serve_swagger_ui()
    {
        await using var factory = new SwaggerWebApplicationFactory("Production");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/index.html").ConfigureAwait(true);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed class SwaggerWebApplicationFactory(string environmentName) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environmentName);
        }
    }
}

#pragma warning restore CA2007
