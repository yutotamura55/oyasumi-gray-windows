using OyasumiGray.Core;

namespace OyasumiGray.Windows;

internal sealed class SettingsForm : Form
{
    private readonly DateTimePicker _start = CreateTimePicker();
    private readonly DateTimePicker _restore = CreateTimePicker();
    private readonly NumericUpDown _duration = new() { Minimum = 1, Maximum = 360, Value = 60, Width = 100 };
    private readonly CheckBox _enabled = new() { Text = "自動切替を有効にする", AutoSize = true };
    public CheckBox LaunchAtLogin { get; } = new() { Text = "Windows へのログオン時に起動", AutoSize = true };
    public Label Status { get; } = new() { AutoSize = true, MaximumSize = new Size(460, 0), AccessibleName = "現在の状態" };
    public Label Preview { get; } = new() { AutoSize = true, MaximumSize = new Size(460, 0) };
    public Button SaveButton { get; } = new() { Text = "設定を保存", AutoSize = true };
    public Button PauseButton { get; } = new() { Text = "15分だけカラーに戻す", AutoSize = true };
    public Button ResumeButton { get; } = new() { Text = "自動切替を再開", AutoSize = true };
    public Button StopButton { get; } = new() { Text = "今すぐカラーに戻して停止", AutoSize = true };
    public Button ForceClearButton { get; } = new() { Text = "現在の画面の色変更を解除する", AutoSize = true };
    private bool _closingForExit;

    public SettingsForm(AppSettings settings)
    {
        Text = "おやすみグレー for Windows";
        ClientSize = new Size(530, 490);
        MinimumSize = new Size(440, 430);
        AutoScroll = true;
        StartPosition = FormStartPosition.CenterScreen;
        Font = SystemFonts.MessageBoxFont;

        var root = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(16), ColumnCount = 2, RowCount = 0 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        AddWide(root, new Label { Text = "指定時刻から画面全体を徐々に白黒にし、朝にカラーへ戻します。", AutoSize = true, MaximumSize = new Size(460, 0) });
        AddWide(root, Status);
        AddWide(root, _enabled);
        AddRow(root, "薄れ始める時刻", _start);
        AddRow(root, "白黒になるまで（分）", _duration);
        AddRow(root, "カラーに戻る時刻", _restore);
        AddWide(root, Preview);
        AddWide(root, LaunchAtLogin);
        AddWide(root, new Label { Text = "ブラウザ拡張版も有効な場合、Webページでは効果が重なります。", AutoSize = true, MaximumSize = new Size(460, 0) });

        var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = true, FlowDirection = FlowDirection.LeftToRight, Width = 470 };
        actions.Controls.AddRange([SaveButton, PauseButton, ResumeButton, StopButton, ForceClearButton]);
        AddWide(root, actions);
        AddWide(root, new Label { Text = "この画面を閉じても通知領域で動作します。", AutoSize = true });

        foreach (var control in new Control[] { _enabled, _start, _duration, _restore })
        {
            if (control is DateTimePicker picker) picker.ValueChanged += (_, _) => RefreshPreview();
            else if (control is NumericUpDown numeric) numeric.ValueChanged += (_, _) => RefreshPreview();
            else if (control is CheckBox check) check.CheckedChanged += (_, _) => RefreshPreview();
        }
        SetSettings(settings);
    }

    public void SetSettings(AppSettings settings)
    {
        _enabled.Checked = settings.Enabled;
        _start.Value = DateTime.Today.Add(settings.Start.ToTimeSpan());
        _duration.Value = settings.FadeMinutes;
        _restore.Value = DateTime.Today.Add(settings.Restore.ToTimeSpan());
        RefreshPreview();
    }

    public AppSettings ReadSettings() => new(_enabled.Checked, TimeOnly.FromDateTime(_start.Value), (int)_duration.Value,
        TimeOnly.FromDateTime(_restore.Value));

    public void RefreshPreview()
    {
        var settings = ReadSettings();
        Preview.Text = settings.Validate() is { } error ? error :
            $"{settings.Start:HH:mm} から薄れ始め、{settings.Start.AddMinutes(settings.FadeMinutes):HH:mm} に白黒、{settings.Restore:HH:mm} にカラーへ戻ります。";
    }

    public void SetStatus(string text)
    {
        if (Status.Text != text) Status.Text = text;
    }

    public void CloseForExit() { _closingForExit = true; Close(); }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_closingForExit && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnFormClosing(e);
    }

    private static DateTimePicker CreateTimePicker() => new()
    {
        Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 100
    };

    private static void AddRow(TableLayoutPanel panel, string label, Control control)
    {
        panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 9, 3, 9) });
        control.Margin = new Padding(3, 5, 3, 5);
        panel.Controls.Add(control);
    }

    private static void AddWide(TableLayoutPanel panel, Control control)
    {
        panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        control.Margin = new Padding(3, 7, 3, 7);
        panel.Controls.Add(control);
        panel.SetColumnSpan(control, 2);
    }
}
