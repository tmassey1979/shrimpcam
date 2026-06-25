using Microsoft.Extensions.Options;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Persistence;

namespace ShrimpCam.Infrastructure.Persistence;

internal sealed class SqliteUserRoleRepository(IOptions<ShrimpCamOptions> optionsAccessor) : IUserRoleRepository
{
    private readonly StorageOptions options = optionsAccessor.Value.Storage;

    public Task AssignAsync(UserRoleRecord roleAssignment, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = SqliteConnectionFactory.OpenConnection(options);
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO user_roles (user_id, role_name, assigned_at_utc)
            VALUES ($userId, $roleName, $assignedAtUtc)
            ON CONFLICT(user_id, role_name) DO UPDATE SET
                assigned_at_utc = excluded.assigned_at_utc;
            """;
        command.Parameters.AddWithValue("$userId", roleAssignment.UserId.ToString());
        command.Parameters.AddWithValue("$roleName", roleAssignment.RoleName);
        command.Parameters.AddWithValue("$assignedAtUtc", roleAssignment.AssignedAtUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        _ = command.ExecuteNonQuery();

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<UserRoleRecord>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var roles = new List<UserRoleRecord>();
        using var connection = SqliteConnectionFactory.OpenConnection(options);
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT user_id, role_name, assigned_at_utc
            FROM user_roles
            WHERE user_id = $userId
            ORDER BY role_name;
            """;
        command.Parameters.AddWithValue("$userId", userId.ToString());

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            roles.Add(
                new UserRoleRecord(
                    Guid.Parse(reader.GetString(0)),
                    reader.GetString(1),
                    DateTimeOffset.Parse(reader.GetString(2), System.Globalization.CultureInfo.InvariantCulture)));
        }

        return Task.FromResult<IReadOnlyList<UserRoleRecord>>(roles);
    }
}
