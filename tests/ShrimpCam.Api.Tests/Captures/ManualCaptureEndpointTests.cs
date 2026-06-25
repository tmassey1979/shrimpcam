using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Cameras;

#nullable enable
#pragma warning disable CA2007

namespace ShrimpCam.Api.Tests.Captures;

public sealed class ManualCaptureEndpointTests
{
    [Fact]
    [Trait("Category", "Api")]
    public async Task Manual_capture_stores_image_and_metadata_together()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            await using var factory = new ManualCaptureWebApplicationFactory(rootPath, shouldFail: false);
            using var client = factory.CreateClient();

            var response = await client.PostAsync("/captures/manual", content: null).ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var payload = await response.Content.ReadFromJsonAsync<ManualCaptureResponse>().ConfigureAwait(true);

            payload.Should().NotBeNull();
            payload!.Status.Should().Be("captured");
            payload.SourceType.Should().Be("Manual");
            payload.RelativeImagePath.Should().Be("2026/06/24/20260624T230000000Z_manual.jpg");
            File.Exists(payload.ImagePath).Should().BeTrue();
            File.Exists(payload.MetadataPath).Should().BeTrue();
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Manual_capture_busy_returns_conflict_without_persisted_metadata()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            await using var factory = new ManualCaptureWebApplicationFactory(rootPath, shouldFail: false, cameraBusy: true);
            using var client = factory.CreateClient();

            var response = await client.PostAsync("/captures/manual", content: null).ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);

            var payload = await response.Content.ReadFromJsonAsync<ManualCaptureFailureResponse>().ConfigureAwait(true);

            payload.Should().NotBeNull();
            payload!.Status.Should().Be("failed");
            payload.Reason.Should().Be("cameraBusy");
            Directory.GetFiles(rootPath, "*.jpg", SearchOption.AllDirectories).Should().BeEmpty();
            Directory.GetFiles(rootPath, "*.json", SearchOption.AllDirectories).Should().BeEmpty();
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Manual_capture_failure_returns_camera_unavailable_without_persisted_metadata()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            await using var factory = new ManualCaptureWebApplicationFactory(rootPath, shouldFail: true);
            using var client = factory.CreateClient();

            var response = await client.PostAsync("/captures/manual", content: null).ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

            var payload = await response.Content.ReadFromJsonAsync<ManualCaptureFailureResponse>().ConfigureAwait(true);

            payload.Should().NotBeNull();
            payload!.Status.Should().Be("failed");
            payload.Reason.Should().Be("cameraUnavailable");
            Directory.GetFiles(rootPath, "*.jpg", SearchOption.AllDirectories).Should().BeEmpty();
            Directory.GetFiles(rootPath, "*.json", SearchOption.AllDirectories).Should().BeEmpty();
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    private sealed record ManualCaptureResponse(
        string Status,
        string SourceType,
        DateTimeOffset CapturedAtUtc,
        string FileName,
        string ImagePath,
        string RelativeImagePath,
        string MetadataPath);

    private sealed record ManualCaptureFailureResponse(string Status, string Reason);

    private sealed class ManualCaptureWebApplicationFactory(string rootPath, bool shouldFail, bool cameraBusy = false) : WebApplicationFactory<Program>
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
                        ["ShrimpCam:Storage:DatabasePath"] = Path.Combine(rootPath, "shrimpcam.db"),
                        ["ShrimpCam:Storage:ImageRootPath"] = rootPath,
                        ["ShrimpCam:Storage:TimelapseRootPath"] = Path.Combine(rootPath, "timelapse"),
                        ["ShrimpCam:Security:InitialAdministrator:Enabled"] = "false",
                    }));

            builder.ConfigureTestServices(
                services =>
                {
                    services.AddSingleton<IClock>(new FixedClock(new DateTimeOffset(2026, 06, 24, 23, 00, 00, TimeSpan.Zero)));
                    services.AddSingleton<IProcessRunner>(new StubProcessRunner(shouldFail));
                    if (cameraBusy)
                    {
                        services.AddSingleton<ICameraResourceCoordinator>(new BusyCameraResourceCoordinator());
                    }
                });
        }
    }

    private sealed class BusyCameraResourceCoordinator : ICameraResourceCoordinator
    {
        public ValueTask<CameraResourceLease?> TryAcquireAsync(string owner, CancellationToken cancellationToken) =>
            ValueTask.FromResult<CameraResourceLease?>(null);
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
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

    private static void DeleteDirectory(string rootPath)
    {
        SqliteConnection.ClearAllPools();

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
