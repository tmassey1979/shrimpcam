using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ShrimpCam.Core.Audit;
using ShrimpCam.Core.Authentication;

namespace ShrimpCam.Api.Authentication;

internal sealed class BearerSessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ISessionAuthenticationService sessionAuthenticationService,
    IAuditEventService auditEventService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "SessionBearer";
    public const string SessionCookieName = "shrimpcam-session";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = GetBearerToken() ?? GetSessionCookieToken() ?? GetLiveStreamQueryToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return AuthenticateResult.NoResult();
        }

        var authenticationResult = await sessionAuthenticationService.AuthenticateAsync(token, Context.RequestAborted).ConfigureAwait(false);
        if (!authenticationResult.Succeeded)
        {
            return AuthenticateResult.Fail(authenticationResult.FailureReason ?? "Invalid session.");
        }

        var identity = authenticationResult.Identity!;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, identity.UserId.ToString()),
            new(ClaimTypes.Name, identity.UserName),
            new("session_id", identity.SessionId.ToString()),
        };

        claims.AddRange(identity.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return AuthenticateResult.Success(ticket);
    }

    private string? GetBearerToken()
    {
        var authorizationHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorizationHeader) ||
            !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return authorizationHeader["Bearer ".Length..].Trim();
    }

    private string? GetSessionCookieToken() =>
        Request.Cookies.TryGetValue(SessionCookieName, out var token)
            ? token
            : null;

    private string? GetLiveStreamQueryToken()
    {
        if (!HttpMethods.IsGet(Request.Method) ||
            !Request.Path.Equals("/stream/live", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Request.Query.TryGetValue("access_token", out var token)
            ? token.ToString()
            : null;
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        await auditEventService.RecordAsync(
                new AuditEventRequest(
                    AuditEventTypes.AuthorizationDenied,
                    Context.User.Identity?.Name ?? "anonymous",
                    AuditOutcomes.Denied,
                    new Dictionary<string, string>
                    {
                        ["method"] = Request.Method,
                        ["path"] = Request.Path,
                        ["statusCode"] = StatusCodes.Status401Unauthorized.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    }),
                Context.RequestAborted)
            .ConfigureAwait(false);

        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/problem+json";

        await Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Title = "Authentication required.",
                Detail = "A valid session token is required to access this endpoint.",
                Status = StatusCodes.Status401Unauthorized,
            }).ConfigureAwait(false);
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        await auditEventService.RecordAsync(
                new AuditEventRequest(
                    AuditEventTypes.AuthorizationDenied,
                    Context.User.Identity?.Name ?? "anonymous",
                    AuditOutcomes.Denied,
                    new Dictionary<string, string>
                    {
                        ["method"] = Request.Method,
                        ["path"] = Request.Path,
                        ["statusCode"] = StatusCodes.Status403Forbidden.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    }),
                Context.RequestAborted)
            .ConfigureAwait(false);

        Response.StatusCode = StatusCodes.Status403Forbidden;
        Response.ContentType = "application/problem+json";

        await Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Title = "Forbidden.",
                Detail = "The authenticated user does not have permission to access this endpoint.",
                Status = StatusCodes.Status403Forbidden,
            }).ConfigureAwait(false);
    }
}
