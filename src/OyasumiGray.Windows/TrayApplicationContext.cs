using Microsoft.Win32;
using OyasumiGray.Core;

namespace OyasumiGray.Windows;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly SettingsStore _store = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OyasumiGray.Windows"));
    private readonly MagnificationEffect _effect;
    private readonly EffectController _controller;
    private readonly SettingsForm _form;
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly ToolStripMenuItem _statusItem = new() { Enabled = false };
    private readonly ToolStripMenuItem _pauseItem = new("15分だけカラーに戻す");
    private readonly ToolStripMenuItem _resumeItem = new("自動切替を再開");
    private readonly ToolStripMenuItem _forceItem = new("現在の画面の色変更を解除する");
    private AppSettings _settings;
    private DateTimeOffset? _pauseUntil;
    private ScheduleSnapshot? _lastSnapshot;
    private bool _settingsProblem;
    private bool _manualHold;
    private bool _locked;
    private bool _sleeping;
    private bool _exiting;
    private int _pendingWrites;
    private int _settingsRevision;
    private bool SessionAvailable => !_locked && !_sleeping;

    public TrayApplicationContext()
    {
        bool showAtStart = !_store.HasSettings;
        try
        {
            _settings = _store.LoadSettings();
            _pauseUntil = _store.LoadPause();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _settings = AppSettings.Default;
            _settingsProblem = true;
            showAtStart = true;
            Log("load", ex);
        }

        _effect = new MagnificationEffect();
        _controller = new EffectController(_effect);
        _controller.CheckInitial();
        _form = new SettingsForm(_settings);
        _ = _form.Handle; // Create the handle so OS event callbacks can marshal to the UI thread.
        try { _form.LaunchAtLogin.Checked = StartupRegistration.IsRegistered(); }
        catch (Exception ex) { Log("startup-read", ex); }
        _form.SaveButton.Click += async (_, _) => await SaveSettingsAsync();
        _form.PauseButton.Click += async (_, _) => await PauseAsync();
        _form.ResumeButton.Click += async (_, _) => await ResumeAsync();
        _form.StopButton.Click += async (_, _) => await StopAsync();
        _form.ForceClearButton.Click += (_, _) => ForceClear();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripMenuItem("設定を開く", null, (_, _) => ShowSettings()));
        menu.Items.Add(_pauseItem);
        menu.Items.Add(_resumeItem);
        menu.Items.Add(_forceItem);
        menu.Items.Add(new ToolStripMenuItem("今すぐカラーに戻して停止", null, async (_, _) => await StopAsync()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("終了", null, (_, _) => ExitRequested()));
        _pauseItem.Click += async (_, _) => await PauseAsync();
        _resumeItem.Click += async (_, _) => await ResumeAsync();
        _forceItem.Click += (_, _) => ForceClear();
        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "おやすみグレー for Windows",
            ContextMenuStrip = menu,
            Visible = true
        };
        _tray.DoubleClick += (_, _) => ShowSettings();
        _timer.Tick += (_, _) => Evaluate();

        SystemEvents.TimeChanged += OnTimeChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;

        Evaluate();
        if (showAtStart || _controller.State is EffectState.Conflict or EffectState.Fault) ShowSettings();
    }

    private void Evaluate()
    {
        _timer.Stop();
        if (_exiting) return;
        if (AutoSwitch.FindBlock(SessionAvailable, _controller.State, _settingsProblem, _manualHold) == AutoSwitchBlock.None)
        {
            try
            {
                var now = DateTimeOffset.Now;
                _lastSnapshot = Schedule.Evaluate(_settings, now, TimeZoneInfo.Local, _pauseUntil);
                if (_lastSnapshot.Ratio > 0) _controller.Apply(_lastSnapshot.Ratio);
                else _controller.Release();
                // Applying can fail with a conflict or fault; re-check before scheduling the next evaluation.
                if (AutoSwitch.FindBlock(SessionAvailable, _controller.State, _settingsProblem, _manualHold) == AutoSwitchBlock.None &&
                    _lastSnapshot.Phase != SchedulePhase.Disabled)
                {
                    var untilBoundary = (_lastSnapshot.NextBoundary - now).TotalMilliseconds;
                    _timer.Interval = (int)Math.Clamp(untilBoundary, 100, _lastSnapshot.Phase == SchedulePhase.Fading ? 1000 : 30_000);
                    _timer.Start();
                }
            }
            catch (Exception ex)
            {
                _settingsProblem = true;
                Log("evaluate", ex);
                MessageBox.Show($"時刻設定を処理できませんでした。\n{ex.Message}", "おやすみグレー for Windows",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        else if (_settingsProblem) _controller.Release();
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        var status = TrayStatusPresentation.Create(SessionAvailable, _controller.State, _controller.Error,
            _settingsProblem, _manualHold, _lastSnapshot);
        _form.SetStatus(status.FormStatusText);
        _statusItem.Text = status.MenuStatusText;
        _pauseItem.Enabled = _form.PauseButton.Enabled = status.CanPause;
        _resumeItem.Enabled = _form.ResumeButton.Enabled = status.CanResume;
        _forceItem.Visible = _form.ForceClearButton.Visible = status.CanForceClear;
    }

    private async Task SaveSettingsAsync()
    {
        if (!_form.SaveButton.Enabled) return;
        var requested = _form.ReadSettings();
        if (requested.Validate() is { } error)
        {
            MessageBox.Show(error, "設定を確認してください", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var revision = ++_settingsRevision;
        _form.SaveButton.Enabled = false;
        _pendingWrites++;
        try
        {
            await _store.SaveSettingsAsync(requested);
            if (revision != _settingsRevision) return;
            _settings = requested;
            if (_settingsProblem)
            {
                await _store.SavePauseAsync(null);
                _pauseUntil = null;
                _settingsProblem = false;
            }
            Evaluate();
            StartupRegistration.Set(_form.LaunchAtLogin.Checked);
        }
        catch (Exception ex)
        {
            Log("save", ex);
            MessageBox.Show($"設定を保存または登録できませんでした。\n{ex.Message}", "おやすみグレー for Windows",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { _pendingWrites--; _form.SaveButton.Enabled = true; }
    }

    private async Task PauseAsync()
    {
        if (_lastSnapshot?.RestoreBoundary is not { } restore ||
            _lastSnapshot.Ratio <= 0 && _lastSnapshot.Phase != SchedulePhase.Paused) return;
        _pauseUntil = DateTimeOffset.UtcNow.AddMinutes(15);
        if (_pauseUntil > restore) _pauseUntil = restore.ToUniversalTime();
        _controller.Release();
        Evaluate();
        _pendingWrites++;
        try { await _store.SavePauseAsync(_pauseUntil); }
        catch (Exception ex) { ShowPersistenceError("一時解除", ex); }
        finally { _pendingWrites--; }
    }

    private async Task ResumeAsync()
    {
        _pauseUntil = null;
        _manualHold = false;
        _controller.Retry();
        Evaluate();
        _pendingWrites++;
        try { await _store.SavePauseAsync(null); }
        catch (Exception ex) { ShowPersistenceError("一時解除の終了", ex); }
        finally { _pendingWrites--; }
    }

    private async Task StopAsync()
    {
        ++_settingsRevision;
        _settings = _settings with { Enabled = false };
        _pauseUntil = null;
        _manualHold = false;
        _controller.Release();
        _form.SetSettings(_settings);
        Evaluate();
        _pendingWrites++;
        try
        {
            await _store.SaveSettingsAsync(_settings);
            await _store.SavePauseAsync(null);
            _settingsProblem = false;
            Evaluate();
        }
        catch (Exception ex) { ShowPersistenceError("停止", ex); }
        finally { _pendingWrites--; }
    }

    private void ForceClear()
    {
        if (MessageBox.Show("ほかのアプリが設定した色変換も解除される可能性があります。現在の色変換を解除しますか？",
                "色変換の解除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        _controller.ForceClear();
        _manualHold = true;
        Evaluate();
    }

    private void ShowSettings()
    {
        try { _form.LaunchAtLogin.Checked = StartupRegistration.IsRegistered(); }
        catch (Exception ex) { Log("startup-read", ex); }
        _form.Show();
        _form.WindowState = FormWindowState.Normal;
        _form.Activate();
    }

    private void ExitRequested()
    {
        if (_pendingWrites > 0)
        {
            MessageBox.Show("設定を保存中です。完了してから終了してください。", "おやすみグレー for Windows",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _controller.Release();
        while (_controller.HasOwnedEffect)
        {
            var choice = MessageBox.Show("画面の色を戻せませんでした。\n「はい」: もう一度試す\n「いいえ」: このまま終了\n「キャンセル」: 常駐を続ける",
                "おやすみグレー for Windows", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (choice == DialogResult.Cancel) return;
            if (choice == DialogResult.No) break;
            _controller.Release();
        }
        _exiting = true;
        _timer.Stop();
        _tray.Visible = false;
        _form.CloseForExit();
        ExitThread();
    }

    private void OnTimeChanged(object? sender, EventArgs e) => OnUi(RefreshTimeZone);
    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) => OnUi(RefreshTimeZone);
    private void RefreshTimeZone()
    {
        TimeZoneInfo.ClearCachedData();
        Evaluate();
    }
    private void OnDisplayChanged(object? sender, EventArgs e) => OnUi(() =>
    {
        if (SessionAvailable) _controller.ReconcileAfterDisplayChange();
        Evaluate();
    });
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e) => OnUi(() =>
    {
        if (e.Mode == PowerModes.Suspend) { _sleeping = true; Suspend(); }
        else if (e.Mode == PowerModes.Resume) { _sleeping = false; ResumeSession(); }
    });
    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e) => OnUi(() =>
    {
        if (e.Reason == SessionSwitchReason.SessionLock) { _locked = true; Suspend(); }
        else if (e.Reason == SessionSwitchReason.SessionUnlock) { _locked = false; ResumeSession(); }
    });

    private void Suspend()
    {
        _timer.Stop();
        _controller.Release();
        RefreshStatus();
    }

    private void ResumeSession()
    {
        if (!SessionAvailable) { RefreshStatus(); return; }
        TimeZoneInfo.ClearCachedData();
        _controller.ReconcileAfterDisplayChange();
        Evaluate();
    }

    private void OnUi(Action action)
    {
        if (_exiting || _form.IsDisposed) return;
        try { _form.BeginInvoke(action); }
        catch (InvalidOperationException) { }
    }

    private void ShowPersistenceError(string operation, Exception ex)
    {
        Log(operation, ex);
        MessageBox.Show($"{operation}の設定を保存できませんでした。再起動後は以前の設定が使われる可能性があります。\n{ex.Message}",
            "おやすみグレー for Windows", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void Log(string operation, Exception ex)
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OyasumiGray.Windows");
            Directory.CreateDirectory(path);
            var current = Path.Combine(path, "errors.log");
            if (File.Exists(current) && new FileInfo(current).Length > 1024 * 1024)
                File.Move(current, Path.Combine(path, "errors.previous.log"), true);
            File.AppendAllText(current, $"{DateTimeOffset.UtcNow:O} {operation} {ex.GetType().Name}{Environment.NewLine}");
        }
        catch { /* Logging must never prevent screen recovery. */ }
    }

    protected override void ExitThreadCore()
    {
        SystemEvents.TimeChanged -= OnTimeChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        _timer.Dispose();
        _tray.Dispose();
        _effect.Dispose();
        _form.Dispose();
        base.ExitThreadCore();
    }
}
