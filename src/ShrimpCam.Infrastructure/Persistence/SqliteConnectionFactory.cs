using System.ComponentModel.DataAnnotations;
using Microsoft.Data.Sqlite;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Infrastructure.Persistence;

internal static class SqliteConnectionFactory
{
    public static SqliteConnection OpenConnection(StorageOptions options)
    {
        ValidateOptions(options);

        var databasePath = Path.GetFullPath(options.DatabasePath);
        var directory = Path.GetDirectoryName(databasePath);

        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
            }.ToString());
        connection.Open();
        return connection;
    }

    private static void ValidateOptions(StorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.DatabasePath))
        {
            throw new ValidationException("Storage database path is required.");
        }
    }
}
