using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using ShrimpCam.Api.Authentication;
using ShrimpCam.Api.Build;
using ShrimpCam.Api.Configuration;
using ShrimpCam.Api.Logging;
using ShrimpCam.Core.Audit;
using ShrimpCam.Core.Authentication;
using ShrimpCam.Core.Backups;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Captures;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Diagnostics;
using ShrimpCam.Core.Health;
using ShrimpCam.Core.Persistence;
using ShrimpCam.Core.Settings;
using ShrimpCam.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSystemd();
builder.Host.UseWindowsService(
    options =>
    {
        options.ServiceName = "ShrimpCam";
    });

builder.Services.AddShrimpCamConfiguration(builder.Configuration);
builder.Services.AddInfrastructure();
builder.Services.AddAuthentication(BearerSessionAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, BearerSessionAuthenticationHandler>(
        BearerSessionAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization(AuthorizationPolicies.Configure);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<ForwardedHeadersOptions>(
    options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    });
builder.Services.AddRateLimiter(
    options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy(
            "authentication",
            httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    QueueLimit = 0,
                    Window = TimeSpan.FromMinutes(1),
                }));
    });

var app = builder.Build();
var buildMetadata = BuildMetadata.FromAssembly(typeof(Program).Assembly);

var options = app.Services.GetRequiredService<IOptions<ShrimpCamOptions>>().Value;
await app.Services.GetRequiredService<IApplicationDataInitializer>()
    .InitializeAsync(options.Storage, CancellationToken.None)
    .ConfigureAwait(false);
await app.Services.GetRequiredService<ICameraStartupProbe>()
    .CheckAsync(CancellationToken.None)
    .ConfigureAwait(false);

app.UseSwagger();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI();
}

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.Use(
    async (context, next) =>
    {
        context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
        context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
        context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
        context.Response.Headers.TryAdd("Content-Security-Policy", "default-src 'self'; img-src 'self' data: blob:; media-src 'self' blob:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'");
        await next(context).ConfigureAwait(false);
    });

app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<StructuredRequestLoggingMiddleware>();
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

app.MapGet(
    "/diagnostics/bundle",
    async (IDiagnosticsBundleService diagnosticsBundleService, CancellationToken cancellationToken) =>
    {
        var bundle = await diagnosticsBundleService.GenerateAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(
            new
            {
                generatedAtUtc = bundle.GeneratedAtUtc,
                applicationVersion = buildMetadata.Version,
                informationalVersion = buildMetadata.InformationalVersion,
                sourceRevision = buildMetadata.SourceRevision,
                buildConfiguration = buildMetadata.BuildConfiguration,
                health = bundle.Health,
                configuration = bundle.Configuration,
                recentAuditEvents = bundle.RecentAuditEvents,
            });
    })
    .RequireAuthorization(AuthorizationPolicies.Administrator);

app.MapPost(
    "/backups/export",
    async (IBackupExportService backupExportService, CancellationToken cancellationToken) =>
    {
        var result = await backupExportService.ExportAsync(cancellationToken).ConfigureAwait(false);
        if (result.Succeeded)
        {
            return Results.Ok(
                new
                {
                    status = "exported",
                    archivePath = result.ArchivePath,
                    fileName = result.FileName,
                    archiveSizeBytes = result.ArchiveSizeBytes,
                    startedAtUtc = result.StartedAtUtc,
                    completedAtUtc = result.CompletedAtUtc,
                });
        }

        return result.FailureReason switch
        {
            BackupExportFailureReasons.ExportAlreadyRunning => Results.Conflict(
                new
                {
                    status = "failed",
                    reason = result.FailureReason,
                }),
            BackupExportFailureReasons.InsufficientStorage => Results.Json(
                new
                {
                    status = "failed",
                    reason = result.FailureReason,
                },
                statusCode: StatusCodes.Status507InsufficientStorage),
            _ => Results.Json(
                new
                {
                    status = "failed",
                    reason = result.FailureReason ?? BackupExportFailureReasons.StorageUnavailable,
                },
                statusCode: StatusCodes.Status503ServiceUnavailable),
        };
    })
    .RequireAuthorization(AuthorizationPolicies.Administrator);

