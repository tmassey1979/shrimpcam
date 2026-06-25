using ShrimpCam.Core.Abstractions;

namespace ShrimpCam.Infrastructure.IO;

internal sealed class SystemFileSystem : IFileSystem
{
    public string Combine(params string[] paths) => Path.Combine(paths);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public bool FileExists(string path) => File.Exists(path);

    public void MoveFile(string sourcePath, string destinationPath) => File.Move(sourcePath, destinationPath);

    public void DeleteFile(string path) => File.Delete(path);

    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);

    public string GetTemporaryFilePath(string extension)
    {
        var normalizedExtension = string.IsNullOrWhiteSpace(extension)
            ? ".tmp"
            : extension[0] == '.'
                ? extension
                : $".{extension}";

        return Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{normalizedExtension}");
    }
}
