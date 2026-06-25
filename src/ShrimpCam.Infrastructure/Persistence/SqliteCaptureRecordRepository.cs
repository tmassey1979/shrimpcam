using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Persistence;

namespace ShrimpCam.Infrastructure.Persistence;

internal sealed class SqliteCaptureRecordRepository(IOptions<ShrimpCamOptions> optionsAccessor) : ICaptureRecordRepository
{
    private readonly StorageOptions options = optionsAccessor.Value.Storage;

    public Task CreateAsync(CaptureRecord captureRecord, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = SqliteConnectionFactory.OpenConnection(options);
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO captures (id, relative_image_path, relative_metadata_path, file_name, source_type, captured_at_utc)
            VALUES ($id, $relativeImagePath, $relativeMetadataPath, $fileName, $sourceType, $capturedAtUtc);
            """;
        command.Parameters.AddWithValue("$id", captureRecord.Id.ToString());
        command.Parameters.AddWithValue("$relativeImagePath", captureRecord.RelativeImagePath);
        command.Parameters.AddWithValue("$relativeMetadataPath", captureRecord.RelativeMetadataPath);
        command.Parameters.AddWithValue("$fileName", captureRecord.FileName);
        command.Parameters.AddWithValue("$sourceType", captureRecord.SourceType);
        command.Parameters.AddWithValue("$capturedAtUtc", captureRecord.CapturedAtUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        _ = command.ExecuteNonQuery();

        return Task.CompletedTask;
    }

    public Task<CaptureRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = SqliteConnectionFactory.OpenConnection(options);
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, relative_image_path, relative_metadata_path, file_name, source_type, captured_at_utc
            FROM captures
            WHERE id = $id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", id.ToString());

        using var reader = command.ExecuteReader();
        return Task.FromResult(reader.Read() ? ReadCaptureRecord(reader) : null);
    }

    public Task<CaptureRecordPage> ListAsync(CaptureRecordQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfLessThan(query.PageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(query.PageSize, 1);

        using var connection = SqliteConnectionFactory.OpenConnection(options);
        using var countCommand = connection.CreateCommand();
        countCommand.CommandText =
            """
            SELECT COUNT(*)
            FROM captures
            WHERE ($fromUtc IS NULL OR captured_at_utc >= $fromUtc)
              AND ($toUtc IS NULL OR captured_at_utc <= $toUtc);
            """;
        AddFilterParameters(countCommand, query);
        var totalItems = Convert.ToInt32(countCommand.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, relative_image_path, relative_metadata_path, file_name, source_type, captured_at_utc
            FROM captures
            WHERE ($fromUtc IS NULL OR captured_at_utc >= $fromUtc)
              AND ($toUtc IS NULL OR captured_at_utc <= $toUtc)
            ORDER BY captured_at_utc DESC, id DESC
            LIMIT $pageSize OFFSET $offset;
            """;
        AddFilterParameters(command, query);
        command.Parameters.AddWithValue("$pageSize", query.PageSize);
        command.Parameters.AddWithValue("$offset", (query.PageNumber - 1) * query.PageSize);

        using var reader = command.ExecuteReader();
        var items = new List<CaptureRecord>();
        while (reader.Read())
        {
            items.Add(ReadCaptureRecord(reader));
        }

        return Task.FromResult(new CaptureRecordPage(items, query.PageNumber, query.PageSize, totalItems));
    }

    private static CaptureRecord ReadCaptureRecord(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            DateTimeOffset.Parse(reader.GetString(5), System.Globalization.CultureInfo.InvariantCulture));

    private static void AddFilterParameters(SqliteCommand command, CaptureRecordQuery query)
    {
        command.Parameters.AddWithValue(
            "$fromUtc",
            query.FromUtc?.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(
            "$toUtc",
            query.ToUtc?.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
    }
}
