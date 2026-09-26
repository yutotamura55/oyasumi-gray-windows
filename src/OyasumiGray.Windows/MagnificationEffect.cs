using System.ComponentModel;
using System.Runtime.InteropServices;
using OyasumiGray.Core;

namespace OyasumiGray.Windows;

internal sealed class MagnificationEffect : IDisplayEffect, IDisposable
{
    private bool _initialized;

    public MagnificationEffect()
    {
        if (!MagInitialize()) throw new Win32Exception(Marshal.GetLastWin32Error(), "画面の色変換を初期化できません。");
        _initialized = true;
    }

    public ColorMatrix Read()
    {
        EnsureInitialized();
        if (!MagGetFullscreenColorEffect(out var value))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "現在の画面の色変換を確認できません。");
        return ColorMatrix.FromArray(value.Values);
    }

    public void Write(ColorMatrix matrix)
    {
        EnsureInitialized();
        var effect = new NativeColorEffect { Values = matrix.ToArray() };
        if (!MagSetFullscreenColorEffect(ref effect))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "画面の色変換を設定できません。");
    }

    public void Dispose()
    {
        if (_initialized)
        {
            MagUninitialize();
            _initialized = false;
        }
    }

    private void EnsureInitialized()
    {
        if (!_initialized) throw new ObjectDisposedException(nameof(MagnificationEffect));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeColorEffect
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 25)]
        public float[] Values;
    }

    [DllImport("Magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagInitialize();

    [DllImport("Magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagUninitialize();

    [DllImport("Magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagGetFullscreenColorEffect(out NativeColorEffect effect);

    [DllImport("Magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagSetFullscreenColorEffect(ref NativeColorEffect effect);
}
