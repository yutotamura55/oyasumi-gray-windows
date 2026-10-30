namespace OyasumiGray.Core;

public sealed record AppSettings(bool Enabled, TimeOnly Start, int FadeMinutes, TimeOnly Restore)
{
    private const int FixedFadeMinutes = 60;
    private static readonly TimeOnly FixedRestore = new(4, 0);

    public static AppSettings Default { get; } = ForStart(false, new TimeOnly(22, 0));

    public static AppSettings ForStart(bool enabled, TimeOnly start) => new(enabled, start, FixedFadeMinutes, FixedRestore);

    public string? Validate()
    {
        if (FadeMinutes is < 1 or > 360) return "移行時間は1〜360分にしてください。";
        int minutes = (int)(Restore.ToTimeSpan() - Start.ToTimeSpan()).TotalMinutes;
        if (minutes <= 0) minutes += 24 * 60;
        if (Start == Restore || FadeMinutes >= minutes)
            return $"開始時刻は {Restore.AddMinutes(-FadeMinutes):HH:mm} より前か、{Restore:HH:mm} より後にしてください。";
        return null;
    }
}
