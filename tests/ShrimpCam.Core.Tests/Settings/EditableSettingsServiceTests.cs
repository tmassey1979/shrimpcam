using NSubstitute;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Persistence;
using ShrimpCam.Core.Settings;

namespace ShrimpCam.Core.Tests.Settings;

public sealed class EditableSettingsServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Get_current_async_merges_persisted_settings_over_configured_defaults()
    {
        var settingsRepository = Substitute.For<ISettingsRepository>();
        var clock = Substitute.For<IClock>();
        var defaults = CreateDefaults();
        settingsRepository.ListAsync(CancellationToken.None)
            .Returns(
                [
                    new PersistedSetting("capture.intervalMinutes", "10", null, DateTimeOffset.UtcNow),
                    new PersistedSetting("storage.retentionDays", "45", null, DateTimeOffset.UtcNow),
                    new PersistedSetting("security.hostMode", "RemoteReady", null, DateTimeOffset.UtcNow),
                ]);
        var service = new EditableSettingsService(settingsRepository, clock, defaults);

        var settings = await service.GetCurrentAsync(CancellationToken.None).ConfigureAwait(true);

        settings.Camera.Source.Should().Be("Logitech C920");
        settings.Capture.IntervalMinutes.Should().Be(10);
        settings.Storage.RetentionDays.Should().Be(45);
        settings.Security.HostMode.Should().Be("RemoteReady");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_reports_field_level_errors_for_invalid_settings()
    {
        var service = new EditableSettingsService(Substitute.For<ISettingsRepository>(), Substitute.For<IClock>(), CreateDefaults());
        var settings = CreateValidSettings() with
        {
            Capture = new CaptureOptions
            {
                Enabled = true,
                IntervalMinutes = 0,
                ActiveStartHourUtc = 23,
                ActiveEndHourUtc = 22,
                MotionHighlightsEnabled = true,
                MotionThreshold = 1.2d,
                MotionCooldownSeconds = -1,
            },
            Storage = new StorageEditableSettings(0),
        };

        var result = service.Validate(settings);

        result.IsValid.Should().BeFalse();
        result.Errors.Keys.Should().Contain(["capture.intervalMinutes", "capture.motionThreshold", "capture.motionCooldownSeconds", "capture.activeEndHourUtc", "storage.retentionDays"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Update_async_persists_all_editable_settings_when_valid()
    {
        var settingsRepository = Substitute.For<ISettingsRepository>();
        var clock = Substitute.For<IClock>();
        var now = new DateTimeOffset(2026, 06, 25, 14, 00, 00, TimeSpan.Zero);
        var settings = CreateValidSettings();
        clock.UtcNow.Returns(now);
        settingsRepository.ListAsync(CancellationToken.None).Returns([]);
        var service = new EditableSettingsService(settingsRepository, clock, CreateDefaults());

        await service.UpdateAsync(settings, CancellationToken.None).ConfigureAwait(true);

        await settingsRepository.Received(19)
            .UpsertAsync(Arg.Any<PersistedSetting>(), CancellationToken.None)
            .ConfigureAwait(true);
        await settingsRepository.Received(1)
            .UpsertAsync(
                Arg.Is<PersistedSetting>(setting =>
                    setting.Key == "capture.intervalMinutes" &&
                    setting.Value == "15" &&
                    setting.UpdatedAtUtc == now),
                CancellationToken.None)
            .ConfigureAwait(true);
        await settingsRepository.Received(1)
            .UpsertAsync(
                Arg.Is<PersistedSetting>(setting =>
                    setting.Key == "capture.motionHighlightsEnabled" &&
                    setting.Value == "true"),
                CancellationToken.None)
            .ConfigureAwait(true);
    }

    private static ShrimpCamOptions CreateDefaults() =>
        new()
        {
            Camera = new CameraOptions
            {
                Platform = "Windows",
                Source = "Logitech C920",
            },
            Capture = new CaptureOptions
            {
                Enabled = false,
                IntervalMinutes = 5,
                ActiveStartHourUtc = 6,
                ActiveEndHourUtc = 22,
            },
            Storage = new StorageOptions
            {
                DatabasePath = "data/shrimpcam.db",
                ImageRootPath = "data/images",
                TimelapseRootPath = "data/timelapse",
                RetentionDays = 30,
            },
            Security = new SecurityOptions
            {
                HostMode = "InternetExposed",
            },
        };

    private static EditableSettings CreateValidSettings() =>
        new(
            new CameraOptions
            {
                Platform = "Windows",
                Source = "Logitech C920",
                CaptureWidth = 1920,
                CaptureHeight = 1080,
                StreamWidth = 1280,
                StreamHeight = 720,
                StreamFramesPerSecond = 24,
                ReconnectRetryAttempts = 3,
                ReconnectBackoffSeconds = 2,
            },
            new CaptureOptions
            {
                Enabled = true,
                IntervalMinutes = 15,
                ActiveStartHourUtc = 7,
                ActiveEndHourUtc = 21,
                MotionHighlightsEnabled = true,
                MotionThreshold = 0.45d,
                MotionCooldownSeconds = 180,
            },
            new StorageEditableSettings(60),
            new SecurityOptions
            {
                HostMode = "RemoteReady",
            });
}
