using Microsoft.Extensions.Options;
using ShrimpCam.Api.Build;
using ShrimpCam.Api.Configuration;
using ShrimpCam.Core.Authentication;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Captures;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Persistence;
using ShrimpCam.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddShrimpCamConfiguration(builder.Configuration);
builder.Services.AddInfrastructure();
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

app.MapGet(
    "/health",
    (IOptions<ShrimpCamOptions> optionsAccessor) => Results.Ok(
        new
        {
            status = "ok",
            cameraPlatform = optionsAccessor.Value.Camera.Platform,
            captureIntervalMinutes = optionsAccessor.Value.Capture.IntervalMinutes,
            hostMode = optionsAccessor.Value.Security.HostMode,
            applicationVersion = buildMetadata.Version,
            informationalVersion = buildMetadata.InformationalVersion,
            sourceRevision = buildMetadata.SourceRevision,
            buildConfiguration = buildMetadata.BuildConfiguration,
        }));

app.MapPost(
    "/auth/login",
    async (LoginRequest request, IAuthenticationService authenticationService, CancellationToken cancellationToken) =>
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

internal sealed record LoginRequest(
    string UserName,
    string Password);

public partial class Program;
