using System.ComponentModel.DataAnnotations;
using ShrimpCam.Core.Cameras;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Cameras;

internal sealed class CameraFrameSourceProviderRegistry(
    ICameraFrameSourceSelector selector,
    IEnumerable<ICameraFrameSourceProvider> providers) : ICameraFrameSourceProviderRegistry
{
    private readonly Dictionary<string, ICameraFrameSourceProvider> providersByKind = providers
        .GroupBy(provider => provider.Descriptor.ProviderKind, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    public ICameraFrameSourceProvider GetProvider(CameraOptions options, string hostPlatform)
    {
        var selection = selector.ChooseFrameSource(options, hostPlatform);
        if (providersByKind.TryGetValue(selection.ProviderKind, out var provider))
        {
            return provider;
        }

        throw new ValidationException($"Camera frame provider '{selection.ProviderKind}' is not registered.");
    }

    public IReadOnlyList<CameraFrameSourceProviderDescriptor> ListProviders() =>
        providersByKind.Values
            .Select(provider => provider.Descriptor)
            .OrderBy(provider => provider.Platform, StringComparer.Ordinal)
            .ThenByDescending(provider => provider.IsPrimary)
            .ThenBy(provider => provider.DisplayName, StringComparer.Ordinal)
            .ToArray();
}
