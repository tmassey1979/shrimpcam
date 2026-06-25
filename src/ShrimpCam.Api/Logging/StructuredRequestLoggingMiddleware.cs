using System.Diagnostics;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace ShrimpCam.Api.Logging;

internal sealed class StructuredRequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<StructuredRequestLoggingMiddleware> logger)
{
    public const string CorrelationHeaderName = "X-Correlation-ID";
    private const string ComponentName = "ShrimpCam.Api";
    private const int MaxCorrelationIdLength = 128;
    private static readonly string[] SensitiveQueryKeyFragments = ["password", "secret", "token", "credential", "key"];
    private static readonly Regex SafeCorrelationIdPattern = new("^[A-Za-z0-9._:-]+$", RegexOptions.Compiled);

    private static readonly Action<ILogger, string, string, string, int, long, string, Exception?> RequestCompleted =
        LoggerMessage.Define<string, string, string, int, long, string>(
            LogLevel.Information,
            new EventId(2001, nameof(RequestCompleted)),
            "HTTP request completed {RequestMethod} {RequestTarget} with correlation {CorrelationId}, status {StatusCode}, elapsed {ElapsedMilliseconds} ms, user {UserIdentifier}.");

    private static readonly Action<ILogger, string, string, string, long, string, Exception?> RequestFailed =
        LoggerMessage.Define<string, string, string, long, string>(
            LogLevel.Error,
            new EventId(2002, nameof(RequestFailed)),
            "HTTP request failed {RequestMethod} {RequestTarget} with correlation {CorrelationId} after {ElapsedMilliseconds} ms for {UserIdentifier}.");

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.Response.Headers[CorrelationHeaderName] = correlationId;
        var startedAt = Stopwatch.GetTimestamp();
        var userIdentifier = GetUserIdentifier(context);
        var requestTarget = BuildRequestTarget(context.Request);

        using var scope = logger.BeginScope(
            new Dictionary<string, object?>
            {
                ["correlationId"] = correlationId,
                ["componentName"] = ComponentName,
                ["eventName"] = "HttpRequest",
                ["requestMethod"] = context.Request.Method,
                ["requestTarget"] = requestTarget,
                ["userIdentifier"] = userIdentifier,
            });

        try
        {
            await next(context).ConfigureAwait(false);

            RequestCompleted(
                logger,
                context.Request.Method,
                requestTarget,
                correlationId,
                context.Response.StatusCode,
                GetElapsedMilliseconds(startedAt),
                userIdentifier,
                null);
        }
        catch (Exception ex)
        {
            RequestFailed(
                logger,
                context.Request.Method,
                requestTarget,
                correlationId,
                GetElapsedMilliseconds(startedAt),
                userIdentifier,
                ex);

            throw;
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var headerValue = context.Request.Headers[CorrelationHeaderName].ToString();
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return Guid.NewGuid().ToString("N");
        }

        var trimmedHeader = headerValue.Trim();
        return trimmedHeader.Length <= MaxCorrelationIdLength && SafeCorrelationIdPattern.IsMatch(trimmedHeader)
            ? trimmedHeader
            : Guid.NewGuid().ToString("N");
    }

    private static string BuildRequestTarget(HttpRequest request)
    {
        if (!request.QueryString.HasValue)
        {
            return request.Path.ToString();
        }

        var sanitizedQuery = string.Join(
            "&",
            request.Query.Select(
                pair => IsSensitiveQueryKey(pair.Key)
                    ? $"{Uri.EscapeDataString(pair.Key)}=[redacted]"
                    : $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value.ToString())}"));

        return $"{request.Path}?{sanitizedQuery}";
    }

    private static bool IsSensitiveQueryKey(string key) =>
        SensitiveQueryKeyFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static long GetElapsedMilliseconds(long startedAt) =>
        (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

    private static string GetUserIdentifier(HttpContext context) =>
        context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? context.User.Identity?.Name
        ?? "anonymous";
}
