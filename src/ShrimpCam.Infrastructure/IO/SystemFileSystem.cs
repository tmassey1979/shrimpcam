using ShrimpCam.Core.Abstractions;

namespace ShrimpCam.Infrastructure.IO;

internal sealed class SystemFileSystem : IFileSystem
{
    public string Combine(params string[] paths) => Path.Combine(paths);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
}
