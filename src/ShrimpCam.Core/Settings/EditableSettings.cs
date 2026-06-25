using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Settings;

public sealed record EditableSettings(
    CameraOptions Camera,
    CaptureOptions Capture,
    StorageEditableSettings Storage,
    SecurityOptions Security);

public sealed record StorageEditableSettings(int RetentionDays);
