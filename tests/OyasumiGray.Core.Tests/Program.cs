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
    ("移行時間と復帰時刻を固定する", FixedSchedule),
    ("読み込んだ設定に固定値を使う", LoadedSettingsUseFixedSchedule),
    ("競合を検出したら終了時に他者の値を消さない", ConflictRelease),
    ("起動時に他者の色変更と読み取りの失敗を見つける", InitialCheck),
    ("競合と異常から再開する", RetryAfterConflictAndFault),
    ("画面構成の変更後に自分の効果を照合する", DisplayChangeReconcile),
    ("強制解除は他者の色変更も消す", ForceClear),
    ("色変更の失敗を異常として止める", EffectFailure),
    ("一時解除を保存し壊れた入力を拒否", PauseStorage),
    ("壊れた設定ファイルを拒否する", BrokenSettingsFiles),
    ("OS から読んだ行列を検証する", MatrixFromArray),
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

static AppSettings CustomSchedule() => new(true, new TimeOnly(22, 0), 60, new TimeOnly(7, 0));

static void SettingsValidation()
{
    Check(CustomSchedule().Validate() is null, "default should be valid");
    Check(new AppSettings(true, new TimeOnly(22, 0), 0, new TimeOnly(7, 0)).Validate() is not null, "zero fade");
    Check(new AppSettings(true, new TimeOnly(22, 0), 60, new TimeOnly(22, 0)).Validate() is not null, "same endpoints");
    Check(new AppSettings(true, new TimeOnly(22, 0), 90, new TimeOnly(23, 0)).Validate() is not null, "fade longer than interval");
}

static void NightSchedule()
{
    var zone = TimeZoneInfo.Utc;
    Check(Schedule.Evaluate(CustomSchedule(), Utc(26, 21), zone, null).Ratio == 0, "before start");
    Check(Schedule.Evaluate(CustomSchedule(), Utc(26, 22, 30), zone, null).Ratio == .5, "mid fade");
    Check(Schedule.Evaluate(CustomSchedule(), Utc(26, 23), zone, null).Ratio == 1, "fade end");
    Check(Schedule.Evaluate(CustomSchedule(), Utc(27, 7), zone, null).Ratio == 0, "restore");
}

static void CatchUp()
{
    var snapshot = Schedule.Evaluate(CustomSchedule(), Utc(27, 1), TimeZoneInfo.Utc, null);
    Check(snapshot.Ratio == 1 && snapshot.NextBoundary == Utc(27, 7), "cross midnight");
}

static void PauseExpiry()
{
    var until = Utc(26, 22, 45);
    Check(Schedule.Evaluate(CustomSchedule(), Utc(26, 22, 30), TimeZoneInfo.Utc, until).Ratio == 0, "pause active");
    Check(Schedule.Evaluate(CustomSchedule(), until, TimeZoneInfo.Utc, until).Ratio == .75, "pause expired");
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
        Check(!store.HasSettings && store.LoadSettings() == AppSettings.Default, "initial defaults");
        var saved = AppSettings.ForStart(true, new TimeOnly(22, 0));
        store.SaveSettingsAsync(saved).GetAwaiter().GetResult();
        Check(store.HasSettings && store.LoadSettings() == saved, "round trip");
        File.WriteAllText(Path.Combine(path, "settings.json"), "{\"schemaVersion\":2}");
        try { store.LoadSettings(); throw new Exception("unsupported schema accepted"); }
        catch (InvalidDataException) { }
        File.WriteAllText(Path.Combine(path, "settings.json"), "{\"schemaVersion\":1,\"startTime\":\"22:00\",\"fadeDurationMinutes\":60,\"restoreTime\":\"07:00\"}");
        try { store.LoadSettings(); throw new Exception("missing enabled accepted"); }
        catch (InvalidDataException) { }
    }
    finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
}

static void LoadedSettingsUseFixedSchedule()
{
    var directory = Path.Combine(Path.GetTempPath(), "oyasumi-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        var store = new SettingsStore(directory);
        var path = Path.Combine(directory, "settings.json");
        File.WriteAllText(path, """{"schemaVersion":1,"enabled":true,"startTime":"21:30","fadeDurationMinutes":30,"restoreTime":"07:00"}""");
        Check(store.LoadSettings() == AppSettings.ForStart(true, new TimeOnly(21, 30)), "old fade and restore are replaced");
        File.WriteAllText(path, """{"schemaVersion":1,"enabled":true,"startTime":"03:30","fadeDurationMinutes":60,"restoreTime":"07:00"}""");
        var rejected = false;
        try { store.LoadSettings(); }
        catch (InvalidDataException) { rejected = true; }
        Check(rejected, "a start that is invalid with the fixed schedule is rejected");
    }
    finally { Directory.Delete(directory, true); }
}

