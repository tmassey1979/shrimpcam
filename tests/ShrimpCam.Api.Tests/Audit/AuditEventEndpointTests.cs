using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShrimpCam.Core.Audit;
using ShrimpCam.Core.Authentication;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Persistence;

#nullable enable
#pragma warning disable CA2007

namespace ShrimpCam.Api.Tests.Audit;

public sealed class AuditEventEndpointTests
{
    [Fact]
    [Trait("Category", "Api")]
    public async Task Bootstrap_and_failed_login_create_redacted_audit_records()
    {
        var rootPath = CreateTempRoot();

        try
        {
            await using var factory = new AuditWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();

            var bootstrapResponse = await client.PostAsJsonAsync(
                    "/auth/bootstrap-admin",
                    new BootstrapRequest("shrimp-admin", "StrongShrimp123"))
                .ConfigureAwait(true);
            var loginResponse = await client.PostAsJsonAsync(
                    "/auth/login",
                    new LoginRequest("shrimp-admin", "WrongPassword123"))
                .ConfigureAwait(true);

            bootstrapResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            var auditRecords = await ListAuditRecordsAsync(rootPath).ConfigureAwait(true);

            auditRecords.Should().Contain(record =>
                record.EventType == AuditEventTypes.BootstrapAdministrator &&
                record.ActorUserName == "shrimp-admin" &&
                record.Outcome == AuditOutcomes.Succeeded);
            auditRecords.Should().Contain(record =>
                record.EventType == AuditEventTypes.SignIn &&
                record.ActorUserName == "shrimp-admin" &&
                record.Outcome == AuditOutcomes.Failed &&
                record.Detail.Contains("\"password\":\"[redacted]\"", StringComparison.Ordinal));
            auditRecords.Select(record => record.Detail).Should().NotContain(detail => detail.Contains("StrongShrimp123", StringComparison.Ordinal));
            auditRecords.Select(record => record.Detail).Should().NotContain(detail => detail.Contains("WrongPassword123", StringComparison.Ordinal));
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Sign_out_and_settings_update_create_successful_audit_records()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-admin", "AdminPass1234", "Administrator").ConfigureAwait(true);
            await using var factory = new AuditWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var settingsResponse = await client.PutAsJsonAsync("/settings", CreateValidUpdateRequest()).ConfigureAwait(true);
            var logoutResponse = await client.PostAsync("/auth/logout", content: null).ConfigureAwait(true);

            settingsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var auditRecords = await ListAuditRecordsAsync(rootPath).ConfigureAwait(true);

            auditRecords.Should().Contain(record =>
                record.EventType == AuditEventTypes.SettingsUpdated &&
                record.ActorUserName == "shrimp-admin" &&
                record.Outcome == AuditOutcomes.Succeeded &&
                record.Detail.Contains("\"retentionDays\":\"60\"", StringComparison.Ordinal));
            auditRecords.Should().Contain(record =>
                record.EventType == AuditEventTypes.SignOut &&
                record.ActorUserName == "shrimp-admin" &&
                record.Outcome == AuditOutcomes.Succeeded);
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Denied_administrator_action_creates_security_audit_record()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-viewer", "ViewerPass123", "Viewer").ConfigureAwait(true);
            await using var factory = new AuditWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.PutAsJsonAsync("/settings", CreateValidUpdateRequest()).ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var auditRecords = await ListAuditRecordsAsync(rootPath).ConfigureAwait(true);

            auditRecords.Should().Contain(record =>
                record.EventType == AuditEventTypes.AuthorizationDenied &&
                record.ActorUserName == "shrimp-viewer" &&
                record.Outcome == AuditOutcomes.Denied &&
                record.Detail.Contains("\"path\":\"/settings\"", StringComparison.Ordinal) &&
                record.Detail.Contains("\"statusCode\":\"403\"", StringComparison.Ordinal));
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Audit_history_is_visible_to_administrators_only()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var adminToken = await SeedUserAndLoginAsync(rootPath, "shrimp-admin", "AdminPass1234", "Administrator").ConfigureAwait(true);
            var viewerToken = await SeedUserAndLoginAsync(rootPath, "shrimp-viewer", "ViewerPass123", "Viewer").ConfigureAwait(true);

            await using var factory = new AuditWebApplicationFactory(rootPath);
            using var adminClient = factory.CreateClient();
            adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            using var viewerClient = factory.CreateClient();
            viewerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", viewerToken);

            var adminResponse = await adminClient.GetAsync("/audit/events?page=1&pageSize=5").ConfigureAwait(true);
            var viewerResponse = await viewerClient.GetAsync("/audit/events").ConfigureAwait(true);

            adminResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var payload = await adminResponse.Content.ReadFromJsonAsync<AuditHistoryResponse>().ConfigureAwait(true);
            payload.Should().NotBeNull();
            payload!.Items.Should().NotBeEmpty();
            payload.Paging.TotalItems.Should().BeGreaterThanOrEqualTo(2);

            viewerResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

    private static async Task<IReadOnlyList<AuditRecord>> ListAuditRecordsAsync(string rootPath)
    {
        using var provider = BuildProvider(rootPath);
        var auditRepository = provider.GetRequiredService<IAuditRecordRepository>();
        var page = await auditRepository.ListAsync(new AuditRecordQuery(1, 100), CancellationToken.None).ConfigureAwait(true);
        return page.Items;
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

        await using var factory = new AuditWebApplicationFactory(rootPath);
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
                Enabled = true,
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

    private sealed record BootstrapRequest(string UserName, string Password);

    private sealed record LoginRequest(string UserName, string Password);

    private sealed record LoginSuccessResponse(
        string Status,
        Guid SessionId,
        Guid UserId,
        string UserName,
        string Token,
        DateTimeOffset ExpiresAtUtc);

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

    private sealed record AuditHistoryResponse(AuditEventResponse[] Items, AuditPagingResponse Paging);

    private sealed record AuditEventResponse(
        Guid Id,
        string EventType,
        string ActorUserName,
        string Outcome,
        string Detail,
        DateTimeOffset OccurredAtUtc);

    private sealed record AuditPagingResponse(
        int PageNumber,
        int PageSize,
        int TotalItems,
        int TotalPages,
        bool HasPreviousPage,
        bool HasNextPage);

    private sealed class AuditWebApplicationFactory(string rootPath) : WebApplicationFactory<Program>
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
                services => services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider()));
        }
    }
}

#pragma warning restore CA2007
