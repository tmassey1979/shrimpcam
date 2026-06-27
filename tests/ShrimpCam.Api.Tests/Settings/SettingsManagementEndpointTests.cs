using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Authentication;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Persistence;

#nullable enable
#pragma warning disable CA2007

namespace ShrimpCam.Api.Tests.Settings;

public sealed class SettingsManagementEndpointTests
{
    [Fact]
    [Trait("Category", "Api")]
    public async Task Administrator_can_read_current_settings()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-admin", "AdminPass1234", "Administrator").ConfigureAwait(true);
            await SeedPersistedSettingsAsync(
                    rootPath,
                    [
                        new PersistedSetting("capture.intervalMinutes", "10", "Capture interval", new DateTimeOffset(2026, 06, 25, 12, 00, 00, TimeSpan.Zero)),
                        new PersistedSetting("storage.retentionDays", "45", "Retention days", new DateTimeOffset(2026, 06, 25, 12, 00, 00, TimeSpan.Zero)),
                    ])
                .ConfigureAwait(true);

            await using var factory = new SettingsWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/settings").ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var payload = await response.Content.ReadFromJsonAsync<SettingsResponse>().ConfigureAwait(true);
            payload.Should().NotBeNull();
            payload!.Capture.IntervalMinutes.Should().Be(10);
            payload.Storage.RetentionDays.Should().Be(45);
            payload.Camera.Platform.Should().Be("Windows");
            payload.Security.HostMode.Should().Be("InternetExposed");
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Administrator_can_update_valid_settings_and_changes_are_persisted()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-admin", "AdminPass1234", "Administrator").ConfigureAwait(true);
            await using var factory = new SettingsWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var request = CreateValidUpdateRequest();

            var response = await client.PutAsJsonAsync("/settings", request).ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var payload = await response.Content.ReadFromJsonAsync<SettingsResponse>().ConfigureAwait(true);
            payload.Should().NotBeNull();
            payload!.Capture.IntervalMinutes.Should().Be(15);
            payload.Capture.MotionHighlightsEnabled.Should().BeTrue();
            payload.Storage.RetentionDays.Should().Be(60);
            payload.Security.HostMode.Should().Be("RemoteReady");

            using var provider = BuildProvider(rootPath);
            var settingsRepository = provider.GetRequiredService<ISettingsRepository>();
            (await settingsRepository.GetByKeyAsync("capture.intervalMinutes", CancellationToken.None).ConfigureAwait(true))!
                .Value.Should().Be("15");
            (await settingsRepository.GetByKeyAsync("capture.motionHighlightsEnabled", CancellationToken.None).ConfigureAwait(true))!
                .Value.Should().Be("true");
            (await settingsRepository.GetByKeyAsync("storage.retentionDays", CancellationToken.None).ConfigureAwait(true))!
                .Value.Should().Be("60");
            (await settingsRepository.GetByKeyAsync("security.hostMode", CancellationToken.None).ConfigureAwait(true))!
                .Value.Should().Be("RemoteReady");
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Invalid_settings_are_rejected_and_existing_values_remain_unchanged()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-admin", "AdminPass1234", "Administrator").ConfigureAwait(true);
            await SeedPersistedSettingsAsync(
                    rootPath,
                    [
                        new PersistedSetting("capture.intervalMinutes", "5", "Capture interval", new DateTimeOffset(2026, 06, 25, 12, 00, 00, TimeSpan.Zero)),
                    ])
                .ConfigureAwait(true);

            await using var factory = new SettingsWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var request = CreateValidUpdateRequest() with
            {
                Capture = new CaptureSettingsRequest(true, 0, 23, 22, true, 1.2d, -1),
                Storage = new StorageSettingsRequest(0),
            };

            var response = await client.PutAsJsonAsync("/settings", request).ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var payload = await response.Content.ReadFromJsonAsync<ValidationProblemDetailsResponse>().ConfigureAwait(true);
            payload.Should().NotBeNull();
            payload!.Errors.Keys.Should().Contain(["capture.intervalMinutes", "capture.motionThreshold", "capture.motionCooldownSeconds", "storage.retentionDays"]);

