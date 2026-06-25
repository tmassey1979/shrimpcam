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
            .ValidateOnStart();

        return services;
    }
}
