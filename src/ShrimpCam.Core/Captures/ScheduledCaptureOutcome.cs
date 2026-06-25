namespace ShrimpCam.Core.Captures;

public enum ScheduledCaptureOutcome
{
    Disabled = 0,
    Waiting = 1,
    Captured = 2,
    SkippedBySchedule = 3,
    Failed = 4,
}
