using OyasumiGray.Core;

namespace OyasumiGray.Windows;

// Why automatic switching is stopped. None means the schedule is applied.
internal enum AutoSwitchBlock { None, SessionUnavailable, Conflict, Fault, SettingsProblem, ManualHold }

internal static class AutoSwitch
{
    // Checked in display priority: when several reasons apply, the first one is shown.
    internal static AutoSwitchBlock FindBlock(bool sessionAvailable, EffectState effectState, bool settingsProblem, bool manualHold)
    {
        if (!sessionAvailable) return AutoSwitchBlock.SessionUnavailable;
        if (effectState == EffectState.Conflict) return AutoSwitchBlock.Conflict;
        if (effectState == EffectState.Fault) return AutoSwitchBlock.Fault;
        if (settingsProblem) return AutoSwitchBlock.SettingsProblem;
        if (manualHold) return AutoSwitchBlock.ManualHold;
        return AutoSwitchBlock.None;
    }
}

internal sealed record TrayStatusPresentation(
    string FormStatusText,
    string MenuStatusText,
    bool CanPause,
    bool CanResume,
    bool CanForceClear)
{
    internal static TrayStatusPresentation Create(
        bool sessionAvailable,
        EffectState effectState,
        string? effectError,
        bool settingsProblem,
        bool manualHold,
        ScheduleSnapshot? snapshot)
    {
        var status = AutoSwitch.FindBlock(sessionAvailable, effectState, settingsProblem, manualHold) switch
        {
            AutoSwitchBlock.SessionUnavailable => "ロックまたはスリープ中",
            AutoSwitchBlock.Conflict => "競合のため停止。ほかの画面の色変更を確認してください。",
            AutoSwitchBlock.Fault => $"適用できません: {effectError}",
            AutoSwitchBlock.SettingsProblem => "設定を読み取れません。確認して保存してください。",
            AutoSwitchBlock.ManualHold => "手動で解除しました。自動切替は停止中です。",
            AutoSwitchBlock.None => ScheduleStatus(snapshot),
            var block => throw new ArgumentOutOfRangeException(nameof(block), block, null)
        };

        var statusText = "現在の状態: " + status;
        if (snapshot is { Phase: not SchedulePhase.Disabled } && snapshot.NextBoundary != DateTimeOffset.MaxValue)
            statusText += $"\n次の切替: {snapshot.NextBoundary.ToLocalTime():MM/dd HH:mm}";

        // Operation rules differ from the display priority (for example, resume stays available
        // during a manual hold even when a settings problem is shown), so they use the raw flags.
        var canPause = sessionAvailable && !manualHold && !settingsProblem &&
            (effectState == EffectState.Applied || snapshot?.Phase == SchedulePhase.Paused);
        var canResume = sessionAvailable && (snapshot?.Phase == SchedulePhase.Paused || manualHold ||
            effectState is EffectState.Conflict or EffectState.Fault);
        var canForceClear = effectState == EffectState.Conflict;
        return new(statusText, status.Length > 40 ? status[..40] + "…" : status, canPause, canResume, canForceClear);
    }

    private static string ScheduleStatus(ScheduleSnapshot? snapshot) => snapshot?.Phase switch
    {
        SchedulePhase.Disabled => "自動切替はオフ",
        SchedulePhase.Waiting => "開始前",
        SchedulePhase.Fading => $"移行中・白黒度{snapshot.Ratio:P0}",
        SchedulePhase.Gray => "白黒表示中",
        SchedulePhase.Paused => "一時解除中",
        _ => "開始前"
    };
}
