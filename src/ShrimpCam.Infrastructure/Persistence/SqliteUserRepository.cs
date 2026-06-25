using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Persistence;

namespace ShrimpCam.Infrastructure.Persistence;

internal sealed class SqliteUserRepository(IOptions<ShrimpCamOptions> optionsAccessor) : IUserRepository
{
    private readonly StorageOptions options = optionsAccessor.Value.Storage;

    public Task CreateAsync(UserRecord user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = SqliteConnectionFactory.OpenConnection(options);
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO users (id, user_name, password_hash, is_enabled, created_at_utc)
            VALUES ($id, $userName, $passwordHash, $isEnabled, $createdAtUtc);
            """;
        command.Parameters.AddWithValue("$id", user.Id.ToString());
        command.Parameters.AddWithValue("$userName", user.UserName);
        command.Parameters.AddWithValue("$passwordHash", user.PasswordHash);
        command.Parameters.AddWithValue("$isEnabled", user.IsEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$createdAtUtc", user.CreatedAtUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        _ = command.ExecuteNonQuery();

        return Task.CompletedTask;
    }

    public Task<UserRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        GetSingleAsync("id = $value", id.ToString(), cancellationToken);

    public Task<UserRecord?> GetByUserNameAsync(string userName, CancellationToken cancellationToken) =>
        GetSingleAsync("user_name = $value", userName, cancellationToken);

    private Task<UserRecord?> GetSingleAsync(string predicate, string value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = SqliteConnectionFactory.OpenConnection(options);
        using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT id, user_name, password_hash, is_enabled, created_at_utc
            FROM users
            WHERE {predicate}
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$value", value);

        using var reader = command.ExecuteReader();
        return Task.FromResult(reader.Read() ? ReadUser(reader) : null);
    }

    private static UserRecord ReadUser(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3) == 1,
            DateTimeOffset.Parse(reader.GetString(4), System.Globalization.CultureInfo.InvariantCulture));
}
