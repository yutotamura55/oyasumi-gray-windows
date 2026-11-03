using OyasumiGray.Core;
using OyasumiGray.Windows;

Application.EnableVisualStyles();

using var form = new SettingsForm(AppSettings.Default);
form.MinimumSize = Size.Empty;
form.ClientSize = new Size(440, 250);
form.Show();
Application.DoEvents();

const string status = "現在の状態: 移行中・白黒度50%\n次の切替: 09/27 23:00";
form.SetStatus(status);
Application.DoEvents();
if (!form.VerticalScroll.Visible) throw new Exception("設定画面がスクロール可能でない");

form.AutoScrollPosition = new Point(0, 100);
Application.DoEvents();
var scrollY = form.VerticalScroll.Value;
if (scrollY == 0) throw new Exception("スクロール位置を設定できない");

var changes = 0;
form.Status.TextChanged += (_, _) => changes++;
form.SetStatus(status);
Application.DoEvents();

if (changes != 0) throw new Exception($"同じ状態で {changes} 回ラベルが更新された");
if (form.VerticalScroll.Value != scrollY)
    throw new Exception($"スクロール位置が {scrollY} から {form.VerticalScroll.Value} に移動した");

Console.WriteLine("PASS 同じ状態の再通知でスクロール位置を保つ");

form.SetSettings(new AppSettings(true, new TimeOnly(21, 30), 30, new TimeOnly(7, 0)));
Check(form.ReadSettings() == AppSettings.ForStart(true, new TimeOnly(21, 30)), "画面からは開始時刻とオン・オフだけを読む");
Check(form.Preview.Text.Contains("21:30") && form.Preview.Text.Contains("22:30") && form.Preview.Text.Contains("04:00"), "予告は固定の時刻を示す");
Console.WriteLine("PASS 設定画面は開始時刻とオン・オフだけを扱う");

// Close() raises FormClosing with CloseReason.UserClosing, the same reason as the title bar's close button.
// Sending the button's own window message is not tested.
form.Close();
Application.DoEvents();
Check(!form.IsDisposed && !form.Visible, "利用者が閉じる操作では画面を隠すだけで常駐を続ける");
form.Show();
form.CloseForExit();
Application.DoEvents();
Check(form.IsDisposed, "終了するときは画面を閉じる");
Console.WriteLine("PASS 設定画面は利用者が閉じても隠すだけで、終了時だけ閉じる");

