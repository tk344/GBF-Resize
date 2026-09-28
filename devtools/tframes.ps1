param([string]$Title="GBFTEST")
Add-Type -Path "$PSScriptRoot\..\GbfWindow.cs"
[GbfWindow]::UsePhysicalPixels()
foreach ($h in ([GbfWindow]::FindAll($Title) | Sort-Object { [GbfWindow]::LeftOf($_) })) { $b=[GbfWindow]::Bounds($h); $f=[GbfWindow]::VisibleFrame($h)
 "0x{0:X}  outer=({1},{2}) {3}x{4}   visible L={5} T={6} R={7} B={8}" -f $h.ToInt64(),$b[0],$b[1],$b[2],$b[3],$f[0],$f[1],$f[2],$f[3] }