app.MapPost(
    "/backups/restore",
    async (
        BackupRestoreHttpRequest request,
        ClaimsPrincipal user,
        IBackupRestoreService backupRestoreService,
        IAuditEventService auditEventService,
        CancellationToken cancellationToken) =>
    {
        var result = await backupRestoreService
            .RestoreAsync(new BackupRestoreRequest(request.ArchivePath), cancellationToken)
            .ConfigureAwait(false);
        var actorUserName = user.Identity?.Name ?? "unknown";

        await auditEventService.RecordAsync(
                new AuditEventRequest(
                    AuditEventTypes.BackupRestored,
                    actorUserName,
                    result.Succeeded ? AuditOutcomes.Succeeded : AuditOutcomes.Failed,
                    new Dictionary<string, string>
                    {
                        ["archivePath"] = request.ArchivePath,
                        ["reason"] = result.FailureReason ?? string.Empty,
                    }),
                cancellationToken)
            .ConfigureAwait(false);

        if (result.Succeeded)
        {
            return Results.Ok(
                new
                {
                    status = "restored",
                    startedAtUtc = result.StartedAtUtc,
                    completedAtUtc = result.CompletedAtUtc,
                });
        }

        return result.FailureReason switch
        {
            BackupRestoreFailureReasons.InvalidBackupPackage or BackupRestoreFailureReasons.UnsupportedSchemaVersion => Results.BadRequest(
                new
                {
                    status = "failed",
                    reason = result.FailureReason,
                }),
            _ => Results.Json(
                new
                {
                    status = "failed",
                    reason = result.FailureReason ?? BackupRestoreFailureReasons.RestoreFailed,
                },
                statusCode: StatusCodes.Status503ServiceUnavailable),
        };
    })
    .RequireAuthorization(AuthorizationPolicies.Administrator);

