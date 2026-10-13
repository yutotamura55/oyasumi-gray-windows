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

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
