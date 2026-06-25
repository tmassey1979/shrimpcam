using Microsoft.Data.Sqlite;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Persistence;

namespace ShrimpCam.Infrastructure.Persistence;

internal sealed class SqliteApplicationDataInitializer : IApplicationDataInitializer
{
    internal const int CurrentSchemaVersion = 1;

    public Task InitializeAsync(StorageOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = SqliteConnectionFactory.OpenConnection(options);
        EnsureSchemaVersionTable(connection);

        var currentVersion = GetCurrentVersion(connection);
        if (currentVersion > CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"The database schema version '{currentVersion}' is newer than this application supports ('{CurrentSchemaVersion}').");
        }

        switch (currentVersion)
        {
            case 0:
                ApplyVersion1Schema(connection);
                SetCurrentVersion(connection, CurrentSchemaVersion);
                break;

            case CurrentSchemaVersion:
                break;

            default:
                throw new InvalidOperationException($"Unsupported database schema version '{currentVersion}'.");
        }

        return Task.CompletedTask;
    }

    internal static int GetCurrentVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT version FROM schema_version LIMIT 1;";
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void EnsureSchemaVersionTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS schema_version (
                version INTEGER NOT NULL,
                applied_at_utc TEXT NOT NULL
            );
            """;
        _ = command.ExecuteNonQuery();

        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM schema_version;";
        var count = Convert.ToInt32(countCommand.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);

        if (count == 0)
        {
            using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText =
                """
                INSERT INTO schema_version (version, applied_at_utc)
                VALUES (0, $appliedAtUtc);
                """;
            insertCommand.Parameters.AddWithValue("$appliedAtUtc", DateTimeOffset.UtcNow.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            _ = insertCommand.ExecuteNonQuery();
        }
    }

    private static void SetCurrentVersion(SqliteConnection connection, int version)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE schema_version
            SET version = $version,
                applied_at_utc = $appliedAtUtc;
            """;
        command.Parameters.AddWithValue("$version", version);
        command.Parameters.AddWithValue("$appliedAtUtc", DateTimeOffset.UtcNow.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        _ = command.ExecuteNonQuery();
    }

    private static void ApplyVersion1Schema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS users (
                id TEXT NOT NULL PRIMARY KEY,
                user_name TEXT NOT NULL,
                password_hash TEXT NOT NULL,
                is_enabled INTEGER NOT NULL,
                created_at_utc TEXT NOT NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ix_users_user_name
                ON users(user_name);

            CREATE TABLE IF NOT EXISTS user_roles (
                user_id TEXT NOT NULL,
                role_name TEXT NOT NULL,
                assigned_at_utc TEXT NOT NULL,
                PRIMARY KEY (user_id, role_name)
            );

            CREATE TABLE IF NOT EXISTS settings (
                key TEXT NOT NULL PRIMARY KEY,
                value TEXT NOT NULL,
                description TEXT NULL,
                updated_at_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS captures (
                id TEXT NOT NULL PRIMARY KEY,
                relative_image_path TEXT NOT NULL,
                relative_metadata_path TEXT NOT NULL,
                file_name TEXT NOT NULL,
                source_type TEXT NOT NULL,
                captured_at_utc TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_captures_captured_at_utc
                ON captures(captured_at_utc);

            CREATE TABLE IF NOT EXISTS sessions (
                id TEXT NOT NULL PRIMARY KEY,
                user_id TEXT NOT NULL,
                token_hash TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                expires_at_utc TEXT NOT NULL,
                revoked_at_utc TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_sessions_user_id
                ON sessions(user_id);

            CREATE TABLE IF NOT EXISTS audit_records (
                id TEXT NOT NULL PRIMARY KEY,
                event_type TEXT NOT NULL,
                actor_user_name TEXT NOT NULL,
                outcome TEXT NOT NULL,
                detail TEXT NOT NULL,
                occurred_at_utc TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_audit_records_occurred_at_utc
                ON audit_records(occurred_at_utc);
            """;
        _ = command.ExecuteNonQuery();
    }
}
