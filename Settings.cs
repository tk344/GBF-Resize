// GBF.ini: plain "key = value" lines, '#' or ';' starts a comment. Missing keys keep the defaults.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class Settings {
    // "グランブルーファンタジー", escaped so the source encoding doesn't matter.
    public const string DefaultTitle = "\u30B0\u30E9\u30F3\u30D6\u30EB\u30FC\u30D5\u30A1\u30F3\u30BF\u30B8\u30FC";

    const string ChromePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe";

    public string Browser = ChromePath;
    public string Profile = "Default";
    public string Url = "https://game.granbluefantasy.jp/#mypage";
    // Window titles that identify a GBF window (exact match).
    public List<string> Titles = new List<string> { DefaultTitle };
    // One {x, y, width, height} per window, left to right (GetWindowRect coordinates, physical pixels).
    public List<int[]> Windows = new List<int[]>();

    public static Settings Load(string path) {
        var s = new Settings();
        bool browserSet = false;
        var windows = new SortedDictionary<int, int[]>();
        string[] lines = File.Exists(path) ? File.ReadAllLines(path, Encoding.UTF8) : new string[0];
        foreach (string raw in lines) {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
            int eq = line.IndexOf('=');
            if (eq < 0) continue;
            string key = line.Substring(0, eq).Trim().ToLowerInvariant();
            string value = line.Substring(eq + 1).Trim();
            int n;
            if (key == "browser") { s.Browser = value; browserSet = true; }
            else if (key == "profile") s.Profile = value;
            else if (key == "url") s.Url = value;
            else if (key == "title") s.Titles = new List<string>(value.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
            else if (key.StartsWith("window") && int.TryParse(key.Substring(6), out n)) {
                int[] rect = ParseRect(value);
                if (rect != null) windows[n] = rect;
            }
        }
        s.Windows = new List<int[]>(windows.Values);
        if (!browserSet) s.Browser = DefaultBrowser();
        return s;
    }

    // Chrome if it is in its usual place, otherwise the first Chromium browser found (Edge ships with Windows).
    static string DefaultBrowser() {
        if (File.Exists(ChromePath)) return ChromePath;
        List<BrowserInfo> found = Browsers.Detect();
        return found.Count > 0 ? found[0].Path : ChromePath;
    }

    public void Save(string path) {
        var sb = new StringBuilder();
        sb.AppendLine("# GBF.exe の設定。GBF.exe を引数なしで起動すると出る設定画面から編集できる。");
        sb.AppendLine("# 手で書き換えてもよい(次の実行から反映される)。");
        sb.AppendLine();
        sb.AppendLine("# GBF を開くブラウザ(Chrome / Edge / Iron などの Chromium 系)の exe");
        sb.AppendLine("browser = " + Browser);
        sb.AppendLine("# ブラウザのプロファイル(--profile-directory)。空欄なら指定しない");
        sb.AppendLine("profile = " + Profile);
        sb.AppendLine("url = " + Url);
        sb.AppendLine("# GBF のウィンドウとみなすタイトル(完全一致、| 区切りで複数可)");
        sb.AppendLine("title = " + string.Join("|", Titles));
        sb.AppendLine();
        sb.AppendLine("# 窓ごとの 左上x, 左上y, 幅, 高さ(物理ピクセル)。左から順に window1, window2, ...");
        for (int i = 0; i < Windows.Count; i++) {
            int[] w = Windows[i];
            sb.AppendLine(string.Format("window{0} = {1}, {2}, {3}, {4}", i + 1, w[0], w[1], w[2], w[3]));
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    static int[] ParseRect(string value) {
        string[] parts = value.Split(',');
        if (parts.Length != 4) return null;
        var r = new int[4];
        for (int i = 0; i < 4; i++) {
            if (!int.TryParse(parts[i].Trim(), out r[i])) return null;
        }
        return r;
    }
}
