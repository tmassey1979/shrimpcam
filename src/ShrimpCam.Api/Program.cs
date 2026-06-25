using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using ShrimpCam.Api.Authentication;
using ShrimpCam.Api.Build;
using ShrimpCam.Api.Configuration;
using ShrimpCam.Core.Authentication;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Captures;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Health;
using ShrimpCam.Core.Persistence;
using ShrimpCam.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddShrimpCamConfiguration(builder.Configuration);
builder.Services.AddInfrastructure();
builder.Services.AddAuthentication(BearerSessionAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, BearerSessionAuthenticationHandler>(
        BearerSessionAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization(AuthorizationPolicies.Configure);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
var buildMetadata = BuildMetadata.FromAssembly(typeof(Program).Assembly);

var options = app.Services.GetRequiredService<IOptions<ShrimpCamOptions>>().Value;
await app.Services.GetRequiredService<IApplicationDataInitializer>()
    .InitializeAsync(options.Storage, CancellationToken.None)
    .ConfigureAwait(false);

app.UseSwagger();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet(
    "/health",
    async (IApplicationHealthService applicationHealthService, CancellationToken cancellationToken) =>
    {
        var report = await applicationHealthService.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        var payload = new
        {
            status = report.Status,
            checkedAtUtc = report.CheckedAtUtc,
            components = report.Components,
            applicationVersion = buildMetadata.Version,
            informationalVersion = buildMetadata.InformationalVersion,
            sourceRevision = buildMetadata.SourceRevision,
            buildConfiguration = buildMetadata.BuildConfiguration,
        };

        return report.Status == HealthStatusLevel.Unhealthy
            ? Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable)
            : Results.Ok(payload);
    });

app.MapPost(
    "/auth/bootstrap-admin",
    async (BootstrapAdministratorHttpRequest request, IBootstrapAdministratorService bootstrapAdministratorService, CancellationToken cancellationToken) =>
    {
        var result = await bootstrapAdministratorService.BootstrapAsync(
                new BootstrapAdministratorRequest(request.UserName, request.Password),
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return result.FailureReason switch
            {
                BootstrapAdministratorFailureReasons.AlreadyConfigured => Results.Problem(
                    title: "Bootstrap is no longer available.",
                    detail: "An administrator account has already been configured.",
                    statusCode: StatusCodes.Status409Conflict),
                BootstrapAdministratorFailureReasons.InvalidUserName => Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["userName"] = ["User name is required."],
                    }),
                BootstrapAdministratorFailureReasons.WeakPassword => Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["password"] = ["Password must be at least 12 characters and include uppercase, lowercase, and numeric characters."],
                    }),
                BootstrapAdministratorFailureReasons.UserNameUnavailable => Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["userName"] = ["That user name is already in use."],
                    }),
                _ => Results.Problem(
                    title: "Bootstrap failed.",
                    detail: "The bootstrap administrator request could not be completed.",
                    statusCode: StatusCodes.Status400BadRequest),
            };
        }

        return Results.Created(
            "/auth/bootstrap-admin",
            new
            {
                status = "bootstrapped",
                userId = result.User!.UserId,
                userName = result.User.UserName,
                roleName = result.User.RoleName,
            });
    })
    .WithName("BootstrapAdmin");

