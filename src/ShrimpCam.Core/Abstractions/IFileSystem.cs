namespace ShrimpCam.Core.Abstractions;

public interface IFileSystem
{
    string Combine(params string[] paths);

    bool DirectoryExists(string path);

    void CreateDirectory(string path);
}