// The real Run key is never touched: these checks use a temporary key under the test's own name.
var testKey = $@"Software\OyasumiGray.Tests\{Guid.NewGuid():N}";
try
{
    var thisCopy = new StartupRegistration(testKey, @"C:\apps\new\OyasumiGray.Windows.exe");
    Check(ReadRegistration(testKey) is null, "初期状態は未登録");
    thisCopy.Set(true);
    Check(ReadRegistration(testKey) == "\"C:\\apps\\new\\OyasumiGray.Windows.exe\"", "今の実行ファイルを引用符付きで登録する");
    new StartupRegistration(testKey, @"C:\apps\other\OyasumiGray.Windows.exe").Set(true);
    Check(ReadRegistration(testKey) == "\"C:\\apps\\other\\OyasumiGray.Windows.exe\"", "最後に起動したコピーが登録される");
    thisCopy.Set(false);
    Check(ReadRegistration(testKey) is null, "オフにすると登録先によらず削除する");
    thisCopy.Set(false);
    var tooLong = new StartupRegistration(testKey, "C:\\" + new string('a', 260) + ".exe");
    var rejected = false;
    try { tooLong.Set(true); }
    catch (InvalidOperationException) { rejected = true; }
    Check(rejected && ReadRegistration(testKey) is null, "長すぎるパスは登録しない");
}
finally { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\OyasumiGray.Tests", throwOnMissingSubKey: false); }
Console.WriteLine("PASS ログオン起動の登録を書き込み、上書きし、削除する");

var gray = new ScheduleSnapshot(1, SchedulePhase.Gray, DateTimeOffset.MaxValue);
var conflict = TrayStatusPresentation.Create(
    sessionAvailable: true, effectState: EffectState.Conflict, effectError: null,
    settingsProblem: true, manualHold: true, snapshot: gray);
Check(conflict.FormStatusText == "現在の状態: 競合のため停止。ほかの画面の色変更を確認してください。", "競合を保存エラーや手動停止より優先する");
Check(!conflict.CanPause && conflict.CanResume && conflict.CanForceClear, "競合中の操作可否");
Console.WriteLine("PASS 競合は保存エラーや手動停止より優先される");

var locked = TrayStatusPresentation.Create(
    sessionAvailable: false, effectState: EffectState.Conflict, effectError: null,
    settingsProblem: true, manualHold: true, snapshot: gray);
Check(locked.FormStatusText == "現在の状態: ロックまたはスリープ中", "ロック中は競合よりロックを表示する");
Check(!locked.CanPause && !locked.CanResume && locked.CanForceClear, "ロック中の操作可否");
Console.WriteLine("PASS ロック中は競合よりロックを表示する");

var fault = TrayStatusPresentation.Create(
    sessionAvailable: true, effectState: EffectState.Fault, effectError: "API error",
    settingsProblem: true, manualHold: false, snapshot: gray);
Check(fault.FormStatusText == "現在の状態: 適用できません: API error", "色変更の異常を設定エラーより優先する");
Check(!fault.CanPause && fault.CanResume && !fault.CanForceClear, "色変更の異常時の操作可否");
Console.WriteLine("PASS 色変更の異常は設定エラーより優先される");

var settingsProblem = TrayStatusPresentation.Create(
    sessionAvailable: true, effectState: EffectState.Idle, effectError: null,
    settingsProblem: true, manualHold: true, snapshot: gray);
Check(settingsProblem.FormStatusText == "現在の状態: 設定を読み取れません。確認して保存してください。", "設定エラーを手動停止より優先する");
Check(!settingsProblem.CanPause && settingsProblem.CanResume, "設定エラーと手動停止が重なったときの操作可否");
Console.WriteLine("PASS 設定エラーは手動停止より優先される");

var manualHold = TrayStatusPresentation.Create(
    sessionAvailable: true, effectState: EffectState.Idle, effectError: null,
    settingsProblem: false, manualHold: true, snapshot: gray);
Check(manualHold.FormStatusText == "現在の状態: 手動で解除しました。自動切替は停止中です。", "手動停止の表示");
Check(!manualHold.CanPause && manualHold.CanResume, "手動停止中の操作可否");
Console.WriteLine("PASS 手動停止中の表示と操作可否");

var paused = new ScheduleSnapshot(0, SchedulePhase.Paused, DateTimeOffset.MaxValue);
var pausedStatus = TrayStatusPresentation.Create(
    sessionAvailable: true, effectState: EffectState.Idle, effectError: null,
    settingsProblem: false, manualHold: false, snapshot: paused);
Check(pausedStatus.FormStatusText == "現在の状態: 一時解除中", "一時解除中の表示");
Check(pausedStatus.CanPause && pausedStatus.CanResume && !pausedStatus.CanForceClear, "一時解除中の操作可否");
Console.WriteLine("PASS 一時解除中の表示と操作可否");

var fading = new ScheduleSnapshot(.5, SchedulePhase.Fading, DateTimeOffset.MaxValue);
var fadingStatus = TrayStatusPresentation.Create(
    sessionAvailable: true, effectState: EffectState.Applied, effectError: null,
    settingsProblem: false, manualHold: false, snapshot: fading);
Check(fadingStatus.FormStatusText == "現在の状態: 移行中・白黒度50%", "移行中の表示");
Check(fadingStatus.MenuStatusText == "移行中・白黒度50%", "メニューの短い状態表示");
Check(fadingStatus.CanPause && !fadingStatus.CanResume && !fadingStatus.CanForceClear, "移行中の操作可否");
Console.WriteLine("PASS 移行中の表示と操作可否");

var grayStatus = TrayStatusPresentation.Create(
    sessionAvailable: true, effectState: EffectState.Applied, effectError: null,
    settingsProblem: false, manualHold: false, snapshot: gray);
Check(grayStatus.FormStatusText == "現在の状態: 白黒表示中", "白黒表示中の表示");
Check(grayStatus.CanPause && !grayStatus.CanResume && !grayStatus.CanForceClear, "白黒表示中の操作可否");
var beforeFirstEvaluation = TrayStatusPresentation.Create(
    sessionAvailable: true, effectState: EffectState.Idle, effectError: null,
    settingsProblem: false, manualHold: false, snapshot: null);
Check(beforeFirstEvaluation.FormStatusText == "現在の状態: 開始前" && !beforeFirstEvaluation.CanPause, "時刻を計算する前の表示");
Console.WriteLine("PASS 白黒表示中と、時刻を計算する前の表示");

var waiting = new ScheduleSnapshot(0, SchedulePhase.Waiting, DateTimeOffset.UtcNow.AddHours(1));
var waitingStatus = TrayStatusPresentation.Create(
    sessionAvailable: true, effectState: EffectState.Idle, effectError: null,
    settingsProblem: false, manualHold: false, snapshot: waiting);
Check(waitingStatus.FormStatusText.Contains("\n次の切替: "), "開始前に次の切替を表示する");
var disabled = waiting with { Phase = SchedulePhase.Disabled };
var disabledStatus = TrayStatusPresentation.Create(
    sessionAvailable: true, effectState: EffectState.Idle, effectError: null,
    settingsProblem: false, manualHold: false, snapshot: disabled);
Check(disabledStatus.FormStatusText == "現在の状態: 自動切替はオフ", "無効化中は次の切替を表示しない");
Console.WriteLine("PASS 次の切替時刻の表示条件");

Check(AutoSwitch.FindBlock(true, EffectState.Applied, false, false) == AutoSwitchBlock.None, "止める理由がなければ自動適用する");
Check(AutoSwitch.FindBlock(true, EffectState.Idle, false, false) == AutoSwitchBlock.None, "未適用でも止める理由がなければ自動適用する");
Check(AutoSwitch.FindBlock(false, EffectState.Applied, false, false) == AutoSwitchBlock.SessionUnavailable, "ロック中は自動適用しない");
Check(AutoSwitch.FindBlock(true, EffectState.Fault, false, false) == AutoSwitchBlock.Fault, "色変更の異常時は自動適用しない");
Check(AutoSwitch.FindBlock(true, EffectState.Idle, true, false) == AutoSwitchBlock.SettingsProblem, "設定エラー時は自動適用しない");
Check(AutoSwitch.FindBlock(true, EffectState.Idle, false, true) == AutoSwitchBlock.ManualHold, "手動停止中は自動適用しない");
Console.WriteLine("PASS 止める理由があるときは自動適用しない");

static string? ReadRegistration(string keyPath)
{
    using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath);
    return key?.GetValue("OyasumiGray.Windows") as string;
}

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
