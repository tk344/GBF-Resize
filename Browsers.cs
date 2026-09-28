// Finds installed Chromium-based browsers (the ones that support --app=<url>).
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

public class BrowserInfo {
    public string Name;
    public string Path;
    public override string ToString() { return Name; }
}

public static class Browsers {
    // Every browser that registers itself as a possible default browser is listed under
    // StartMenuInternet. Gecko-based ones (Firefox and its forks, which ship omni.ja) and
    // Internet Explorer have no --app mode, so they are left out.
    public static List<BrowserInfo> Detect() {
        var found = new List<BrowserInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        RegistryKey[] roots = { Registry.CurrentUser, Registry.LocalMachine };
        string[] keys = { @"SOFTWARE\Clients\StartMenuInternet", @"SOFTWARE\WOW6432Node\Clients\StartMenuInternet" };
        foreach (RegistryKey root in roots) {
            foreach (string keyPath in keys) {
                using (RegistryKey key = root.OpenSubKey(keyPath)) {
                    if (key == null) continue;
                    foreach (string sub in key.GetSubKeyNames()) {
                        using (RegistryKey b = key.OpenSubKey(sub)) {
                            string exe = ExeFromCommand(ReadDefault(b, @"shell\open\command"));
                            if (exe == null || !File.Exists(exe) || !seen.Add(exe)) continue;
                            if (!IsChromium(exe)) continue;
                            string name = ReadDefault(b, "") ?? sub;
                            found.Add(new BrowserInfo { Name = name, Path = exe });
                        }
                    }
                }
            }
        }
        return found;
    }

    static bool IsChromium(string exe) {
        if (System.IO.Path.GetFileName(exe).Equals("iexplore.exe", StringComparison.OrdinalIgnoreCase)) return false;
        return !File.Exists(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(exe), "omni.ja"));
    }

    static string ReadDefault(RegistryKey parent, string subPath) {
        if (parent == null) return null;
        RegistryKey k = subPath.Length == 0 ? parent : parent.OpenSubKey(subPath);
        if (k == null) return null;
        try { return k.GetValue("") as string; }
        finally { if (k != parent) k.Close(); }
    }

    // "C:\...\chrome.exe" --flag  ->  C:\...\chrome.exe
    static string ExeFromCommand(string cmd) {
        if (string.IsNullOrEmpty(cmd)) return null;
        cmd = cmd.Trim();
        if (cmd.StartsWith("\"")) {
            int end = cmd.IndexOf('"', 1);
            return end > 1 ? cmd.Substring(1, end - 1) : null;
        }
        int exeEnd = cmd.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exeEnd > 0 ? cmd.Substring(0, exeEnd + 4) : null;
    }
}
