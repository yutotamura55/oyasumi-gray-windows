namespace OyasumiGray.Core;

public sealed record AppSettings(bool Enabled, TimeOnly Start, int FadeMinutes, TimeOnly Restore)
{
    public static AppSettings Default { get; } = new(false, new TimeOnly(22, 0), 60, new TimeOnly(7, 0));

    public string? Validate()
    {
        if (FadeMinutes is < 1 or > 360) return "移行時間は1〜360分にしてください。";
        if (Start == Restore) return "開始時刻と復帰時刻は別にしてください。";
        int minutes = (int)(Restore.ToTimeSpan() - Start.ToTimeSpan()).TotalMinutes;
        if (minutes <= 0) minutes += 24 * 60;
        if (FadeMinutes >= minutes) return "移行時間は開始から復帰までの時間より短くしてください。";
        return null;
    }
}
