using System.Net;
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

public sealed class LoginEndpointTests
{
    [Fact]
    [Trait("Category", "Api")]
    public async Task Login_succeeds_for_valid_local_credentials()
    {
        var rootPath = CreateTempRoot();

        try
        {
            await SeedUserAsync(rootPath, "shrimp-admin", "shrimp-password", isEnabled: true).ConfigureAwait(true);
            await using var factory = new LoginWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest("shrimp-admin", "shrimp-password")).ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var payload = await response.Content.ReadFromJsonAsync<LoginSuccessResponse>().ConfigureAwait(true);

            payload.Should().NotBeNull();
            payload!.Status.Should().Be("authenticated");
            payload.UserName.Should().Be("shrimp-admin");
            payload.Token.Should().NotBeNullOrWhiteSpace();
            payload.ExpiresAtUtc.Should().BeAfter(DateTimeOffset.UtcNow.AddHours(7));
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Login_rejects_invalid_passwords_without_leaking_password_details()
    {
        var rootPath = CreateTempRoot();

        try
        {
            await SeedUserAsync(rootPath, "shrimp-admin", "shrimp-password", isEnabled: true).ConfigureAwait(true);
            await using var factory = new LoginWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest("shrimp-admin", "wrong-password")).ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            var payload = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>().ConfigureAwait(true);

            payload.Should().NotBeNull();
            payload!.Title.Should().Be("Authentication failed.");
            payload.Detail.Should().Be("Invalid username or password.");
            payload.Detail.Should().NotContain("wrong-password");
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Login_rejects_disabled_accounts_with_an_unauthorized_response()
    {
        var rootPath = CreateTempRoot();

        try
        {
            await SeedUserAsync(rootPath, "shrimp-viewer", "shrimp-password", isEnabled: false).ConfigureAwait(true);
            await using var factory = new LoginWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest("shrimp-viewer", "shrimp-password")).ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    private static async Task SeedUserAsync(string rootPath, string userName, string password, bool isEnabled)
    {
        var services = new ServiceCollection();
        var options = new ShrimpCamOptions
        {
            Storage = new StorageOptions
            {
                DatabasePath = Path.Combine(rootPath, "shrimpcam.db"),
                ImageRootPath = Path.Combine(rootPath, "images"),
                TimelapseRootPath = Path.Combine(rootPath, "timelapse"),
                RetentionDays = 30,
            },
        };

        services.AddSingleton<IOptions<ShrimpCamOptions>>(Options.Create(options));
        ShrimpCam.Infrastructure.DependencyInjection.AddInfrastructure(services);

        using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IApplicationDataInitializer>()
            .InitializeAsync(options.Storage, CancellationToken.None)
            .ConfigureAwait(true);

        var passwordHasher = provider.GetRequiredService<IPasswordHasher>();
        var userRepository = provider.GetRequiredService<IUserRepository>();

        await userRepository.CreateAsync(
                new UserRecord(
                    Guid.NewGuid(),
                    userName,
                    passwordHasher.HashPassword(password),
                    isEnabled,
                    new DateTimeOffset(2026, 06, 25, 02, 00, 00, TimeSpan.Zero)),
                CancellationToken.None)
            .ConfigureAwait(true);
    }

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

    private sealed class LoginWebApplicationFactory(string rootPath) : WebApplicationFactory<Program>
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