app.MapPost(
    "/auth/bootstrap-admin",
    async (
        BootstrapAdministratorHttpRequest request,
        IBootstrapAdministratorService bootstrapAdministratorService,
        IAuditEventService auditEventService,
        CancellationToken cancellationToken) =>
    {
        var result = await bootstrapAdministratorService.BootstrapAsync(
                new BootstrapAdministratorRequest(request.UserName, request.Password),
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            await auditEventService.RecordAsync(
                    new AuditEventRequest(
                        AuditEventTypes.BootstrapAdministrator,
                        request.UserName,
                        AuditOutcomes.Failed,
                        new Dictionary<string, string>
                        {
                            ["reason"] = result.FailureReason ?? "unknown",
                            ["password"] = request.Password,
                        }),
                    cancellationToken)
                .ConfigureAwait(false);

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

        await auditEventService.RecordAsync(
                new AuditEventRequest(
                    AuditEventTypes.BootstrapAdministrator,
                    result.User!.UserName,
                    AuditOutcomes.Succeeded,
                    new Dictionary<string, string>
                    {
                        ["userName"] = result.User.UserName,
                        ["roleName"] = result.User.RoleName,
                    }),
                cancellationToken)
            .ConfigureAwait(false);

        return Results.Created(
            "/auth/bootstrap-admin",
            new
            {
                status = "bootstrapped",
                userId = result.User.UserId,
                userName = result.User.UserName,
                roleName = result.User.RoleName,
            });
    })
    .WithName("BootstrapAdmin");

app.MapPost(
    "/auth/login",
    async (
        LoginRequest request,
        ShrimpCam.Core.Authentication.IAuthenticationService authenticationService,
        IAuditEventService auditEventService,
        CancellationToken cancellationToken) =>
    {
        var result = await authenticationService.AuthenticateAsync(
                new AuthenticationRequest(request.UserName, request.Password),
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            await auditEventService.RecordAsync(
                    new AuditEventRequest(
                        AuditEventTypes.SignIn,
                        request.UserName,
                        AuditOutcomes.Failed,
                        new Dictionary<string, string>
                        {
                            ["reason"] = result.FailureReason ?? "unknown",
                            ["password"] = request.Password,
                        }),
                    cancellationToken)
                .ConfigureAwait(false);

            return Results.Problem(
                title: "Authentication failed.",
                detail: "Invalid username or password.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        await auditEventService.RecordAsync(
                new AuditEventRequest(
                    AuditEventTypes.SignIn,
                    result.Session!.UserName,
                    AuditOutcomes.Succeeded,
                    new Dictionary<string, string>
                    {
                        ["sessionId"] = result.Session.SessionId.ToString(),
                        ["token"] = result.Session.Token,
                    }),
                cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(
            new
            {
                status = "authenticated",
                sessionId = result.Session.SessionId,
                userId = result.Session.UserId,
                userName = result.Session.UserName,
                token = result.Session.Token,
                expiresAtUtc = result.Session.ExpiresAtUtc,
            });
    })
    .WithName("Login")
    .RequireRateLimiting("authentication");

app.MapPost(
        "/auth/logout",
        [Authorize] async (
            HttpContext httpContext,
            ISessionRevocationService sessionRevocationService,
            IAuditEventService auditEventService,
            CancellationToken cancellationToken) =>
        {
            var actor = httpContext.User.Identity?.Name ?? "anonymous";
            var sessionIdValue = httpContext.User.FindFirst("session_id")?.Value;
            if (!Guid.TryParse(sessionIdValue, out var sessionId))
            {
                await auditEventService.RecordAsync(
                        new AuditEventRequest(
                            AuditEventTypes.SignOut,
                            actor,
                            AuditOutcomes.Failed,
                            new Dictionary<string, string>
                            {
                                ["reason"] = "invalidSessionClaim",
                            }),
                        cancellationToken)
                    .ConfigureAwait(false);

                return Results.Problem(
                    title: "Authentication required.",
                    detail: "A valid session token is required to access this endpoint.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var result = await sessionRevocationService.RevokeAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                await auditEventService.RecordAsync(
                        new AuditEventRequest(
                            AuditEventTypes.SignOut,
                            actor,
                            AuditOutcomes.Failed,
                            new Dictionary<string, string>
                            {
                                ["sessionId"] = sessionId.ToString(),
                                ["reason"] = result.FailureReason ?? "unknown",
                            }),
                        cancellationToken)
                    .ConfigureAwait(false);

                return Results.Problem(
                    title: "Session not found.",
                    detail: "The active session could not be located for logout.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            await auditEventService.RecordAsync(
                    new AuditEventRequest(
                        AuditEventTypes.SignOut,
                        actor,
                        AuditOutcomes.Succeeded,
                        new Dictionary<string, string>
                        {
                            ["sessionId"] = result.RevokedSession!.Id.ToString(),
                        }),
                    cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(
                new
                {
                    status = "signedOut",
                    sessionId = result.RevokedSession.Id,
                    revokedAtUtc = result.RevokedSession.RevokedAtUtc,
                });
        })
    .WithName("Logout");

app.MapGet(
        "/settings",
        [Authorize(Policy = AuthorizationPolicies.Administrator)] async (IEditableSettingsService settingsService, CancellationToken cancellationToken) =>
        {
            var settings = await settingsService.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(SettingsEndpointMapping.ToSettingsResponse(settings));
        })
    .WithName("GetSettings");

app.MapPut(
        "/settings",
        [Authorize(Policy = AuthorizationPolicies.Administrator)] async (
            HttpContext httpContext,
            UpdateSettingsHttpRequest request,
            IEditableSettingsService settingsService,
            IAuditEventService auditEventService,
            CancellationToken cancellationToken) =>
        {
            var settings = request.ToEditableSettings();
            var validation = settingsService.Validate(settings);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(
                    validation.Errors.ToDictionary(
                        error => error.Key,
                        error => error.Value,
                        StringComparer.OrdinalIgnoreCase));
            }

            var updated = await settingsService.UpdateAsync(settings, cancellationToken).ConfigureAwait(false);
            await auditEventService.RecordAsync(
                    new AuditEventRequest(
                        AuditEventTypes.SettingsUpdated,
                        httpContext.User.Identity?.Name ?? "anonymous",
                        AuditOutcomes.Succeeded,
                        new Dictionary<string, string>
                        {
                            ["cameraSource"] = updated.Camera.Source,
                            ["captureIntervalMinutes"] = updated.Capture.IntervalMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ["retentionDays"] = updated.Storage.RetentionDays.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ["hostMode"] = updated.Security.HostMode,
                        }),
                    cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(SettingsEndpointMapping.ToSettingsResponse(updated));
        })
    .WithName("UpdateSettings");

app.MapGet(
        "/audit/events",
        [Authorize(Policy = AuthorizationPolicies.Administrator)] async (
            int? page,
            int? pageSize,
            IAuditRecordRepository auditRecordRepository,
            CancellationToken cancellationToken) =>
        {
            var validation = AuditHistoryEndpointMapping.ValidateQuery(page, pageSize);
            if (validation.Count > 0)
            {
                return Results.ValidationProblem(validation);
            }

            var query = new AuditRecordQuery(page ?? 1, pageSize ?? 25);
            var auditPage = await auditRecordRepository.ListAsync(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(AuditHistoryEndpointMapping.ToListResponse(auditPage));
        })
    .WithName("ListAuditEvents");

app.MapGet(
        "/captures",
        [Authorize(Policy = AuthorizationPolicies.Viewer)] async (
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? page,
            int? pageSize,
            ICaptureRecordRepository captureRepository,
            CancellationToken cancellationToken) =>
        {
            var validation = CaptureBrowsingEndpointMapping.ValidateQuery(fromUtc, toUtc, page, pageSize);
            if (validation.Count > 0)
            {
                return Results.ValidationProblem(validation);
            }

            var query = new CaptureRecordQuery(fromUtc, toUtc, page ?? 1, pageSize ?? 25);
            var capturePage = await captureRepository.ListAsync(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(CaptureBrowsingEndpointMapping.ToListResponse(capturePage));
        })
    .WithName("ListCaptures");

app.MapGet(
        "/captures/{id:guid}",
        [Authorize(Policy = AuthorizationPolicies.Viewer)] async (Guid id, ICaptureRecordRepository captureRepository, CancellationToken cancellationToken) =>
        {
            var capture = await captureRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return capture is null
                ? Results.NotFound()
                : Results.Ok(CaptureBrowsingEndpointMapping.ToCaptureResponse(capture));
        })
    .WithName("GetCapture");

app.MapGet(
        "/captures/{id:guid}/image",
        [Authorize(Policy = AuthorizationPolicies.Viewer)] async (
            Guid id,
            ICaptureRecordRepository captureRepository,
            IOptions<ShrimpCamOptions> options,
            CancellationToken cancellationToken) =>
        {
            var capture = await captureRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (capture is null)
            {
                return Results.NotFound();
            }

            var imagePath = CaptureFileEndpointMapping.TryResolveManagedCapturePath(
                options.Value.Storage.ImageRootPath,
                capture.RelativeImagePath);

            return imagePath is null || !File.Exists(imagePath)
                ? Results.NotFound()
                : Results.File(imagePath, CaptureFileEndpointMapping.GetImageContentType(imagePath), capture.FileName);
        })
    .WithName("GetCaptureImage");

app.MapGet(
        "/captures/{id:guid}/metadata",
        [Authorize(Policy = AuthorizationPolicies.Viewer)] async (
            Guid id,
            ICaptureRecordRepository captureRepository,
            IOptions<ShrimpCamOptions> options,
            CancellationToken cancellationToken) =>
        {
            var capture = await captureRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (capture is null)
            {
                return Results.NotFound();
            }

            var metadataPath = CaptureFileEndpointMapping.TryResolveManagedCapturePath(
                options.Value.Storage.ImageRootPath,
                capture.RelativeMetadataPath);

            return metadataPath is null || !File.Exists(metadataPath)
                ? Results.NotFound()
                : Results.File(metadataPath, "application/json", Path.GetFileName(metadataPath));
        })
    .WithName("GetCaptureMetadata");

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

internal sealed record BackupRestoreHttpRequest(string ArchivePath);

internal sealed record BootstrapAdministratorHttpRequest(
    string UserName,
    string Password);

internal sealed record LoginRequest(
    string UserName,
    string Password);

internal sealed record UpdateSettingsHttpRequest(
    CameraSettingsHttpRequest Camera,
    CaptureSettingsHttpRequest Capture,
    StorageSettingsHttpRequest Storage,
    SecuritySettingsHttpRequest Security)
{
    public EditableSettings ToEditableSettings() =>
        new(
            new CameraOptions
            {
                Platform = Camera.Platform,
                Source = Camera.Source,
                CaptureWidth = Camera.CaptureWidth,
                CaptureHeight = Camera.CaptureHeight,
                StreamWidth = Camera.StreamWidth,
                StreamHeight = Camera.StreamHeight,
                StreamFramesPerSecond = Camera.StreamFramesPerSecond,
                ReconnectRetryAttempts = Camera.ReconnectRetryAttempts,
                ReconnectBackoffSeconds = Camera.ReconnectBackoffSeconds,
            },
            new CaptureOptions
            {
                Enabled = Capture.Enabled,
                IntervalMinutes = Capture.IntervalMinutes,
                ActiveStartHourUtc = Capture.ActiveStartHourUtc,
                ActiveEndHourUtc = Capture.ActiveEndHourUtc,
                MotionHighlightsEnabled = Capture.MotionHighlightsEnabled,
                MotionThreshold = Capture.MotionThreshold,
                MotionCooldownSeconds = Capture.MotionCooldownSeconds,
            },
            new StorageEditableSettings(Storage.RetentionDays),
            new SecurityOptions
            {
                HostMode = Security.HostMode,
            });
}

internal sealed record CameraSettingsHttpRequest(
    string Platform,
    string Source,
    int CaptureWidth,
    int CaptureHeight,
    int StreamWidth,
    int StreamHeight,
    int StreamFramesPerSecond,
    int ReconnectRetryAttempts,
    int ReconnectBackoffSeconds);

internal sealed record CaptureSettingsHttpRequest(
    bool Enabled,
    int IntervalMinutes,
    int ActiveStartHourUtc,
    int ActiveEndHourUtc,
    bool MotionHighlightsEnabled,
    double MotionThreshold,
    int MotionCooldownSeconds);

internal sealed record StorageSettingsHttpRequest(int RetentionDays);

internal sealed record SecuritySettingsHttpRequest(string HostMode);

internal static class AuditHistoryEndpointMapping
{
    private const int MaxPageSize = 100;

    public static Dictionary<string, string[]> ValidateQuery(int? page, int? pageSize)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (page is < 1)
        {
            errors["page"] = ["Page must be greater than or equal to 1."];
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            errors["pageSize"] = [$"Page size must be between 1 and {MaxPageSize}."];
        }

        return errors;
    }

    public static object ToListResponse(AuditRecordPage auditPage) =>
        new
        {
            items = auditPage.Items.Select(ToAuditRecordResponse).ToArray(),
            paging = new
            {
                auditPage.PageNumber,
                auditPage.PageSize,
                auditPage.TotalItems,
                auditPage.TotalPages,
                auditPage.HasPreviousPage,
                auditPage.HasNextPage,
            },
        };

    private static object ToAuditRecordResponse(AuditRecord auditRecord) =>
        new
        {
            auditRecord.Id,
            auditRecord.EventType,
            auditRecord.ActorUserName,
            auditRecord.Outcome,
            auditRecord.Detail,
            auditRecord.OccurredAtUtc,
        };
}

internal static class CaptureBrowsingEndpointMapping
{
    private const int MaxPageSize = 100;

    public static Dictionary<string, string[]> ValidateQuery(DateTimeOffset? fromUtc, DateTimeOffset? toUtc, int? page, int? pageSize)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            errors["fromUtc"] = ["fromUtc must be earlier than or equal to toUtc."];
            errors["toUtc"] = ["toUtc must be later than or equal to fromUtc."];
        }

        if (page is < 1)
        {
            errors["page"] = ["Page must be greater than or equal to 1."];
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            errors["pageSize"] = [$"Page size must be between 1 and {MaxPageSize}."];
        }

        return errors;
    }

    public static object ToListResponse(CaptureRecordPage capturePage) =>
        new
        {
            items = capturePage.Items.Select(ToCaptureResponse).ToArray(),
            paging = new
            {
                capturePage.PageNumber,
                capturePage.PageSize,
                capturePage.TotalItems,
                capturePage.TotalPages,
                capturePage.HasPreviousPage,
                capturePage.HasNextPage,
            },
        };

    public static object ToCaptureResponse(CaptureRecord capture) =>
        new
        {
            capture.Id,
            capture.RelativeImagePath,
            capture.RelativeMetadataPath,
            capture.FileName,
            capture.SourceType,
            capture.CapturedAtUtc,
            imageUrl = $"/captures/{capture.Id}/image",
            metadataUrl = $"/captures/{capture.Id}/metadata",
        };
}

internal static class CaptureFileEndpointMapping
{
    public static string? TryResolveManagedCapturePath(string imageRootPath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(imageRootPath) || string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            return null;
        }

        var rootPath = Path.GetFullPath(imageRootPath);
        var candidatePath = Path.GetFullPath(Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        return candidatePath.StartsWith(rootPath + Path.DirectorySeparatorChar, comparison) ? candidatePath : null;
    }

    public static string GetImageContentType(string imagePath) =>
        Path.GetExtension(imagePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg",
        };
}

internal static class SettingsEndpointMapping
{
    public static object ToSettingsResponse(EditableSettings settings) =>
        new
        {
            camera = new
            {
                settings.Camera.Platform,
                settings.Camera.Source,
                settings.Camera.CaptureWidth,
                settings.Camera.CaptureHeight,
                settings.Camera.StreamWidth,
                settings.Camera.StreamHeight,
                settings.Camera.StreamFramesPerSecond,
                settings.Camera.ReconnectRetryAttempts,
                settings.Camera.ReconnectBackoffSeconds,
            },
            capture = new
            {
                settings.Capture.Enabled,
                settings.Capture.IntervalMinutes,
                settings.Capture.ActiveStartHourUtc,
                settings.Capture.ActiveEndHourUtc,
                settings.Capture.MotionHighlightsEnabled,
                settings.Capture.MotionThreshold,
                settings.Capture.MotionCooldownSeconds,
            },
            storage = new
            {
                settings.Storage.RetentionDays,
            },
            security = new
            {
                settings.Security.HostMode,
            },
        };
}

public partial class Program;
