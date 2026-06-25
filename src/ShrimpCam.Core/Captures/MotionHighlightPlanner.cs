using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Captures;

public static class MotionHighlightPlanner
{
    public static MotionHighlightPlan Evaluate(
        CaptureOptions options,
        MotionHighlightEvent motionEvent,
        MotionHighlightState state)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(motionEvent);
        ArgumentNullException.ThrowIfNull(state);

        var fingerprint = BuildEventFingerprint(motionEvent);

        if (!options.MotionHighlightsEnabled)
        {
            return new MotionHighlightPlan(MotionHighlightOutcome.Disabled, fingerprint);
        }

        if (IsDuplicate(state, motionEvent, fingerprint))
        {
            return new MotionHighlightPlan(MotionHighlightOutcome.SuppressedDuplicate, fingerprint);
        }

        if (motionEvent.Score < options.MotionThreshold)
        {
            return new MotionHighlightPlan(MotionHighlightOutcome.BelowThreshold, fingerprint);
        }

        if (IsWithinCooldown(options, state, motionEvent))
        {
            return new MotionHighlightPlan(MotionHighlightOutcome.SuppressedByCooldown, fingerprint);
        }

        return new MotionHighlightPlan(MotionHighlightOutcome.Captured, fingerprint);
    }

    internal static string BuildEventFingerprint(MotionHighlightEvent motionEvent)
    {
        if (!string.IsNullOrWhiteSpace(motionEvent.EventId))
        {
            return motionEvent.EventId.Trim();
        }

        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{motionEvent.OccurredAtUtc.UtcDateTime:O}|{motionEvent.Score:F4}");
    }

    internal static bool IsDuplicate(
        MotionHighlightState state,
        MotionHighlightEvent motionEvent,
        string fingerprint) =>
        state.LastProcessedEventOccurredAtUtc == motionEvent.OccurredAtUtc
        && string.Equals(state.LastProcessedEventFingerprint, fingerprint, StringComparison.Ordinal);

    internal static bool IsWithinCooldown(
        CaptureOptions options,
        MotionHighlightState state,
        MotionHighlightEvent motionEvent) =>
        state.LastHighlightCapturedAtUtc is { } lastHighlight
        && motionEvent.OccurredAtUtc < lastHighlight.AddSeconds(options.MotionCooldownSeconds);
}
