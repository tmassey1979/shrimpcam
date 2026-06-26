using System.ComponentModel.DataAnnotations;

namespace ShrimpCam.Core.Configuration;

public sealed class CameraOptions
{
    [Required]
    [RegularExpression("Windows|Linux", ErrorMessage = "Camera platform must be Linux or Windows.")]
    public string Platform { get; init; } = "Windows";

    [Required]
    [MinLength(1)]
    public string Source { get; init; } = string.Empty;

    [Range(1, 7680)]
    public int CaptureWidth { get; init; } = 1920;

    [Range(1, 4320)]
    public int CaptureHeight { get; init; } = 1080;

    [Range(1, 7680)]
    public int StreamWidth { get; init; } = 1280;

    [Range(1, 4320)]
    public int StreamHeight { get; init; } = 720;

    [Range(1, 120)]
    public int StreamFramesPerSecond { get; init; } = 15;

    [Range(0, 10)]
    public int ReconnectRetryAttempts { get; init; } = 2;

    [Range(1, 60)]
    public int ReconnectBackoffSeconds { get; init; } = 1;

    public bool AlwaysOnStreamEnabled { get; init; }
}
