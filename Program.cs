// GBF.exe -- open GBF windows in a Chromium browser and keep them at a recorded layout.
//   GBF.exe launch [N] [--log <path>]   open N GBF windows (default: count in GBF.ini) and place them
//   GBF.exe resize                      put already-open GBF windows back to the layout for that many windows
//   GBF.exe record                      record the current GBF windows as the layout for that many windows
//   GBF.exe                             open the settings window
// Settings live in GBF.ini next to the exe (see README.md). Build with build.cmd.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

static class Program {
    // Keep watching this long after the last window was placed (the page may still resize itself)...
    const int WatchSeconds = 15;
    // ...but give up after this long in total. A browser starting cold can take 10 s+ to show a window.
    const int TimeoutSeconds = 60;
    // When recording, windows this close to each other (px) are snapped together.
    const int SnapDistance = 16;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vKey);

    static bool LeftMouseButtonDown() { return (GetAsyncKeyState(0x01) & 0x8000) != 0; }

    static string IniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GBF.ini");

    [STAThread]
    static int Main(string[] args) {
        if (args.Length == 0) {
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.Run(new SettingsForm(IniPath));
            return 0;
        }

        GbfWindow.UsePhysicalPixels();
        Settings s = Settings.Load(IniPath);

        string mode = args[0].ToLowerInvariant();
        string log = null;
        int count = s.Count;
        for (int i = 1; i < args.Length; i++) {
            int n;
            if (args[i] == "--log" && i + 1 < args.Length) log = args[++i];
            else if (int.TryParse(args[i], out n)) count = Math.Max(1, Math.Min(Settings.MaxWindows, n));
        }

        if (mode == "launch") { Launch(s, count, log); return 0; }
        if (mode == "resize") { return Resize(s) ? 0 : 1; }
        if (mode == "record") { return Record(s) ? 0 : 1; }
        MessageBoxW(IntPtr.Zero, "使い方: GBF.exe launch [窓の数] | resize | record\n(引数なしで起動すると設定画面が開きます)", "GBF-Resize", 0);
        return 2;
    }

    // Launch GBF windows and keep forcing them to their slot size while they load
    // (the page's own JS sometimes calls window.resizeTo() during load, so a
    // single resize attempt right after launch can get overwritten).
    // Without a recorded layout for `count` windows, they are lined up from the top-left of the primary monitor.
    static void Launch(Settings s, int count, string log) {
        var clock = Stopwatch.StartNew();
        string[] titles = s.Titles.ToArray();
        // GBF windows that were already open before this launch are left alone.
        var existing = new HashSet<IntPtr>(GbfWindow.FindAll(titles));
        List<int[]> layout = s.Layout(count);

        string args = (s.Profile.Length > 0 ? "--profile-directory=\"" + s.Profile + "\" " : "") + "--app=\"" + s.Url + "\"";
        for (int i = 0; i < count; i++) {
            var psi = new ProcessStartInfo(s.Browser, args);
            psi.UseShellExecute = false;
            psi.WorkingDirectory = Path.GetDirectoryName(s.Browser);
            try {
                Process.Start(psi);
            } catch (Exception e) {
                MessageBoxW(IntPtr.Zero, "ブラウザを起動できませんでした。\n" + s.Browser + "\n\n" + e.Message, "GBF-Resize", 0);
                return;
            }
        }

        // New windows take the slots left to right in the order they show up.
        // Checking is cheap (well under 1 ms), so poll often and only touch a window when its size is off.
        var placed = new List<IntPtr>();
        // Where each placed window belongs, as {x, y, width, height}.
        var targets = new List<int[]>();
        // Windows the user started resizing by hand; they are not forced back any more.
        var released = new HashSet<IntPtr>();
        double lastPlacedAt = 0;
        while (clock.Elapsed.TotalSeconds < TimeoutSeconds
               && !(placed.Count >= count && clock.Elapsed.TotalSeconds - lastPlacedAt > WatchSeconds)) {
            foreach (IntPtr h in GbfWindow.FindAll(titles)) {
                if (existing.Contains(h)) continue;
                int slot = placed.IndexOf(h);
                if (slot < 0) {
                    placed.Add(h);
                    lastPlacedAt = clock.Elapsed.TotalSeconds;
                    slot = placed.Count - 1;
                    if (slot < count) {
                        int[] w;
                        if (layout != null) {
                            w = layout[slot];
                            GbfWindow.Place(h, w[0], w[1], w[2], w[3]);
                        } else {
                            w = PlaceSideBySide(h, slot == 0 ? IntPtr.Zero : placed[slot - 1], count);
                        }
                        targets.Add(w);
                        Log(log, clock, string.Format("placed 0x{0:X} in slot {1}", h.ToInt64(), slot));
                    }
                } else if (slot < targets.Count && !released.Contains(h)) {
                    int[] want = targets[slot];
                    int[] now = GbfWindow.Bounds(h);
                    if (now == null || (now[2] == want[2] && now[3] == want[3])) continue;
                    // The page's resizeTo() happens without the mouse; a size change while the left
                    // button is down is the user dragging the border (e.g. while arranging to record).
                    if (LeftMouseButtonDown()) {
                        released.Add(h);
                        Log(log, clock, string.Format("released 0x{0:X} (user is resizing it)", h.ToInt64()));
                    } else if (GbfWindow.FitSize(h, want[2], want[3])) {
                        Log(log, clock, string.Format("resized 0x{0:X}", h.ToInt64()));
                    }
                }
            }
            Thread.Sleep(100);
        }
    }

    // Starting layout when none is recorded: full work-area height, a GBF-like portrait width
    // (narrowed if `count` windows would not fit), lined up from the left with the visible frames touching.
    // Returns where the window ended up.
    static int[] PlaceSideBySide(IntPtr h, IntPtr leftNeighbour, int count) {
        int[] area = GbfWindow.PrimaryWorkArea();
        int height = area[3] - area[1];
        int width = Math.Min(height * 48 / 100, (area[2] - area[0]) / count);
        if (leftNeighbour == IntPtr.Zero) {
            GbfWindow.Place(h, area[0], area[1], width, height);
            // Move the invisible left border off-screen so the visible frame starts at the edge.
            int[] b = GbfWindow.Bounds(h), f = GbfWindow.VisibleFrame(h);
            if (b != null && f != null && f[0] != area[0]) GbfWindow.Place(h, b[0] + area[0] - f[0], area[1], width, height);
        } else {
            GbfWindow.PlaceRightOf(h, leftNeighbour, area[1], width, height);
        }
        return GbfWindow.Bounds(h) ?? new[] { area[0], area[1], width, height };
    }

    // Keep the current left-to-right order and use the layout recorded for this many windows
    // (see Settings.LayoutFor when there is none); any window beyond the layout is left as it is.
    static bool Resize(Settings s) {
        List<IntPtr> windows = SortedGbfWindows(s);
        if (windows.Count == 0) {
            MessageBoxW(IntPtr.Zero, "GBF の窓が見つかりません。GBF を開いてから実行してください。", "GBF-Resize", 0);
            return false;
        }
        List<int[]> layout = s.LayoutFor(windows.Count);
        if (layout == null) {
            MessageBoxW(IntPtr.Zero, "配置がまだ記録されていません。\nGBF の窓を並べてから、設定画面の「今の配置を記録」を押してください。", "GBF-Resize", 0);
            return false;
        }
        for (int i = 0; i < windows.Count && i < layout.Count; i++) {
            int[] w = layout[i];
            GbfWindow.Place(windows[i], w[0], w[1], w[2], w[3]);
        }
        return true;
    }

    // Save the open GBF windows (left to right) as the layout for that many windows. Small hand-arranging
    // errors are cleaned up first: tops within SnapDistance of the leftmost window are aligned to it, and a
    // window whose visible frame is within SnapDistance of its left neighbour's is moved to touch it.
    static bool Record(Settings s) {
        List<IntPtr> windows = SortedGbfWindows(s);
        if (windows.Count == 0) {
            MessageBoxW(IntPtr.Zero, "GBF の窓が見つかりません。GBF を開いて並べてから実行してください。", "GBF-Resize", 0);
            return false;
        }
        if (windows.Count > Settings.MaxWindows) {
            MessageBoxW(IntPtr.Zero, string.Format("GBF の窓が {0} 個開いています。記録できるのは {1} 個までです。", windows.Count, Settings.MaxWindows), "GBF-Resize", 0);
            return false;
        }
        var layout = new List<int[]>();
        int[] prevFrame = null;
        int top = 0;
        for (int i = 0; i < windows.Count; i++) {
            int[] b = GbfWindow.Bounds(windows[i]);
            int[] f = GbfWindow.VisibleFrame(windows[i]);
            if (b == null || f == null) continue;
            if (layout.Count == 0) {
                top = b[1];
            } else {
                if (Math.Abs(b[1] - top) <= SnapDistance) b[1] = top;
                int gap = f[0] - prevFrame[2];
                if (Math.Abs(gap) <= SnapDistance) b[0] -= gap;
            }
            GbfWindow.Place(windows[i], b[0], b[1], b[2], b[3]);
            layout.Add(b);
            prevFrame = GbfWindow.VisibleFrame(windows[i]);
        }
        s.Layouts[layout.Count] = layout;
        s.Count = layout.Count;
        s.Save(IniPath);
        return true;
    }

    static List<IntPtr> SortedGbfWindows(Settings s) {
        var windows = new List<IntPtr>(GbfWindow.FindAll(s.Titles.ToArray()));
        windows.Sort((a, b) => GbfWindow.LeftOf(a).CompareTo(GbfWindow.LeftOf(b)));
        return windows;
    }

    static void Log(string path, Stopwatch clock, string msg) {
        if (path != null) File.AppendAllText(path, string.Format("{0,6} ms  {1}\r\n", clock.ElapsedMilliseconds, msg));
    }
}
