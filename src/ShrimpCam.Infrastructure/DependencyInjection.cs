using Microsoft.Extensions.DependencyInjection;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Infrastructure.Cameras.Linux;
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
        services.AddSingleton<ILinuxCameraDiscovery, LinuxCameraDiscovery>();

        return services;
    }
}
