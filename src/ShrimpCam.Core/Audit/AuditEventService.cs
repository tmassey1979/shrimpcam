using System.Text.Json;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Persistence;

namespace ShrimpCam.Core.Audit;

public sealed class AuditEventService(
    IAuditRecordRepository auditRecordRepository,
    IClock clock) : IAuditEventService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] SensitiveKeyFragments = ["password", "secret", "token", "hash", "credential", "key"];

    public async Task<AuditRecord> RecordAsync(AuditEventRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var record = new AuditRecord(
            Guid.NewGuid(),
            NormalizeRequired(request.EventType, "Audit event type is required."),
            NormalizeActor(request.ActorUserName),
            NormalizeRequired(request.Outcome, "Audit outcome is required."),
            SerializeDetail(request.Detail),
            clock.UtcNow);

        await auditRecordRepository.CreateAsync(record, cancellationToken).ConfigureAwait(false);

        return record;
    }

    internal static string SerializeDetail(IReadOnlyDictionary<string, string> detail)
    {
        if (detail.Count == 0)
        {
            return "{}";
        }

        var redacted = detail.ToDictionary(
            pair => pair.Key,
            pair => IsSensitiveKey(pair.Key) ? "[redacted]" : pair.Value,
            StringComparer.OrdinalIgnoreCase);

        return JsonSerializer.Serialize(redacted, SerializerOptions);
    }

    private static bool IsSensitiveKey(string key) =>
        SensitiveKeyFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeActor(string actorUserName) =>
        string.IsNullOrWhiteSpace(actorUserName)
            ? "anonymous"
            : actorUserName.Trim();

    private static string NormalizeRequired(string value, string failureMessage) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException(failureMessage, nameof(value))
            : value.Trim();
}
