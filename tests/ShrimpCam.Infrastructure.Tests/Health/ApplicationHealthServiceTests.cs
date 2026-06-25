using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Health;
using ShrimpCam.Infrastructure.Health;

namespace ShrimpCam.Infrastructure.Tests.Health;

public sealed class ApplicationHealthServiceTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Health_service_reports_healthy_when_database_storage_and_camera_are_available()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var clock = Substitute.For<IClock>();
            var cameraStatus = Substitute.For<ICameraStatusService>();
            var fileSystem = new StubFileSystem();
            var options = CreateOptions(rootPath);
            var now = new DateTimeOffset(2026, 06, 25, 12, 00, 00, TimeSpan.Zero);

            clock.UtcNow.Returns(now);
            cameraStatus.GetSnapshot().Returns(new CameraStatusSnapshot(CameraStatusLevel.Online, null, now));

            var databaseProbe = new SqliteDatabaseHealthProbe(Options.Create(options));
            var storageProbe = new StorageHealthProbe(Options.Create(options), fileSystem);
            var service = new ApplicationHealthService(clock, cameraStatus, databaseProbe, storageProbe);

            var report = await service.GetCurrentAsync(CancellationToken.None).ConfigureAwait(true);

            report.Status.Should().Be(HealthStatusLevel.Healthy);
            report.Components["database"].Status.Should().Be(HealthStatusLevel.Healthy);
            report.Components["storage"].Status.Should().Be(HealthStatusLevel.Healthy);
            report.Components["camera"].Status.Should().Be(HealthStatusLevel.Healthy);
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Health_service_reports_degraded_when_camera_status_is_degraded()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var clock = Substitute.For<IClock>();
            var cameraStatus = Substitute.For<ICameraStatusService>();
            var fileSystem = new StubFileSystem();
            var options = CreateOptions(rootPath);
            var now = new DateTimeOffset(2026, 06, 25, 12, 00, 00, TimeSpan.Zero);

            clock.UtcNow.Returns(now);
            cameraStatus.GetSnapshot().Returns(new CameraStatusSnapshot(CameraStatusLevel.Degraded, "camera unavailable", now));

            var service = new ApplicationHealthService(
                clock,
                cameraStatus,
                new SqliteDatabaseHealthProbe(Options.Create(options)),
                new StorageHealthProbe(Options.Create(options), fileSystem));

            var report = await service.GetCurrentAsync(CancellationToken.None).ConfigureAwait(true);

            report.Status.Should().Be(HealthStatusLevel.Degraded);
            report.Components["camera"].Status.Should().Be(HealthStatusLevel.Unhealthy);
            report.Components["camera"].Detail.Should().Be("camera unavailable");
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Database_probe_reports_unhealthy_when_database_cannot_be_opened()
    {
        var rootPath = CreateTempRoot();
        var databaseDirectoryPath = Path.Combine(rootPath, "directory-as-database");
        Directory.CreateDirectory(databaseDirectoryPath);

        try
        {
            var probe = new SqliteDatabaseHealthProbe(
                Options.Create(
                    new ShrimpCamOptions
                    {
                        Storage = new StorageOptions
                        {
                            DatabasePath = databaseDirectoryPath,
                            ImageRootPath = Path.Combine(rootPath, "images"),
                            TimelapseRootPath = Path.Combine(rootPath, "timelapse"),
                            RetentionDays = 30,
                        },
                    }));

            var result = await probe.CheckAsync(CancellationToken.None).ConfigureAwait(true);

            result.Status.Should().Be(HealthStatusLevel.Unhealthy);
            result.Detail.Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Storage_probe_reports_unhealthy_when_a_required_storage_path_cannot_be_created()
    {
        var rootPath = CreateTempRoot();
        Directory.CreateDirectory(rootPath);
        var imageFilePath = Path.Combine(rootPath, "images-as-file");
        await File.WriteAllTextAsync(imageFilePath, "occupied").ConfigureAwait(true);

        try
        {
            var probe = new StorageHealthProbe(
                Options.Create(
                    new ShrimpCamOptions
                    {
                        Storage = new StorageOptions
                        {
                            DatabasePath = Path.Combine(rootPath, "shrimpcam.db"),
                            ImageRootPath = imageFilePath,
                            TimelapseRootPath = Path.Combine(rootPath, "timelapse"),
                            RetentionDays = 30,
                        },
                    }),
                new StubFileSystem());

            var result = await probe.CheckAsync(CancellationToken.None).ConfigureAwait(true);

            result.Status.Should().Be(HealthStatusLevel.Unhealthy);
            result.Detail.Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Infrastructure_registers_application_health_services()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOptions<ShrimpCamOptions>>(Options.Create(CreateOptions("data")));
        Infrastructure.DependencyInjection.AddInfrastructure(services);

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IApplicationHealthService>().Should().NotBeNull();
        provider.GetRequiredService<IDatabaseHealthProbe>().Should().NotBeNull();
        provider.GetRequiredService<IStorageHealthProbe>().Should().NotBeNull();
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

    private sealed class StubFileSystem : IFileSystem
    {
        public string Combine(params string[] paths) => Path.Combine(paths);
        public bool DirectoryExists(string path) => Directory.Exists(path);
        public void CreateDirectory(string path) => Directory.CreateDirectory(path);
        public IEnumerable<string> EnumerateDirectories(string path) => Directory.EnumerateDirectories(path);
        public bool FileExists(string path) => File.Exists(path);
        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) => Directory.EnumerateFiles(path, searchPattern, searchOption);
        public void WriteAllLines(string path, IEnumerable<string> contents) => File.WriteAllLines(path, contents);
        public DateTimeOffset GetLastWriteTimeUtc(string path) => File.GetLastWriteTimeUtc(path);
        public void MoveFile(string sourcePath, string destinationPath) => File.Move(sourcePath, destinationPath);
        public void DeleteFile(string path) => File.Delete(path);
        public string ReadAllText(string path) => File.ReadAllText(path);
        public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);
        public string GetTemporaryFilePath(string extension) => Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");
    }
}
