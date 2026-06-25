namespace ShrimpCam.Core.Captures;

public sealed record StoredCapture(
    string ImagePath,
    string MetadataPath,
    string RelativeImagePath,
    string FileName,
    DateTimeOffset CapturedAtUtc,
    string SourceType);
