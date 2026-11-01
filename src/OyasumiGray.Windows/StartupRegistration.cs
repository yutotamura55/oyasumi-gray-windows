using Microsoft.Win32;

namespace OyasumiGray.Windows;

internal sealed class StartupRegistration(string runKeyPath, string executablePath)
{
    private const string WindowsRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "OyasumiGray.Windows";

    public static StartupRegistration ForThisApp() => new(WindowsRunKey, Application.ExecutablePath);

    public void Set(bool enabled)
    {
        if (!enabled)
        {
            using var key = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var command = $"\"{executablePath}\"";
        if (command.Length > 260) throw new InvalidOperationException("実行ファイルのパスが長すぎるため、ログオン起動を登録できません。");
        using var writable = Registry.CurrentUser.CreateSubKey(runKeyPath);
        writable.SetValue(ValueName, command, RegistryValueKind.String);
    }
}
