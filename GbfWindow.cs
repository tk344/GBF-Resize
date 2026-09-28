// Shared Win32 helpers for GBF.exe and the PowerShell scripts (loaded with Add-Type -Path).
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public static class GbfWindow {
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextW(IntPtr hWnd, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassNameW(IntPtr hWnd, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out RECT r, int size);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
    [DllImport("user32.dll")] static extern bool GetMonitorInfoW(IntPtr hMonitor, ref MONITORINFO info);
    struct RECT { public int Left, Top, Right, Bottom; }
    struct POINT { public int X, Y; }
    struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }

    const uint SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;
    const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    // Work in physical pixels regardless of the display scale (Per-Monitor V2).
    public static void UsePhysicalPixels() {
        SetThreadDpiAwarenessContext(new IntPtr(-4));
    }

    // Visible, non-minimized Chromium-browser windows (Chrome, Edge, Iron, ... all use the
    // Chrome_WidgetWin_1 class) whose title is exactly one of `titles`.
    // Enumerates windows directly: Get-Process's MainWindowTitle only exposes one window
    // per process, and Chromium keeps all its windows in one browser process.
    public static IntPtr[] FindAll(params string[] titles) {
        var wanted = new HashSet<string>(titles);
        var found = new List<IntPtr>();
        EnumWindows((h, l) => {
            if (!IsWindowVisible(h) || IsIconic(h)) return true;
            var cls = new StringBuilder(64); GetClassNameW(h, cls, 64);
            if (cls.ToString() != "Chrome_WidgetWin_1") return true;
            var txt = new StringBuilder(256); GetWindowTextW(h, txt, 256);
            if (wanted.Contains(txt.ToString())) found.Add(h);
            return true;
        }, IntPtr.Zero);
        return found.ToArray();
    }

    // Work area (without the taskbar) of the primary monitor as {left, top, right, bottom}.
    public static int[] PrimaryWorkArea() {
        var info = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
        GetMonitorInfoW(MonitorFromPoint(new POINT { X = 0, Y = 0 }, 1 /* MONITOR_DEFAULTTOPRIMARY */), ref info);
        return new[] { info.rcWork.Left, info.rcWork.Top, info.rcWork.Right, info.rcWork.Bottom };
    }

    // Outer bounds as {x, y, width, height} (GetWindowRect coordinates, what Place() takes).
    public static int[] Bounds(IntPtr h) {
        RECT r;
        if (!GetWindowRect(h, out r)) return null;
        return new[] { r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top };
    }

    // Visible frame as {left, top, right, bottom}, i.e. without the invisible resize borders.
    public static int[] VisibleFrame(IntPtr h) {
        RECT r;
        if (DwmGetWindowAttribute(h, DWMWA_EXTENDED_FRAME_BOUNDS, out r, Marshal.SizeOf(typeof(RECT))) != 0
            && !GetWindowRect(h, out r)) return null;
        return new[] { r.Left, r.Top, r.Right, r.Bottom };
    }

    // Resize without moving, re-ordering or focusing. Returns true only if the size had to change.
    public static bool FitSize(IntPtr h, int width, int height) {
        RECT r;
        if (!GetWindowRect(h, out r)) return false;
        if (r.Right - r.Left == width && r.Bottom - r.Top == height) return false;
        SetWindowPos(h, IntPtr.Zero, 0, 0, width, height, SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
        return true;
    }

    // Left edge in GetWindowRect coordinates (used to keep the current left-to-right order).
    public static int LeftOf(IntPtr h) {
        RECT r;
        return GetWindowRect(h, out r) ? r.Left : int.MaxValue;
    }

    // Put the window's top-left at (x, y) with the given size (GetWindowRect coordinates).
    public static void Place(IntPtr h, int x, int y, int width, int height) {
        SetWindowPos(h, IntPtr.Zero, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
    }

    // Place `h` directly to the right of `left`, top at y, so the *visible* frames touch.
    // Windows 10/11 windows have invisible resize borders (a few px on left/right/bottom),
    // so lining windows up by GetWindowRect would leave a visible gap between them.
    public static void PlaceRightOf(IntPtr h, IntPtr left, int y, int width, int height) {
        RECT leftFrame;
        if (DwmGetWindowAttribute(left, DWMWA_EXTENDED_FRAME_BOUNDS, out leftFrame, Marshal.SizeOf(typeof(RECT))) != 0) {
            GetWindowRect(left, out leftFrame);
        }
        // First place it roughly, then measure its own invisible border (depends on the monitor's DPI) and correct.
        Place(h, leftFrame.Right, y, width, height);
        RECT outer, frame;
        if (!GetWindowRect(h, out outer)) return;
        if (DwmGetWindowAttribute(h, DWMWA_EXTENDED_FRAME_BOUNDS, out frame, Marshal.SizeOf(typeof(RECT))) != 0) return;
        int dx = leftFrame.Right - frame.Left;
        if (dx != 0) Place(h, outer.Left + dx, y, width, height);
    }
}
