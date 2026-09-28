param([string]$Out)
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class Shot { [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr c);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h); }
"@
[Shot]::SetThreadDpiAwarenessContext([IntPtr](-4)) | Out-Null
$root=[Windows.Automation.AutomationElement]::RootElement
$cond = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, "GBF-Resize (非公式)")
$form = $null; for ($i=0; $i -lt 30 -and -not $form; $i++) { $form = $root.FindFirst([Windows.Automation.TreeScope]::Children, $cond); if (-not $form) { Start-Sleep -Milliseconds 200 } }
if (-not $form) { "form not found"; exit 1 }
[Shot]::SetForegroundWindow([IntPtr]$form.Current.NativeWindowHandle) | Out-Null; Start-Sleep -Milliseconds 300
$r = $form.Current.BoundingRectangle
$b = New-Object Drawing.Bitmap ([int]$r.Width), ([int]$r.Height); $g=[Drawing.Graphics]::FromImage($b); $g.CopyFromScreen([int]$r.X,[int]$r.Y,0,0,$b.Size); $b.Save($Out)
"form rect: $r"
