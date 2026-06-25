using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Captures;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Infrastructure.Captures;

#pragma warning disable CA2007

namespace ShrimpCam.Infrastructure.Tests.Captures;

public sealed class CaptureRetentionServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Cleanup_deletes_only_captures_older_than_the_retention_cutoff()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var clock = Substitute.For<IClock>();
        var rootPath = Path.GetFullPath("data/images");
        var expiredImage = Path.Combine(rootPath, "2026", "05", "01", "expired.jpg");
        var expiredMetadata = Path.Combine(rootPath, "2026", "05", "01", "expired.json");
        var recentImage = Path.Combine(rootPath, "2026", "06", "24", "recent.jpg");

        clock.UtcNow.Returns(new DateTimeOffset(2026, 06, 24, 18, 00, 00, TimeSpan.Zero));
        fileSystem.DirectoryExists(rootPath).Returns(true);
        fileSystem.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
            .Returns([expiredImage, expiredMetadata, recentImage]);
        fileSystem.GetLastWriteTimeUtc(expiredImage).Returns(new DateTimeOffset(2026, 05, 01, 12, 00, 00, TimeSpan.Zero));
        fileSystem.GetLastWriteTimeUtc(expiredMetadata).Returns(new DateTimeOffset(2026, 05, 01, 12, 00, 00, TimeSpan.Zero));
        fileSystem.GetLastWriteTimeUtc(recentImage).Returns(new DateTimeOffset(2026, 06, 24, 12, 00, 00, TimeSpan.Zero));
        fileSystem.FileExists(expiredImage).Returns(true);
        fileSystem.FileExists(expiredMetadata).Returns(true);

        var service = new CaptureRetentionService(clock, fileSystem);

        var result = await service.CleanupExpiredCapturesAsync(
                new StorageOptions { ImageRootPath = rootPath, RetentionDays = 30 },
                CancellationToken.None)
            .ConfigureAwait(true);

        result.DeletedCount.Should().Be(1);
        result.FailedCount.Should().Be(0);
        result.Items.Should().ContainSingle(item => item.RelativePath == "2026/05/01/expired.jpg" && item.Deleted);
        fileSystem.Received(1).DeleteFile(expiredImage);
        fileSystem.Received(1).DeleteFile(expiredMetadata);
        fileSystem.DidNotReceive().DeleteFile(recentImage);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Cleanup_records_failures_and_continues_with_remaining_expired_items()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var clock = Substitute.For<IClock>();
        var rootPath = Path.GetFullPath("data/images");
        var lockedImage = Path.Combine(rootPath, "2026", "05", "01", "locked.jpg");
        var lockedMetadata = Path.Combine(rootPath, "2026", "05", "01", "locked.json");
        var staleImage = Path.Combine(rootPath, "2026", "05", "02", "stale.jpg");
        var staleMetadata = Path.Combine(rootPath, "2026", "05", "02", "stale.json");
        var failure = new IOException("file locked");

        clock.UtcNow.Returns(new DateTimeOffset(2026, 06, 24, 18, 00, 00, TimeSpan.Zero));
        fileSystem.DirectoryExists(rootPath).Returns(true);
        fileSystem.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
            .Returns([lockedImage, lockedMetadata, staleImage, staleMetadata]);
        fileSystem.GetLastWriteTimeUtc(Arg.Any<string>()).Returns(new DateTimeOffset(2026, 05, 01, 12, 00, 00, TimeSpan.Zero));
        fileSystem.FileExists(Arg.Any<string>()).Returns(true);
        fileSystem.When(fs => fs.DeleteFile(lockedImage)).Do(_ => throw failure);
        fileSystem.When(fs => fs.DeleteFile(lockedMetadata)).Do(_ => throw failure);

        var service = new CaptureRetentionService(clock, fileSystem);

        var result = await service.CleanupExpiredCapturesAsync(
                new StorageOptions { ImageRootPath = rootPath, RetentionDays = 30 },
                CancellationToken.None)
            .ConfigureAwait(true);

        result.DeletedCount.Should().Be(1);
        result.FailedCount.Should().Be(1);
        result.Items.Should().ContainSingle(item =>
            item.RelativePath == "2026/05/01/locked.jpg"
            && !item.Deleted
            && item.FailureReason == "file locked");
        result.Items.Should().ContainSingle(item => item.RelativePath == "2026/05/02/stale.jpg" && item.Deleted);
        fileSystem.Received(1).DeleteFile(staleImage);
        fileSystem.Received(1).DeleteFile(staleMetadata);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Managed_capture_filter_rejects_paths_outside_the_image_root()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var clock = Substitute.For<IClock>();
        var rootPath = Path.GetFullPath("data/images");

        CaptureRetentionService.IsManagedCaptureFile(rootPath, Path.GetFullPath(Path.Combine(rootPath, "..", "escape.jpg"))).Should().BeFalse();
        CaptureRetentionService.IsManagedCaptureFile(rootPath, Path.Combine(rootPath, ".shrimpcam", "scheduled-capture-state.json")).Should().BeFalse();
        CaptureRetentionService.IsManagedCaptureFile(rootPath, Path.Combine(rootPath, "2026", "06", "24", "capture.jpg")).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Cleanup_removes_expired_image_and_metadata_from_disk()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var captureDirectory = Path.Combine(rootPath, "2026", "05", "01");
        var expiredImage = Path.Combine(captureDirectory, "expired.jpg");
        var expiredMetadata = Path.Combine(captureDirectory, "expired.json");

        Directory.CreateDirectory(captureDirectory);
        await File.WriteAllTextAsync(expiredImage, "image-bytes").ConfigureAwait(true);
        await File.WriteAllTextAsync(expiredMetadata, "{}").ConfigureAwait(true);
        File.SetLastWriteTimeUtc(expiredImage, new DateTime(2026, 05, 01, 12, 00, 00, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(expiredMetadata, new DateTime(2026, 05, 01, 12, 00, 00, DateTimeKind.Utc));

        try
        {
            var services = new ServiceCollection();
            Infrastructure.DependencyInjection.AddInfrastructure(services);
            services.AddSingleton<IClock>(new FixedClock(new DateTimeOffset(2026, 06, 24, 18, 00, 00, TimeSpan.Zero)));

            using var provider = services.BuildServiceProvider();
            var cleanup = provider.GetRequiredService<ICaptureRetentionService>();

            var result = await cleanup.CleanupExpiredCapturesAsync(
                    new StorageOptions { ImageRootPath = rootPath, RetentionDays = 30 },
                    CancellationToken.None)
                .ConfigureAwait(true);

            result.DeletedCount.Should().Be(1);
            File.Exists(expiredImage).Should().BeFalse();
            File.Exists(expiredMetadata).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Invalid_storage_root_is_rejected_before_cleanup_runs()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var clock = Substitute.For<IClock>();
        var service = new CaptureRetentionService(clock, fileSystem);

        var act = () => service.CleanupExpiredCapturesAsync(
            new StorageOptions { ImageRootPath = string.Empty, RetentionDays = 30 },
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Infrastructure_registers_capture_retention_service()
    {
        var services = new ServiceCollection();
        Infrastructure.DependencyInjection.AddInfrastructure(services);

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ICaptureRetentionService>().Should().NotBeNull();
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}

#pragma warning restore CA2007
