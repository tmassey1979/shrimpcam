using ShrimpCam.Core.Captures;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Tests.Captures;

public sealed class MotionHighlightPlannerTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_returns_below_threshold_when_score_is_too_low()
    {
        var plan = MotionHighlightPlanner.Evaluate(
            CreateOptions(),
            new MotionHighlightEvent(new DateTimeOffset(2026, 06, 25, 00, 10, 00, TimeSpan.Zero), 0.39d, "event-01"),
            MotionHighlightState.Empty);

        plan.Outcome.Should().Be(MotionHighlightOutcome.BelowThreshold);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_returns_disabled_when_motion_highlights_are_not_enabled()
    {
        var plan = MotionHighlightPlanner.Evaluate(
            new CaptureOptions { MotionHighlightsEnabled = false, MotionThreshold = 0.4d, MotionCooldownSeconds = 300 },
            new MotionHighlightEvent(new DateTimeOffset(2026, 06, 25, 00, 10, 00, TimeSpan.Zero), 0.88d, "event-disabled"),
            MotionHighlightState.Empty);

        plan.Outcome.Should().Be(MotionHighlightOutcome.Disabled);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_suppresses_duplicate_event_with_same_fingerprint_and_timestamp()
    {
        var occurredAtUtc = new DateTimeOffset(2026, 06, 25, 00, 10, 00, TimeSpan.Zero);
        var motionEvent = new MotionHighlightEvent(occurredAtUtc, 0.75d, "event-02");
        var state = new MotionHighlightState(
            LastHighlightCapturedAtUtc: null,
            LastProcessedEventFingerprint: "event-02",
            LastProcessedEventOccurredAtUtc: occurredAtUtc);

        var plan = MotionHighlightPlanner.Evaluate(CreateOptions(), motionEvent, state);

        plan.Outcome.Should().Be(MotionHighlightOutcome.SuppressedDuplicate);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_suppresses_highlight_when_event_occurs_inside_cooldown_window()
    {
        var state = new MotionHighlightState(
            LastHighlightCapturedAtUtc: new DateTimeOffset(2026, 06, 25, 00, 10, 00, TimeSpan.Zero),
            LastProcessedEventFingerprint: "event-02",
            LastProcessedEventOccurredAtUtc: new DateTimeOffset(2026, 06, 25, 00, 10, 00, TimeSpan.Zero));

        var plan = MotionHighlightPlanner.Evaluate(
            CreateOptions(),
            new MotionHighlightEvent(new DateTimeOffset(2026, 06, 25, 00, 12, 00, TimeSpan.Zero), 0.9d, "event-03"),
            state);

        plan.Outcome.Should().Be(MotionHighlightOutcome.SuppressedByCooldown);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_allows_capture_when_threshold_is_met_outside_cooldown()
    {
        var state = new MotionHighlightState(
            LastHighlightCapturedAtUtc: new DateTimeOffset(2026, 06, 25, 00, 00, 00, TimeSpan.Zero),
            LastProcessedEventFingerprint: "event-00",
            LastProcessedEventOccurredAtUtc: new DateTimeOffset(2026, 06, 25, 00, 00, 00, TimeSpan.Zero));

        var plan = MotionHighlightPlanner.Evaluate(
            CreateOptions(),
            new MotionHighlightEvent(new DateTimeOffset(2026, 06, 25, 00, 10, 00, TimeSpan.Zero), 0.85d, "event-04"),
            state);

        plan.Outcome.Should().Be(MotionHighlightOutcome.Captured);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_builds_fingerprint_from_timestamp_and_score_when_event_id_is_missing()
    {
        var occurredAtUtc = new DateTimeOffset(2026, 06, 25, 00, 10, 00, TimeSpan.Zero);
        var plan = MotionHighlightPlanner.Evaluate(
            CreateOptions(),
            new MotionHighlightEvent(occurredAtUtc, 0.75d),
            MotionHighlightState.Empty);

        plan.EventFingerprint.Should().Be("2026-06-25T00:10:00.0000000Z|0.7500");
    }

    private static CaptureOptions CreateOptions() =>
        new()
        {
            MotionHighlightsEnabled = true,
            MotionThreshold = 0.4d,
            MotionCooldownSeconds = 300,
        };
}
