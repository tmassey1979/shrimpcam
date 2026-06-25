using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Captures;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Captures;

internal sealed class FileSystemCaptureStorage(IFileSystem fileSystem) : ICaptureStorage
{
    private static readonly JsonSerializerOptions MetadataSerializerOptions = new() { WriteIndented = true };

    public Task<StoredCapture> StoreAsync(
        StorageOptions options,
        CaptureStorageRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateInputs(options, request);

        var rootPath = Path.GetFullPath(options.ImageRootPath);
        var year = request.CapturedAtUtc.UtcDateTime.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var month = request.CapturedAtUtc.UtcDateTime.ToString("MM", System.Globalization.CultureInfo.InvariantCulture);
        var day = request.CapturedAtUtc.UtcDateTime.ToString("dd", System.Globalization.CultureInfo.InvariantCulture);
        var directoryPath = fileSystem.Combine(rootPath, year, month, day);

        if (!fileSystem.DirectoryExists(directoryPath))
        {
            fileSystem.CreateDirectory(directoryPath);
        }

        var baseFileName = $"{request.CapturedAtUtc.UtcDateTime:yyyyMMddTHHmmssfffZ}_{NormalizeSourceType(request.SourceType)}";
        var fileExtension = NormalizeExtension(request.FileExtension);
        var imagePath = GetNextAvailablePath(directoryPath, baseFileName, fileExtension);
        var metadataPath = Path.ChangeExtension(imagePath, ".json");

        fileSystem.MoveFile(request.StagedFilePath, imagePath);

        try
        {
            var metadata = new
            {
                capturedAtUtc = request.CapturedAtUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                sourceType = request.SourceType,
                fileName = Path.GetFileName(imagePath),
                relativeImagePath = Path.GetRelativePath(rootPath, imagePath).Replace('\\', '/'),
            };

            fileSystem.WriteAllText(
                metadataPath,
                JsonSerializer.Serialize(metadata, MetadataSerializerOptions));
        }
        catch
        {
            if (fileSystem.FileExists(imagePath))
            {
                fileSystem.DeleteFile(imagePath);
            }

            throw;
        }

        return Task.FromResult(
            new StoredCapture(
                imagePath,
                metadataPath,
                Path.GetRelativePath(rootPath, imagePath).Replace('\\', '/'),
                Path.GetFileName(imagePath),
                request.CapturedAtUtc,
                request.SourceType));
    }

    internal static string NormalizeSourceType(string sourceType)
    {
        if (string.IsNullOrWhiteSpace(sourceType))
        {
            throw new ValidationException("Capture source type is required.");
        }

        return sourceType.Trim().ToLowerInvariant();
    }

    internal static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new ValidationException("Capture file extension is required.");
        }

        return extension[0] == '.' ? extension : $".{extension}";
    }

    internal string GetNextAvailablePath(string directoryPath, string baseFileName, string extension)
    {
        var candidatePath = fileSystem.Combine(directoryPath, $"{baseFileName}{extension}");
        if (!fileSystem.FileExists(candidatePath))
        {
            return candidatePath;
        }

        for (var suffix = 1; suffix < 1000; suffix++)
        {
            candidatePath = fileSystem.Combine(directoryPath, $"{baseFileName}_{suffix:000}{extension}");

            if (!fileSystem.FileExists(candidatePath))
            {
                return candidatePath;
            }
        }

        throw new IOException($"Could not find an available capture file name for '{baseFileName}{extension}'.");
    }

    private static void ValidateInputs(StorageOptions options, CaptureStorageRequest request)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(options.ImageRootPath))
        {
            throw new ValidationException("Storage image root path is required.");
        }

        if (string.IsNullOrWhiteSpace(request.StagedFilePath))
        {
            throw new ValidationException("Staged capture file path is required.");
        }

        if (!File.Exists(request.StagedFilePath))
        {
            throw new FileNotFoundException("Staged capture file was not found.", request.StagedFilePath);
        }

        _ = NormalizeSourceType(request.SourceType);
        _ = NormalizeExtension(request.FileExtension);
    }
}
