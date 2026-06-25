using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Captures;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Infrastructure.Captures;

#pragma warning disable CA2007

namespace ShrimpCam.Infrastructure.Tests.Captures;

public sealed class ScheduledCaptureIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Scheduler_captures_only_once_for_a_due_interval()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            var services = new ServiceCollection();
            var clock = new FixedClock(new DateTimeOffset(2026, 06, 24, 12, 03, 00, TimeSpan.Zero));
            var processRunner = new StubProcessRunner(shouldFail: false);

            Infrastructure.DependencyInjection.AddInfrastructure(services);
            services.AddSingleton<IClock>(clock);
            services.AddSingleton<IProcessRunner>(processRunner);

            using var provider = services.BuildServiceProvider();
            var scheduler = provider.GetRequiredService<IScheduledCaptureService>();

            var options = CreateOptions(rootPath, enabled: true);

            var firstResult = await scheduler.RunDueCaptureAsync(options, CancellationToken.None).ConfigureAwait(true);
            var secondResult = await scheduler.RunDueCaptureAsync(options, CancellationToken.None).ConfigureAwait(true);

            firstResult.Outcome.Should().Be(ScheduledCaptureOutcome.Captured);
            secondResult.Outcome.Should().Be(ScheduledCaptureOutcome.Waiting);
            processRunner.InvocationCount.Should().Be(1);

            var imageFiles = Directory.GetFiles(rootPath, "*.jpg", SearchOption.AllDirectories);
            imageFiles.Should().ContainSingle();
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
    public async Task Worker_logs_failed_capture_without_throwing()
    {
        var scheduledCaptureService = NSubstitute.Substitute.For<IScheduledCaptureService>();
        var logger = new ListLogger<ScheduledCaptureWorker>();
        var worker = new ScheduledCaptureWorker(
            Microsoft.Extensions.Options.Options.Create(CreateOptions("data/images", enabled: true)),
            scheduledCaptureService,
            logger);

        scheduledCaptureService.RunDueCaptureAsync(NSubstitute.Arg.Any<ShrimpCamOptions>(), NSubstitute.Arg.Any<CancellationToken>())
            .Returns(ScheduledCaptureRunResult.Create(
                ScheduledCaptureOutcome.Failed,
                new DateTimeOffset(2026, 06, 24, 12, 00, 00, TimeSpan.Zero),
                new DateTimeOffset(2026, 06, 24, 12, 05, 00, TimeSpan.Zero),
                failureReason: ManualCaptureFailureReasons.CameraUnavailable));

        Func<Task> act = () => worker.RunSingleIterationAsync(CancellationToken.None);

        await act.Should().NotThrowAsync().ConfigureAwait(true);
        logger.Entries.Should().ContainSingle(entry =>
            entry.LogLevel == Microsoft.Extensions.Logging.LogLevel.Warning
            && entry.Message.Contains("cameraUnavailable", StringComparison.Ordinal));
    }

    private static ShrimpCamOptions CreateOptions(string rootPath, bool enabled) =>
        new()
        {
            Camera = new CameraOptions
            {
                Platform = "Linux",
                Source = "/dev/video0",
            },
            Capture = new CaptureOptions
            {
                Enabled = enabled,
                IntervalMinutes = 5,
                ActiveStartHourUtc = 6,
                ActiveEndHourUtc = 22,
            },
            Storage = new StorageOptions
            {
                ImageRootPath = rootPath,
                RetentionDays = 30,
            },
        };

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class StubProcessRunner(bool shouldFail) : IProcessRunner
    {
        public int InvocationCount { get; private set; }

        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InvocationCount++;

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

    private sealed class ListLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public List<(Microsoft.Extensions.Logging.LogLevel LogLevel, string Message)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull =>
            NullScope.Instance;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}

#pragma warning restore CA2007
