// Settings window shown when GBF.exe is started without arguments.
// It only edits GBF.ini; the actual window work is done by running GBF.exe record / resize / launch
// as a separate process, so this window's DPI handling never mixes with the physical-pixel code.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

public class SettingsForm : Form {
    readonly string iniPath;
    Settings settings;

    readonly ComboBox browserBox = new ComboBox();
    readonly TextBox profileBox = new TextBox();
    readonly TextBox urlBox = new TextBox();
    readonly ComboBox countBox = new ComboBox();
    readonly ListBox layoutList = new ListBox();

    public SettingsForm(string iniPath) {
        this.iniPath = iniPath;
        Text = "GBF-Resize (非公式)";
        Font = new Font("Yu Gothic UI", 9f);
        AutoScaleMode = AutoScaleMode.Font;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var grid = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, Padding = new Padding(10), Dock = DockStyle.Fill };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        Controls.Add(grid);

        var steps = new Label {
            AutoSize = true, MaximumSize = new Size(560, 0), Margin = new Padding(3, 3, 3, 10),
            Text = "使い方\n" +
                   "1. 「窓の数」を選んで「起動して試す」を押す(記録がなければ左上から横に並べて開く)\n" +
                   "2. 好きな位置・大きさに直して「今の配置を記録」を押す(数 px の隙間や上端のずれは自動で詰める)\n" +
                   "3. 「デスクトップにショートカットを作成」を押す。以後はそのショートカットから起動する\n" +
                   "配置は窓の数ごとに記録される。使う数ごとに同じ手順を行えば、それぞれのショートカットができる"
        };
        grid.Controls.Add(steps, 0, 0);
        grid.SetColumnSpan(steps, 3);

        browserBox.DropDownStyle = ComboBoxStyle.DropDown;
        browserBox.Dock = DockStyle.Fill;
        foreach (BrowserInfo b in Browsers.Detect()) browserBox.Items.Add(b);
        var browse = new Button { Text = "参照...", AutoSize = true };
        browse.Click += delegate { BrowseForBrowser(); };
        AddRow(grid, 1, "ブラウザ", browserBox, browse);

        profileBox.Dock = DockStyle.Fill;
        AddRow(grid, 2, "プロファイル", profileBox, new Label { Text = "空欄なら指定しない", AutoSize = true, Anchor = AnchorStyles.Left });

        urlBox.Dock = DockStyle.Fill;
        AddRow(grid, 3, "URL", urlBox, null);

        countBox.DropDownStyle = ComboBoxStyle.DropDownList;
        countBox.Width = 80;
        for (int n = 1; n <= Settings.MaxWindows; n++) countBox.Items.Add(n + " 窓");
        countBox.SelectedIndexChanged += delegate { ShowLayout(); };
        AddRow(grid, 4, "窓の数", countBox, null);

