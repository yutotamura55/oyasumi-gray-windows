# Reads the full-screen color matrix through the Magnification API, for checks on a real Windows PC.
# -Action Other writes a tint (green and blue at 80%) that stands in for another app's color effect,
# and keeps it for -HoldSeconds. In the 2026-10-26 check, the tint was gone after this script exited.
# -Action Same writes back the matrix it has just read, to test whether writing is allowed at all.
param(
    [ValidateSet('Read', 'Other', 'Same')] [string] $Action = 'Read',
    [ValidateRange(0, 3600)] [int] $HoldSeconds = 0
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

# The functions return FALSE on failure. They are not documented to set the last Win32 error,
# so this script reports only that the call failed.
if (-not [Magnification]::MagInitialize()) { throw 'MagInitialize returned FALSE.' }
try {
    if ($Action -ne 'Read') {
        $effect = New-Object Magnification+ColorEffect
        if ($Action -eq 'Same') {
            if (-not [Magnification]::MagGetFullscreenColorEffect([ref] $effect)) { throw 'MagGetFullscreenColorEffect returned FALSE.' }
        }
        else {
            $values = New-Object float[] 25
            foreach ($i in 0, 6, 12, 18, 24) { $values[$i] = 1 }
            $values[6] = 0.8
            $values[12] = 0.8
            $effect.Values = $values
        }
        if (-not [Magnification]::MagSetFullscreenColorEffect([ref] $effect)) { throw 'MagSetFullscreenColorEffect returned FALSE.' }
        Start-Sleep -Seconds $HoldSeconds
    }

    $effect = New-Object Magnification+ColorEffect
    if (-not [Magnification]::MagGetFullscreenColorEffect([ref] $effect)) { throw 'MagGetFullscreenColorEffect returned FALSE.' }
    $isIdentity = $true
    for ($i = 0; $i -lt 25; $i++) {
        $expected = if ($i % 6 -eq 0) { 1 } else { 0 }
        if ([math]::Abs($effect.Values[$i] - $expected) -gt 0.001) { $isIdentity = $false }
    }
    # In the app's matrix, the first value is 1 - p + 0.2126p, where p is the grayscale ratio.
    # The ratio is shown only when the whole matrix matches the app's matrix for that ratio.
    $ratio = (1 - $effect.Values[0]) / (1 - 0.2126)
    $weights = 0.2126, 0.7152, 0.0722
    $isAppMatrix = $true
    for ($i = 0; $i -lt 25; $i++) {
        $row = [math]::Floor($i / 5)
        $column = $i % 5
        $expected = if ($row -lt 3 -and $column -lt 3) { $ratio * $weights[$row] + $(if ($row -eq $column) { 1 - $ratio } else { 0 }) }
                    elseif ($i -eq 18 -or $i -eq 24) { 1 } else { 0 }
        if ([math]::Abs($effect.Values[$i] - $expected) -gt 0.001) { $isAppMatrix = $false }
    }
    $grayRatio = if ($isAppMatrix) { $ratio.ToString('0.000') } else { 'n/a (not the app''s matrix)' }
    '{0:HH:mm:ss} identity={1} grayRatio={2}' -f (Get-Date), $isIdentity, $grayRatio
    for ($row = 0; $row -lt 5; $row++) {
        ($effect.Values[($row * 5)..($row * 5 + 4)] | ForEach-Object { $_.ToString('0.000') }) -join ' '
    }
}
finally {
    [void][Magnification]::MagUninitialize()
}
