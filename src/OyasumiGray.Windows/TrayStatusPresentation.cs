using OyasumiGray.Core;

namespace OyasumiGray.Windows;

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
        string status;
        if (!sessionAvailable) status = "ロックまたはスリープ中";
        else if (effectState == EffectState.Conflict) status = "競合のため停止。ほかの画面の色変更を確認してください。";
        else if (effectState == EffectState.Fault) status = $"適用できません: {effectError}";
        else if (settingsProblem) status = "設定を読み取れません。確認して保存してください。";
        else if (manualHold) status = "手動で解除しました。自動切替は停止中です。";
        else status = snapshot?.Phase switch
        {
            SchedulePhase.Disabled => "自動切替はオフ",
            SchedulePhase.Waiting => "開始前",
            SchedulePhase.Fading => $"移行中・白黒度{snapshot.Ratio:P0}",
            SchedulePhase.Gray => "白黒表示中",
            SchedulePhase.Paused => "一時解除中",
            _ => "開始前"
        };

        var statusText = "現在の状態: " + status;
        if (snapshot is { Phase: not SchedulePhase.Disabled } && snapshot.NextBoundary != DateTimeOffset.MaxValue)
            statusText += $"\n次の切替: {snapshot.NextBoundary.ToLocalTime():MM/dd HH:mm}";

        var canPause = sessionAvailable && !manualHold && !settingsProblem &&
            (effectState == EffectState.Applied || snapshot?.Phase == SchedulePhase.Paused);
        var canResume = sessionAvailable && (snapshot?.Phase == SchedulePhase.Paused || manualHold ||
            effectState is EffectState.Conflict or EffectState.Fault);
        var canForceClear = effectState == EffectState.Conflict;
        return new(statusText, status.Length > 40 ? status[..40] + "…" : status, canPause, canResume, canForceClear);
    }
}
