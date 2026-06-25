namespace ShrimpCam.Core.Persistence;

public sealed record CaptureRecordQuery(
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int PageNumber,
    int PageSize);
