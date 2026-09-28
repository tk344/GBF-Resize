param([string]$Button)
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class BC { [DllImport("user32.dll")] public static extern bool PostMessageW(IntPtr h, uint m, IntPtr w, IntPtr l); }
"@
$root=[Windows.Automation.AutomationElement]::RootElement
$form = $root.FindFirst([Windows.Automation.TreeScope]::Children, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, "GBF-Resize (非公式)")))
if (-not $form) { "form not found"; exit 1 }
$btn = $form.FindFirst([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, $Button)))
if (-not $btn) { "button not found: $Button"; exit 1 }
[void][BC]::PostMessageW([IntPtr]$btn.Current.NativeWindowHandle, 0xF5, [IntPtr]::Zero, [IntPtr]::Zero); "clicked: $Button"
