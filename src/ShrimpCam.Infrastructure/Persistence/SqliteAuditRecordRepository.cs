using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Persistence;

namespace ShrimpCam.Infrastructure.Persistence;

internal sealed class SqliteAuditRecordRepository(IOptions<ShrimpCamOptions> optionsAccessor) : IAuditRecordRepository
{
    private readonly StorageOptions options = optionsAccessor.Value.Storage;

    public Task CreateAsync(AuditRecord auditRecord, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = SqliteConnectionFactory.OpenConnection(options);
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO audit_records (id, event_type, actor_user_name, outcome, detail, occurred_at_utc)
            VALUES ($id, $eventType, $actorUserName, $outcome, $detail, $occurredAtUtc);
            """;
        command.Parameters.AddWithValue("$id", auditRecord.Id.ToString());
        command.Parameters.AddWithValue("$eventType", auditRecord.EventType);
        command.Parameters.AddWithValue("$actorUserName", auditRecord.ActorUserName);
        command.Parameters.AddWithValue("$outcome", auditRecord.Outcome);
        command.Parameters.AddWithValue("$detail", auditRecord.Detail);
        command.Parameters.AddWithValue("$occurredAtUtc", auditRecord.OccurredAtUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        _ = command.ExecuteNonQuery();

        return Task.CompletedTask;
    }

    public Task<AuditRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = SqliteConnectionFactory.OpenConnection(options);
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, event_type, actor_user_name, outcome, detail, occurred_at_utc
            FROM audit_records
            WHERE id = $id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", id.ToString());

        using var reader = command.ExecuteReader();
        return Task.FromResult(reader.Read() ? ReadAuditRecord(reader) : null);
    }

    public Task<AuditRecordPage> ListAsync(AuditRecordQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfLessThan(query.PageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(query.PageSize, 1);

        using var connection = SqliteConnectionFactory.OpenConnection(options);
        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM audit_records;";
        var totalItems = Convert.ToInt32(countCommand.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, event_type, actor_user_name, outcome, detail, occurred_at_utc
            FROM audit_records
            ORDER BY occurred_at_utc DESC, id DESC
            LIMIT $pageSize OFFSET $offset;
            """;
        command.Parameters.AddWithValue("$pageSize", query.PageSize);
        command.Parameters.AddWithValue("$offset", (query.PageNumber - 1) * query.PageSize);

        using var reader = command.ExecuteReader();
        var items = new List<AuditRecord>();
        while (reader.Read())
        {
            items.Add(ReadAuditRecord(reader));
        }

        return Task.FromResult(new AuditRecordPage(items, query.PageNumber, query.PageSize, totalItems));
    }

    private static AuditRecord ReadAuditRecord(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            DateTimeOffset.Parse(reader.GetString(5), System.Globalization.CultureInfo.InvariantCulture));
}
