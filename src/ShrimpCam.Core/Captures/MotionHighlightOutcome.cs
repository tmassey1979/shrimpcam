namespace ShrimpCam.Core.Captures;

public static class MotionHighlightOutcome
{
    public const string Captured = "captured";
    public const string Disabled = "disabled";
    public const string BelowThreshold = "belowThreshold";
    public const string SuppressedByCooldown = "suppressedByCooldown";
    public const string SuppressedDuplicate = "suppressedDuplicate";
    public const string Failed = "failed";
}
