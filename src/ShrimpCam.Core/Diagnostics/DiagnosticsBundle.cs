using ShrimpCam.Core.Health;
using ShrimpCam.Core.Persistence;

namespace ShrimpCam.Core.Diagnostics;

public sealed record DiagnosticsBundle(
    DateTimeOffset GeneratedAtUtc,
    ApplicationHealthReport Health,
    IReadOnlyDictionary<string, object?> Configuration,
    IReadOnlyList<AuditRecord> RecentAuditEvents);
