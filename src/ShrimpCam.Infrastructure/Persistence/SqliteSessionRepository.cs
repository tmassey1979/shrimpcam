using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Persistence;

namespace ShrimpCam.Infrastructure.Persistence;

internal sealed class SqliteSessionRepository(IOptions<ShrimpCamOptions> optionsAccessor) : ISessionRepository
{
    private readonly StorageOptions options = optionsAccessor.Value.Storage;

    public Task CreateAsync(SessionRecord session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = SqliteConnectionFactory.OpenConnection(options);
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO sessions (id, user_id, token_hash, created_at_utc, expires_at_utc, revoked_at_utc)
            VALUES ($id, $userId, $tokenHash, $createdAtUtc, $expiresAtUtc, $revokedAtUtc);
            """;
        command.Parameters.AddWithValue("$id", session.Id.ToString());
        command.Parameters.AddWithValue("$userId", session.UserId.ToString());
        command.Parameters.AddWithValue("$tokenHash", session.TokenHash);
        command.Parameters.AddWithValue("$createdAtUtc", session.CreatedAtUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$expiresAtUtc", session.ExpiresAtUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$revokedAtUtc", session.RevokedAtUtc?.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
        _ = command.ExecuteNonQuery();

        return Task.CompletedTask;
    }

    public Task UpdateAsync(SessionRecord session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = SqliteConnectionFactory.OpenConnection(options);
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE sessions
            SET user_id = $userId,
                token_hash = $tokenHash,
                created_at_utc = $createdAtUtc,
                expires_at_utc = $expiresAtUtc,
                revoked_at_utc = $revokedAtUtc
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", session.Id.ToString());
        command.Parameters.AddWithValue("$userId", session.UserId.ToString());
        command.Parameters.AddWithValue("$tokenHash", session.TokenHash);
        command.Parameters.AddWithValue("$createdAtUtc", session.CreatedAtUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$expiresAtUtc", session.ExpiresAtUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$revokedAtUtc", session.RevokedAtUtc?.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
        _ = command.ExecuteNonQuery();

        return Task.CompletedTask;
    }

    public Task<SessionRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = SqliteConnectionFactory.OpenConnection(options);
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, user_id, token_hash, created_at_utc, expires_at_utc, revoked_at_utc
            FROM sessions
            WHERE id = $id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", id.ToString());

        using var reader = command.ExecuteReader();
        return Task.FromResult(reader.Read() ? ReadSession(reader) : null);
    }

    public Task<SessionRecord?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = SqliteConnectionFactory.OpenConnection(options);
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, user_id, token_hash, created_at_utc, expires_at_utc, revoked_at_utc
            FROM sessions
            WHERE token_hash = $tokenHash
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$tokenHash", tokenHash);

        using var reader = command.ExecuteReader();
        return Task.FromResult(reader.Read() ? ReadSession(reader) : null);
    }

    private static SessionRecord ReadSession(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetString(2),
            DateTimeOffset.Parse(reader.GetString(3), System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(reader.GetString(4), System.Globalization.CultureInfo.InvariantCulture),
            reader.IsDBNull(5)
                ? null
                : DateTimeOffset.Parse(reader.GetString(5), System.Globalization.CultureInfo.InvariantCulture));
}
