using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Captures;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Captures;

internal sealed class MotionHighlightStateStore(IFileSystem fileSystem) : IMotionHighlightStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public Task<MotionHighlightState> LoadAsync(
        StorageOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateOptions(options);

        var statePath = GetStatePath(options);
        if (!fileSystem.FileExists(statePath))
        {
            return Task.FromResult(MotionHighlightState.Empty);
        }

        var payload = fileSystem.ReadAllText(statePath);
        var state = JsonSerializer.Deserialize<MotionHighlightState>(payload, SerializerOptions);

        return Task.FromResult(state ?? MotionHighlightState.Empty);
    }

    public Task SaveAsync(
        StorageOptions options,
        MotionHighlightState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(state);
        ValidateOptions(options);

        var statePath = GetStatePath(options);
        var stateDirectory = Path.GetDirectoryName(statePath)
            ?? throw new InvalidOperationException("Motion highlight state path did not include a parent directory.");

        if (!fileSystem.DirectoryExists(stateDirectory))
        {
            fileSystem.CreateDirectory(stateDirectory);
        }

        fileSystem.WriteAllText(
            statePath,
            JsonSerializer.Serialize(state, SerializerOptions));

        return Task.CompletedTask;
    }

    internal string GetStatePath(StorageOptions options)
    {
        var rootPath = Path.GetFullPath(options.ImageRootPath);
        return fileSystem.Combine(rootPath, ".shrimpcam", "motion-highlight-state.json");
    }

    private static void ValidateOptions(StorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ImageRootPath))
        {
            throw new ValidationException("Storage image root path is required.");
        }
    }
}
