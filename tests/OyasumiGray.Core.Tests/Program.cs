using OyasumiGray.Core;

var cases = new (string Name, Action Run)[]
{
    ("設定値を検証する", SettingsValidation),
    ("夜間の移行と朝の復帰", NightSchedule),
    ("途中起動と日付越え", CatchUp),
    ("一時解除の期限", PauseExpiry),
    ("夏時間の欠落と重複", DaylightSaving),
    ("行列はグレー化しアルファを保つ", Matrix),
    ("競合時に書き換えない", Conflict),
    ("停止時に自分の効果だけ解除", OwnedRelease),
    ("設定を保存し壊れた入力を拒否", SettingsStorage),
    ("微小な変化で誤って競合にしない", TinyFadeSteps),
    ("競合を検出したら終了時に他者の値を消さない", ConflictRelease),
};

var failures = 0;
foreach (var test in cases)
{
    try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures++; Console.WriteLine($"FAIL {test.Name}: {ex}"); }
}
return failures == 0 ? 0 : 1;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static DateTimeOffset Utc(int day, int hour, int minute = 0) => new(2026, 9, day, hour, minute, 0, TimeSpan.Zero);

static AppSettings Defaults() => new(true, new TimeOnly(22, 0), 60, new TimeOnly(7, 0));

static void SettingsValidation()
{
    Check(Defaults().Validate() is null, "default should be valid");
    Check(new AppSettings(true, new TimeOnly(22, 0), 0, new TimeOnly(7, 0)).Validate() is not null, "zero fade");
    Check(new AppSettings(true, new TimeOnly(22, 0), 60, new TimeOnly(22, 0)).Validate() is not null, "same endpoints");
    Check(new AppSettings(true, new TimeOnly(22, 0), 90, new TimeOnly(23, 0)).Validate() is not null, "fade longer than interval");
}

static void NightSchedule()
{
    var zone = TimeZoneInfo.Utc;
    Check(Schedule.Evaluate(Defaults(), Utc(26, 21), zone, null).Ratio == 0, "before start");
    Check(Schedule.Evaluate(Defaults(), Utc(26, 22, 30), zone, null).Ratio == .5, "mid fade");
    Check(Schedule.Evaluate(Defaults(), Utc(26, 23), zone, null).Ratio == 1, "fade end");
    Check(Schedule.Evaluate(Defaults(), Utc(27, 7), zone, null).Ratio == 0, "restore");
}

static void CatchUp()
{
    var snapshot = Schedule.Evaluate(Defaults(), Utc(27, 1), TimeZoneInfo.Utc, null);
    Check(snapshot.Ratio == 1 && snapshot.NextBoundary == Utc(27, 7), "cross midnight");
}

static void PauseExpiry()
{
    var until = Utc(26, 22, 45);
    Check(Schedule.Evaluate(Defaults(), Utc(26, 22, 30), TimeZoneInfo.Utc, until).Ratio == 0, "pause active");
    Check(Schedule.Evaluate(Defaults(), until, TimeZoneInfo.Utc, until).Ratio == .75, "pause expired");
}

static void DaylightSaving()
{
    var zone = TimeZoneInfo.CreateCustomTimeZone("test-east", TimeSpan.FromHours(-5), "east", "EST", "EDT",
        [TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 2, DayOfWeek.Sunday),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 11, 1, DayOfWeek.Sunday))]);
    Check(Schedule.ResolveLocal(new DateTime(2026, 3, 8, 2, 30, 0), zone).TimeOfDay == TimeSpan.FromHours(3.5), "spring gap shifts by one hour");
    Check(Schedule.ResolveLocal(new DateTime(2026, 11, 1, 1, 30, 0), zone).Offset == TimeSpan.FromHours(-4), "first fall occurrence");
}

static void Matrix()
{
    var half = ColorMatrix.ForRatio(.5);
    Check(half.Values.Length == 25, "matrix size");
    Check(Math.Abs(half.Values[0] - .6063f) < .0001, "red coefficient");
    Check(half.Values[18] == 1 && half.Values[24] == 1, "alpha and constant");
    Check(ColorMatrix.ForRatio(0).IsIdentity, "identity");
    Check(!ColorMatrix.ForRatio(1).IsIdentity, "gray");
}

static void Conflict()
{
    var fake = new FakeEffect { Current = ColorMatrix.ForRatio(1) };
    var controller = new EffectController(fake);
    controller.Apply(.5);
    Check(controller.State == EffectState.Conflict && fake.Writes == 0, "preexisting effect untouched");
}

static void OwnedRelease()
{
    var fake = new FakeEffect();
    var controller = new EffectController(fake);
    controller.Apply(.5);
    Check(fake.Writes == 1, "apply once");
    controller.Release();
    Check(fake.Current.IsIdentity && fake.Writes == 2, "own effect cleared");
    controller.Apply(.5);
    fake.Current = ColorMatrix.ForRatio(.25);
    controller.Release();
    Check(fake.Writes == 3 && controller.State == EffectState.Conflict, "external effect untouched");
}

static void SettingsStorage()
{
    var path = Path.Combine(Path.GetTempPath(), "oyasumi-test-" + Guid.NewGuid().ToString("N"));
    try
    {
        var store = new SettingsStore(path);
        Check(store.LoadSettings() == AppSettings.Default, "initial defaults");
        store.SaveSettingsAsync(Defaults()).GetAwaiter().GetResult();
        Check(store.LoadSettings() == Defaults(), "round trip");
        File.WriteAllText(Path.Combine(path, "settings.json"), "{\"schemaVersion\":2}");
        try { store.LoadSettings(); throw new Exception("unsupported schema accepted"); }
        catch (InvalidDataException) { }
        File.WriteAllText(Path.Combine(path, "settings.json"), "{\"schemaVersion\":1,\"startTime\":\"22:00\",\"fadeDurationMinutes\":60,\"restoreTime\":\"07:00\"}");
        try { store.LoadSettings(); throw new Exception("missing enabled accepted"); }
        catch (InvalidDataException) { }
    }
    finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
}

static void TinyFadeSteps()
{
    var fake = new FakeEffect();
    var controller = new EffectController(fake);
    controller.Apply(.5);
    for (int i = 1; i <= 300; i++) controller.Apply(.5 + i * .000001);
    Check(controller.State == EffectState.Applied && fake.Writes >= 2, "small step must keep true last written matrix");
}

static void ConflictRelease()
{
    var fake = new FakeEffect();
    var controller = new EffectController(fake);
    controller.Apply(.5);
    fake.Current = ColorMatrix.ForRatio(.25);
    controller.Release();
    Check(controller.State == EffectState.Conflict && !controller.HasOwnedEffect, "ownership relinquished");
    controller.Release();
    Check(fake.Current.NearlyEquals(ColorMatrix.ForRatio(.25)), "external matrix stays");
}

sealed class FakeEffect : IDisplayEffect
{
    public ColorMatrix Current { get; set; } = ColorMatrix.Identity;
    public int Writes { get; private set; }
    public ColorMatrix Read() => Current;
    public void Write(ColorMatrix matrix) { Current = matrix; Writes++; }
}
