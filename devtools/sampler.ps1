param([int]$Seconds=17,[string]$Out)
Add-Type -Path "$PSScriptRoot\..\GbfWindow.cs"
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class R { [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r); public struct RECT { public int L,T,Rt,B; } }
"@
[GbfWindow]::UsePhysicalPixels()
$t = -join ([char[]](0x30B0,0x30E9,0x30F3,0x30D6,0x30EB,0x30FC,0x30D5,0x30A1,0x30F3,0x30BF,0x30B8,0x30FC))
$c=[Diagnostics.Stopwatch]::StartNew(); $last=@{}
while ($c.Elapsed.TotalSeconds -lt $Seconds) {
  foreach ($h in [GbfWindow]::FindAll($t)) { $r=New-Object R+RECT; [void][R]::GetWindowRect($h,[ref]$r)
    $s="{0}x{1} @({2},{3})" -f ($r.Rt-$r.L),($r.B-$r.T),$r.L,$r.T; $k=$h.ToInt64()
    if ($last[$k] -ne $s) { Add-Content $Out ("{0,6} ms  0x{1:X}  {2}" -f $c.ElapsedMilliseconds,$k,$s); $last[$k]=$s } }
  Start-Sleep -Milliseconds 50 }