static void FixedSchedule()
{
    var settings = AppSettings.ForStart(true, new TimeOnly(21, 30));
    Check(settings == new AppSettings(true, new TimeOnly(21, 30), 60, new TimeOnly(4, 0)), "fixed fade and restore");
    Check(AppSettings.Default == AppSettings.ForStart(false, new TimeOnly(22, 0)), "default start");
    Check(AppSettings.ForStart(true, new TimeOnly(2, 59)).Validate() is null, "start before the last valid minute");
    foreach (var start in new[] { new TimeOnly(3, 0), new TimeOnly(3, 30), new TimeOnly(4, 0) })
        Check(AppSettings.ForStart(true, start).Validate() == "開始時刻は 03:00 より前か、04:00 より後にしてください。", $"start {start}");
    Check(AppSettings.ForStart(true, new TimeOnly(4, 1)).Validate() is null, "start just after restore");
    var night = AppSettings.ForStart(true, new TimeOnly(22, 0));
    Check(Schedule.Evaluate(night, new DateTimeOffset(2026, 1, 2, 3, 59, 59, TimeSpan.Zero), TimeZoneInfo.Utc, null).Phase == SchedulePhase.Gray, "gray until restore");
    var restored = Schedule.Evaluate(night, new DateTimeOffset(2026, 1, 2, 4, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc, null);
    Check(restored.Phase == SchedulePhase.Waiting && restored.NextBoundary == new DateTimeOffset(2026, 1, 2, 22, 0, 0, TimeSpan.Zero), "color at 04:00, next start tonight");
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

static void InitialCheck()
{
    var clean = new EffectController(new FakeEffect());
    clean.CheckInitial();
    Check(clean.State == EffectState.Idle, "no effect at start");

    var other = new FakeEffect { Current = ColorMatrix.ForRatio(.3) };
    var controller = new EffectController(other);
    controller.CheckInitial();
    Check(controller.State == EffectState.Conflict, "another app's effect at start");
    controller.Apply(.5);
    Check(other.Writes == 0 && other.Current.NearlyEquals(ColorMatrix.ForRatio(.3)), "no write while in conflict");

    var broken = new EffectController(new FakeEffect { ReadError = new InvalidOperationException("boom") });
    broken.CheckInitial();
    Check(broken.State == EffectState.Fault && broken.Error == "InvalidOperationException: boom", "read failure at start");
}

static void RetryAfterConflictAndFault()
{
    var fake = new FakeEffect { Current = ColorMatrix.ForRatio(.3) };
    var controller = new EffectController(fake);
    controller.Apply(.5);
    controller.Retry();
    Check(controller.State == EffectState.Conflict && fake.Writes == 0, "retry keeps the conflict while the other effect remains");
    fake.Current = ColorMatrix.Identity;
    controller.Retry();
    Check(controller.State == EffectState.Idle, "retry after the other effect is gone");
    controller.Apply(.5);
    Check(controller.State == EffectState.Applied && fake.Writes == 1, "apply after retry");
    controller.Retry();
    Check(controller.State == EffectState.Applied && controller.HasOwnedEffect, "retry does nothing while applied");

    var failing = new FakeEffect { ReadError = new InvalidOperationException("boom") };
    var faulted = new EffectController(failing);
    faulted.Apply(.5);
    Check(faulted.State == EffectState.Fault, "read failure while applying");
    failing.ReadError = null;
    faulted.Retry();
    Check(faulted.State == EffectState.Idle && faulted.Error is null, "retry clears the fault");
}

static void DisplayChangeReconcile()
{
    var fake = new FakeEffect();
    var controller = new EffectController(fake);
    controller.Apply(.5);
    controller.ReconcileAfterDisplayChange();
    Check(controller.State == EffectState.Applied && controller.HasOwnedEffect, "own effect survives the change");

    fake.Current = ColorMatrix.Identity;
    controller.ReconcileAfterDisplayChange();
    Check(controller.State == EffectState.Idle && !controller.HasOwnedEffect, "effect reset by the OS");
    controller.Apply(.5);
    Check(controller.State == EffectState.Applied && fake.Writes == 2, "apply again after the reset");

    fake.Current = ColorMatrix.ForRatio(.25);
    controller.ReconcileAfterDisplayChange();
    Check(controller.State == EffectState.Conflict && !controller.HasOwnedEffect, "another app's effect after the change");
    fake.Current = ColorMatrix.Identity;
    controller.ReconcileAfterDisplayChange();
    Check(controller.State == EffectState.Conflict, "a conflict is cleared only by retry");

    var idle = new EffectController(new FakeEffect { Current = ColorMatrix.ForRatio(.25) });
    idle.ReconcileAfterDisplayChange();
    Check(idle.State == EffectState.Conflict, "an effect that this app never wrote");
}

static void ForceClear()
{
    var fake = new FakeEffect { Current = ColorMatrix.ForRatio(.3) };
    var controller = new EffectController(fake);
    controller.CheckInitial();
    controller.ForceClear();
    Check(fake.Current.IsIdentity && fake.Writes == 1 && controller.State == EffectState.Idle, "clears another app's effect");

    var failing = new FakeEffect { WriteError = new InvalidOperationException("denied") };
    var faulted = new EffectController(failing);
    faulted.ForceClear();
    Check(faulted.State == EffectState.Fault && faulted.Error == "InvalidOperationException: denied", "write failure while clearing");
}

static void EffectFailure()
{
    var fake = new FakeEffect { WriteError = new InvalidOperationException("denied") };
    var controller = new EffectController(fake);
    controller.Apply(.5);
    Check(controller.State == EffectState.Fault && !controller.HasOwnedEffect, "write failure while applying");
    fake.WriteError = null;
    controller.Apply(.5);
    Check(controller.State == EffectState.Fault && fake.Writes == 0, "no write while in fault");

    var releasing = new FakeEffect();
    var owner = new EffectController(releasing);
    owner.Apply(.5);
    releasing.WriteError = new InvalidOperationException("denied");
    owner.Release();
    // The exit dialog keeps asking while the app still owns an effect.
    Check(owner.State == EffectState.Fault && owner.HasOwnedEffect, "release failure keeps ownership");

    var reading = new FakeEffect();
    var reconciling = new EffectController(reading);
    reconciling.Apply(.5);
    reading.ReadError = new InvalidOperationException("boom");
    reconciling.ReconcileAfterDisplayChange();
    Check(reconciling.State == EffectState.Fault, "read failure after a display change");
    reconciling.Retry();
    Check(reconciling.State == EffectState.Fault && reconciling.Error == "InvalidOperationException: boom", "read failure while retrying");
}

static void PauseStorage()
{
    var directory = Path.Combine(Path.GetTempPath(), "oyasumi-test-" + Guid.NewGuid().ToString("N"));
    try
    {
        var store = new SettingsStore(directory);
        Check(store.LoadPause() is null, "no pause file");
        var until = Utc(26, 22, 45);
        store.SavePauseAsync(until).GetAwaiter().GetResult();
        Check(store.LoadPause() == until, "round trip");
        store.SavePauseAsync(null).GetAwaiter().GetResult();
        Check(store.LoadPause() is null, "pause cleared");

        var rejected = false;
        try { store.SavePauseAsync(new DateTimeOffset(2026, 9, 26, 22, 45, 0, TimeSpan.FromHours(9))).GetAwaiter().GetResult(); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "a time that is not UTC is not saved");

        foreach (var json in new[]
        {
            """{"schemaVersion":1,"pausedUntilUtc":"2026-09-26T22:45:00+09:00"}""",
            """{"schemaVersion":1}""",
            """{"schemaVersion":2,"pausedUntilUtc":null}""",
            "{",
        })
        {
            File.WriteAllText(store.PausePath, json);
            var broken = false;
            try { store.LoadPause(); }
            catch (InvalidDataException) { broken = true; }
            Check(broken, $"broken pause file accepted: {json}");
        }
    }
    finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}

static void BrokenSettingsFiles()
{
    var directory = Path.Combine(Path.GetTempPath(), "oyasumi-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        var store = new SettingsStore(directory);
        foreach (var json in new[]
        {
            "{",
            """{"schemaVersion":1,"enabled":true,"startTime":"25:00","fadeDurationMinutes":60,"restoreTime":"04:00"}""",
            // A valid file made larger than the 16 KiB limit.
            "{\"schemaVersion\":1,\"enabled\":true,\"startTime\":\"22:00\",\"fadeDurationMinutes\":60,\"restoreTime\":\"04:00\",\"padding\":\"" + new string('x', 16 * 1024) + "\"}",
        })
        {
            File.WriteAllText(store.SettingsPath, json);
            var broken = false;
            try { store.LoadSettings(); }
            catch (InvalidDataException) { broken = true; }
            Check(broken, $"broken settings file accepted: {json[..Math.Min(json.Length, 60)]}");
        }
    }
    finally { Directory.Delete(directory, true); }
}

static void MatrixFromArray()
{
    var values = ColorMatrix.Identity.ToArray();
    var matrix = ColorMatrix.FromArray(values);
    Check(matrix.IsIdentity, "identity from the OS");
    values[0] = 0;
    Check(matrix.IsIdentity, "the matrix keeps its own copy");
    matrix.ToArray()[0] = 0;
    Check(matrix.IsIdentity, "the returned array is a copy");
    foreach (var bad in new[] { new float[24], Enumerable.Repeat(float.NaN, 25).ToArray(), Enumerable.Repeat(float.PositiveInfinity, 25).ToArray() })
    {
        var rejected = false;
        try { ColorMatrix.FromArray(bad); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, $"invalid matrix accepted: length {bad.Length}, first {bad.FirstOrDefault()}");
    }
}

sealed class FakeEffect : IDisplayEffect
{
    public ColorMatrix Current { get; set; } = ColorMatrix.Identity;
    public int Writes { get; private set; }
    public Exception? ReadError { get; set; }
    public Exception? WriteError { get; set; }
    public ColorMatrix Read() => ReadError is null ? Current : throw ReadError;
    public void Write(ColorMatrix matrix)
    {
        if (WriteError is not null) throw WriteError;
        Current = matrix;
        Writes++;
    }
}
