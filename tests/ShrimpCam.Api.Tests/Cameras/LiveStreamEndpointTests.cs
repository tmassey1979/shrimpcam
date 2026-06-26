using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShrimpCam.Core.Authentication;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Persistence;

#nullable enable
#pragma warning disable CA2007

namespace ShrimpCam.Api.Tests.Cameras;

public sealed class LiveStreamEndpointTests
{
    [Fact]
    [Trait("Category", "Api")]
    public async Task Live_stream_endpoint_returns_multipart_mjpeg_response()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-viewer", "ViewerPass123", "Viewer").ConfigureAwait(true);
            await using var factory = new LiveStreamWebApplicationFactory(rootPath, shouldFail: false);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

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
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Live_stream_endpoint_returns_service_unavailable_when_camera_cannot_start()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-viewer", "ViewerPass123", "Viewer").ConfigureAwait(true);
            await using var factory = new LiveStreamWebApplicationFactory(rootPath, shouldFail: true);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/stream/live", HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

            var payload = await response.Content.ReadFromJsonAsync<LiveStreamFailureResponse>().ConfigureAwait(true);

            payload.Should().NotBeNull();
            payload!.Status.Should().Be("failed");
            payload.Reason.Should().Be("cameraUnavailable");
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Live_stream_endpoint_accepts_session_cookie_for_browser_image_streams()
    {
        var rootPath = CreateTempRoot();

        try
        {
            await SeedUserAsync(rootPath, "shrimp-viewer", "ViewerPass123", "Viewer").ConfigureAwait(true);
            await using var factory = new LiveStreamWebApplicationFactory(rootPath, shouldFail: false);
            using var client = factory.CreateClient();

            var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest("shrimp-viewer", "ViewerPass123")).ConfigureAwait(true);
            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            client.DefaultRequestHeaders.Authorization.Should().BeNull();

            var response = await client.GetAsync("/stream/live", HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType?.MediaType.Should().Be("multipart/x-mixed-replace");
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Live_stream_endpoint_accepts_query_token_for_browser_image_streams()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-viewer", "ViewerPass123", "Viewer").ConfigureAwait(true);
            await using var factory = new LiveStreamWebApplicationFactory(rootPath, shouldFail: false);
            using var client = factory.CreateClient();

            var response = await client
                .GetAsync($"/stream/live?access_token={Uri.EscapeDataString(token)}", HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType?.MediaType.Should().Be("multipart/x-mixed-replace");
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Live_stream_endpoint_uses_saved_camera_settings()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-viewer", "ViewerPass123", "Viewer").ConfigureAwait(true);
            await SeedCameraSourceAsync(rootPath, "/dev/saved-video").ConfigureAwait(true);
            await using var factory = new LiveStreamWebApplicationFactory(rootPath, shouldFail: false);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/stream/live", HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            factory.StreamService.LastOptions?.Source.Should().Be("/dev/saved-video");
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Live_stream_endpoint_returns_unauthorized_for_anonymous_callers()
    {
        var rootPath = CreateTempRoot();

        try
        {
            await using var factory = new LiveStreamWebApplicationFactory(rootPath, shouldFail: false);
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/stream/live", HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    private sealed record LiveStreamFailureResponse(string Status, string Reason);

    private static async Task<string> SeedUserAndLoginAsync(string rootPath, string userName, string password, string roleName)
    {
        await SeedUserAsync(rootPath, userName, password, roleName).ConfigureAwait(true);

        await using var factory = new LiveStreamWebApplicationFactory(rootPath, shouldFail: false);
        using var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest(userName, password)).ConfigureAwait(true);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await loginResponse.Content.ReadFromJsonAsync<LoginSuccessResponse>().ConfigureAwait(true);
        payload.Should().NotBeNull();
        return payload!.Token;
    }

    private static async Task SeedUserAsync(string rootPath, string userName, string password, string roleName)
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
    }

    private static async Task SeedCameraSourceAsync(string rootPath, string source)
    {
        var updatedAtUtc = new DateTimeOffset(2026, 06, 25, 04, 00, 00, TimeSpan.Zero);
        using var provider = BuildProvider(rootPath);
        var settingsRepository = provider.GetRequiredService<ISettingsRepository>();

        await settingsRepository
            .UpsertAsync(new PersistedSetting("camera.source", source, "Camera source", updatedAtUtc), CancellationToken.None)
            .ConfigureAwait(true);
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
            Storage = new StorageOptions
            {
                DatabasePath = Path.Combine(rootPath, "shrimpcam.db"),
                ImageRootPath = Path.Combine(rootPath, "images"),
                TimelapseRootPath = Path.Combine(rootPath, "timelapse"),
                RetentionDays = 30,
            },
        };

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

    private sealed record LoginRequest(string UserName, string Password);

    private sealed record LoginSuccessResponse(
        string Status,
        Guid SessionId,
        Guid UserId,
        string UserName,
        string Token,
        DateTimeOffset ExpiresAtUtc);

    private sealed class LiveStreamWebApplicationFactory(string rootPath, bool shouldFail) : WebApplicationFactory<Program>
    {
        public StubCameraLiveStreamService StreamService { get; } = new(shouldFail);

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
                        ["ShrimpCam:Storage:ImageRootPath"] = Path.Combine(rootPath, "images"),
                        ["ShrimpCam:Storage:TimelapseRootPath"] = Path.Combine(rootPath, "timelapse"),
                        ["ShrimpCam:Security:InitialAdministrator:Enabled"] = "false",
                    }));

            builder.ConfigureTestServices(
                services =>
                {
                    services.AddSingleton<ICameraLiveStreamService>(StreamService);
                });
        }
    }

    internal sealed class StubCameraLiveStreamService(bool shouldFail) : ICameraLiveStreamService
    {
        public CameraOptions? LastOptions { get; private set; }

        public Task<CameraLiveStreamStartResult> StartAsync(CameraOptions options, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastOptions = options;

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
