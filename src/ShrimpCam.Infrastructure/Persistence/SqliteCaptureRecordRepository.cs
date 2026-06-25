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

    private static CaptureRecord ReadCaptureRecord(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            DateTimeOffset.Parse(reader.GetString(5), System.Globalization.CultureInfo.InvariantCulture));
}
