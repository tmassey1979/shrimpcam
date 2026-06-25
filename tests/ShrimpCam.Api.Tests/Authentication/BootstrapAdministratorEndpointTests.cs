using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Persistence;

#nullable enable
#pragma warning disable CA2007

namespace ShrimpCam.Api.Tests.Authentication;

public sealed class BootstrapAdministratorEndpointTests
{
    [Fact]
    [Trait("Category", "Api")]
    public async Task Bootstrap_creates_the_first_administrator_and_assigns_the_admin_role()
    {
        var rootPath = CreateTempRoot();

        try
        {
            await using var factory = new BootstrapWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync(
                    "/auth/bootstrap-admin",
                    new BootstrapRequest("shrimp-admin", "StrongShrimp123"))
                .ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var payload = await response.Content.ReadFromJsonAsync<BootstrapSuccessResponse>().ConfigureAwait(true);

            payload.Should().NotBeNull();
            payload!.Status.Should().Be("bootstrapped");
            payload.UserName.Should().Be("shrimp-admin");
            payload.RoleName.Should().Be("Administrator");

            using var provider = BuildProvider(rootPath);
            var userRepository = provider.GetRequiredService<IUserRepository>();
            var roleRepository = provider.GetRequiredService<IUserRoleRepository>();

            var user = await userRepository.GetByUserNameAsync("shrimp-admin", CancellationToken.None).ConfigureAwait(true);
            user.Should().NotBeNull();
            (await roleRepository.ListByUserIdAsync(user!.Id, CancellationToken.None).ConfigureAwait(true))
                .Should()
                .ContainSingle(role => role.RoleName == "Administrator");
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Bootstrap_is_rejected_after_an_administrator_has_already_been_created()
    {
        var rootPath = CreateTempRoot();

        try
        {
            await SeedAdministratorAsync(rootPath).ConfigureAwait(true);
            await using var factory = new BootstrapWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync(
                    "/auth/bootstrap-admin",
                    new BootstrapRequest("another-admin", "StrongShrimp123"))
                .ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);

            var payload = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>().ConfigureAwait(true);

            payload.Should().NotBeNull();
            payload!.Title.Should().Be("Bootstrap is no longer available.");
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Bootstrap_rejects_weak_passwords_with_validation_errors()
    {
        var rootPath = CreateTempRoot();

        try
        {
            await using var factory = new BootstrapWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync(
                    "/auth/bootstrap-admin",
                    new BootstrapRequest("shrimp-admin", "weak"))
                .ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var payload = await response.Content.ReadFromJsonAsync<ValidationProblemDetailsResponse>().ConfigureAwait(true);

            payload.Should().NotBeNull();
            payload!.Errors.Should().ContainKey("password");
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    private static async Task SeedAdministratorAsync(string rootPath)
    {
        using var provider = BuildProvider(rootPath);
        var initializer = provider.GetRequiredService<IApplicationDataInitializer>();
        var options = provider.GetRequiredService<IOptions<ShrimpCamOptions>>().Value;
        var passwordHasher = provider.GetRequiredService<ShrimpCam.Core.Authentication.IPasswordHasher>();
        var userRepository = provider.GetRequiredService<IUserRepository>();
        var roleRepository = provider.GetRequiredService<IUserRoleRepository>();
        var createdAtUtc = new DateTimeOffset(2026, 06, 25, 03, 30, 00, TimeSpan.Zero);
        var userId = Guid.NewGuid();

        await initializer.InitializeAsync(options.Storage, CancellationToken.None).ConfigureAwait(true);
        await userRepository.CreateAsync(
                new UserRecord(userId, "shrimp-admin", passwordHasher.HashPassword("StrongShrimp123"), true, createdAtUtc),
                CancellationToken.None)
            .ConfigureAwait(true);
        await roleRepository.AssignAsync(
                new UserRoleRecord(userId, "Administrator", createdAtUtc),
                CancellationToken.None)
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

    private sealed record BootstrapRequest(string UserName, string Password);

    private sealed record BootstrapSuccessResponse(string Status, Guid UserId, string UserName, string RoleName);

    private sealed record ProblemDetailsResponse(string Type, string Title, int Status, string Detail);

    private sealed record ValidationProblemDetailsResponse(string Type, string Title, int Status, Dictionary<string, string[]> Errors);

    private sealed class BootstrapWebApplicationFactory(string rootPath) : WebApplicationFactory<Program>
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
