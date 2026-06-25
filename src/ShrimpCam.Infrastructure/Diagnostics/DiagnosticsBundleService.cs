using Microsoft.Extensions.Options;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Diagnostics;
using ShrimpCam.Core.Health;
using ShrimpCam.Core.Persistence;

namespace ShrimpCam.Infrastructure.Diagnostics;

internal sealed class DiagnosticsBundleService(
    IClock clock,
    IOptions<ShrimpCamOptions> options,
    IApplicationHealthService applicationHealthService,
    IAuditRecordRepository auditRecordRepository) : IDiagnosticsBundleService
{
    private const int RecentAuditEventLimit = 25;

    public async Task<DiagnosticsBundle> GenerateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var health = await applicationHealthService.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        var recentAuditEvents = await auditRecordRepository
            .ListAsync(new AuditRecordQuery(1, RecentAuditEventLimit), cancellationToken)
            .ConfigureAwait(false);

        return new DiagnosticsBundle(
            clock.UtcNow,
            health,
            CreateConfigurationSnapshot(options.Value),
            recentAuditEvents.Items);
    }

    private static Dictionary<string, object?> CreateConfigurationSnapshot(ShrimpCamOptions source) =>
        new Dictionary<string, object?>
        {
            ["camera"] = new Dictionary<string, object?>
            {
                ["platform"] = source.Camera.Platform,
                ["source"] = source.Camera.Source,
                ["captureWidth"] = source.Camera.CaptureWidth,
                ["captureHeight"] = source.Camera.CaptureHeight,
                ["streamWidth"] = source.Camera.StreamWidth,
                ["streamHeight"] = source.Camera.StreamHeight,
                ["streamFramesPerSecond"] = source.Camera.StreamFramesPerSecond,
                ["reconnectRetryAttempts"] = source.Camera.ReconnectRetryAttempts,
                ["reconnectBackoffSeconds"] = source.Camera.ReconnectBackoffSeconds,
            },
            ["capture"] = new Dictionary<string, object?>
            {
                ["intervalMinutes"] = source.Capture.IntervalMinutes,
                ["activeStartHourUtc"] = source.Capture.ActiveStartHourUtc,
                ["activeEndHourUtc"] = source.Capture.ActiveEndHourUtc,
                ["motionHighlightsEnabled"] = source.Capture.MotionHighlightsEnabled,
                ["motionThreshold"] = source.Capture.MotionThreshold,
                ["motionCooldownSeconds"] = source.Capture.MotionCooldownSeconds,
            },
            ["storage"] = new Dictionary<string, object?>
            {
                ["databasePath"] = source.Storage.DatabasePath,
                ["imageRootPath"] = source.Storage.ImageRootPath,
                ["timelapseRootPath"] = source.Storage.TimelapseRootPath,
                ["retentionDays"] = source.Storage.RetentionDays,
            },
            ["security"] = new Dictionary<string, object?>
            {
                ["hostMode"] = source.Security.HostMode,
                ["secrets"] = "[redacted]",
            },
        };
}
