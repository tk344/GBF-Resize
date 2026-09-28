Add-Type -Path "$PSScriptRoot\..\GbfWindow.cs"
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class F { public struct RECT { public int L,T,R,B; }
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
 [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s); }
"@
[GbfWindow]::UsePhysicalPixels()
$t = -join ([char[]](0x30B0,0x30E9,0x30F3,0x30D6,0x30EB,0x30FC,0x30D5,0x30A1,0x30F3,0x30BF,0x30B8,0x30FC))
foreach ($h in ([GbfWindow]::FindAll($t) | Sort-Object { [GbfWindow]::LeftOf($_) })) { $o=New-Object F+RECT; $v=New-Object F+RECT
 [void][F]::GetWindowRect($h,[ref]$o); [void][F]::DwmGetWindowAttribute($h,9,[ref]$v,16)
 "0x{0:X}  outer=({1},{2}) {3}x{4}   visible L={5} R={6} T={7} B={8}" -f $h.ToInt64(),$o.L,$o.T,($o.R-$o.L),($o.B-$o.T),$v.L,$v.R,$v.T,$v.B }
