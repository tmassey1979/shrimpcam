namespace ShrimpCam.Core.Captures;

public sealed record DailyTimelapseGenerationResult(
    string Status,
    DateOnly Day,
    int FrameCount,
    string? VideoPath,
    string? RelativeVideoPath);