app.MapPost(
    "/auth/login",
    async (LoginRequest request, ShrimpCam.Core.Authentication.IAuthenticationService authenticationService, CancellationToken cancellationToken) =>
    {
        var result = await authenticationService.AuthenticateAsync(
                new AuthenticationRequest(request.UserName, request.Password),
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return Results.Problem(
                title: "Authentication failed.",
                detail: "Invalid username or password.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return Results.Ok(
            new
            {
                status = "authenticated",
                sessionId = result.Session!.SessionId,
                userId = result.Session.UserId,
                userName = result.Session.UserName,
                token = result.Session.Token,
                expiresAtUtc = result.Session.ExpiresAtUtc,
            });
    })
    .WithName("Login");

app.MapPost(
        "/auth/logout",
        [Authorize] async (HttpContext httpContext, ISessionRevocationService sessionRevocationService, CancellationToken cancellationToken) =>
        {
            var sessionIdValue = httpContext.User.FindFirst("session_id")?.Value;
            if (!Guid.TryParse(sessionIdValue, out var sessionId))
            {
                return Results.Problem(
                    title: "Authentication required.",
                    detail: "A valid session token is required to access this endpoint.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var result = await sessionRevocationService.RevokeAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                return Results.Problem(
                    title: "Session not found.",
                    detail: "The active session could not be located for logout.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            return Results.Ok(
                new
                {
                    status = "signedOut",
                    sessionId = result.RevokedSession!.Id,
                    revokedAtUtc = result.RevokedSession.RevokedAtUtc,
                });
        })
    .WithName("Logout");

app.MapGet(
        "/settings",
        [Authorize(Policy = AuthorizationPolicies.Administrator)] (IOptions<ShrimpCamOptions> optionsAccessor) => Results.Ok(
            new
            {
                storage = new
                {
                    retentionDays = optionsAccessor.Value.Storage.RetentionDays,
                },
                security = new
                {
                    hostMode = optionsAccessor.Value.Security.HostMode,
                },
            }))
    .WithName("GetSettings");

app.MapPost(
    "/captures/manual",
    async (IManualCaptureService captureService, IOptions<ShrimpCamOptions> options, CancellationToken cancellationToken) =>
    {
        var result = await captureService.CaptureAsync(options.Value, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return Results.Json(
                new
                {
                    status = "failed",
                    reason = result.FailureReason,
                },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(
            new
            {
                status = "captured",
                sourceType = result.Capture!.SourceType,
                capturedAtUtc = result.Capture.CapturedAtUtc,
                fileName = result.Capture.FileName,
                imagePath = result.Capture.ImagePath,
                relativeImagePath = result.Capture.RelativeImagePath,
                metadataPath = result.Capture.MetadataPath,
            });
    });

app.MapPost(
    "/captures/highlights/motion",
    async (MotionHighlightRequest request, IMotionHighlightService motionHighlightService, IOptions<ShrimpCamOptions> options, CancellationToken cancellationToken) =>
    {
        var result = await motionHighlightService.EvaluateAsync(
                options.Value,
                new MotionHighlightEvent(request.OccurredAtUtc, request.Score, request.EventId),
                cancellationToken)
            .ConfigureAwait(false);

        if (result.Outcome == MotionHighlightOutcome.Failed)
        {
            return Results.Json(
                new
                {
                    status = "failed",
                    outcome = result.Outcome,
                    reason = result.FailureReason,
                },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (result.Capture is null)
        {
            return Results.Ok(
                new
                {
                    status = "skipped",
                    outcome = result.Outcome,
                });
        }

        return Results.Ok(
            new
            {
                status = "captured",
                outcome = result.Outcome,
                sourceType = result.Capture.SourceType,
                capturedAtUtc = result.Capture.CapturedAtUtc,
                fileName = result.Capture.FileName,
                imagePath = result.Capture.ImagePath,
                relativeImagePath = result.Capture.RelativeImagePath,
                metadataPath = result.Capture.MetadataPath,
            });
    });

app.MapGet(
    "/stream/live",
    async (ICameraLiveStreamService liveStreamService, IOptions<ShrimpCamOptions> options, CancellationToken cancellationToken) =>
    {
        var result = await liveStreamService.StartAsync(options.Value.Camera, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return Results.Json(
                new
                {
                    status = "failed",
                    reason = result.FailureReason,
                },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var session = result.Session!;

        return Results.Stream(
            async (outputStream) =>
            {
                try
                {
                    await session.Content.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    await session.DisposeAsync().ConfigureAwait(false);
                }
            },
            session.ContentType);
    });

app.Run();

internal sealed record MotionHighlightRequest(
    DateTimeOffset OccurredAtUtc,
    double Score,
    string? EventId);

internal sealed record BootstrapAdministratorHttpRequest(
    string UserName,
    string Password);

internal sealed record LoginRequest(
    string UserName,
    string Password);

public partial class Program;
