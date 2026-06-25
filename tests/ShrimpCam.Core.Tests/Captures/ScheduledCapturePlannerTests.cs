using ShrimpCam.Core.Captures;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Tests.Captures;

public sealed class ScheduledCapturePlannerTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Disabled_schedule_returns_disabled_plan()
    {
        var plan = ScheduledCapturePlanner.Evaluate(
            new CaptureOptions
            {
                Enabled = false,
                IntervalMinutes = 5,
                ActiveStartHourUtc = 6,
                ActiveEndHourUtc = 22,
            },
            new DateTimeOffset(2026, 06, 24, 12, 03, 12, TimeSpan.Zero),
            lastProcessedIntervalUtc: null);

        plan.Outcome.Should().Be(ScheduledCaptureOutcome.Disabled);
        plan.NextEligibleIntervalUtc.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Due_interval_inside_active_window_returns_capture_plan()
    {
        var plan = ScheduledCapturePlanner.Evaluate(
            CreateOptions(),
            new DateTimeOffset(2026, 06, 24, 12, 03, 12, TimeSpan.Zero),
            lastProcessedIntervalUtc: null);

        plan.Outcome.Should().Be(ScheduledCaptureOutcome.Captured);
        plan.IntervalStartUtc.Should().Be(new DateTimeOffset(2026, 06, 24, 12, 00, 00, TimeSpan.Zero));
        plan.NextEligibleIntervalUtc.Should().Be(new DateTimeOffset(2026, 06, 24, 12, 05, 00, TimeSpan.Zero));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Interval_outside_active_window_is_marked_as_skipped()
    {
        var plan = ScheduledCapturePlanner.Evaluate(
            CreateOptions(),
            new DateTimeOffset(2026, 06, 24, 23, 03, 12, TimeSpan.Zero),
            lastProcessedIntervalUtc: null);

        plan.Outcome.Should().Be(ScheduledCaptureOutcome.SkippedBySchedule);
        plan.IntervalStartUtc.Should().Be(new DateTimeOffset(2026, 06, 24, 23, 00, 00, TimeSpan.Zero));
        plan.NextEligibleIntervalUtc.Should().Be(new DateTimeOffset(2026, 06, 25, 06, 00, 00, TimeSpan.Zero));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Already_processed_interval_waits_for_next_due_slot()
    {
        var processedInterval = new DateTimeOffset(2026, 06, 24, 12, 00, 00, TimeSpan.Zero);

        var plan = ScheduledCapturePlanner.Evaluate(
            CreateOptions(),
            new DateTimeOffset(2026, 06, 24, 12, 04, 59, TimeSpan.Zero),
            processedInterval);

        plan.Outcome.Should().Be(ScheduledCaptureOutcome.Waiting);
        plan.IntervalStartUtc.Should().Be(processedInterval);
        plan.NextEligibleIntervalUtc.Should().Be(new DateTimeOffset(2026, 06, 24, 12, 05, 00, TimeSpan.Zero));
    }

    private static CaptureOptions CreateOptions() =>
        new()
        {
            Enabled = true,
            IntervalMinutes = 5,
            ActiveStartHourUtc = 6,
            ActiveEndHourUtc = 22,
        };
}
