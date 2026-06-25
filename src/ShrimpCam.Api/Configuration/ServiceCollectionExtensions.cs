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
                options => IsInitialAdministratorValid(options.Security.InitialAdministrator),
                "Initial administrator credentials must include a user name and a password with at least 12 characters, uppercase, lowercase, and numeric characters.")
            .Validate(
                _ => IsAllowedHostsSafeForInternetExposure(configuration),
                "Internet-exposed deployments must set AllowedHosts to explicit host names instead of '*'.")
            .ValidateOnStart();

        return services;
    }

    private static bool IsInitialAdministratorValid(InitialAdministratorOptions options)
    {
        if (!options.Enabled)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(options.UserName)
            || string.IsNullOrWhiteSpace(options.Password)
            || options.Password.Length < 12)
        {
            return false;
        }

        var hasUpper = false;
        var hasLower = false;
        var hasDigit = false;

        foreach (var character in options.Password)
        {
            hasUpper |= char.IsUpper(character);
            hasLower |= char.IsLower(character);
            hasDigit |= char.IsDigit(character);
        }

        return hasUpper && hasLower && hasDigit;
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
