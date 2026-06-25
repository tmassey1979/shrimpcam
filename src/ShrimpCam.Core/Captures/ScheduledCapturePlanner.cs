using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Captures;

public static class ScheduledCapturePlanner
{
    public static ScheduledCapturePlan Evaluate(
        CaptureOptions options,
        DateTimeOffset now,
        DateTimeOffset? lastProcessedIntervalUtc)
    {
        ArgumentNullException.ThrowIfNull(options);

        var intervalStartUtc = AlignToIntervalStartUtc(options, now);
        var nextEligibleIntervalUtc = GetNextEligibleIntervalAfter(options, intervalStartUtc);

        if (!options.Enabled)
        {
            return new ScheduledCapturePlan(
                ScheduledCaptureOutcome.Disabled,
                intervalStartUtc,
                NextEligibleIntervalUtc: null);
        }

        if (lastProcessedIntervalUtc is not null && lastProcessedIntervalUtc.Value >= intervalStartUtc)
        {
            return new ScheduledCapturePlan(
                ScheduledCaptureOutcome.Waiting,
                intervalStartUtc,
                nextEligibleIntervalUtc);
        }

        return IsWithinActiveWindow(options, intervalStartUtc)
            ? new ScheduledCapturePlan(
                ScheduledCaptureOutcome.Captured,
                intervalStartUtc,
                nextEligibleIntervalUtc)
            : new ScheduledCapturePlan(
                ScheduledCaptureOutcome.SkippedBySchedule,
                intervalStartUtc,
                nextEligibleIntervalUtc);
    }

    public static DateTimeOffset AlignToIntervalStartUtc(CaptureOptions options, DateTimeOffset instant)
    {
        ArgumentNullException.ThrowIfNull(options);

        var utcInstant = instant.ToUniversalTime();
        var alignedMinutes = utcInstant.Minute - (utcInstant.Minute % options.IntervalMinutes);

        return new DateTimeOffset(
            utcInstant.Year,
            utcInstant.Month,
            utcInstant.Day,
            utcInstant.Hour,
            alignedMinutes,
            0,
            TimeSpan.Zero);
    }

    public static DateTimeOffset? GetNextEligibleIntervalAfter(CaptureOptions options, DateTimeOffset intervalStartUtc)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return null;
        }

        var candidate = intervalStartUtc.ToUniversalTime().AddMinutes(options.IntervalMinutes);
        for (var attempts = 0; attempts < 1441; attempts++)
        {
            if (IsWithinActiveWindow(options, candidate))
            {
                return candidate;
            }

            candidate = candidate.AddMinutes(options.IntervalMinutes);
        }

        throw new InvalidOperationException("Unable to find the next eligible scheduled capture interval within 24 hours.");
    }

    public static bool IsWithinActiveWindow(CaptureOptions options, DateTimeOffset intervalStartUtc)
    {
        ArgumentNullException.ThrowIfNull(options);

        var hour = intervalStartUtc.ToUniversalTime().Hour;
        var start = options.ActiveStartHourUtc;
        var end = options.ActiveEndHourUtc % 24;

        if (start == end)
        {
            return true;
        }

        if (start < end)
        {
            return hour >= start && hour < end;
        }

        return hour >= start || hour < end;
    }
}
