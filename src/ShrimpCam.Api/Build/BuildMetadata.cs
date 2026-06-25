using System.Reflection;

namespace ShrimpCam.Api.Build;

internal sealed record BuildMetadata(
    string Version,
    string InformationalVersion,
    string SourceRevision,
    string BuildConfiguration)
{
    public static BuildMetadata FromAssembly(Assembly assembly)
    {
        var version = assembly.GetName().Version?.ToString() ?? "0.0.0.0";
        var informationalVersion =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? version;

        return new BuildMetadata(
            version,
            informationalVersion,
            ReadMetadata(assembly, "SourceRevision", "local"),
            ReadMetadata(assembly, "BuildConfiguration", "Unknown"));
    }

    private static string ReadMetadata(Assembly assembly, string key, string fallback) =>
        assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.OrdinalIgnoreCase))
            ?.Value
        ?? fallback;
}
