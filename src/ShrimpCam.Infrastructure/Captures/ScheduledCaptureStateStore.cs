using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Captures;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Captures;

internal sealed class ScheduledCaptureStateStore(IFileSystem fileSystem) : IScheduledCaptureStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public Task<ScheduledCaptureState> LoadAsync(
        StorageOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateOptions(options);

        var statePath = GetStatePath(options);
        if (!fileSystem.FileExists(statePath))
        {
            return Task.FromResult(ScheduledCaptureState.Empty);
        }

        var payload = fileSystem.ReadAllText(statePath);
        var state = JsonSerializer.Deserialize<ScheduledCaptureState>(payload, SerializerOptions);

        return Task.FromResult(state ?? ScheduledCaptureState.Empty);
    }

    public Task SaveAsync(
        StorageOptions options,
        ScheduledCaptureState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(state);
        ValidateOptions(options);

        var statePath = GetStatePath(options);
        var stateDirectory = Path.GetDirectoryName(statePath)
            ?? throw new InvalidOperationException("Scheduled capture state path did not include a parent directory.");

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
        return fileSystem.Combine(rootPath, ".shrimpcam", "scheduled-capture-state.json");
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
