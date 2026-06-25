using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Persistence;

namespace ShrimpCam.Infrastructure.Persistence;

internal sealed class SqliteSettingsRepository(IOptions<ShrimpCamOptions> optionsAccessor) : ISettingsRepository
{
    private readonly StorageOptions options = optionsAccessor.Value.Storage;

    public Task UpsertAsync(PersistedSetting setting, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = SqliteConnectionFactory.OpenConnection(options);
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO settings (key, value, description, updated_at_utc)
            VALUES ($key, $value, $description, $updatedAtUtc)
            ON CONFLICT(key) DO UPDATE SET
                value = excluded.value,
                description = excluded.description,
                updated_at_utc = excluded.updated_at_utc;
            """;
        command.Parameters.AddWithValue("$key", setting.Key);
        command.Parameters.AddWithValue("$value", setting.Value);
        command.Parameters.AddWithValue("$description", setting.Description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$updatedAtUtc", setting.UpdatedAtUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        _ = command.ExecuteNonQuery();

        return Task.CompletedTask;
    }

    public Task<PersistedSetting?> GetByKeyAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = SqliteConnectionFactory.OpenConnection(options);
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT key, value, description, updated_at_utc
            FROM settings
            WHERE key = $key
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$key", key);

        using var reader = command.ExecuteReader();
        return Task.FromResult(reader.Read() ? ReadSetting(reader) : null);
    }

    private static PersistedSetting ReadSetting(SqliteDataReader reader) =>
        new(
            reader.GetString(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            DateTimeOffset.Parse(reader.GetString(3), System.Globalization.CultureInfo.InvariantCulture));
}