            using var provider = BuildProvider(rootPath);
            var settingsRepository = provider.GetRequiredService<ISettingsRepository>();
            (await settingsRepository.GetByKeyAsync("capture.intervalMinutes", CancellationToken.None).ConfigureAwait(true))!
                .Value.Should().Be("5");
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Viewer_cannot_update_settings()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-viewer", "ViewerPass123", "Viewer").ConfigureAwait(true);
            await using var factory = new SettingsWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.PutAsJsonAsync("/settings", CreateValidUpdateRequest()).ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Administrator_can_discover_camera_sources_for_selected_platform()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-admin", "AdminPass1234", "Administrator").ConfigureAwait(true);
            await using var factory = new SettingsWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/cameras?platform=Windows").ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var payload = await response.Content.ReadFromJsonAsync<CameraDiscoveryResponse>().ConfigureAwait(true);
            payload.Should().NotBeNull();
            payload!.Platform.Should().Be("Windows");
            payload.Cameras.Should().ContainSingle();
            payload.Cameras[0].DisplayName.Should().Be("Logitech C920");
            payload.Cameras[0].DevicePath.Should().Be("@device_pnp_\\\\?\\usb#vid_046d&pid_082d#shrimp#{abc}");
            payload.Providers.Should().Contain(provider =>
                provider.ProviderKind == "windows-media-foundation" &&
                provider.IsPrimary &&
                !provider.RequiresExternalProcess &&
                !provider.IsRuntimeAvailable &&
                provider.UnavailableReason == "mediaFoundationNativeBoundaryUnavailable");
            payload.Providers.Should().Contain(provider =>
                provider.ProviderKind == "windows-ffmpeg-directshow" &&
                !provider.IsPrimary &&
                provider.RequiresExternalProcess &&
                provider.IsRuntimeAvailable &&
                provider.UnavailableReason == null);
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Administrator_can_discover_linux_camera_sources_and_v4l2_provider_diagnostics()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-admin", "AdminPass1234", "Administrator").ConfigureAwait(true);
            await using var factory = new SettingsWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/cameras?platform=Linux").ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var payload = await response.Content.ReadFromJsonAsync<CameraDiscoveryResponse>().ConfigureAwait(true);
            payload.Should().NotBeNull();
            payload!.Platform.Should().Be("Linux");
            payload.Cameras.Should().ContainSingle();
            payload.Cameras[0].DisplayName.Should().Be("HD Pro Webcam C920 (usb-0000:01:00.0-1)");
            payload.Cameras[0].DevicePath.Should().Be("/dev/video0");
            payload.Providers.Should().ContainSingle(provider =>
                provider.ProviderKind == "linux-v4l2-ffmpeg" &&
                provider.IsPrimary &&
                provider.RequiresExternalProcess &&
                provider.IsRuntimeAvailable &&
                provider.UnavailableReason == null);
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Anonymous_users_cannot_discover_camera_sources()
    {
        var rootPath = CreateTempRoot();

        try
        {
            await using var factory = new SettingsWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/cameras?platform=Windows").ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Unsupported_camera_platform_is_rejected()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-admin", "AdminPass1234", "Administrator").ConfigureAwait(true);
            await using var factory = new SettingsWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/cameras?platform=BeOS").ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var payload = await response.Content.ReadFromJsonAsync<ValidationProblemDetailsResponse>().ConfigureAwait(true);
            payload.Should().NotBeNull();
            payload!.Errors.Keys.Should().Contain("platform");
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    private static UpdateSettingsRequest CreateValidUpdateRequest() =>
        new(
            new CameraSettingsRequest("Windows", "Logitech C920", 1920, 1080, 1280, 720, 24, 3, 2),
            new CaptureSettingsRequest(true, 15, 7, 21, true, 0.45d, 180),
            new StorageSettingsRequest(60),
            new SecuritySettingsRequest("RemoteReady"));

    private static async Task SeedPersistedSettingsAsync(string rootPath, IReadOnlyList<PersistedSetting> settings)
    {
        using var provider = BuildProvider(rootPath);
        var initializer = provider.GetRequiredService<IApplicationDataInitializer>();
        var settingsRepository = provider.GetRequiredService<ISettingsRepository>();

        await initializer.InitializeAsync(provider.GetRequiredService<IOptions<ShrimpCamOptions>>().Value.Storage, CancellationToken.None).ConfigureAwait(true);

        foreach (var setting in settings)
        {
            await settingsRepository.UpsertAsync(setting, CancellationToken.None).ConfigureAwait(true);
        }
    }

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

        await using var factory = new SettingsWebApplicationFactory(rootPath);
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
                Platform = "Windows",
                Source = "Logitech C920",
            },
            Capture = new CaptureOptions
            {
                Enabled = false,
                IntervalMinutes = 5,
                ActiveStartHourUtc = 6,
                ActiveEndHourUtc = 22,
            },
            Storage = new StorageOptions
            {
                DatabasePath = Path.Combine(rootPath, "shrimpcam.db"),
                ImageRootPath = Path.Combine(rootPath, "images"),
                TimelapseRootPath = Path.Combine(rootPath, "timelapse"),
                RetentionDays = 30,
            },
            Security = new SecurityOptions
            {
                HostMode = "InternetExposed",
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

    private sealed record SettingsResponse(
        CameraSettingsResponse Camera,
        CaptureSettingsResponse Capture,
        StorageSettingsResponse Storage,
        SecuritySettingsResponse Security);

    private sealed record CameraDiscoveryResponse(
        string Platform,
        CameraDiscoveryItem[] Cameras,
        CameraFrameSourceProviderItem[] Providers);

    private sealed record CameraDiscoveryItem(
        string DisplayName,
        string DevicePath,
        string Platform);

    private sealed record CameraFrameSourceProviderItem(
        string ProviderKind,
        string DisplayName,
        string Platform,
        bool IsPrimary,
        bool RequiresExternalProcess,
        string DiagnosticsName,
        bool IsRuntimeAvailable,
        string? UnavailableReason);

    private sealed record CameraSettingsResponse(
        string Platform,
        string Source,
        int CaptureWidth,
        int CaptureHeight,
        int StreamWidth,
        int StreamHeight,
        int StreamFramesPerSecond,
        int ReconnectRetryAttempts,
        int ReconnectBackoffSeconds);

    private sealed record CaptureSettingsResponse(
        bool Enabled,
        int IntervalMinutes,
        int ActiveStartHourUtc,
        int ActiveEndHourUtc,
        bool MotionHighlightsEnabled,
        double MotionThreshold,
        int MotionCooldownSeconds);

    private sealed record StorageSettingsResponse(int RetentionDays);

    private sealed record SecuritySettingsResponse(string HostMode);

    private sealed record UpdateSettingsRequest(
        CameraSettingsRequest Camera,
        CaptureSettingsRequest Capture,
        StorageSettingsRequest Storage,
        SecuritySettingsRequest Security);

    private sealed record CameraSettingsRequest(
        string Platform,
        string Source,
        int CaptureWidth,
        int CaptureHeight,
        int StreamWidth,
        int StreamHeight,
        int StreamFramesPerSecond,
        int ReconnectRetryAttempts,
        int ReconnectBackoffSeconds);

    private sealed record CaptureSettingsRequest(
        bool Enabled,
        int IntervalMinutes,
        int ActiveStartHourUtc,
        int ActiveEndHourUtc,
        bool MotionHighlightsEnabled,
        double MotionThreshold,
        int MotionCooldownSeconds);

    private sealed record StorageSettingsRequest(int RetentionDays);

    private sealed record SecuritySettingsRequest(string HostMode);

    private sealed record ValidationProblemDetailsResponse(string Type, string Title, int Status, Dictionary<string, string[]> Errors);

    private sealed class SettingsWebApplicationFactory(string rootPath) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration(
                (_, configBuilder) => configBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ShrimpCam:Camera:Platform"] = "Windows",
                        ["ShrimpCam:Camera:Source"] = "Logitech C920",
                        ["ShrimpCam:Storage:DatabasePath"] = Path.Combine(rootPath, "shrimpcam.db"),
                        ["ShrimpCam:Storage:ImageRootPath"] = Path.Combine(rootPath, "images"),
                        ["ShrimpCam:Storage:TimelapseRootPath"] = Path.Combine(rootPath, "timelapse"),
                    }));
            builder.ConfigureServices(
                services =>
                {
                    services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
                    services.AddSingleton<IProcessRunner>(new StubProcessRunner());
                });
        }
    }

    private sealed class StubProcessRunner : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            if (request.FileName.Equals("ffmpeg", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(
                    new ProcessResult(
                        1,
                        string.Empty,
                        """
                        [dshow @ 000001] DirectShow video devices
                        [dshow @ 000001]  "Logitech C920"
                        [dshow @ 000001]     Alternative name "@device_pnp_\\?\usb#vid_046d&pid_082d#shrimp#{abc}"
                        """));
            }

            if (request.FileName.Equals("v4l2-ctl", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(
                    new ProcessResult(
                        0,
                        """
                        HD Pro Webcam C920 (usb-0000:01:00.0-1):
                        	/dev/video0
                        """,
                        string.Empty));
            }

            return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
        }
    }
}

#pragma warning restore CA2007
