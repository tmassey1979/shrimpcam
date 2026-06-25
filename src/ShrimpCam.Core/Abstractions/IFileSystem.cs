namespace ShrimpCam.Core.Abstractions;

public interface IFileSystem
{
    string Combine(params string[] paths);

    bool DirectoryExists(string path);

    void CreateDirectory(string path);

    IEnumerable<string> EnumerateDirectories(string path);

    bool FileExists(string path);

    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);

    void WriteAllLines(string path, IEnumerable<string> contents);

    DateTimeOffset GetLastWriteTimeUtc(string path);

    void MoveFile(string sourcePath, string destinationPath);

    void DeleteFile(string path);

    string ReadAllText(string path);

    void WriteAllText(string path, string contents);

    string GetTemporaryFilePath(string extension);
}
