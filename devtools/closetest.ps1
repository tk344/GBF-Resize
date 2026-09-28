# Close every window titled GBFTEST (test windows only), including minimized and maximized ones
Add-Type -Path "$PSScriptRoot\..\GbfWindow.cs"
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class CT { [DllImport("user32.dll")] public static extern bool PostMessageW(IntPtr h, uint m, IntPtr w, IntPtr l); }
"@
foreach ($h in [GbfWindow]::FindAny(@("GBFTEST"))) { [void][CT]::PostMessageW($h, 0x10, [IntPtr]::Zero, [IntPtr]::Zero) }
