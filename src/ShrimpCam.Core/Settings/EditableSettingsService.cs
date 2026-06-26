using System.ComponentModel.DataAnnotations;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Persistence;

namespace ShrimpCam.Core.Settings;

public sealed class EditableSettingsService(
    ISettingsRepository settingsRepository,
    IClock clock,
    ShrimpCamOptions defaults) : IEditableSettingsService
{
    public async Task<EditableSettings> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var persistedSettings = await settingsRepository.ListAsync(cancellationToken).ConfigureAwait(false);
        var persisted = persistedSettings.ToDictionary(setting => setting.Key, setting => setting.Value, StringComparer.OrdinalIgnoreCase);

        return new EditableSettings(
            new CameraOptions
            {
                Platform = GetString(persisted, "camera.platform", defaults.Camera.Platform),
                Source = GetString(persisted, "camera.source", defaults.Camera.Source),
                CaptureWidth = GetInt(persisted, "camera.captureWidth", defaults.Camera.CaptureWidth),
                CaptureHeight = GetInt(persisted, "camera.captureHeight", defaults.Camera.CaptureHeight),
                StreamWidth = GetInt(persisted, "camera.streamWidth", defaults.Camera.StreamWidth),
                StreamHeight = GetInt(persisted, "camera.streamHeight", defaults.Camera.StreamHeight),
                StreamFramesPerSecond = GetInt(persisted, "camera.streamFramesPerSecond", defaults.Camera.StreamFramesPerSecond),
                ReconnectRetryAttempts = GetInt(persisted, "camera.reconnectRetryAttempts", defaults.Camera.ReconnectRetryAttempts),
                ReconnectBackoffSeconds = GetInt(persisted, "camera.reconnectBackoffSeconds", defaults.Camera.ReconnectBackoffSeconds),
                AlwaysOnStreamEnabled = GetBool(persisted, "camera.alwaysOnStreamEnabled", defaults.Camera.AlwaysOnStreamEnabled),
            },
            new CaptureOptions
            {
                Enabled = GetBool(persisted, "capture.enabled", defaults.Capture.Enabled),
                IntervalMinutes = GetInt(persisted, "capture.intervalMinutes", defaults.Capture.IntervalMinutes),
                ActiveStartHourUtc = GetInt(persisted, "capture.activeStartHourUtc", defaults.Capture.ActiveStartHourUtc),
                ActiveEndHourUtc = GetInt(persisted, "capture.activeEndHourUtc", defaults.Capture.ActiveEndHourUtc),
                MotionHighlightsEnabled = GetBool(persisted, "capture.motionHighlightsEnabled", defaults.Capture.MotionHighlightsEnabled),
                MotionThreshold = GetDouble(persisted, "capture.motionThreshold", defaults.Capture.MotionThreshold),
                MotionCooldownSeconds = GetInt(persisted, "capture.motionCooldownSeconds", defaults.Capture.MotionCooldownSeconds),
            },
            new StorageEditableSettings(
                GetInt(persisted, "storage.retentionDays", defaults.Storage.RetentionDays)),
            new SecurityOptions
            {
                HostMode = GetString(persisted, "security.hostMode", defaults.Security.HostMode),
            });
    }

    public EditableSettingsValidationResult Validate(EditableSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        ValidateObject(settings.Camera, "camera", errors);
        ValidateObject(settings.Capture, "capture", errors);
        ValidateStorage(settings.Storage, errors);
        ValidateObject(settings.Security, "security", errors);

        if (settings.Capture.ActiveEndHourUtc <= settings.Capture.ActiveStartHourUtc)
        {
            AddError(errors, "capture.activeEndHourUtc", "Capture active end hour must be greater than the start hour.");
        }

        return errors.Count == 0
            ? EditableSettingsValidationResult.Success()
            : EditableSettingsValidationResult.Failure(errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase));
    }

    public async Task<EditableSettings> UpdateAsync(EditableSettings settings, CancellationToken cancellationToken)
    {
        var validation = Validate(settings);
        if (!validation.IsValid)
        {
            throw new ValidationException("Editable settings are invalid.");
        }

        var updatedAtUtc = clock.UtcNow;
        foreach (var setting in ToPersistedSettings(settings, updatedAtUtc))
        {
            await settingsRepository.UpsertAsync(setting, cancellationToken).ConfigureAwait(false);
        }

        return await GetCurrentAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<PersistedSetting> ToPersistedSettings(EditableSettings settings, DateTimeOffset updatedAtUtc) =>
        [
            CreateSetting("camera.platform", settings.Camera.Platform, "Camera platform", updatedAtUtc),
            CreateSetting("camera.source", settings.Camera.Source, "Camera source", updatedAtUtc),
            CreateSetting("camera.captureWidth", settings.Camera.CaptureWidth, "Capture width", updatedAtUtc),
            CreateSetting("camera.captureHeight", settings.Camera.CaptureHeight, "Capture height", updatedAtUtc),
            CreateSetting("camera.streamWidth", settings.Camera.StreamWidth, "Stream width", updatedAtUtc),
            CreateSetting("camera.streamHeight", settings.Camera.StreamHeight, "Stream height", updatedAtUtc),
            CreateSetting("camera.streamFramesPerSecond", settings.Camera.StreamFramesPerSecond, "Stream frames per second", updatedAtUtc),
            CreateSetting("camera.reconnectRetryAttempts", settings.Camera.ReconnectRetryAttempts, "Reconnect retry attempts", updatedAtUtc),
            CreateSetting("camera.reconnectBackoffSeconds", settings.Camera.ReconnectBackoffSeconds, "Reconnect backoff seconds", updatedAtUtc),
            CreateSetting("camera.alwaysOnStreamEnabled", settings.Camera.AlwaysOnStreamEnabled, "Always-on stream enabled", updatedAtUtc),
            CreateSetting("capture.enabled", settings.Capture.Enabled, "Capture enabled", updatedAtUtc),
            CreateSetting("capture.intervalMinutes", settings.Capture.IntervalMinutes, "Capture interval minutes", updatedAtUtc),
            CreateSetting("capture.activeStartHourUtc", settings.Capture.ActiveStartHourUtc, "Capture active start hour UTC", updatedAtUtc),
            CreateSetting("capture.activeEndHourUtc", settings.Capture.ActiveEndHourUtc, "Capture active end hour UTC", updatedAtUtc),
            CreateSetting("capture.motionHighlightsEnabled", settings.Capture.MotionHighlightsEnabled, "Motion highlights enabled", updatedAtUtc),
            CreateSetting("capture.motionThreshold", settings.Capture.MotionThreshold, "Motion threshold", updatedAtUtc),
            CreateSetting("capture.motionCooldownSeconds", settings.Capture.MotionCooldownSeconds, "Motion cooldown seconds", updatedAtUtc),
            CreateSetting("storage.retentionDays", settings.Storage.RetentionDays, "Retention days", updatedAtUtc),
            CreateSetting("security.hostMode", settings.Security.HostMode, "Host mode", updatedAtUtc),
        ];

    private static PersistedSetting CreateSetting(string key, object value, string description, DateTimeOffset updatedAtUtc) =>
        new(
            key,
            value is bool booleanValue
                ? booleanValue.ToString().ToLowerInvariant()
                : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!,
            description,
            updatedAtUtc);

    private static void ValidateObject(object instance, string prefix, Dictionary<string, List<string>> errors)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);

        foreach (var result in results)
        {
            foreach (var memberName in result.MemberNames)
            {
                AddError(errors, $"{prefix}.{ToCamelCase(memberName)}", result.ErrorMessage ?? "Validation failed.");
            }
        }
    }

    private static void ValidateStorage(StorageEditableSettings storage, Dictionary<string, List<string>> errors)
    {
        if (storage.RetentionDays is < 1 or > 3650)
        {
            AddError(errors, "storage.retentionDays", "Retention days must be between 1 and 3650.");
        }
    }

    private static void AddError(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var messages))
        {
            messages = [];
            errors[key] = messages;
        }

        messages.Add(message);
    }

    private static string ToCamelCase(string value) =>
        $"{char.ToLowerInvariant(value[0])}{value[1..]}";

    private static string GetString(Dictionary<string, string> persisted, string key, string fallback) =>
        persisted.TryGetValue(key, out var value) ? value : fallback;

    private static int GetInt(Dictionary<string, string> persisted, string key, int fallback) =>
        persisted.TryGetValue(key, out var value) && int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static bool GetBool(Dictionary<string, string> persisted, string key, bool fallback) =>
        persisted.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
            ? parsed
            : fallback;

    private static double GetDouble(Dictionary<string, string> persisted, string key, double fallback) =>
        persisted.TryGetValue(key, out var value) && double.TryParse(value, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
}
