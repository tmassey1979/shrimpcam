using Microsoft.Extensions.Options;
using ShrimpCam.Api.Configuration;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddShrimpCamConfiguration(builder.Configuration);
builder.Services.AddInfrastructure();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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
        }));

app.Run();

public partial class Program;
