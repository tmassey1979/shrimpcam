using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ShrimpCam.Core.Authentication;

namespace ShrimpCam.Api.Authentication;

internal sealed class BearerSessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ISessionAuthenticationService sessionAuthenticationService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "SessionBearer";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorizationHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorizationHeader) ||
            !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = authorizationHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return AuthenticateResult.Fail("A bearer token is required.");
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

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/problem+json";

        return Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Title = "Authentication required.",
                Detail = "A valid session token is required to access this endpoint.",
                Status = StatusCodes.Status401Unauthorized,
            });
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        Response.ContentType = "application/problem+json";

        return Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Title = "Forbidden.",
                Detail = "The authenticated user does not have permission to access this endpoint.",
                Status = StatusCodes.Status403Forbidden,
            });
    }
}
