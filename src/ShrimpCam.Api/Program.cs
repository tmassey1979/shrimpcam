using Microsoft.Extensions.Options;
using ShrimpCam.Api.Build;
using ShrimpCam.Api.Configuration;
using ShrimpCam.Core.Captures;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddShrimpCamConfiguration(builder.Configuration);
builder.Services.AddInfrastructure();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
var buildMetadata = BuildMetadata.FromAssembly(typeof(Program).Assembly);

_ = app.Services.GetRequiredService<IOptions<ShrimpCamOptions>>().Value;

app.UseSwagger();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI();
}

app.MapGet(
    "/health",
    (IOptions<ShrimpCamOptions> options) => Results.Ok(
        new
        {
            status = "ok",
            cameraPlatform = options.Value.Camera.Platform,
            captureIntervalMinutes = options.Value.Capture.IntervalMinutes,
            hostMode = options.Value.Security.HostMode,
            applicationVersion = buildMetadata.Version,
            informationalVersion = buildMetadata.InformationalVersion,
            sourceRevision = buildMetadata.SourceRevision,
            buildConfiguration = buildMetadata.BuildConfiguration,
        }));

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

app.Run();

public partial class Program;
