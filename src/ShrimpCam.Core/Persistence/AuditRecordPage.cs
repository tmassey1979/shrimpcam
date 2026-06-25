namespace ShrimpCam.Core.Persistence;

public sealed record AuditRecordPage(
    IReadOnlyList<AuditRecord> Items,
    int PageNumber,
    int PageSize,
    int TotalItems)
{
    public int TotalPages => TotalItems == 0 ? 0 : (int)Math.Ceiling((double)TotalItems / PageSize);

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;
}
