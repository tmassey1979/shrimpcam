using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Health;
using ShrimpCam.Infrastructure.Persistence;

namespace ShrimpCam.Infrastructure.Health;

public sealed class SqliteDatabaseHealthProbe(IOptions<ShrimpCamOptions> optionsAccessor) : IDatabaseHealthProbe
{
    private readonly StorageOptions options = optionsAccessor.Value.Storage;

    public Task<HealthComponentReport> CheckAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var connection = SqliteConnectionFactory.OpenConnection(options);
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            _ = command.ExecuteScalar();

            return Task.FromResult(new HealthComponentReport(HealthStatusLevel.Healthy, null));
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.DataAnnotations.ValidationException)
        {
            return Task.FromResult(new HealthComponentReport(HealthStatusLevel.Unhealthy, ex.Message));
        }
    }
}
