using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Captures;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Infrastructure.Captures;

#pragma warning disable CA2007

namespace ShrimpCam.Infrastructure.Tests.Captures;

public sealed class DailyTimelapseServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Generate_skips_days_with_insufficient_frames()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var processRunner = Substitute.For<IProcessRunner>();
        var imageRoot = Path.GetFullPath("data/images");
        var timelapseRoot = Path.GetFullPath("data/timelapse");
        var day = new DateOnly(2026, 06, 24);
        var frameDirectory = Path.GetFullPath(Path.Combine(imageRoot, "2026", "06", "24"));

        fileSystem.DirectoryExists(Arg.Any<string>()).Returns(true);
        fileSystem.EnumerateFiles(Arg.Any<string>(), "*.jpg", SearchOption.TopDirectoryOnly)
            .Returns([Path.Combine(frameDirectory, "20260624T120000000Z_scheduled.jpg")]);

        var service = new DailyTimelapseService(fileSystem, processRunner);

        var result = await service.GenerateAsync(
                new StorageOptions { ImageRootPath = imageRoot, TimelapseRootPath = timelapseRoot, RetentionDays = 30 },
                day,
                CancellationToken.None)
            .ConfigureAwait(true);

        result.Status.Should().Be(DailyTimelapseGenerationStatus.SkippedInsufficientFrames);
        result.FrameCount.Should().Be(1);
        await processRunner.DidNotReceiveWithAnyArgs().RunAsync(default!, default).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Generate_returns_existing_artifact_without_rerun()
    {
        var imageRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "images");
        var timelapseRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "timelapse");
        var day = new DateOnly(2026, 06, 24);
        var frameDirectory = Path.Combine(imageRoot, "2026", "06", "24");
        var outputDirectory = Path.Combine(timelapseRoot, "2026", "06");
        var outputPath = Path.Combine(outputDirectory, "20260624_timelapse.mp4");

        Directory.CreateDirectory(frameDirectory);
        Directory.CreateDirectory(outputDirectory);
        await File.WriteAllTextAsync(Path.Combine(frameDirectory, "20260624T120000000Z_scheduled.jpg"), "frame-01").ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(frameDirectory, "20260624T120100000Z_scheduled.jpg"), "frame-02").ConfigureAwait(true);
        await File.WriteAllTextAsync(outputPath, "existing-video").ConfigureAwait(true);

        try
        {
            var services = new ServiceCollection();
            Infrastructure.DependencyInjection.AddInfrastructure(services);
            var processRunner = Substitute.For<IProcessRunner>();
            services.AddSingleton(processRunner);

            using var provider = services.BuildServiceProvider();
            var service = provider.GetRequiredService<IDailyTimelapseService>();

            var result = await service.GenerateAsync(
                    new StorageOptions { ImageRootPath = imageRoot, TimelapseRootPath = timelapseRoot, RetentionDays = 30 },
                    day,
                    CancellationToken.None)
                .ConfigureAwait(true);

            result.Status.Should().Be(DailyTimelapseGenerationStatus.AlreadyExists);
            result.FrameCount.Should().Be(2);
            result.VideoPath.Should().Be(outputPath);
            result.RelativeVideoPath.Should().Be("2026/06/20260624_timelapse.mp4");
            await processRunner.DidNotReceiveWithAnyArgs().RunAsync(default!, default).ConfigureAwait(true);
        }
        finally
        {
            var imageParent = Directory.GetParent(imageRoot)?.FullName;
            var timelapseParent = Directory.GetParent(timelapseRoot)?.FullName;

            if (imageParent is not null && Directory.Exists(imageParent))
            {
                Directory.Delete(imageParent, recursive: true);
            }

            if (timelapseParent is not null && Directory.Exists(timelapseParent))
            {
                Directory.Delete(timelapseParent, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Frame_selection_orders_images_by_filename()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var processRunner = Substitute.For<IProcessRunner>();
        var service = new DailyTimelapseService(fileSystem, processRunner);
        var frameDirectory = Path.GetFullPath("data/images/2026/06/24");

        fileSystem.DirectoryExists(frameDirectory).Returns(true);
        fileSystem.EnumerateFiles(frameDirectory, "*.jpg", SearchOption.TopDirectoryOnly)
            .Returns(
            [
                Path.Combine(frameDirectory, "20260624T120500000Z_scheduled.jpg"),
                Path.Combine(frameDirectory, "20260624T120000000Z_scheduled.jpg"),
                Path.Combine(frameDirectory, "20260624T120100000Z_scheduled.jpg"),
            ]);

        var ordered = service.GetOrderedFramePaths(frameDirectory).ToArray();

        ordered.Should().ContainInOrder(
            Path.Combine(frameDirectory, "20260624T120000000Z_scheduled.jpg"),
            Path.Combine(frameDirectory, "20260624T120100000Z_scheduled.jpg"),
            Path.Combine(frameDirectory, "20260624T120500000Z_scheduled.jpg"));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Generate_assembles_a_timelapse_video_artifact_for_a_completed_day()
    {
        var imageRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "images");
        var timelapseRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "timelapse");
        var day = new DateOnly(2026, 06, 24);
        var frameDirectory = Path.Combine(imageRoot, "2026", "06", "24");
        var frameOne = Path.Combine(frameDirectory, "20260624T120000000Z_scheduled.jpg");
        var frameTwo = Path.Combine(frameDirectory, "20260624T120100000Z_scheduled.jpg");
        var expectedOutput = Path.Combine(timelapseRoot, "2026", "06", "20260624_timelapse.mp4");

        Directory.CreateDirectory(frameDirectory);
        await File.WriteAllTextAsync(frameOne, "frame-01").ConfigureAwait(true);
        await File.WriteAllTextAsync(frameTwo, "frame-02").ConfigureAwait(true);

        try
        {
            var services = new ServiceCollection();
            Infrastructure.DependencyInjection.AddInfrastructure(services);
            services.AddSingleton<IProcessRunner>(new StubTimelapseProcessRunner());

            using var provider = services.BuildServiceProvider();
            var timelapse = provider.GetRequiredService<IDailyTimelapseService>();

            var result = await timelapse.GenerateAsync(
                    new StorageOptions { ImageRootPath = imageRoot, TimelapseRootPath = timelapseRoot, RetentionDays = 30 },
                    day,
                    CancellationToken.None)
                .ConfigureAwait(true);

            result.Status.Should().Be(DailyTimelapseGenerationStatus.Generated);
            result.FrameCount.Should().Be(2);
            result.VideoPath.Should().Be(expectedOutput);
            result.RelativeVideoPath.Should().Be("2026/06/20260624_timelapse.mp4");
            File.Exists(expectedOutput).Should().BeTrue();
        }
        finally
        {
            var imageParent = Directory.GetParent(imageRoot)?.FullName;
            var timelapseParent = Directory.GetParent(timelapseRoot)?.FullName;

            if (imageParent is not null && Directory.Exists(imageParent))
            {
                Directory.Delete(imageParent, recursive: true);
            }

            if (timelapseParent is not null && Directory.Exists(timelapseParent))
            {
                Directory.Delete(timelapseParent, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Invalid_timelapse_root_is_rejected_before_generation_runs()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var processRunner = Substitute.For<IProcessRunner>();
        var service = new DailyTimelapseService(fileSystem, processRunner);

        var act = () => service.GenerateAsync(
            new StorageOptions { ImageRootPath = "data/images", TimelapseRootPath = string.Empty, RetentionDays = 30 },
            new DateOnly(2026, 06, 24),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Infrastructure_registers_daily_timelapse_service()
    {
        var services = new ServiceCollection();
        Infrastructure.DependencyInjection.AddInfrastructure(services);

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IDailyTimelapseService>().Should().NotBeNull();
    }

    private sealed class StubTimelapseProcessRunner : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var manifestPath = ExtractQuotedArguments(request.Arguments)[0];
            var outputPath = ExtractQuotedArguments(request.Arguments)[1];
            var manifestContents = File.ReadAllText(manifestPath);

            File.WriteAllText(outputPath, $"timelapse:{manifestContents}");

            return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
        }

        private static string[] ExtractQuotedArguments(string arguments)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(arguments, "\"([^\"]+)\"");
            return matches.Select(match => match.Groups[1].Value).ToArray();
        }
    }
}

#pragma warning restore CA2007
