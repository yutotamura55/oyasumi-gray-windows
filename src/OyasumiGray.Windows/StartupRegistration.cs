using Microsoft.Win32;

namespace OyasumiGray.Windows;

internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "OyasumiGray.Windows";

    public static bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string value &&
            string.Equals(value, $"\"{Application.ExecutablePath}\"", StringComparison.OrdinalIgnoreCase);
    }

    public static void Set(bool enabled)
    {
        if (!enabled)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var command = $"\"{Application.ExecutablePath}\"";
        if (command.Length > 260) throw new InvalidOperationException("実行ファイルのパスが長すぎるため、ログオン起動を登録できません。");
        using var writable = Registry.CurrentUser.CreateSubKey(RunKey);
        writable.SetValue(ValueName, command, RegistryValueKind.String);
    }
}
