namespace ShrimpCam.Core.Persistence;

public sealed record CaptureRecord(
    Guid Id,
    string RelativeImagePath,
    string RelativeMetadataPath,
    string FileName,
    string SourceType,
    DateTimeOffset CapturedAtUtc);
