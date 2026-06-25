namespace ShrimpCam.Core.Captures;

public sealed record CaptureStorageRequest(
    DateTimeOffset CapturedAtUtc,
    string SourceType,
    string StagedFilePath,
    string FileExtension = ".jpg");
