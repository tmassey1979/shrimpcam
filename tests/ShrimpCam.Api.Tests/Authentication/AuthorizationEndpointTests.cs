using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShrimpCam.Core.Authentication;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Persistence;

#nullable enable
#pragma warning disable CA2007

namespace ShrimpCam.Api.Tests.Authentication;

public sealed class AuthorizationEndpointTests
{
    [Fact]
    [Trait("Category", "Api")]
    public async Task Protected_settings_endpoint_returns_unauthorized_for_anonymous_callers()
    {
        var rootPath = CreateTempRoot();

        try
        {
            await using var factory = new AuthorizationWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/settings").ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            var payload = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>().ConfigureAwait(true);
            payload.Should().NotBeNull();
            payload!.Title.Should().Be("Authentication required.");
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Protected_settings_endpoint_returns_forbidden_for_viewers()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-viewer", "ViewerPass123", "Viewer").ConfigureAwait(true);
            await using var factory = new AuthorizationWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/settings").ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            var payload = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>().ConfigureAwait(true);
            payload.Should().NotBeNull();
            payload!.Title.Should().Be("Forbidden.");
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Protected_settings_endpoint_allows_administrators()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-admin", "AdminPass1234", "Administrator").ConfigureAwait(true);
            await using var factory = new AuthorizationWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/settings").ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var payload = await response.Content.ReadFromJsonAsync<SettingsResponse>().ConfigureAwait(true);
            payload.Should().NotBeNull();
            payload!.Storage.RetentionDays.Should().Be(30);
            payload.Security.HostMode.Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            DeleteDirectory(rootPath);
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

        await using var factory = new AuthorizationWebApplicationFactory(rootPath);
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

    private sealed record ProblemDetailsResponse(string Type, string Title, int Status, string Detail);

    private sealed record SettingsResponse(SettingsStorageResponse Storage, SettingsSecurityResponse Security);

    private sealed record SettingsStorageResponse(int RetentionDays);

    private sealed record SettingsSecurityResponse(string HostMode);

    private sealed class AuthorizationWebApplicationFactory(string rootPath) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration(
                (_, configBuilder) => configBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ShrimpCam:Storage:DatabasePath"] = Path.Combine(rootPath, "shrimpcam.db"),
                        ["ShrimpCam:Storage:ImageRootPath"] = Path.Combine(rootPath, "images"),
                        ["ShrimpCam:Storage:TimelapseRootPath"] = Path.Combine(rootPath, "timelapse"),
                    }));
        }
    }
}

#pragma warning restore CA2007
