// GBF.ini: plain "key = value" lines, '#' or ';' starts a comment. Missing keys keep the defaults.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class Settings {
    // "グランブルーファンタジー", escaped so the source encoding doesn't matter.
    public const string DefaultTitle = "\u30B0\u30E9\u30F3\u30D6\u30EB\u30FC\u30D5\u30A1\u30F3\u30BF\u30B8\u30FC";
    // Layouts can be recorded for 1..MaxWindows windows.
    public const int MaxWindows = 3;

    const string ChromePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe";

    public string Browser = ChromePath;
    public string Profile = "Default";
    public string Url = "https://game.granbluefantasy.jp/#mypage";
    // Window titles that identify a GBF window (exact match).
    public List<string> Titles = new List<string> { DefaultTitle };
    // How many windows "launch" opens when no number is given (the one last chosen in the settings window).
    public int Count = 1;
    // Window count -> one {x, y, width, height} per window, left to right (GetWindowRect coordinates, physical pixels).
    public SortedDictionary<int, List<int[]>> Layouts = new SortedDictionary<int, List<int[]>>();

    public List<int[]> Layout(int count) {
        List<int[]> layout;
        return Layouts.TryGetValue(count, out layout) ? layout : null;
    }

    // The layout to put `count` open windows back into: the one recorded for that many windows,
    // otherwise the next larger one (its first slots), otherwise the largest one (extra windows stay as they are).
    public List<int[]> LayoutFor(int count) {
        List<int[]> best = null;
        foreach (KeyValuePair<int, List<int[]>> kv in Layouts) {
            best = kv.Value;
            if (kv.Key >= count) break;
        }
        return best;
    }

    public static Settings Load(string path) {
        var s = new Settings();
        bool browserSet = false, countSet = false;
        // window1, window2, ... from before layouts were kept per window count.
        var legacy = new SortedDictionary<int, int[]>();
        string[] lines = File.Exists(path) ? File.ReadAllLines(path, Encoding.UTF8) : new string[0];
        foreach (string raw in lines) {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
            int eq = line.IndexOf('=');
            if (eq < 0) continue;
            string key = line.Substring(0, eq).Trim().ToLowerInvariant();
            string value = line.Substring(eq + 1).Trim();
            int n;
            if (key == "browser") { s.Browser = CleanPath(value); browserSet = true; }
            else if (key == "profile") s.Profile = value;
            else if (key == "url") s.Url = value;
            else if (key == "title") {
                s.Titles = new List<string>();
                foreach (string t in value.Split('|')) {
                    if (t.Trim().Length > 0) s.Titles.Add(t.Trim());
                }
                // Nothing left to match would make every window "not found"; keep the default instead.
                if (s.Titles.Count == 0) s.Titles.Add(DefaultTitle);
            }
            else if (key == "count" && int.TryParse(value, out n)) { s.Count = n; countSet = true; }
            else if (key.StartsWith("layout") && int.TryParse(key.Substring(6), out n)) {
                List<int[]> layout = ParseLayout(value);
                if (layout != null && layout.Count == n) s.Layouts[n] = layout;
            }
            else if (key.StartsWith("window") && int.TryParse(key.Substring(6), out n)) {
                int[] rect = ParseRect(value);
                if (rect != null) legacy[n] = rect;
            }
        }
        if (legacy.Count > 0 && !s.Layouts.ContainsKey(legacy.Count)) {
            s.Layouts[legacy.Count] = new List<int[]>(legacy.Values);
            if (!countSet) s.Count = legacy.Count;
        }
        s.Count = Math.Max(1, Math.Min(MaxWindows, s.Count));
        if (!browserSet) s.Browser = DefaultBrowser();
        return s;
    }

    // A path as pasted from Explorer's "Copy as path" comes with quotes.
    public static string CleanPath(string path) {
        return path.Trim().Trim('"').Trim();
    }

    // Message for a failed Save (typically GBF.exe placed in a folder the user can't write to).
    public static string SaveErrorText(Exception e) {
        return "設定を保存できませんでした。\nGBF.exe を、ドキュメントなど自分で書き込めるフォルダに置いてください。\n\n" + e.Message;
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
        sb.AppendLine("# 窓の数を指定せずに launch したときに開く数");
        sb.AppendLine("count = " + Count);
        sb.AppendLine("# 窓の数ごとの配置。窓ごとに 左上x, 左上y, 幅, 高さ(物理ピクセル)を、左の窓から順に | で区切って並べる");
        foreach (KeyValuePair<int, List<int[]>> kv in Layouts) {
            var rects = new List<string>();
            foreach (int[] w in kv.Value) rects.Add(string.Format("{0}, {1}, {2}, {3}", w[0], w[1], w[2], w[3]));
            sb.AppendLine(string.Format("layout{0} = {1}", kv.Key, string.Join(" | ", rects)));
        }
        // Write beside it and swap, so a crash half-way never leaves a truncated GBF.ini.
        string temp = path + ".tmp";
        File.WriteAllText(temp, sb.ToString(), new UTF8Encoding(true));
        if (File.Exists(path)) File.Replace(temp, path, null);
        else File.Move(temp, path);
    }

    static List<int[]> ParseLayout(string value) {
        var layout = new List<int[]>();
        foreach (string part in value.Split('|')) {
            int[] rect = ParseRect(part);
            if (rect == null) return null;
            layout.Add(rect);
        }
        return layout;
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
