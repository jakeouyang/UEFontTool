using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace UEFontTool;

public sealed class MainForm : Form
{
    static readonly Color Red = Color.FromArgb(235, 32, 39), PanelColor = Color.FromArgb(18, 18, 18);
    readonly TextBox game = Input(), font = Input(), filter = Input();
    readonly RichTextBox log = new() { Dock = DockStyle.Fill, ReadOnly = true, BackColor = PanelColor, ForeColor = Color.Silver, BorderStyle = BorderStyle.None, Font = new Font("Microsoft YaHei UI", 10), DetectUrls = false };
    readonly CheckedListBox targets = new() { Dock = DockStyle.Fill, CheckOnClick = true, BackColor = PanelColor, ForeColor = Color.Silver, BorderStyle = BorderStyle.None, HorizontalScrollbar = true, IntegralHeight = false };
    readonly Label status = new() { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(40, 200, 90), TextAlign = ContentAlignment.MiddleLeft };
    readonly Label subtitle = Label(), gameLabel = Label(), fontLabel = Label(), languagesLabel = Label(), targetLabel = Label(), guidance = Label();
    readonly CheckBox simplified = Check(), traditional = Check(), english = Check(), showEngine = Check();
    readonly Button remember;
    readonly Button language, scanButton, buildButton, installButton, restoreButton, exportButton, gameBrowse, fontBrowse, advanced;
    readonly List<Control> disabled = new();
    readonly HashSet<string> chosen = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,string> perFont = new(StringComparer.OrdinalIgnoreCase);
    readonly CheckBox protectIcons = new() { Text="保护私用字形 / PUA",Checked=true,AutoSize=true,ForeColor=Color.Silver };
    ScanReport? report;
    bool busy, filling, chinese = false;
    string? aesKey;
    Languages Languages => (simplified.Checked ? Languages.Simplified : 0) | (traditional.Checked ? Languages.Traditional : 0) | (english.Checked ? Languages.English : 0);
    string T(string en, string zh) => chinese ? zh : en;
    internal bool EnglishDefault=>!chinese&&language.Text=="中文";
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wparam, IntPtr lparam);

    public MainForm(bool loadSettings = true)
    {
        Text = "UNREAL ENGINE Font Tool"; BackColor = Color.Black; ForeColor = Red;
        Font = new Font("Microsoft YaHei UI", 10.5f);
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1080, 840); MinimumSize = new Size(1020, 780);
        FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterScreen;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28, 12, 28, 12), ColumnCount = 1, RowCount = 12 };
        foreach (int h in new[] { 58, 34, 48, 48, 42, 42, 42 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        Controls.Add(layout);
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, Margin = Padding.Empty };
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (int w in new[] { 120, 100, 42, 42 }) header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, w));
        var title = Label(); title.Name = "appTitle"; title.Text = "UNREAL ENGINE Font Tool"; title.Font = new Font("Segoe UI", 19);
        title.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); } };
        Icon = AppBranding.CreateIcon();
        var titleArea = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        titleArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        titleArea.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52)); titleArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var titleImage = new PictureBox { Image = AppBranding.CreateLogo(), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(40, 44), Anchor = AnchorStyles.Left, Margin = Padding.Empty };
        titleImage.Disposed += (_, _) => titleImage.Image?.Dispose();
        titleImage.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); } };
        titleArea.Controls.Add(titleImage, 0, 0); titleArea.Controls.Add(title, 1, 0); header.Controls.Add(titleArea, 0, 0);
        language = Button("English", () => SelectLanguage(!chinese));
        language.Dock = DockStyle.Fill; language.Margin = Padding.Empty; language.Padding = Padding.Empty;
        header.Controls.Add(language, 1, 0);
        var github = Button("GitHub", () => Process.Start(new ProcessStartInfo("https://github.com/jakeouyang/UEFontTool") { UseShellExecute = true }));
        github.Name = "github"; header.Controls.Add(github, 2, 0);
        header.Controls.Add(Button("—", () => WindowState = FormWindowState.Minimized), 3, 0);
        header.Controls.Add(Button("×", Close), 4, 0);
        foreach (var button in header.Controls.OfType<Button>()) { button.Dock = DockStyle.Fill; button.Margin = Padding.Empty; button.Padding = Padding.Empty; button.TextAlign = ContentAlignment.MiddleCenter; }
        layout.Controls.Add(header, 0, 0);
        subtitle.ForeColor = Color.Silver; layout.Controls.Add(subtitle, 0, 1);
        gameBrowse = Button("", BrowseGame); fontBrowse = Button("", BrowseFont);
        layout.Controls.Add(PathRow(gameLabel, game, gameBrowse), 0, 2);
        layout.Controls.Add(PathRow(fontLabel, font, fontBrowse), 0, 3);
        var langs = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Margin = Padding.Empty };
        langs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 136));
        for (int i = 0; i < 3; i++) langs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        langs.Controls.Add(languagesLabel, 0, 0); langs.Controls.Add(simplified, 1, 0); langs.Controls.Add(traditional, 2, 0); langs.Controls.Add(english, 3, 0);
        simplified.Checked = true;
        foreach (var box in new[] { simplified, traditional, english }) box.CheckedChanged += (_, _) => ApplyRecommendations();
        layout.Controls.Add(langs, 0, 4);
        advanced = Button("", EditAesKey);
        layout.Controls.Add(PathRow(targetLabel, filter, advanced), 0, 5);
        filter.TextChanged += (_, _) => FillTargets();
        guidance.ForeColor = Color.Gray; guidance.Font = new Font("Microsoft YaHei UI", 9);
        var selectionBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        selectionBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        selectionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); selectionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 206)); selectionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 176));
        showEngine.CheckedChanged += (_, _) => { if (!showEngine.Checked) chosen.RemoveWhere(LanguageMap.IsEngine); FillTargets(); };
        remember = Button("", () => {
            try { if (report == null) throw new InvalidOperationException(T("Scan first.", "请先扫描。")); LanguageMap.Save(report, Languages, chosen); Append(T("Saved this game's language mapping.", "已记住当前游戏的语言映射，下次扫描会使用它。")); }
            catch (Exception ex) { Append(ex.Message); }
        });
        remember.Dock = DockStyle.Fill; remember.Margin = Padding.Empty;
        var mappingMenu = new ContextMenuStrip{BackColor=Color.Black,ForeColor=Color.Silver,ShowImageMargin=false,Renderer=new TextMenuRenderer()};
        mappingMenu.Items.Add("恢复自动识别 / Reset language mapping", null, (_, _) => {
            try { if (report != null) { LanguageMap.Forget(report, Languages); ApplyRecommendations(); Append(T("Automatic language matching restored.", "已恢复自动识别。")); } }
            catch (Exception ex) { Append(ex.Message); }
        });
        remember.ContextMenuStrip = mappingMenu;
        selectionBar.Controls.Add(guidance, 0, 0); selectionBar.Controls.Add(showEngine, 1, 0); selectionBar.Controls.Add(remember, 2, 0);
        layout.Controls.Add(selectionBar, 0, 6);
        var listPanel = new Panel { Dock = DockStyle.Fill, BackColor = PanelColor, Padding = new Padding(10), Margin = new Padding(0, 4, 0, 4) };
        listPanel.Controls.Add(targets); layout.Controls.Add(listPanel, 0, 7);
        var detail = new TextBox { Dock=DockStyle.Bottom,Height=48,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=PanelColor,ForeColor=Color.Silver,BorderStyle=BorderStyle.None };
        var listBar = new FlowLayoutPanel { Dock=DockStyle.Top,Height=36,WrapContents=false };
        var all=Button("",()=>SetVisibleSelection(true));all.Name="selectAll";var none=Button("",()=>SetVisibleSelection(false));none.Name="clearAll";
        all.Width=150;none.Width=150;
        var cooked=Button("",()=>{using var window=new CookedForm(game.Text.Trim(),aesKey);window.ShowDialog(this);});cooked.Name="cookedTools";cooked.Width=190;
        var assign=Button("",()=>{using var d=new OpenFileDialog{Filter="TTF / OTF|*.ttf;*.otf"};if(d.ShowDialog(this)==DialogResult.OK){foreach(var p in chosen)perFont[p]=d.FileName;Append(T("Assigned font to checked resources.","已为勾选的资源分配字体。"));}});assign.Name="assignFont";assign.Width=180;
        listBar.Controls.AddRange(new Control[]{all,none,cooked,assign,protectIcons});listPanel.Controls.Add(detail);listPanel.Controls.Add(listBar);
        disabled.AddRange(new Control[]{assign,protectIcons});
        disabled.AddRange(new Control[]{all,none,cooked});
        targets.FormattingEnabled=true;targets.Format+=(_,e)=>{if(e.ListItem is string path)e.Value=Path.GetFileName(path);};
        targets.SelectedIndexChanged+=(_,_)=>{if(targets.SelectedItem is string path)detail.Text=path+Environment.NewLine+string.Join("; ",report?.Fonts.GetValueOrDefault(path)?.Select(s=>Path.GetFileName(s.Pak))??Array.Empty<string>())+" | "+perFont.GetValueOrDefault(path,font.Text);};
        targets.ItemCheck += (_, e) =>
        {
            if (filling) return;
            string path = (string)targets.Items[e.Index];
            if (e.NewValue == CheckState.Checked) chosen.Add(path); else chosen.Remove(path);
            UpdateSelectionSummary();
        };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, WrapContents = false };
        scanButton = Button("", () => Run("scan")); buildButton = Button("", () => Run("build"));
        installButton = Button("", () => Run("install")); restoreButton = Button("", () => Run("restore"));
        exportButton = Button("", () => Run("export"));
        foreach (var button in new[] { scanButton, buildButton, installButton, restoreButton, exportButton }) { button.Width = 196; actions.Controls.Add(button); }
        layout.Controls.Add(actions, 0, 8);
        var logPanel = new Panel { Dock = DockStyle.Fill, BackColor = PanelColor, Padding = new Padding(12), Margin = new Padding(0, 4, 0, 4) };
        logPanel.Controls.Add(log); layout.Controls.Add(logPanel, 0, 9);
        var hint = Label(); hint.ForeColor = Color.Gray; hint.Font = new Font("Microsoft YaHei UI", 9);
        hint.Name = "limit"; layout.Controls.Add(hint, 0, 10);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        footer.Controls.Add(status);
        var version = Label(); version.Text = "v" + Application.ProductVersion.Split('+')[0]; version.ForeColor = Color.Gray; version.TextAlign = ContentAlignment.MiddleRight; footer.Controls.Add(version);
        layout.Controls.Add(footer, 0, 11);
        disabled.AddRange(new Control[] { game, font, filter, gameBrowse, fontBrowse, simplified, traditional, english, targets, advanced, scanButton, buildButton, installButton, restoreButton, exportButton, language, showEngine, remember });
        try
        {
            string settings = Path.Combine(FontService.DataRoot, "settings.json");
            if (loadSettings && File.Exists(settings))
            {
                var saved = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(settings));
                if (saved != null) { game.Text = saved.GetValueOrDefault("game", game.Text); font.Text = saved.GetValueOrDefault("font", font.Text); }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException) { Append("设置读取失败 / Could not read saved settings: " + ex.Message); }
        game.AutoCompleteMode = AutoCompleteMode.SuggestAppend; game.AutoCompleteSource = AutoCompleteSource.CustomSource;
        if (!string.IsNullOrWhiteSpace(game.Text)) game.AutoCompleteCustomSource.Add(game.Text);
        game.TextChanged += (_, _) => { report = null; chosen.Clear();perFont.Clear(); FillTargets(); };
        FormClosing += (_, e) => { if (busy) { e.Cancel = true; Append(T("Please wait until the operation finishes.", "请等待当前操作完成。")); } };
        SelectLanguage(false);
        Append(T("Select a game and font, then scan. Suggested targets can be adjusted manually.", "选择游戏与新字体，先扫描；可手动调整自动勾选的目标。"));
        Append(T("Existing font mods are checked before installation. Build-only preserves the test package.", "安装前检查现有字体 Mod 冲突；“仅生成”可保留测试包供检查。"));
    }
    static Label Label() => new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty, AutoEllipsis = true };
    void EditAesKey()
    {
        using var dialog = new Form { Text = T("AES key (this session only)", "AES 密钥（仅本次会话）"), Icon=AppBranding.CreateIcon(), ClientSize = new Size(640, 190), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, BackColor = Color.Black, ForeColor = Color.Silver, Font = Font };
        var note = new Label { Text = T("Paste 64 hex digits (optional 0x prefix), or a 32-byte Base64 key.\nThe key is not saved to settings or logs. Leave empty to clear.", "粘贴 64 位十六进制（可带 0x）或 32 字节 Base64 密钥。\n密钥不保存到配置或日志；留空可清除。"), Bounds = new Rectangle(16, 12, 608, 54) };
        var input = new TextBox { Text = aesKey ?? "", UseSystemPasswordChar = true, Bounds = new Rectangle(16, 76, 608, 30), BackColor = PanelColor, ForeColor = Color.White };
        var show = new CheckBox { Text = T("Show key", "显示密钥"), Bounds = new Rectangle(16, 126, 180, 36) };
        show.CheckedChanged += (_, _) => input.UseSystemPasswordChar = !show.Checked;
        var ok = Button(T("Apply", "应用"), () => {
            try
            {
                PakTools.Keys(input.Text.Trim()); aesKey = input.Text.Trim(); report = null; chosen.Clear(); FillTargets();
                Append(T("AES input updated; scan again.", "AES 输入已更新，请重新扫描。")); dialog.DialogResult = DialogResult.OK;
            }
            catch (InvalidDataException ex) { MessageBox.Show(dialog, ex.Message, "AES", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        });
        ok.SetBounds(506, 124, 118, 40); dialog.AcceptButton = ok;
        dialog.Controls.AddRange(new Control[] { note, input, show, ok }); dialog.ShowDialog(this);
        input.Clear();
    }
    static CheckBox Check() => new() { Anchor = AnchorStyles.Left, AutoSize = true, ForeColor = Color.Silver };
    static TextBox Input() => new() { Dock = DockStyle.Fill, BackColor = PanelColor, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 7, 10, 0) };
    Button Button(string text, Action action)
    {
        var b = new TextButton { Text = text, ForeColor = Red, BackColor = Color.Black, Width = 98, Height = 40, Margin = new Padding(0, 2, 8, 2) };
        b.Click += (_, _) => action(); return b;
    }
    static Control PathRow(Label label, TextBox text, Button browse)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = Padding.Empty };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 136)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        row.Controls.Add(label, 0, 0); row.Controls.Add(text, 1, 0); row.Controls.Add(browse, 2, 0); return row;
    }
    void BrowseGame()
    {
        using var dialog = new FolderBrowserDialog { Description = T("Select the game root folder", "选择游戏根目录"), UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) game.Text = dialog.SelectedPath;
    }
    void BrowseFont()
    {
        using var dialog = new OpenFileDialog { Filter = "Font files (*.ttf;*.otf)|*.ttf;*.otf" };
        if (dialog.ShowDialog(this) == DialogResult.OK) font.Text = dialog.FileName;
    }
    public void SelectLanguage(bool value)
    {
        chinese = value; language.Text = T("中文", "English");
        void ActionText(string name,string en,string zh){var button=Controls.Find(name,true).OfType<Button>().SingleOrDefault();if(button!=null)button.Text=T(en,zh);}
        ActionText("selectAll","Select all","全选");ActionText("clearAll","Clear","清空");ActionText("cookedTools","IoStore","内联 / IoStore");ActionText("assignFont","Choose font","所选项新字体");protectIcons.Text=T("Protect custom glyphs","保护私用字形 / PUA");
        if(remember.ContextMenuStrip?.Items.Count>0)remember.ContextMenuStrip.Items[0].Text=T("Reset language mapping","恢复自动识别");
        subtitle.Text = T("Create an Unreal Engine font mod  /  Build, install and restore", "虚幻引擎字体 MOD  /  扫描、生成、安装与还原");
        gameLabel.Text = T("Game path", "游戏目录"); fontLabel.Text = T("Font file", "字体文件");
        languagesLabel.Text = T("Auto-select", "按语言勾选"); targetLabel.Text = T("Path filter", "路径筛选");
        simplified.Text = T("Simplified Chinese", "简体中文"); traditional.Text = T("Traditional Chinese", "繁體中文"); english.Text = T("English", "英文");
        gameBrowse.Text = fontBrowse.Text = T("Open", "浏览"); advanced.Text = "AES";
        scanButton.Text = T("Scan fonts", "扫描字体"); buildButton.Text = T("Build only", "仅生成");
        installButton.Text = T("Build and install", "生成并安装"); restoreButton.Text = T("Restore font", "还原字体");
        exportButton.Text = T("Export fonts", "导出字体");
        showEngine.Text = T("Engine fonts", "显示引擎字体"); remember.Text = T("Save mapping", "记住选择");
        UpdateSelectionSummary();
        var limit = Controls.Find("limit", true).First();
        limit.Text = T("Standalone fonts: scan above. Inline FontFace / IoStore: use the separate workflow; engine version is required.", "独立字体在此扫描；内联 FontFace / IoStore 请使用上方专用窗口，并确认引擎版本。");
        status.Text = T("Ready", "就绪");
    }
    void Append(string text) { log.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}"); log.ScrollToCaret(); }
    public void LoadPreview(string path)
    {
        var loaded = JsonSerializer.Deserialize<ScanReport>(File.ReadAllText(path))!;
        game.Text = loaded.Game; report = loaded; ApplyRecommendations();
        Append($"字体 / Fonts: {loaded.Fonts.Count}; PAK: {loaded.Containers}; IoStore: {loaded.IoStoreContainers}");
        foreach (var conflict in loaded.Conflicts.Values.SelectMany(v => v).Distinct()) Append("已有字体 Mod / Existing font mod: " + Path.GetFileName(conflict));
    }
    void ApplyRecommendations()
    {
        if (report == null) return;
        chosen.Clear();
        try { chosen.UnionWith(LanguageMap.Select(report, Languages, showEngine.Checked)); }
        catch (Exception ex) { Append(ex.Message); FillTargets(); return; }
        foreach (var language in new[] { Languages.Simplified, Languages.Traditional, Languages.English })
        {
            if (!Languages.HasFlag(language)) continue;
            int count = LanguageMap.Select(report, language, showEngine.Checked).Count;
            if (count == 0) Append(T($"No reliable mapping for {language}. Select the matching fonts manually, then Remember selection.", $"{language} 未找到可靠映射：请手动勾选对应字体，再点击“记住选择”。"));
        }
        FillTargets();
    }
    void UpdateSelectionSummary()
    {
        int visibleChosen = chosen.Count(p => p.Contains(filter.Text.Trim(), StringComparison.OrdinalIgnoreCase) && (showEngine.Checked || !LanguageMap.IsEngine(p)));
        guidance.Text = report == null ? T("Scan first; language choices select matching fonts.", "先扫描；语言选项会自动勾选匹配字体。") : T($"Selected {chosen.Count} · hidden by filter {chosen.Count - visibleChosen} · visible {targets.Items.Count}", $"已勾选 {chosen.Count} 项 · 筛选隐藏 {chosen.Count - visibleChosen} 项 · 显示 {targets.Items.Count} 项");
    }
    internal void CheckLayout()
    {
        SelectLanguage(false); PerformLayout();
        int text = TextRenderer.MeasureText(language.Text, language.Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
        if (language.ClientSize.Width < text + 12) throw new InvalidOperationException("Language button is too narrow.");
        SelectLanguage(true); PerformLayout();
        text = TextRenderer.MeasureText(language.Text, language.Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
        if (language.ClientSize.Width < text + 12) throw new InvalidOperationException("English label would be clipped.");
        if (Icon == null) throw new InvalidOperationException("Application icon is missing.");
        var github = Controls.Find("github", true).First();
        if (github.Top != language.Top || github.Height != language.Height) throw new InvalidOperationException("Header buttons are not vertically aligned.");
        var title = Controls.Find("appTitle", true).First();
        if (title.ClientSize.Height < TextRenderer.MeasureText(title.Text, title.Font).Height) throw new InvalidOperationException("Title height clips its text.");
    }
    internal void CheckSelection(ScanReport sample)
    {
        game.Text = sample.Game; report = sample;
        simplified.Checked = true; traditional.Checked = false; english.Checked = false;
        ApplyRecommendations();
        if (targets.Items.Cast<string>().Any(LanguageMap.IsEngine)) throw new InvalidOperationException("Engine fonts must be hidden by default.");
        if (chosen.Count != 1 || !chosen.Single().Contains("_SC_")) throw new InvalidOperationException("Simplified selection mismatch.");
        simplified.Checked = false; traditional.Checked = true;
        if (chosen.Count != 1 || !chosen.Single().Contains("_TC_")) throw new InvalidOperationException("Traditional selection mismatch.");
        showEngine.Checked = true;
        if (!targets.Items.Cast<string>().Any(LanguageMap.IsEngine)) throw new InvalidOperationException("Show Engine toggle failed.");
        filter.Text="_SC_";SetVisibleSelection(true);
        if(chosen.Count!=2)throw new InvalidOperationException("Select visible must retain hidden selections.");
        SetVisibleSelection(false);
        if(chosen.Count!=1 || !chosen.Single().Contains("_TC_"))throw new InvalidOperationException("Clear visible must preserve hidden selections.");
        filter.Clear();
        if(targets.Items.Count>0 && targets.GetItemText(targets.Items[0])!=Path.GetFileName((string)targets.Items[0]))throw new InvalidOperationException("List must show readable filenames.");
    }
    void FillTargets()
    {
        filling = true; targets.BeginUpdate(); targets.Items.Clear();
        if (report != null)
            foreach (string path in report.Fonts.Keys.Where(p => (showEngine.Checked || !LanguageMap.IsEngine(p)) && p.Contains(filter.Text.Trim(), StringComparison.OrdinalIgnoreCase)).OrderByDescending(p => chosen.Contains(p)).ThenBy(p => p.StartsWith("Engine/")).ThenBy(p => p))
                targets.Items.Add(path, chosen.Contains(path));
        targets.EndUpdate(); filling = false;
        UpdateSelectionSummary();
    }
    void SetVisibleSelection(bool value)
    {
        for(int i=0;i<targets.Items.Count;i++)targets.SetItemChecked(i,value);
        UpdateSelectionSummary();
    }
    async void Run(string command)
    {
        if (busy) return;
        string root = game.Text.Trim().Trim('"'), file = font.Text.Trim().Trim('"');
        string[] selection = chosen.ToArray();
        var mapped=selection.ToDictionary(p=>p,p=>perFont.GetValueOrDefault(p,file),StringComparer.OrdinalIgnoreCase);
        bool protect=protectIcons.Checked;
        if(string.IsNullOrWhiteSpace(file)&&mapped.Count>0)file=mapped.Values.First();
        if (command is "build" or "install" or "export" && report == null) { Append(T("Scan the game first.", "请先扫描游戏目录。")); return; }
        if (command == "export" && selection.Length == 0) { Append(T("Check at least one font to export.", "请先勾选要导出的字体。")); return; }
        busy = true; disabled.ForEach(c => c.Enabled = false); status.Text = T("Working…", "正在处理…");
        try
        {
            Directory.CreateDirectory(FontService.DataRoot);
            File.WriteAllText(Path.Combine(FontService.DataRoot, "settings.json"), JsonSerializer.Serialize(new Dictionary<string, string> { ["game"] = root, ["font"] = file }, FontService.Json));
            var progress = new Progress<string>(Append);
            Action<string> log = text => ((IProgress<string>)progress).Report(text);
            if (command == "scan")
            {
                advanced.ForeColor=Red;
                report = await Task.Run(() => FontService.Scan(root, aesKey, log)); ApplyRecommendations();
                foreach (var error in report.Errors.Concat(report.ModErrors)) Append(error);
                if(report.Errors.Any(error=>EncryptionHints.Failure(error,!string.IsNullOrWhiteSpace(aesKey))!=AesPrompt.None)||(string.IsNullOrWhiteSpace(aesKey)&&EncryptionHints.EncryptedArchives(report.Paks).Length>0))
                {advanced.ForeColor=Color.Khaki;Append(T("Encrypted resources need an AES key. Click AES key above, paste the key for this game build, then scan again. Read failures may also require version/format support.","检测到加密资源，需要 AES 密钥。点击上方“AES 密钥”，输入匹配当前游戏版本的密钥后重新扫描；读取失败也可能与版本或格式支持有关。"));}
                foreach (var conflict in report.Conflicts.Values.SelectMany(v => v).Distinct()) Append(T("Existing font mod: ", "已有字体 Mod：") + conflict);
                if (report.IoStoreContainers > 0) Append(T("This game uses IoStore containers; PAK fonts may not be what the runtime loads. Verify replacements in game.", "游戏含 IoStore 容器；PAK 内字体未必是运行时实际引用的资源，替换是否生效请游戏内验证。"));
            }
            else if (command == "restore") await Task.Run(() => FontService.Restore(root, log));
            else if (command == "export")
            {
                var current = report!;
                using var dialog = new FolderBrowserDialog { Description = T("Select the export folder", "选择导出目录"), UseDescriptionForTitle = true };
                if (dialog.ShowDialog() != DialogResult.OK) { status.Text = T("Ready", "就绪"); return; }
                string folder = dialog.SelectedPath;
                int count = await Task.Run(() => FontService.Export(current, selection, folder, aesKey, log));
                Append(T($"Exported {count} font(s) to {folder}.", $"已导出 {count} 个字体到 {folder}。"));
            }
            else
            {
                var current = report!;
                await Task.Run(() =>
                {
                    var manifest = FontService.Build(current, file, selection, aesKey, log,mapped,protect);
                    if (command == "install") FontService.Install(manifest, current, log);
                });
            }
            status.Text = T("Done", "完成");
        }
        catch (UnauthorizedAccessException) { status.Text = T("Access denied", "没有写入权限"); Append(T("Cannot write to this folder. Choose a writable location or run as administrator.", "无法写入该目录，请检查权限；需要时以管理员身份运行。")); }
        catch (Exception ex) { status.Text = T("Not completed", "操作未完成"); Append(ex.Message);if(EncryptionHints.Failure(ex.Message,!string.IsNullOrWhiteSpace(aesKey))!=AesPrompt.None){advanced.ForeColor=Color.Khaki;Append(T("Use the AES key button above to enter/check the key for this game build, then scan again.","点击上方“AES 密钥”输入或检查当前游戏版本的密钥，然后重新扫描。"));} }
        finally { busy = false; disabled.ForEach(c => c.Enabled = true); }
    }
    protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); using var pen = new Pen(Red); e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1); }
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == 0x84 && (int)m.Result == 1)
        {
            var p = PointToClient(Cursor.Position);
            if (p.X >= Width - 12 && p.Y >= Height - 12) m.Result = (IntPtr)17;
        }
    }
}
