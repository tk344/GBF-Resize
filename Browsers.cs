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

public class ProfileInfo {
    public string Dir;    // what --profile-directory takes, e.g. "Default", "Profile 1"
    public string Label;  // what the browser shows, e.g. "Taro (taro@example.com)"
    public override string ToString() { return Label; }
}

public static class Browsers {
    // Where each browser keeps its profiles, below %LOCALAPPDATA% (matched against the exe path).
    static readonly string[,] UserDataDirs = {
        { @"\Google\Chrome\", @"Google\Chrome\User Data" },
        { @"\Microsoft\Edge\", @"Microsoft\Edge\User Data" },
        { @"\BraveSoftware\Brave-Browser\", @"BraveSoftware\Brave-Browser\User Data" },
        { @"\Vivaldi\", @"Vivaldi\User Data" },
        { @"\SRWare Iron", @"Chromium\User Data" },
    };

    // The browser's profiles in the order it lists them, read from "Local State" in its user data folder.
    // Empty when the browser is not one of the above or the file can't be read.
    public static List<ProfileInfo> Profiles(string exe) {
        var found = new List<ProfileInfo>();
        string localState = null;
        for (int i = 0; i < UserDataDirs.GetLength(0); i++) {
            if (exe.IndexOf(UserDataDirs[i, 0], StringComparison.OrdinalIgnoreCase) >= 0) {
                localState = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                                    UserDataDirs[i, 1], "Local State");
                break;
            }
        }
        if (localState == null || !File.Exists(localState)) return found;
        try {
            var json = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var root = (Dictionary<string, object>)json.DeserializeObject(File.ReadAllText(localState, System.Text.Encoding.UTF8));
            var profile = (Dictionary<string, object>)root["profile"];
            var cache = (Dictionary<string, object>)profile["info_cache"];
            var order = new List<string>();
            object listed;
            if (profile.TryGetValue("profiles_order", out listed) && listed is object[]) {
                foreach (object dir in (object[])listed) order.Add(dir as string);
            }
            foreach (string dir in cache.Keys) {
                if (!order.Contains(dir)) order.Add(dir);
            }
            foreach (string dir in order) {
                object entry;
                if (dir == null || !cache.TryGetValue(dir, out entry)) continue;
                var info = entry as Dictionary<string, object>;
                string name = info != null ? Text(info, "name") : "";
                string account = info != null ? Text(info, "user_name") : "";
                if (name.Length == 0) name = dir;
                found.Add(new ProfileInfo { Dir = dir, Label = account.Length > 0 && account != name ? name + " (" + account + ")" : name });
            }
        } catch (Exception) {
            // Unknown format: fall back to typing the folder name by hand.
            found.Clear();
        }
        return found;
    }

    static string Text(Dictionary<string, object> d, string key) {
        object v;
        return d.TryGetValue(key, out v) && v is string ? (string)v : "";
    }

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
