# Reads the full-screen color matrix through the Magnification API, for checks on a real Windows PC.
# -Action Other writes a tint (green and blue at 80%) that stands in for another app's color effect,
# and keeps it for -HoldSeconds. In the 2026-10-26 check, the tint was gone after this script exited.
param(
    [ValidateSet('Read', 'Other')] [string] $Action = 'Read',
    [int] $HoldSeconds = 0
)

$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;

public static class Magnification
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ColorEffect
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 25)]
        public float[] Values;
    }

    [DllImport("Magnification.dll", SetLastError = true)]
    public static extern bool MagInitialize();

    [DllImport("Magnification.dll", SetLastError = true)]
    public static extern bool MagUninitialize();

    [DllImport("Magnification.dll", SetLastError = true)]
    public static extern bool MagGetFullscreenColorEffect(out ColorEffect effect);

    [DllImport("Magnification.dll", SetLastError = true)]
    public static extern bool MagSetFullscreenColorEffect(ref ColorEffect effect);
}
'@

function Get-LastError { [Runtime.InteropServices.Marshal]::GetLastWin32Error() }

if (-not [Magnification]::MagInitialize()) { throw "MagInitialize failed with error $(Get-LastError)" }
try {
    if ($Action -eq 'Other') {
        $values = New-Object float[] 25
        foreach ($i in 0, 6, 12, 18, 24) { $values[$i] = 1 }
        $values[6] = 0.8
        $values[12] = 0.8
        $effect = New-Object Magnification+ColorEffect
        $effect.Values = $values
        if (-not [Magnification]::MagSetFullscreenColorEffect([ref] $effect)) {
            throw "MagSetFullscreenColorEffect failed with error $(Get-LastError)"
        }
        Start-Sleep -Seconds $HoldSeconds
    }

    $effect = New-Object Magnification+ColorEffect
    if (-not [Magnification]::MagGetFullscreenColorEffect([ref] $effect)) {
        throw "MagGetFullscreenColorEffect failed with error $(Get-LastError)"
    }
    $isIdentity = $true
    for ($i = 0; $i -lt 25; $i++) {
        $expected = if ($i % 6 -eq 0) { 1 } else { 0 }
        if ([math]::Abs($effect.Values[$i] - $expected) -gt 0.001) { $isIdentity = $false }
    }
    # In the app's matrix, the first value is 1 - p + 0.2126p, where p is the grayscale ratio.
    '{0:HH:mm:ss} identity={1} grayRatio={2:0.000}' -f (Get-Date), $isIdentity, ((1 - $effect.Values[0]) / (1 - 0.2126))
    for ($row = 0; $row -lt 5; $row++) {
        ($effect.Values[($row * 5)..($row * 5 + 4)] | ForEach-Object { $_.ToString('0.000') }) -join ' '
    }
}
finally {
    [void][Magnification]::MagUninitialize()
}
