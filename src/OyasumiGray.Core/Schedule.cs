namespace OyasumiGray.Core;

public enum SchedulePhase { Disabled, Waiting, Fading, Gray, Paused }

public sealed record ScheduleSnapshot(double Ratio, SchedulePhase Phase, DateTimeOffset NextBoundary)
{
    public DateTimeOffset? RestoreBoundary { get; init; }
}

public static class Schedule
{
    public static ScheduleSnapshot Evaluate(AppSettings settings, DateTimeOffset now, TimeZoneInfo zone, DateTimeOffset? pauseUntil)
    {
        if (!settings.Enabled) return new(0, SchedulePhase.Disabled, DateTimeOffset.MaxValue);
        if (settings.Validate() is not null) throw new ArgumentException("Invalid schedule", nameof(settings));

        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).Date);
        for (int offset = -1; offset <= 0; offset++)
        {
            var date = localDate.AddDays(offset);
            var start = ResolveLocal(date.ToDateTime(settings.Start), zone);
            var restoreDate = settings.Restore <= settings.Start ? date.AddDays(1) : date;
            var restore = ResolveLocal(restoreDate.ToDateTime(settings.Restore), zone);
            if (restore <= start || now < start || now >= restore) continue;

            var fadeEnd = start.AddMinutes(settings.FadeMinutes);
            if (pauseUntil is { } pause && pause > now)
                return new(0, SchedulePhase.Paused, pause < restore ? pause : restore) { RestoreBoundary = restore };
            if (now < fadeEnd)
                return new(Math.Clamp((now - start).TotalSeconds / (settings.FadeMinutes * 60d), 0, 1), SchedulePhase.Fading,
                    fadeEnd < restore ? fadeEnd : restore) { RestoreBoundary = restore };
            return new(1, SchedulePhase.Gray, restore) { RestoreBoundary = restore };
        }

        for (int offset = 0; offset <= 2; offset++)
        {
            var next = ResolveLocal(localDate.AddDays(offset).ToDateTime(settings.Start), zone);
            if (next > now) return new(0, SchedulePhase.Waiting, next);
        }
        throw new InvalidOperationException("Next schedule boundary was not found.");
    }

    public static DateTimeOffset ResolveLocal(DateTime local, TimeZoneInfo zone)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
        {
            var before = local.AddMinutes(-1);
            var after = local.AddMinutes(1);
            while (zone.IsInvalidTime(before)) before = before.AddMinutes(-1);
            while (zone.IsInvalidTime(after)) after = after.AddMinutes(1);
            local = local.Add(zone.GetUtcOffset(after) - zone.GetUtcOffset(before));
        }
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }
}
