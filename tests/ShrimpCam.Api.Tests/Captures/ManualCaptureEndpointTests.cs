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
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Persistence;

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
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-admin", "AdminPass1234", "Administrator").ConfigureAwait(true);
            await using var factory = new ManualCaptureWebApplicationFactory(rootPath, shouldFail: false);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

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
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-admin", "AdminPass1234", "Administrator").ConfigureAwait(true);
            await using var factory = new ManualCaptureWebApplicationFactory(rootPath, shouldFail: false, cameraBusy: true);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

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
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-admin", "AdminPass1234", "Administrator").ConfigureAwait(true);
            await using var factory = new ManualCaptureWebApplicationFactory(rootPath, shouldFail: true);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

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

    [Fact]
    [Trait("Category", "Api")]
    public async Task Manual_capture_returns_unauthorized_for_anonymous_callers()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            await using var factory = new ManualCaptureWebApplicationFactory(rootPath, shouldFail: false);
            using var client = factory.CreateClient();

            var response = await client.PostAsync("/captures/manual", content: null).ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Manual_capture_returns_forbidden_for_viewers()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-viewer", "ViewerPass123", "Viewer").ConfigureAwait(true);
            await using var factory = new ManualCaptureWebApplicationFactory(rootPath, shouldFail: false);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.PostAsync("/captures/manual", content: null).ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

        await using var factory = new ManualCaptureWebApplicationFactory(rootPath, shouldFail: false);
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
                        ["ShrimpCam:Camera:AlwaysOnStreamEnabled"] = "false",
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
