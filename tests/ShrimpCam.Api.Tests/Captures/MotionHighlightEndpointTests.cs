using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Authentication;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Persistence;

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
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-admin", "AdminPass1234", "Administrator").ConfigureAwait(true);
            await using var factory = new MotionHighlightWebApplicationFactory(rootPath, shouldFail: false);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

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
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Repeated_motion_inside_cooldown_is_suppressed_without_creating_a_second_capture()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-admin", "AdminPass1234", "Administrator").ConfigureAwait(true);
            await using var factory = new MotionHighlightWebApplicationFactory(rootPath, shouldFail: false);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

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
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Motion_highlight_returns_unauthorized_for_anonymous_callers()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            await using var factory = new MotionHighlightWebApplicationFactory(rootPath, shouldFail: false);
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync(
                    "/captures/highlights/motion",
                    new MotionHighlightRequest(new DateTimeOffset(2026, 06, 25, 00, 10, 00, TimeSpan.Zero), 0.82d, "event-401"))
                .ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Motion_highlight_returns_forbidden_for_viewers()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-viewer", "ViewerPass123", "Viewer").ConfigureAwait(true);
            await using var factory = new MotionHighlightWebApplicationFactory(rootPath, shouldFail: false);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.PostAsJsonAsync(
                    "/captures/highlights/motion",
                    new MotionHighlightRequest(new DateTimeOffset(2026, 06, 25, 00, 10, 00, TimeSpan.Zero), 0.82d, "event-402"))
                .ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            DeleteDirectory(rootPath);
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

    private static async Task<string> SeedUserAndLoginAsync(string rootPath, string userName, string password, string roleName)
    {
        var createdAtUtc = new DateTimeOffset(2026, 06, 25, 04, 00, 00, TimeSpan.Zero);
        using var provider = BuildProvider(rootPath);
        var initializer = provider.GetRequiredService<IApplicationDataInitializer>();
        var passwordHasher = provider.GetRequiredService<IPasswordHasher>();
        var userRepository = provider.GetRequiredService<IUserRepository>();
        var roleRepository = provider.GetRequiredService<IUserRoleRepository>();
        var userId = Guid.NewGuid();

        await initializer.InitializeAsync(provider.GetRequiredService<IOptions<ShrimpCamOptions>>().Value.Storage, CancellationToken.None).ConfigureAwait(true);
        await userRepository.CreateAsync(
                new UserRecord(userId, userName, passwordHasher.HashPassword(password), true, createdAtUtc),
                CancellationToken.None)
            .ConfigureAwait(true);
        await roleRepository.AssignAsync(new UserRoleRecord(userId, roleName, createdAtUtc), CancellationToken.None).ConfigureAwait(true);

        await using var factory = new MotionHighlightWebApplicationFactory(rootPath, shouldFail: false);
        using var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest(userName, password)).ConfigureAwait(true);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await loginResponse.Content.ReadFromJsonAsync<LoginSuccessResponse>().ConfigureAwait(true);
        payload.Should().NotBeNull();
        return payload!.Token;
    }

    private static ServiceProvider BuildProvider(string rootPath)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOptions<ShrimpCamOptions>>(Options.Create(CreateOptions(rootPath)));
        ShrimpCam.Infrastructure.DependencyInjection.AddInfrastructure(services);
        return services.BuildServiceProvider();
    }

    private static ShrimpCamOptions CreateOptions(string rootPath) =>
        new()
        {
            Camera = new CameraOptions
            {
                Platform = "Linux",
                Source = "/dev/video0",
            },
            Capture = new CaptureOptions
            {
                MotionHighlightsEnabled = true,
                MotionThreshold = 0.4d,
                MotionCooldownSeconds = 300,
            },
            Storage = new StorageOptions
            {
                DatabasePath = Path.Combine(rootPath, "shrimpcam.db"),
                ImageRootPath = rootPath,
                TimelapseRootPath = Path.Combine(rootPath, "timelapse"),
                RetentionDays = 30,
            },
        };

    private sealed record LoginRequest(string UserName, string Password);

    private sealed record LoginSuccessResponse(
        string Status,
        Guid SessionId,
        Guid UserId,
        string UserName,
        string Token,
        DateTimeOffset ExpiresAtUtc);

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
                        ["ShrimpCam:Storage:DatabasePath"] = Path.Combine(rootPath, "shrimpcam.db"),
                        ["ShrimpCam:Storage:ImageRootPath"] = rootPath,
                        ["ShrimpCam:Storage:TimelapseRootPath"] = Path.Combine(rootPath, "timelapse"),
                        ["ShrimpCam:Capture:MotionHighlightsEnabled"] = "true",
                        ["ShrimpCam:Capture:MotionThreshold"] = "0.4",
                        ["ShrimpCam:Capture:MotionCooldownSeconds"] = "300",
                        ["ShrimpCam:Security:InitialAdministrator:Enabled"] = "false",
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
