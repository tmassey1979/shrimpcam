using System.ComponentModel.DataAnnotations;

namespace ShrimpCam.Core.Configuration;

public sealed class CaptureOptions
{
    public bool Enabled { get; init; }

    [Range(1, 1440)]
    public int IntervalMinutes { get; init; } = 5;

    [Range(0, 23)]
    public int ActiveStartHourUtc { get; init; } = 6;

    [Range(1, 24)]
    public int ActiveEndHourUtc { get; init; } = 22;

    public bool MotionHighlightsEnabled { get; init; }

    [Range(0.01d, 1d)]
    public double MotionThreshold { get; init; } = 0.35d;

    [Range(0, 86400)]
    public int MotionCooldownSeconds { get; init; } = 300;
}
