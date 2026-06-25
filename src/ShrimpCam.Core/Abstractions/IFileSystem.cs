namespace ShrimpCam.Core.Abstractions;

public interface IFileSystem
{
    string Combine(params string[] paths);

    bool DirectoryExists(string path);

    void CreateDirectory(string path);

    bool FileExists(string path);

    void MoveFile(string sourcePath, string destinationPath);

    void DeleteFile(string path);

    void WriteAllText(string path, string contents);
}
