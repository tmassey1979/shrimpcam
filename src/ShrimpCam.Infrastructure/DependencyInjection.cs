using Microsoft.Extensions.DependencyInjection;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Audit;
using ShrimpCam.Core.Authentication;
using ShrimpCam.Core.Backups;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Captures;
using ShrimpCam.Core.Diagnostics;
using ShrimpCam.Core.Health;
using ShrimpCam.Core.Persistence;
using ShrimpCam.Core.Settings;
using ShrimpCam.Infrastructure.Backups;
using ShrimpCam.Infrastructure.Cameras;
using ShrimpCam.Infrastructure.Cameras.Linux;
using ShrimpCam.Infrastructure.Cameras.Windows;
using ShrimpCam.Infrastructure.Captures;
using ShrimpCam.Infrastructure.Diagnostics;
using ShrimpCam.Infrastructure.Health;
using ShrimpCam.Infrastructure.IO;
using ShrimpCam.Infrastructure.Persistence;
using ShrimpCam.Infrastructure.Processes;
using ShrimpCam.Infrastructure.Time;

namespace ShrimpCam.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IAsyncDelay, SystemAsyncDelay>();
        services.AddSingleton<IAuditEventService, AuditEventService>();
        services.AddSingleton<IFileSystem, SystemFileSystem>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IAuthenticationService, LocalAuthenticationService>();
        services.AddSingleton<IBackupExportService, BackupExportService>();
        services.AddSingleton<IBackupRestoreService, BackupRestoreService>();
        services.AddSingleton<IBackupStorageCapacityProbe, DriveInfoBackupStorageCapacityProbe>();
        services.AddSingleton<IDiagnosticsBundleService, DiagnosticsBundleService>();
        services.AddSingleton<IApplicationHealthService, ApplicationHealthService>();
        services.AddSingleton<IDatabaseHealthProbe, SqliteDatabaseHealthProbe>();
        services.AddSingleton<IEditableSettingsService>(
            provider => new EditableSettingsService(
                provider.GetRequiredService<ISettingsRepository>(),
                provider.GetRequiredService<IClock>(),
                provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ShrimpCam.Core.Configuration.ShrimpCamOptions>>().Value));
        services.AddSingleton<ISessionAuthenticationService, SessionAuthenticationService>();
        services.AddSingleton<ISessionRevocationService, SessionRevocationService>();
        services.AddSingleton<IStorageHealthProbe, StorageHealthProbe>();
        services.AddSingleton<IPasswordPolicy, DefaultPasswordPolicy>();
        services.AddSingleton<IBootstrapAdministratorService, BootstrapAdministratorService>();
        services.AddSingleton<IApplicationDataInitializer, SqliteApplicationDataInitializer>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IProcessStreamRunner, ProcessStreamRunner>();
        services.AddSingleton<IAuditRecordRepository, SqliteAuditRecordRepository>();
        services.AddSingleton<ICameraCommandFactory, CameraCommandFactory>();
        services.AddSingleton<ICameraFrameSourceSelector, CameraFrameSourceSelector>();
        services.AddSingleton<ICameraFrameSourceProvider, LinuxV4l2FfmpegFrameSourceProvider>();
        services.AddSingleton<ICameraFrameSourceProvider, WindowsFfmpegFallbackFrameSourceProvider>();
        services.AddSingleton<ICameraFrameSourceProvider, WindowsMediaFoundationFrameSourceProvider>();
        services.AddSingleton<ICameraFrameSourceProviderRegistry, CameraFrameSourceProviderRegistry>();
        services.AddSingleton<ICameraResourceCoordinator, InMemoryCameraResourceCoordinator>();
        services.AddSingleton<ILiveFrameSnapshotStore, LiveFrameSnapshotStore>();
        services.AddSingleton<ISharedCameraStreamHub, SharedCameraStreamHub>();
        services.AddSingleton<ICameraStartupProbe, CameraStartupProbe>();
        services.AddSingleton<ICameraStatusService, CameraStatusService>();
        services.AddSingleton<ICameraLiveStreamService, CameraLiveStreamService>();
        services.AddSingleton<ICaptureRecordRepository, SqliteCaptureRecordRepository>();
        services.AddSingleton<ICaptureStorage, FileSystemCaptureStorage>();
        services.AddSingleton<ICaptureRetentionService, CaptureRetentionService>();
        services.AddSingleton<IDailyTimelapseService, DailyTimelapseService>();
        services.AddSingleton<IMotionHighlightStateStore, MotionHighlightStateStore>();
        services.AddSingleton<IManualCaptureService>(
            provider => new ManualCaptureService(
                provider.GetRequiredService<ICameraCommandFactory>(),
                provider.GetRequiredService<ICameraResourceCoordinator>(),
                provider.GetRequiredService<ICameraStatusService>(),
                provider.GetRequiredService<ICaptureRecordRepository>(),
                provider.GetRequiredService<ICaptureStorage>(),
                provider.GetRequiredService<IClock>(),
                provider.GetRequiredService<IFileSystem>(),
                provider.GetRequiredService<IProcessRunner>(),
                provider.GetRequiredService<ILiveFrameSnapshotStore>()));
        services.AddSingleton<IMotionHighlightService, MotionHighlightService>();
        services.AddSingleton<ISettingsRepository, SqliteSettingsRepository>();
        services.AddSingleton<ISessionRepository, SqliteSessionRepository>();
        services.AddSingleton<IScheduledCaptureStateStore, ScheduledCaptureStateStore>();
        services.AddSingleton<IScheduledCaptureService>(
            provider => new ScheduledCaptureService(
                provider.GetRequiredService<IAsyncDelay>(),
                provider.GetRequiredService<ICameraCommandFactory>(),
                provider.GetRequiredService<ICameraResourceCoordinator>(),
                provider.GetRequiredService<ICameraStatusService>(),
                provider.GetRequiredService<ICaptureRecordRepository>(),
                provider.GetRequiredService<ICaptureStorage>(),
                provider.GetRequiredService<IClock>(),
                provider.GetRequiredService<IFileSystem>(),
                provider.GetRequiredService<IProcessRunner>(),
                provider.GetRequiredService<IScheduledCaptureStateStore>(),
                provider.GetRequiredService<ILiveFrameSnapshotStore>()));
        services.AddSingleton<ILinuxCameraDiscovery, LinuxCameraDiscovery>();
        services.AddSingleton<IUserRepository, SqliteUserRepository>();
        services.AddSingleton<IUserRoleRepository, SqliteUserRoleRepository>();
        services.AddSingleton<IWindowsCameraDiscovery, WindowsCameraDiscovery>();
        services.AddHostedService<CameraFrameEvaluationWorker>();
        services.AddHostedService<ScheduledCaptureWorker>();

        return services;
    }
}
