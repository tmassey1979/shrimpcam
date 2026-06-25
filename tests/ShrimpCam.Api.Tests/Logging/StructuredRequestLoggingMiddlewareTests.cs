using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShrimpCam.Api.Logging;

namespace ShrimpCam.Api.Tests.Logging;

public sealed class StructuredRequestLoggingMiddlewareTests
{
    [Fact]
    public async Task Completed_request_logs_structured_fields_and_sets_correlation_response_header()
    {
        var logger = new CapturingLogger<StructuredRequestLoggingMiddleware>();
        var middleware = new StructuredRequestLoggingMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            },
            logger);
        var context = CreateContext("/health", "?page=1");
        context.Request.Headers[StructuredRequestLoggingMiddleware.CorrelationHeaderName] = "shrimp-correlation-1";
        context.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "user-123")],
                authenticationType: "test"));

        await middleware.InvokeAsync(context).ConfigureAwait(true);

        context.Response.Headers[StructuredRequestLoggingMiddleware.CorrelationHeaderName].ToString()
            .Should().Be("shrimp-correlation-1");

        var entry = logger.SingleEntry();
        entry.LogLevel.Should().Be(LogLevel.Information);
        entry.EventId.Name.Should().Be("RequestCompleted");
        entry.Fields["RequestMethod"].Should().Be("GET");
        entry.Fields["RequestTarget"].Should().Be("/health?page=1");
        entry.Fields["CorrelationId"].Should().Be("shrimp-correlation-1");
        entry.Fields["StatusCode"].Should().Be(204);
        entry.Fields["UserIdentifier"].Should().Be("user-123");
        entry.Scopes.Should().Contain(scope => scope["componentName"]!.Equals("ShrimpCam.Api"));
        entry.Scopes.Should().Contain(scope => scope["eventName"]!.Equals("HttpRequest"));
        entry.Scopes.Should().Contain(scope => scope["correlationId"]!.Equals("shrimp-correlation-1"));
        entry.Scopes.Should().Contain(scope => scope["userIdentifier"]!.Equals("user-123"));
    }

    [Fact]
    public async Task Failed_request_logs_exception_with_same_correlation_identifier()
    {
        var logger = new CapturingLogger<StructuredRequestLoggingMiddleware>();
        var middleware = new StructuredRequestLoggingMiddleware(
            _ => throw new InvalidOperationException("database unavailable"),
            logger);
        var context = CreateContext("/health", string.Empty);
        context.Request.Headers[StructuredRequestLoggingMiddleware.CorrelationHeaderName] = "failure-correlation";

        var act = () => middleware.InvokeAsync(context);

        await act.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);

        var entry = logger.SingleEntry();
        entry.LogLevel.Should().Be(LogLevel.Error);
        entry.EventId.Name.Should().Be("RequestFailed");
        entry.Exception.Should().BeOfType<InvalidOperationException>();
        entry.Fields["CorrelationId"].Should().Be("failure-correlation");
        entry.Scopes.Should().Contain(scope => scope["correlationId"]!.Equals("failure-correlation"));
    }

    [Fact]
    public async Task Request_logging_redacts_sensitive_query_values()
    {
        var logger = new CapturingLogger<StructuredRequestLoggingMiddleware>();
        var middleware = new StructuredRequestLoggingMiddleware(_ => Task.CompletedTask, logger);
        var context = CreateContext("/auth/login", "?userName=shrimp&password=SuperSecret123&token=abc123");

        await middleware.InvokeAsync(context).ConfigureAwait(true);

        var entry = logger.SingleEntry();
        entry.Fields["RequestTarget"].Should().Be("/auth/login?userName=shrimp&password=[redacted]&token=[redacted]");
        entry.Message.Should().NotContain("SuperSecret123");
        entry.Message.Should().NotContain("abc123");
        entry.Scopes.SelectMany(scope => scope.Values.Select(value => value?.ToString() ?? string.Empty))
            .Should().NotContain(value => value.Contains("SuperSecret123", StringComparison.Ordinal));
        entry.Scopes.SelectMany(scope => scope.Values.Select(value => value?.ToString() ?? string.Empty))
            .Should().NotContain(value => value.Contains("abc123", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Unsafe_correlation_header_is_replaced_before_logging()
    {
        var logger = new CapturingLogger<StructuredRequestLoggingMiddleware>();
        var middleware = new StructuredRequestLoggingMiddleware(_ => Task.CompletedTask, logger);
        var context = CreateContext("/health", string.Empty);
        context.Request.Headers[StructuredRequestLoggingMiddleware.CorrelationHeaderName] = "bad correlation with spaces";

        await middleware.InvokeAsync(context).ConfigureAwait(true);

        var correlationId = context.Response.Headers[StructuredRequestLoggingMiddleware.CorrelationHeaderName].ToString();
        correlationId.Should().NotBe("bad correlation with spaces");
        correlationId.Should().HaveLength(32);
        logger.SingleEntry().Fields["CorrelationId"].Should().Be(correlationId);
    }

    private static DefaultHttpContext CreateContext(string path, string queryString)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(queryString);
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        private readonly List<Dictionary<string, object?>> _activeScopes = [];

        public List<CapturedLogEntry> Entries { get; } = [];

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
            Entries.Add(
                new CapturedLogEntry(
                    logLevel,
                    eventId,
                    formatter(state, exception),
                    ToFields(state),
                    _activeScopes.Select(scope => new Dictionary<string, object?>(scope)).ToList(),
                    exception));
        }

        public CapturedLogEntry SingleEntry() => Entries.Should().ContainSingle().Subject;

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
        IReadOnlyList<Dictionary<string, object?>> Scopes,
        Exception? Exception);
}
