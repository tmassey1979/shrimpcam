using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Api.Configuration;

internal static class ServiceCollectionExtensions
{
    private const string SectionName = "ShrimpCam";

    public static IServiceCollection AddShrimpCamConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<ShrimpCamOptions>()
            .Bind(configuration.GetSection(SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => options.Capture.ActiveEndHourUtc > options.Capture.ActiveStartHourUtc,
                "Capture active end hour must be greater than the start hour.")
            .Validate(
                _ => IsAllowedHostsSafeForInternetExposure(configuration),
                "Internet-exposed deployments must set AllowedHosts to explicit host names instead of '*'.")
            .ValidateOnStart();

        return services;
    }

    private static bool IsAllowedHostsSafeForInternetExposure(IConfiguration configuration)
    {
        var hostMode = configuration.GetSection(SectionName).GetValue<string>("Security:HostMode");
        if (!string.Equals(hostMode, "InternetExposed", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var allowedHosts = configuration.GetValue<string>("AllowedHosts");
        if (string.IsNullOrWhiteSpace(allowedHosts))
        {
            return false;
        }

        return allowedHosts
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(host => host != "*");
    }
}
