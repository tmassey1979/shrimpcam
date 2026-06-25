using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;

#nullable enable
#pragma warning disable CA2007

namespace ShrimpCam.Api.Tests.Cameras;

public sealed class LiveStreamEndpointTests
{
    [Fact]
    [Trait("Category", "Api")]
    public async Task Live_stream_endpoint_returns_multipart_mjpeg_response()
    {
        await using var factory = new LiveStreamWebApplicationFactory(shouldFail: false);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/stream/live", HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(true);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("multipart/x-mixed-replace");
        response.Content.Headers.ContentType?.Parameters.Should()
            .Contain(parameter => parameter.Name == "boundary" && parameter.Value == LiveStreamConstants.Boundary);

        var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(true);

        payload.Should().Contain($"--{LiveStreamConstants.Boundary}");
        payload.Should().Contain("Content-Type: image/jpeg");
        payload.Should().Contain("frame-01");
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Live_stream_endpoint_returns_service_unavailable_when_camera_cannot_start()
    {
        await using var factory = new LiveStreamWebApplicationFactory(shouldFail: true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/stream/live", HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(true);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var payload = await response.Content.ReadFromJsonAsync<LiveStreamFailureResponse>().ConfigureAwait(true);

        payload.Should().NotBeNull();
        payload!.Status.Should().Be("failed");
        payload.Reason.Should().Be("cameraUnavailable");
    }

    private sealed record LiveStreamFailureResponse(string Status, string Reason);

    private sealed class LiveStreamWebApplicationFactory(bool shouldFail) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration(
                (_, configBuilder) => configBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ShrimpCam:Camera:Platform"] = "Linux",
                        ["ShrimpCam:Camera:Source"] = "/dev/video0",
                    }));

            builder.ConfigureTestServices(
                services =>
                {
                    services.AddSingleton<ICameraLiveStreamService>(new StubCameraLiveStreamService(shouldFail));
                });
        }
    }

    private sealed class StubCameraLiveStreamService(bool shouldFail) : ICameraLiveStreamService
    {
        public Task<CameraLiveStreamStartResult> StartAsync(CameraOptions options, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (shouldFail)
            {
                return Task.FromResult(CameraLiveStreamStartResult.Failure(CameraLiveStreamFailureReasons.CameraUnavailable));
            }

            var payload = $"""
                --{LiveStreamConstants.Boundary}
                Content-Type: image/jpeg

                frame-01
                --{LiveStreamConstants.Boundary}--
                """.ReplaceLineEndings("\r\n");

            var stream = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(payload));
            return Task.FromResult<CameraLiveStreamStartResult>(
                CameraLiveStreamStartResult.Success(new StubLiveStreamSession(stream)));
        }
    }

    private sealed class StubLiveStreamSession(Stream content) : ICameraLiveStreamSession
    {
        public string ContentType => LiveStreamConstants.ContentType;

        public Stream Content { get; } = content;

        public ValueTask DisposeAsync() => Content.DisposeAsync();
    }
}

#pragma warning restore CA2007
