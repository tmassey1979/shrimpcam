using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Persistence;

#pragma warning disable CA2007

namespace ShrimpCam.Infrastructure.Tests.Persistence;

public sealed class SqlitePersistenceTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Initializer_creates_database_tables_indexes_and_schema_version()
    {
        var rootPath = CreateTempRoot();
        var options = CreateStorageOptions(rootPath);

        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<IOptions<ShrimpCamOptions>>(Options.Create(new ShrimpCamOptions { Storage = options }));
            ShrimpCam.Infrastructure.DependencyInjection.AddInfrastructure(services);

            using var provider = services.BuildServiceProvider();
            var initializer = provider.GetRequiredService<IApplicationDataInitializer>();

            await initializer.InitializeAsync(options, CancellationToken.None).ConfigureAwait(true);

            File.Exists(Path.GetFullPath(options.DatabasePath)).Should().BeTrue();

            using var connection = new SqliteConnection($"Data Source={Path.GetFullPath(options.DatabasePath)}");
            connection.Open();

            GetSchemaVersion(connection).Should().Be(1);
            TableExists(connection, "users").Should().BeTrue();
            TableExists(connection, "user_roles").Should().BeTrue();
            TableExists(connection, "settings").Should().BeTrue();
            TableExists(connection, "captures").Should().BeTrue();
            TableExists(connection, "sessions").Should().BeTrue();
            TableExists(connection, "audit_records").Should().BeTrue();
            IndexExists(connection, "ix_users_user_name").Should().BeTrue();
            IndexExists(connection, "ix_captures_captured_at_utc").Should().BeTrue();
            IndexExists(connection, "ix_sessions_user_id").Should().BeTrue();
            IndexExists(connection, "ix_audit_records_occurred_at_utc").Should().BeTrue();
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Initializer_upgrades_version_zero_schema_to_current_version()
    {
        var rootPath = CreateTempRoot();
        var options = CreateStorageOptions(rootPath);
        Directory.CreateDirectory(rootPath);

        try
        {
            using (var connection = new SqliteConnection($"Data Source={Path.GetFullPath(options.DatabasePath)}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    CREATE TABLE schema_version (
                        version INTEGER NOT NULL,
                        applied_at_utc TEXT NOT NULL
                    );

                    INSERT INTO schema_version (version, applied_at_utc)
                    VALUES (0, '2026-06-25T00:00:00.0000000Z');
                    """;
                _ = command.ExecuteNonQuery();
            }

            var services = new ServiceCollection();
            services.AddSingleton<IOptions<ShrimpCamOptions>>(Options.Create(new ShrimpCamOptions { Storage = options }));
            ShrimpCam.Infrastructure.DependencyInjection.AddInfrastructure(services);

            using var provider = services.BuildServiceProvider();
            var initializer = provider.GetRequiredService<IApplicationDataInitializer>();
            await initializer.InitializeAsync(options, CancellationToken.None).ConfigureAwait(true);

            using var verification = new SqliteConnection($"Data Source={Path.GetFullPath(options.DatabasePath)}");
            verification.Open();
            GetSchemaVersion(verification).Should().Be(1);
            TableExists(verification, "users").Should().BeTrue();
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Repositories_round_trip_users_roles_settings_captures_sessions_and_audit_records()
    {
        var rootPath = CreateTempRoot();
        var options = CreateShrimpCamOptions(rootPath);

        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<IOptions<ShrimpCamOptions>>(Options.Create(options));
            ShrimpCam.Infrastructure.DependencyInjection.AddInfrastructure(services);

            using var provider = services.BuildServiceProvider();

            await provider.GetRequiredService<IApplicationDataInitializer>()
                .InitializeAsync(options.Storage, CancellationToken.None)
                .ConfigureAwait(true);

            var settingsRepository = provider.GetRequiredService<ISettingsRepository>();
            var userRepository = provider.GetRequiredService<IUserRepository>();
            var userRoleRepository = provider.GetRequiredService<IUserRoleRepository>();
            var captureRepository = provider.GetRequiredService<ICaptureRecordRepository>();
            var sessionRepository = provider.GetRequiredService<ISessionRepository>();
            var auditRepository = provider.GetRequiredService<IAuditRecordRepository>();

            var updatedAtUtc = new DateTimeOffset(2026, 06, 25, 00, 20, 00, TimeSpan.Zero);
            var userId = Guid.NewGuid();
            var captureId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var auditId = Guid.NewGuid();

            await settingsRepository.UpsertAsync(
                    new PersistedSetting("capture.intervalMinutes", "5", "Capture interval", updatedAtUtc),
                    CancellationToken.None)
                .ConfigureAwait(true);
            await userRepository.CreateAsync(
                    new UserRecord(userId, "shrimp-admin", "hashed-password", true, updatedAtUtc),
                    CancellationToken.None)
                .ConfigureAwait(true);
            await userRoleRepository.AssignAsync(
                    new UserRoleRecord(userId, "Administrator", updatedAtUtc),
                    CancellationToken.None)
                .ConfigureAwait(true);
            await captureRepository.CreateAsync(
                    new CaptureRecord(captureId, "2026/06/25/capture.jpg", "2026/06/25/capture.json", "capture.jpg", "Scheduled", updatedAtUtc),
                    CancellationToken.None)
                .ConfigureAwait(true);
            await sessionRepository.CreateAsync(
                    new SessionRecord(sessionId, userId, "token-hash", updatedAtUtc, updatedAtUtc.AddHours(8), null),
                    CancellationToken.None)
                .ConfigureAwait(true);
            await auditRepository.CreateAsync(
                    new AuditRecord(auditId, "SettingsUpdated", "shrimp-admin", "Succeeded", "Capture interval changed", updatedAtUtc),
                    CancellationToken.None)
                .ConfigureAwait(true);

            (await settingsRepository.GetByKeyAsync("capture.intervalMinutes", CancellationToken.None).ConfigureAwait(true))
                .Should()
                .Be(new PersistedSetting("capture.intervalMinutes", "5", "Capture interval", updatedAtUtc));
            (await settingsRepository.ListAsync(CancellationToken.None).ConfigureAwait(true))
                .Should()
                .ContainSingle(setting => setting == new PersistedSetting("capture.intervalMinutes", "5", "Capture interval", updatedAtUtc));
            (await userRepository.GetByIdAsync(userId, CancellationToken.None).ConfigureAwait(true))
                .Should()
                .Be(new UserRecord(userId, "shrimp-admin", "hashed-password", true, updatedAtUtc));
            (await userRepository.GetByUserNameAsync("shrimp-admin", CancellationToken.None).ConfigureAwait(true))
                .Should()
                .Be(new UserRecord(userId, "shrimp-admin", "hashed-password", true, updatedAtUtc));
            (await userRoleRepository.ListByUserIdAsync(userId, CancellationToken.None).ConfigureAwait(true))
                .Should()
                .BeEquivalentTo([new UserRoleRecord(userId, "Administrator", updatedAtUtc)]);
            (await captureRepository.GetByIdAsync(captureId, CancellationToken.None).ConfigureAwait(true))
                .Should()
                .Be(new CaptureRecord(captureId, "2026/06/25/capture.jpg", "2026/06/25/capture.json", "capture.jpg", "Scheduled", updatedAtUtc));
            (await captureRepository.ListAsync(new CaptureRecordQuery(null, null, 1, 10), CancellationToken.None).ConfigureAwait(true))
                .Items.Should()
                .ContainSingle(capture => capture == new CaptureRecord(captureId, "2026/06/25/capture.jpg", "2026/06/25/capture.json", "capture.jpg", "Scheduled", updatedAtUtc));
            (await sessionRepository.GetByIdAsync(sessionId, CancellationToken.None).ConfigureAwait(true))
                .Should()
                .Be(new SessionRecord(sessionId, userId, "token-hash", updatedAtUtc, updatedAtUtc.AddHours(8), null));
            (await sessionRepository.GetByTokenHashAsync("token-hash", CancellationToken.None).ConfigureAwait(true))
                .Should()
                .Be(new SessionRecord(sessionId, userId, "token-hash", updatedAtUtc, updatedAtUtc.AddHours(8), null));
            await sessionRepository.UpdateAsync(
                    new SessionRecord(sessionId, userId, "token-hash", updatedAtUtc, updatedAtUtc.AddHours(8), updatedAtUtc.AddHours(1)),
                    CancellationToken.None)
                .ConfigureAwait(true);
            (await sessionRepository.GetByIdAsync(sessionId, CancellationToken.None).ConfigureAwait(true))
                .Should()
                .Be(new SessionRecord(sessionId, userId, "token-hash", updatedAtUtc, updatedAtUtc.AddHours(8), updatedAtUtc.AddHours(1)));
            (await auditRepository.GetByIdAsync(auditId, CancellationToken.None).ConfigureAwait(true))
                .Should()
                .Be(new AuditRecord(auditId, "SettingsUpdated", "shrimp-admin", "Succeeded", "Capture interval changed", updatedAtUtc));
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Capture_repository_lists_filtered_captures_newest_first_with_stable_paging()
    {
        var rootPath = CreateTempRoot();
        var options = CreateShrimpCamOptions(rootPath);

        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<IOptions<ShrimpCamOptions>>(Options.Create(options));
            ShrimpCam.Infrastructure.DependencyInjection.AddInfrastructure(services);

            using var provider = services.BuildServiceProvider();

            await provider.GetRequiredService<IApplicationDataInitializer>()
                .InitializeAsync(options.Storage, CancellationToken.None)
                .ConfigureAwait(true);

            var captureRepository = provider.GetRequiredService<ICaptureRecordRepository>();
            var captures = new[]
            {
                new CaptureRecord(Guid.Parse("00000000-0000-0000-0000-000000000001"), "2026/06/23/old.jpg", "2026/06/23/old.json", "old.jpg", "Scheduled", new DateTimeOffset(2026, 06, 23, 08, 00, 00, TimeSpan.Zero)),
                new CaptureRecord(Guid.Parse("00000000-0000-0000-0000-000000000002"), "2026/06/24/morning.jpg", "2026/06/24/morning.json", "morning.jpg", "Scheduled", new DateTimeOffset(2026, 06, 24, 08, 00, 00, TimeSpan.Zero)),
                new CaptureRecord(Guid.Parse("00000000-0000-0000-0000-000000000003"), "2026/06/24/noon.jpg", "2026/06/24/noon.json", "noon.jpg", "Scheduled", new DateTimeOffset(2026, 06, 24, 12, 00, 00, TimeSpan.Zero)),
                new CaptureRecord(Guid.Parse("00000000-0000-0000-0000-000000000004"), "2026/06/25/new.jpg", "2026/06/25/new.json", "new.jpg", "Scheduled", new DateTimeOffset(2026, 06, 25, 08, 00, 00, TimeSpan.Zero)),
            };

            foreach (var capture in captures)
            {
                await captureRepository.CreateAsync(capture, CancellationToken.None).ConfigureAwait(true);
            }

            var page = await captureRepository.ListAsync(
                    new CaptureRecordQuery(
                        new DateTimeOffset(2026, 06, 24, 00, 00, 00, TimeSpan.Zero),
                        new DateTimeOffset(2026, 06, 24, 23, 59, 59, TimeSpan.Zero),
                        2,
                        1),
                    CancellationToken.None)
                .ConfigureAwait(true);

            page.Items.Should().ContainSingle(capture => capture.Id == Guid.Parse("00000000-0000-0000-0000-000000000002"));
            page.TotalItems.Should().Be(2);
            page.TotalPages.Should().Be(2);
            page.HasPreviousPage.Should().BeTrue();
            page.HasNextPage.Should().BeFalse();
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Infrastructure_registers_sqlite_persistence_services()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOptions<ShrimpCamOptions>>(
            Options.Create(
                new ShrimpCamOptions
                {
                    Storage = new StorageOptions
                    {
                        DatabasePath = "data/shrimpcam.db",
                        ImageRootPath = "data/images",
                        TimelapseRootPath = "data/timelapse",
                        RetentionDays = 30,
                    },
                }));
        ShrimpCam.Infrastructure.DependencyInjection.AddInfrastructure(services);

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IApplicationDataInitializer>().Should().NotBeNull();
        provider.GetRequiredService<ISettingsRepository>().Should().NotBeNull();
        provider.GetRequiredService<IUserRepository>().Should().NotBeNull();
        provider.GetRequiredService<IUserRoleRepository>().Should().NotBeNull();
        provider.GetRequiredService<ICaptureRecordRepository>().Should().NotBeNull();
        provider.GetRequiredService<ISessionRepository>().Should().NotBeNull();
        provider.GetRequiredService<IAuditRecordRepository>().Should().NotBeNull();
    }

    private static ShrimpCamOptions CreateShrimpCamOptions(string rootPath) =>
        new()
        {
            Storage = CreateStorageOptions(rootPath),
        };

    private static StorageOptions CreateStorageOptions(string rootPath) =>
        new()
        {
            DatabasePath = Path.Combine(rootPath, "shrimpcam.db"),
            ImageRootPath = Path.Combine(rootPath, "images"),
            TimelapseRootPath = Path.Combine(rootPath, "timelapse"),
            RetentionDays = 30,
        };

    private static string CreateTempRoot() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    private static void DeleteDirectory(string rootPath)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (!Directory.Exists(rootPath))
            {
                return;
            }

            try
            {
                Directory.Delete(rootPath, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(100);
            }
            catch (IOException)
            {
                return;
            }
        }
    }

    private static int GetSchemaVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT version FROM schema_version LIMIT 1;";
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) == 1;
    }

    private static bool IndexExists(SqliteConnection connection, string indexName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = $name;";
        command.Parameters.AddWithValue("$name", indexName);
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) == 1;
    }
}

#pragma warning restore CA2007
