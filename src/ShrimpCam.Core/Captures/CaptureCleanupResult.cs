namespace ShrimpCam.Core.Captures;

public sealed record CaptureCleanupResult(
    DateTimeOffset CutoffUtc,
    int DeletedCount,
    int FailedCount,
    IReadOnlyList<CaptureCleanupItemResult> Items);
