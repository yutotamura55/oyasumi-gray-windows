using System.Threading;

namespace OyasumiGray.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var mutex = new Mutex(true, @"Local\OyasumiGray.Windows", out bool firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("すでに起動しています。通知領域から設定を開いてください。", "おやすみグレー for Windows",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try { Application.Run(new TrayApplicationContext()); }
        catch (Exception ex)
        {
            MessageBox.Show($"起動できませんでした。\n{ex.GetType().Name}: {ex.Message}", "おやすみグレー for Windows",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
