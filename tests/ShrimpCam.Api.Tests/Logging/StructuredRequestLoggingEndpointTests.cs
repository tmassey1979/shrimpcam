using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShrimpCam.Api.Logging;
using ShrimpCam.Core.Abstractions;

#nullable enable
#pragma warning disable CA2007

namespace ShrimpCam.Api.Tests.Logging;

public sealed class StructuredRequestLoggingEndpointTests
{
    [Fact]
    [Trait("Category", "Api")]
    public async Task Api_request_emits_structured_completion_log_with_correlation_and_redacted_query()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var loggerProvider = new CapturingLoggerProvider();

        try
        {
            await using var factory = new LoggingWebApplicationFactory(rootPath, loggerProvider);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Add(StructuredRequestLoggingMiddleware.CorrelationHeaderName, "api-correlation-1");

            var response = await client.GetAsync("/health?token=super-secret-token&camera=ok").ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.GetValues(StructuredRequestLoggingMiddleware.CorrelationHeaderName)
                .Should().ContainSingle().Which.Should().Be("api-correlation-1");

            var entry = loggerProvider.Entries
                .Single(logEntry => logEntry.EventId.Name == "RequestCompleted");

            entry.Fields["CorrelationId"].Should().Be("api-correlation-1");
            entry.Fields["RequestTarget"].Should().Be("/health?token=[redacted]&camera=ok");
            entry.Fields["StatusCode"].Should().Be(200);
            entry.Message.Should().NotContain("super-secret-token");
            entry.Scopes.Should().Contain(scope => scope["correlationId"]!.Equals("api-correlation-1"));
            entry.Scopes.SelectMany(scope => scope.Values.Select(value => value?.ToString() ?? string.Empty))
                .Should().NotContain(value => value.Contains("super-secret-token", StringComparison.Ordinal));
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    private sealed class LoggingWebApplicationFactory(
        string rootPath,
        CapturingLoggerProvider loggerProvider) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(
                logging =>
                {
                    logging.ClearProviders();
                    logging.AddProvider(loggerProvider);
                });
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
                services => services.AddSingleton<IProcessRunner>(new StubProcessRunner()));
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
                    Logitech USB Camera:
                        /dev/video0
                    """,
                    string.Empty));
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<CapturedLogEntry> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(List<CapturedLogEntry> entries) : ILogger
    {
        private readonly List<Dictionary<string, object?>> _activeScopes = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            var scope = ToFields(state);
            _activeScopes.Add(scope);
            return new Scope(() => _activeScopes.Remove(scope));
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Add(
                new CapturedLogEntry(
                    logLevel,
                    eventId,
                    formatter(state, exception),
                    ToFields(state),
                    _activeScopes.Select(scope => new Dictionary<string, object?>(scope)).ToList()));
        }

        private static Dictionary<string, object?> ToFields<TState>(TState state)
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                return pairs.ToDictionary(pair => pair.Key, pair => pair.Value);
            }

            return new Dictionary<string, object?>
            {
                ["state"] = state,
            };
        }

        private sealed class Scope(Action onDispose) : IDisposable
        {
            public void Dispose() => onDispose();
        }
    }

    private sealed record CapturedLogEntry(
        LogLevel LogLevel,
        EventId EventId,
        string Message,
        Dictionary<string, object?> Fields,
        IReadOnlyList<Dictionary<string, object?>> Scopes);

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
}

#pragma warning restore CA2007
