using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShrimpCam.Core.Abstractions;

#nullable enable
#pragma warning disable CA2007

namespace ShrimpCam.Api.Tests.Captures;

public sealed class MotionHighlightEndpointTests
{
    [Fact]
    [Trait("Category", "Api")]
    public async Task Qualifying_motion_event_creates_highlight_capture_and_metadata()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            await using var factory = new MotionHighlightWebApplicationFactory(rootPath, shouldFail: false);
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync(
                    "/captures/highlights/motion",
                    new MotionHighlightRequest(new DateTimeOffset(2026, 06, 25, 00, 10, 00, TimeSpan.Zero), 0.82d, "event-201"))
                .ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var payload = await response.Content.ReadFromJsonAsync<MotionHighlightCaptureResponse>().ConfigureAwait(true);

            payload.Should().NotBeNull();
            payload!.Status.Should().Be("captured");
            payload.Outcome.Should().Be("captured");
            payload.SourceType.Should().Be("MotionHighlight");
            payload.RelativeImagePath.Should().Be("2026/06/25/20260625T001000000Z_motionhighlight.jpg");
            File.Exists(payload.ImagePath).Should().BeTrue();
            File.Exists(payload.MetadataPath).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Repeated_motion_inside_cooldown_is_suppressed_without_creating_a_second_capture()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            await using var factory = new MotionHighlightWebApplicationFactory(rootPath, shouldFail: false);
            using var client = factory.CreateClient();

            var firstResponse = await client.PostAsJsonAsync(
                    "/captures/highlights/motion",
                    new MotionHighlightRequest(new DateTimeOffset(2026, 06, 25, 00, 10, 00, TimeSpan.Zero), 0.82d, "event-301"))
                .ConfigureAwait(true);
            var secondResponse = await client.PostAsJsonAsync(
                    "/captures/highlights/motion",
                    new MotionHighlightRequest(new DateTimeOffset(2026, 06, 25, 00, 12, 00, TimeSpan.Zero), 0.91d, "event-302"))
                .ConfigureAwait(true);

            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var secondPayload = await secondResponse.Content.ReadFromJsonAsync<MotionHighlightSkippedResponse>().ConfigureAwait(true);

            secondPayload.Should().NotBeNull();
            secondPayload!.Status.Should().Be("skipped");
            secondPayload.Outcome.Should().Be("suppressedByCooldown");
            Directory.GetFiles(rootPath, "*.jpg", SearchOption.AllDirectories).Should().ContainSingle();
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    private sealed record MotionHighlightRequest(DateTimeOffset OccurredAtUtc, double Score, string? EventId);

    private sealed record MotionHighlightCaptureResponse(
        string Status,
        string Outcome,
        string SourceType,
        DateTimeOffset CapturedAtUtc,
        string FileName,
        string ImagePath,
        string RelativeImagePath,
        string MetadataPath);

    private sealed record MotionHighlightSkippedResponse(string Status, string Outcome);

    private sealed class MotionHighlightWebApplicationFactory(string rootPath, bool shouldFail) : WebApplicationFactory<Program>
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
                        ["ShrimpCam:Storage:ImageRootPath"] = rootPath,
                        ["ShrimpCam:Capture:MotionHighlightsEnabled"] = "true",
                        ["ShrimpCam:Capture:MotionThreshold"] = "0.4",
                        ["ShrimpCam:Capture:MotionCooldownSeconds"] = "300",
                    }));

            builder.ConfigureTestServices(
                services =>
                {
                    services.AddSingleton<IProcessRunner>(new StubProcessRunner(shouldFail));
                });
        }
    }

    private sealed class StubProcessRunner(bool shouldFail) : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (shouldFail)
            {
                return Task.FromResult(new ProcessResult(1, string.Empty, "camera unavailable"));
            }

            var outputPath = ExtractLastQuotedArgument(request.Arguments);
            File.WriteAllText(outputPath, "image-bytes");

            return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
        }

        private static string ExtractLastQuotedArgument(string arguments)
        {
            var lastQuote = arguments.LastIndexOf('"');
            var firstQuote = arguments.LastIndexOf('"', lastQuote - 1);

            return arguments.Substring(firstQuote + 1, lastQuote - firstQuote - 1)
                .Replace("\\\\", "\\", StringComparison.Ordinal)
                .Replace("\\\"", "\"", StringComparison.Ordinal);
        }
    }
}

#pragma warning restore CA2007
