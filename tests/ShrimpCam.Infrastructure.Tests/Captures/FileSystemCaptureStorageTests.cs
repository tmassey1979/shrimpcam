using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Captures;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Infrastructure.Captures;

namespace ShrimpCam.Infrastructure.Tests.Captures;

public sealed class FileSystemCaptureStorageTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Source_type_names_are_normalized_for_file_naming()
    {
        FileSystemCaptureStorage.NormalizeSourceType(" Manual ").Should().Be("manual");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void File_extension_gets_a_leading_dot_when_missing()
    {
        FileSystemCaptureStorage.NormalizeExtension("jpg").Should().Be(".jpg");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Collision_handling_appends_a_numeric_suffix()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var storage = new FileSystemCaptureStorage(fileSystem);

        fileSystem.Combine("root/2026/06/24", "20260624T120000000Z_manual.jpg")
            .Returns("root/2026/06/24/20260624T120000000Z_manual.jpg");
        fileSystem.Combine("root/2026/06/24", "20260624T120000000Z_manual_001.jpg")
            .Returns("root/2026/06/24/20260624T120000000Z_manual_001.jpg");
        fileSystem.FileExists("root/2026/06/24/20260624T120000000Z_manual.jpg").Returns(true);
        fileSystem.FileExists("root/2026/06/24/20260624T120000000Z_manual_001.jpg").Returns(false);

        var path = storage.GetNextAvailablePath("root/2026/06/24", "20260624T120000000Z_manual", ".jpg");

        path.Should().Be("root/2026/06/24/20260624T120000000Z_manual_001.jpg");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Storage_layout_is_created_and_metadata_is_written()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var stagedFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jpg");
        await File.WriteAllTextAsync(stagedFilePath, "image-bytes").ConfigureAwait(true);

        try
        {
            var services = new ServiceCollection();
            Infrastructure.DependencyInjection.AddInfrastructure(services);

            using var provider = services.BuildServiceProvider();
            var storage = provider.GetRequiredService<ICaptureStorage>();

            var storedCapture = await storage.StoreAsync(
                    new StorageOptions { ImageRootPath = rootPath, RetentionDays = 30 },
                    new CaptureStorageRequest(
                        new DateTimeOffset(2026, 06, 24, 18, 15, 30, TimeSpan.Zero),
                        CaptureSourceTypes.Manual,
                        stagedFilePath),
                    CancellationToken.None)
                .ConfigureAwait(true);

            storedCapture.RelativeImagePath.Should().Be("2026/06/24/20260624T181530000Z_manual.jpg");
            File.Exists(storedCapture.ImagePath).Should().BeTrue();
            File.Exists(storedCapture.MetadataPath).Should().BeTrue();

            var metadataJson = await File.ReadAllTextAsync(storedCapture.MetadataPath).ConfigureAwait(true);
            metadataJson.Should().Contain("\"sourceType\": \"Manual\"");
            metadataJson.Should().Contain("\"relativeImagePath\": \"2026/06/24/20260624T181530000Z_manual.jpg\"");
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }

            if (File.Exists(stagedFilePath))
            {
                File.Delete(stagedFilePath);
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Invalid_storage_root_is_rejected_before_metadata_is_written()
    {
        var stagedFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jpg");
        await File.WriteAllTextAsync(stagedFilePath, "image-bytes").ConfigureAwait(true);

        try
        {
            var services = new ServiceCollection();
            Infrastructure.DependencyInjection.AddInfrastructure(services);

            using var provider = services.BuildServiceProvider();
            var storage = provider.GetRequiredService<ICaptureStorage>();

            var act = () => storage.StoreAsync(
                new StorageOptions { ImageRootPath = string.Empty, RetentionDays = 30 },
                new CaptureStorageRequest(
                    DateTimeOffset.UtcNow,
                    CaptureSourceTypes.Manual,
                    stagedFilePath),
                CancellationToken.None);

            await act.Should().ThrowAsync<ValidationException>().ConfigureAwait(true);
        }
        finally
        {
            if (File.Exists(stagedFilePath))
            {
                File.Delete(stagedFilePath);
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Infrastructure_registers_capture_storage()
    {
        var services = new ServiceCollection();
        Infrastructure.DependencyInjection.AddInfrastructure(services);

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ICaptureStorage>().Should().NotBeNull();
    }
}
