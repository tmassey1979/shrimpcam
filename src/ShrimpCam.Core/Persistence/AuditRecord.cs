namespace ShrimpCam.Core.Persistence;

public sealed record AuditRecord(
    Guid Id,
    string EventType,
    string ActorUserName,
    string Outcome,
    string Detail,
    DateTimeOffset OccurredAtUtc);
