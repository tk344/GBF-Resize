// Settings window shown when GBF.exe is started without arguments.
// It only edits GBF.ini; the actual window work is done by running GBF.exe record / resize / launch
// as a separate process, so this window's DPI handling never mixes with the physical-pixel code.
using System;
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
                   "1. GBF を使いたい数だけ開き、好きな位置・大きさに並べる(「起動して試す」でも開ける)\n" +
                   "2. 「今の配置を記録」を押す(数 px の隙間や上端のずれは自動で詰める)\n" +
                   "3. 「デスクトップにショートカットを作成」を押す。以後はそのショートカットから起動する"
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

        layoutList.Dock = DockStyle.Fill;
        layoutList.Height = 90;
        layoutList.IntegralHeight = false;
        AddRow(grid, 4, "記録済みの配置", layoutList, null);

        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0) };
        buttons.Controls.Add(MakeButton("今の配置を記録", delegate { RunSelf("record"); }));
        buttons.Controls.Add(MakeButton("記録どおりに並べ直す", delegate { RunSelf("resize"); }));
        buttons.Controls.Add(MakeButton("起動して試す", delegate { RunSelf("launch", false); }));
        buttons.Controls.Add(MakeButton("デスクトップにショートカットを作成", delegate { CreateShortcuts(); }));
        grid.Controls.Add(buttons, 0, 5);
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

        layoutList.Items.Clear();
        for (int i = 0; i < settings.Windows.Count; i++) {
            int[] w = settings.Windows[i];
            layoutList.Items.Add(string.Format("窓{0}:  位置 ({1}, {2})   大きさ {3} × {4}", i + 1, w[0], w[1], w[2], w[3]));
        }
        if (settings.Windows.Count == 0) layoutList.Items.Add("(まだ記録されていません。起動するとウィンドウを1つ開くだけになります)");
    }

    void SaveFromForm() {
        var picked = browserBox.SelectedItem as BrowserInfo;
        settings.Browser = picked != null ? picked.Path : browserBox.Text.Trim();
        settings.Profile = profileBox.Text.Trim();
        settings.Url = urlBox.Text.Trim();
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
        MakeShortcut(Path.Combine(desktop, "GBF 起動.lnk"), exe, "launch", icon, "GBF を記録した配置で開く");
        MakeShortcut(Path.Combine(desktop, "GBF 並べ直し.lnk"), exe, "resize", exe + ",0", "開いている GBF を記録した配置に戻す");
        MessageBox.Show(this, "デスクトップに「GBF 起動」と「GBF 並べ直し」を作りました。", Text);
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
