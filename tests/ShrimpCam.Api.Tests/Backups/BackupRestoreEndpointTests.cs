using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Authentication;
using ShrimpCam.Core.Backups;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Persistence;

#nullable enable
#pragma warning disable CA2007

namespace ShrimpCam.Api.Tests.Backups;

public sealed class BackupRestoreEndpointTests
{
    private static readonly string[] DatabaseManifestEntries = ["database/shrimpcam.db"];

    [Fact]
    [Trait("Category", "Api")]
    public async Task Administrator_can_restore_valid_backup_package_into_replacement_host()
    {
        var sourceRoot = CreateTempRoot();
        var targetRoot = CreateTempRoot();

        try
        {
            var archivePath = await CreateSourceBackupAsync(sourceRoot).ConfigureAwait(true);
            var targetToken = await SeedUserAndLoginAsync(targetRoot, "target-admin", "TargetPass1234", "Administrator").ConfigureAwait(true);

            await using var targetFactory = new RestoreWebApplicationFactory(targetRoot);
            using var targetClient = targetFactory.CreateClient();
            targetClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", targetToken);

            var response = await targetClient.PostAsJsonAsync("/backups/restore", new RestoreRequest(archivePath)).ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var payload = await response.Content.ReadFromJsonAsync<RestoreResponse>().ConfigureAwait(true);
            payload.Should().NotBeNull();
            payload!.Status.Should().Be("restored");
            File.Exists(Path.Combine(targetRoot, "images", "2026", "06", "25", "source-shrimp.jpg")).Should().BeTrue();

            SqliteConnection.ClearAllPools();
            using var provider = BuildProvider(targetRoot);
            var sourceUser = await provider.GetRequiredService<IUserRepository>()
                .GetByUserNameAsync("source-admin", CancellationToken.None)
                .ConfigureAwait(true);
            sourceUser.Should().NotBeNull();
        }
        finally
        {
            DeleteDirectory(sourceRoot);
            DeleteDirectory(targetRoot);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Incompatible_backup_schema_is_rejected_before_existing_data_changes()
    {
        var targetRoot = CreateTempRoot();

        try
        {
            var targetToken = await SeedUserAndLoginAsync(targetRoot, "target-admin", "TargetPass1234", "Administrator").ConfigureAwait(true);
            var archivePath = CreateBackupWithDatabaseSchemaVersion(targetRoot, version: 99);

            await using var targetFactory = new RestoreWebApplicationFactory(targetRoot);
            using var targetClient = targetFactory.CreateClient();
            targetClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", targetToken);

            var response = await targetClient.PostAsJsonAsync("/backups/restore", new RestoreRequest(archivePath)).ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var payload = await response.Content.ReadFromJsonAsync<RestoreFailureResponse>().ConfigureAwait(true);
            payload.Should().NotBeNull();
            payload!.Reason.Should().Be(BackupRestoreFailureReasons.UnsupportedSchemaVersion);

            using var provider = BuildProvider(targetRoot);
            var targetUser = await provider.GetRequiredService<IUserRepository>()
                .GetByUserNameAsync("target-admin", CancellationToken.None)
                .ConfigureAwait(true);
            targetUser.Should().NotBeNull();
        }
        finally
        {
            DeleteDirectory(targetRoot);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Restore_failure_after_validation_rolls_back_existing_files()
    {
        var sourceRoot = CreateTempRoot();
        var targetRoot = CreateTempRoot();

        try
        {
            var archivePath = await CreateSourceBackupAsync(sourceRoot).ConfigureAwait(true);
            var targetToken = await SeedUserAndLoginAsync(targetRoot, "target-admin", "TargetPass1234", "Administrator").ConfigureAwait(true);
            var imageRootPath = Path.Combine(targetRoot, "images");
            if (Directory.Exists(imageRootPath))
            {
                Directory.Delete(imageRootPath, recursive: true);
            }

            await File.WriteAllTextAsync(imageRootPath, "occupied").ConfigureAwait(true);

            await using var targetFactory = new RestoreWebApplicationFactory(targetRoot);
            using var targetClient = targetFactory.CreateClient();
            targetClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", targetToken);

            var response = await targetClient.PostAsJsonAsync("/backups/restore", new RestoreRequest(archivePath)).ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            var payload = await response.Content.ReadFromJsonAsync<RestoreFailureResponse>().ConfigureAwait(true);
            payload.Should().NotBeNull();
            payload!.Reason.Should().Be(BackupRestoreFailureReasons.RestoreFailed);
            File.ReadAllText(imageRootPath).Should().Be("occupied");

            using var provider = BuildProvider(targetRoot);
            var targetUser = await provider.GetRequiredService<IUserRepository>()
                .GetByUserNameAsync("target-admin", CancellationToken.None)
                .ConfigureAwait(true);
            targetUser.Should().NotBeNull();
        }
        finally
        {
            DeleteDirectory(sourceRoot);
            DeleteDirectory(targetRoot);
        }
    }

    private static async Task<string> CreateSourceBackupAsync(string sourceRoot)
    {
        var sourceToken = await SeedUserAndLoginAsync(sourceRoot, "source-admin", "SourcePass1234", "Administrator").ConfigureAwait(true);
        var capturePath = Path.Combine(sourceRoot, "images", "2026", "06", "25", "source-shrimp.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(capturePath)!);
        await File.WriteAllTextAsync(capturePath, "source-image").ConfigureAwait(true);

        await using var sourceFactory = new RestoreWebApplicationFactory(sourceRoot, capacityProbe: new AllowCapacityProbe());
        using var sourceClient = sourceFactory.CreateClient();
        sourceClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sourceToken);
        var response = await sourceClient.PostAsync("/backups/export", content: null).ConfigureAwait(true);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<BackupExportResponse>().ConfigureAwait(true);
        payload.Should().NotBeNull();
        return payload!.ArchivePath!;
    }

    private static string CreateBackupWithDatabaseSchemaVersion(string rootPath, int version)
    {
        var packageRoot = Path.Combine(rootPath, "bad-package");
        var databaseDirectory = Path.Combine(packageRoot, "database");
        Directory.CreateDirectory(databaseDirectory);
        var databasePath = Path.Combine(databaseDirectory, "shrimpcam.db");

        using (var connection = new SqliteConnection($"Data Source={databasePath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE schema_version (
                    version INTEGER NOT NULL,
                    applied_at_utc TEXT NOT NULL
                );

                INSERT INTO schema_version (version, applied_at_utc)
                VALUES ($version, '2026-06-25T00:00:00.0000000Z');
                """;
            command.Parameters.AddWithValue("$version", version);
            _ = command.ExecuteNonQuery();
        }

        using (var manifestStream = File.Create(Path.Combine(packageRoot, "manifest.json")))
        {
            JsonSerializer.Serialize(
                manifestStream,
                new
                {
                    version = 1,
                    entries = DatabaseManifestEntries,
                });
        }

        var archivePath = Path.Combine(rootPath, "incompatible-backup.zip");
        SqliteConnection.ClearAllPools();
        ZipFile.CreateFromDirectory(packageRoot, archivePath);
        return archivePath;
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

        await using var factory = new RestoreWebApplicationFactory(rootPath);
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
            if (!Directory.Exists(rootPath) && !File.Exists(rootPath))
            {
                return;
            }

            try
            {
                if (File.Exists(rootPath))
                {
                    File.Delete(rootPath);
                    return;
                }

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

    private sealed record RestoreRequest(string ArchivePath);

    private sealed record RestoreResponse(
        string Status,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset CompletedAtUtc);

    private sealed record RestoreFailureResponse(string Status, string Reason);

    private sealed record BackupExportResponse(string Status, string? ArchivePath);

    private sealed record LoginRequest(string UserName, string Password);

    private sealed record LoginSuccessResponse(
        string Status,
        Guid SessionId,
        Guid UserId,
        string UserName,
        string Token,
        DateTimeOffset ExpiresAtUtc);

    private sealed class RestoreWebApplicationFactory(
        string rootPath,
        IBackupStorageCapacityProbe? capacityProbe = null) : WebApplicationFactory<Program>
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
                        ["ShrimpCam:Storage:ImageRootPath"] = Path.Combine(rootPath, "images"),
                        ["ShrimpCam:Storage:TimelapseRootPath"] = Path.Combine(rootPath, "timelapse"),
                    }));
            builder.ConfigureTestServices(
                services =>
                {
                    services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
                    services.AddSingleton<IProcessRunner>(new StubProcessRunner());

                    if (capacityProbe is not null)
                    {
                        services.AddSingleton(capacityProbe);
                    }
                });
        }
    }

    private sealed class AllowCapacityProbe : IBackupStorageCapacityProbe
    {
        public bool HasAvailableSpace(string directoryPath, long requiredBytes) => true;
    }

    private sealed class StubProcessRunner : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                new ProcessResult(
                    0,
                    """
                    Logitech C920:
                        /dev/video0
                    """,
                    string.Empty));
        }
    }
}

#pragma warning restore CA2007
