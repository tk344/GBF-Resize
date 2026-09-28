// GBF.exe -- open GBF windows in a Chromium browser and keep them at a recorded layout.
//   GBF.exe launch [--log <path>]   open one GBF window per recorded slot and place them
//   GBF.exe resize                  put already-open GBF windows back to the recorded layout
//   GBF.exe record                  record the current GBF windows as the layout
//   GBF.exe                         open the settings window
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
        for (int i = 1; i + 1 < args.Length; i++) {
            if (args[i] == "--log") log = args[i + 1];
        }

        if (mode == "launch") { Launch(s, log); return 0; }
        if (mode == "resize") { return Resize(s) ? 0 : 1; }
        if (mode == "record") { return Record(s) ? 0 : 1; }
        MessageBoxW(IntPtr.Zero, "使い方: GBF.exe launch | resize | record\n(引数なしで起動すると設定画面が開きます)", "GBF-Resize", 0);
        return 2;
    }

    // Launch GBF windows and keep forcing them to their slot size while they load
    // (the page's own JS sometimes calls window.resizeTo() during load, so a
    // single resize attempt right after launch can get overwritten).
    static void Launch(Settings s, string log) {
        var clock = Stopwatch.StartNew();
        string[] titles = s.Titles.ToArray();
        // GBF windows that were already open before this launch are left alone.
        var existing = new HashSet<IntPtr>(GbfWindow.FindAll(titles));

        int count = Math.Max(1, s.Windows.Count);
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
        if (s.Windows.Count == 0) return;  // nothing recorded yet: just open it

        // New windows take the slots left to right in the order they show up.
        // Checking is cheap (well under 1 ms), so poll often and only touch a window when its size is off.
        var placed = new List<IntPtr>();
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
                    if (slot < s.Windows.Count) {
                        int[] w = s.Windows[slot];
                        GbfWindow.Place(h, w[0], w[1], w[2], w[3]);
                        Log(log, clock, string.Format("placed 0x{0:X} in slot {1}", h.ToInt64(), slot));
                    }
                } else if (slot < s.Windows.Count && !released.Contains(h)) {
                    int[] want = s.Windows[slot];
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

    // Keep the current left-to-right order: the leftmost windows go back to the recorded slots,
    // any window beyond the recorded count is left as it is.
    static bool Resize(Settings s) {
        if (s.Windows.Count == 0) {
            MessageBoxW(IntPtr.Zero, "配置がまだ記録されていません。\nGBF の窓を並べてから、設定画面の「今の配置を記録」を押してください。", "GBF-Resize", 0);
            return false;
        }
        List<IntPtr> windows = SortedGbfWindows(s);
        if (windows.Count == 0) {
            MessageBoxW(IntPtr.Zero, "GBF の窓が見つかりません。GBF を開いてから実行してください。", "GBF-Resize", 0);
            return false;
        }
        for (int i = 0; i < windows.Count && i < s.Windows.Count; i++) {
            int[] w = s.Windows[i];
            GbfWindow.Place(windows[i], w[0], w[1], w[2], w[3]);
        }
        return true;
    }

    // Save the open GBF windows (left to right) as the layout. Small hand-arranging errors are
    // cleaned up first: tops within SnapDistance of the leftmost window are aligned to it, and a
    // window whose visible frame is within SnapDistance of its left neighbour's is moved to touch it.
    static bool Record(Settings s) {
        List<IntPtr> windows = SortedGbfWindows(s);
        if (windows.Count == 0) {
            MessageBoxW(IntPtr.Zero, "GBF の窓が見つかりません。GBF を開いて並べてから実行してください。", "GBF-Resize", 0);
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
        s.Windows = layout;
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
