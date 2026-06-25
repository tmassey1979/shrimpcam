using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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

public sealed class BackupExportEndpointTests
{
    [Fact]
    [Trait("Category", "Api")]
    public async Task Administrator_can_export_backup_archive_with_manifest_diagnostics_database_and_capture_files()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-admin", "AdminPass1234", "Administrator").ConfigureAwait(true);
            var capturePath = Path.Combine(rootPath, "images", "2026", "06", "25", "shrimp.jpg");
            Directory.CreateDirectory(Path.GetDirectoryName(capturePath)!);
            await File.WriteAllTextAsync(capturePath, "fake-image").ConfigureAwait(true);

            await using var factory = new BackupWebApplicationFactory(rootPath, capacityProbe: new AllowCapacityProbe());
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.PostAsync("/backups/export", content: null).ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var payload = await response.Content.ReadFromJsonAsync<BackupExportResponse>().ConfigureAwait(true);
            payload.Should().NotBeNull();
            payload!.Status.Should().Be("exported");
            payload.ArchivePath.Should().NotBeNullOrWhiteSpace();
            payload.FileName.Should().StartWith("shrimpcam-backup-").And.EndWith(".zip");
            File.Exists(payload.ArchivePath).Should().BeTrue();
            payload.ArchiveSizeBytes.Should().BeGreaterThan(0);

            using var archive = ZipFile.OpenRead(payload.ArchivePath!);
            archive.Entries.Select(entry => entry.FullName).Should().Contain("manifest.json");
            archive.Entries.Select(entry => entry.FullName).Should().Contain("diagnostics.json");
            archive.Entries.Select(entry => entry.FullName).Should().Contain("database/shrimpcam.db");
            archive.Entries.Select(entry => entry.FullName).Should().Contain("captures/images/2026/06/25/shrimp.jpg");
            ReadEntry(archive, "manifest.json").Should().Contain("\"version\": 1");
            ReadEntry(archive, "diagnostics.json").Should().Contain("\"configuration\"");
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Overlapping_backup_export_returns_conflict()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-admin", "AdminPass1234", "Administrator").ConfigureAwait(true);
            await using var factory = new BackupWebApplicationFactory(
                rootPath,
                backupExportService: new StubBackupExportService(
                    BackupExportResult.Failure(
                        BackupExportFailureReasons.ExportAlreadyRunning,
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow)));
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.PostAsync("/backups/export", content: null).ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            var payload = await response.Content.ReadFromJsonAsync<BackupFailureResponse>().ConfigureAwait(true);
            payload.Should().NotBeNull();
            payload!.Reason.Should().Be(BackupExportFailureReasons.ExportAlreadyRunning);
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Backup_export_with_insufficient_storage_returns_operator_visible_failure()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-admin", "AdminPass1234", "Administrator").ConfigureAwait(true);
            await using var factory = new BackupWebApplicationFactory(rootPath, capacityProbe: new DenyCapacityProbe());
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.PostAsync("/backups/export", content: null).ConfigureAwait(true);

            ((int)response.StatusCode).Should().Be(StatusCodes.Status507InsufficientStorage);
            var payload = await response.Content.ReadFromJsonAsync<BackupFailureResponse>().ConfigureAwait(true);
            payload.Should().NotBeNull();
            payload!.Reason.Should().Be(BackupExportFailureReasons.InsufficientStorage);
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Viewer_cannot_start_backup_export()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-viewer", "ViewerPass123", "Viewer").ConfigureAwait(true);
            await using var factory = new BackupWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.PostAsync("/backups/export", content: null).ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    private static string ReadEntry(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName);
        entry.Should().NotBeNull();
        using var reader = new StreamReader(entry!.Open());
        return reader.ReadToEnd();
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

        await using var factory = new BackupWebApplicationFactory(rootPath);
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

    private sealed record BackupExportResponse(
        string Status,
        string? ArchivePath,
        string? FileName,
        long ArchiveSizeBytes,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset CompletedAtUtc);

    private sealed record BackupFailureResponse(string Status, string Reason);

    private sealed class BackupWebApplicationFactory(
        string rootPath,
        IBackupStorageCapacityProbe? capacityProbe = null,
        IBackupExportService? backupExportService = null) : WebApplicationFactory<Program>
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

                    if (backupExportService is not null)
                    {
                        services.AddSingleton(backupExportService);
                    }
                });
        }
    }

    private sealed class AllowCapacityProbe : IBackupStorageCapacityProbe
    {
        public bool HasAvailableSpace(string directoryPath, long requiredBytes) => true;
    }

    private sealed class DenyCapacityProbe : IBackupStorageCapacityProbe
    {
        public bool HasAvailableSpace(string directoryPath, long requiredBytes) => false;
    }

    private sealed class StubBackupExportService(BackupExportResult result) : IBackupExportService
    {
        public Task<BackupExportResult> ExportAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }
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
