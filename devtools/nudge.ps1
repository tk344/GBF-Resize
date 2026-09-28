param([string]$Hwnd,[int]$X,[int]$Y,[int]$W,[int]$H)
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class N { [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
 [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr c); }
"@
[N]::SetThreadDpiAwarenessContext([IntPtr](-4)) | Out-Null
[N]::SetWindowPos([IntPtr][Convert]::ToInt64($Hwnd,16), [IntPtr]::Zero, $X, $Y, $W, $H, 0x14) | Out-Null
