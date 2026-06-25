using Microsoft.Extensions.DependencyInjection;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Captures;
using ShrimpCam.Infrastructure.Cameras;
using ShrimpCam.Infrastructure.Cameras.Linux;
using ShrimpCam.Infrastructure.Cameras.Windows;
using ShrimpCam.Infrastructure.Captures;
using ShrimpCam.Infrastructure.IO;
using ShrimpCam.Infrastructure.Processes;
using ShrimpCam.Infrastructure.Time;

namespace ShrimpCam.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IFileSystem, SystemFileSystem>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IProcessStreamRunner, ProcessStreamRunner>();
        services.AddSingleton<ICameraCommandFactory, CameraCommandFactory>();
        services.AddSingleton<ICameraLiveStreamService, CameraLiveStreamService>();
        services.AddSingleton<ICaptureStorage, FileSystemCaptureStorage>();
        services.AddSingleton<IManualCaptureService, ManualCaptureService>();
        services.AddSingleton<IScheduledCaptureStateStore, ScheduledCaptureStateStore>();
        services.AddSingleton<IScheduledCaptureService, ScheduledCaptureService>();
        services.AddSingleton<ILinuxCameraDiscovery, LinuxCameraDiscovery>();
        services.AddSingleton<IWindowsCameraDiscovery, WindowsCameraDiscovery>();
        services.AddHostedService<ScheduledCaptureWorker>();

        return services;
    }
}