        layoutList.Dock = DockStyle.Fill;
        layoutList.Height = 70;
        layoutList.IntegralHeight = false;
        AddRow(grid, 5, "記録済みの配置", layoutList, null);

        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0) };
        buttons.Controls.Add(MakeButton("今の配置を記録", delegate { RunSelf("record"); }));
        buttons.Controls.Add(MakeButton("記録どおりに並べ直す", delegate { RunSelf("resize"); }));
        buttons.Controls.Add(MakeButton("起動して試す", delegate { RunSelf("launch " + SelectedCount(), false); }));
        buttons.Controls.Add(MakeButton("デスクトップにショートカットを作成", delegate { CreateShortcuts(); }));
        grid.Controls.Add(buttons, 0, 6);
        grid.SetColumnSpan(buttons, 3);

        LoadIntoForm();
        FormClosing += delegate { SaveFromForm(); };
    }

    static void AddRow(TableLayoutPanel grid, int row, string label, Control main, Control extra) {
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        grid.Controls.Add(main, 1, row);
        if (extra != null) grid.Controls.Add(extra, 2, row);
    }

    static Button MakeButton(string text, EventHandler onClick) {
        var b = new Button { Text = text, AutoSize = true, Padding = new Padding(6, 2, 6, 2) };
        b.Click += onClick;
        return b;
    }

    void LoadIntoForm() {
        settings = Settings.Load(iniPath);
        browserBox.SelectedItem = null;
        foreach (BrowserInfo b in browserBox.Items) {
            if (string.Equals(b.Path, settings.Browser, StringComparison.OrdinalIgnoreCase)) browserBox.SelectedItem = b;
        }
        if (browserBox.SelectedItem == null) browserBox.Text = settings.Browser;
        profileBox.Text = settings.Profile;
        urlBox.Text = settings.Url;
        countBox.SelectedIndex = settings.Count - 1;
        ShowLayout();
    }

    int SelectedCount() { return countBox.SelectedIndex + 1; }

    void ShowLayout() {
        int count = SelectedCount();
        List<int[]> layout = settings.Layout(count);
        layoutList.Items.Clear();
        if (layout == null) {
            layoutList.Items.Add(string.Format("({0} 窓の配置はまだ記録されていません。起動すると左上から横に並べて開きます)", count));
            return;
        }
        for (int i = 0; i < layout.Count; i++) {
            int[] w = layout[i];
            layoutList.Items.Add(string.Format("窓{0}:  位置 ({1}, {2})   大きさ {3} × {4}", i + 1, w[0], w[1], w[2], w[3]));
        }
    }

    void SaveFromForm() {
        var picked = browserBox.SelectedItem as BrowserInfo;
        settings.Browser = picked != null ? picked.Path : browserBox.Text.Trim();
        settings.Profile = profileBox.Text.Trim();
        settings.Url = urlBox.Text.Trim();
        settings.Count = SelectedCount();
        settings.Save(iniPath);
    }

    void BrowseForBrowser() {
        using (var dlg = new OpenFileDialog { Filter = "ブラウザの exe (*.exe)|*.exe", Title = "Chromium 系ブラウザの exe を選ぶ" }) {
            if (dlg.ShowDialog(this) == DialogResult.OK) {
                browserBox.SelectedItem = null;
                browserBox.Text = dlg.FileName;
            }
        }
    }

    // Save the form, run "GBF.exe <mode>" and (unless it keeps running, like launch) reload the result.
    void RunSelf(string mode, bool wait = true) {
        SaveFromForm();
        Process p = Process.Start(Assembly.GetExecutingAssembly().Location, mode);
        if (!wait) return;
        UseWaitCursor = true;
        p.WaitForExit();
        UseWaitCursor = false;
        LoadIntoForm();
    }

    void CreateShortcuts() {
        SaveFromForm();
        string exe = Assembly.GetExecutingAssembly().Location;
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string icon = File.Exists(settings.Browser) ? settings.Browser + ",0" : exe + ",0";
        int count = SelectedCount();
        string launchName = string.Format("GBF 起動 ({0}窓)", count);
        MakeShortcut(Path.Combine(desktop, launchName + ".lnk"), exe, "launch " + count, icon, string.Format("GBF を {0} 窓、記録した配置で開く", count));
        MakeShortcut(Path.Combine(desktop, "GBF 並べ直し.lnk"), exe, "resize", exe + ",0", "開いている GBF を、その窓の数で記録した配置に戻す");
        MessageBox.Show(this, "デスクトップに「" + launchName + "」と「GBF 並べ直し」を作りました。", Text);
    }

    // WScript.Shell through late binding, so no COM interop assembly is needed.
    static void MakeShortcut(string path, string target, string args, string icon, string description) {
        Type shellType = Type.GetTypeFromProgID("WScript.Shell");
        object shell = Activator.CreateInstance(shellType);
        object link = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
        Type linkType = link.GetType();
        linkType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { target });
        linkType.InvokeMember("Arguments", BindingFlags.SetProperty, null, link, new object[] { args });
        linkType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { Path.GetDirectoryName(target) });
        linkType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, link, new object[] { icon });
        linkType.InvokeMember("Description", BindingFlags.SetProperty, null, link, new object[] { description });
        linkType.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
    }
}
