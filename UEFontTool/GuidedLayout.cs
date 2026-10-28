using System.Diagnostics;
using System.Text.Json;

namespace UEFontTool;

public sealed partial class GuidedForm
{
    readonly Button restoreFonts=Button(),moreActions=Button();
    readonly ToolStripMenuItem languageMenu=new(),githubMenu=new("GitHub"),settingsMenu=new();
    readonly SplitContainer infoSplit=new(){Size=new Size(1000,520),Orientation=Orientation.Horizontal,SplitterWidth=8,Panel1MinSize=80,Panel2MinSize=80,SplitterDistance=280,Dock=DockStyle.Fill,BackColor=Color.FromArgb(48,48,48)};
    readonly Label detailsHeading=new(){AutoSize=true,ForeColor=Color.Gray,Margin=new Padding(0,0,0,10)};
    bool installAfterBuild=true,preventActivation;
    protected override bool ShowWithoutActivation=>preventActivation;
    public GuidedForm(bool loadSettings=true)
    {
        preventActivation=!loadSettings;
        Text="UNREAL ENGINE Font Tool · v"+Application.ProductVersion.Split('+')[0];ClientSize=new Size(1180,940);MinimumSize=new Size(1000,760);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.Black;ForeColor=Color.Silver;Font=new Font("Microsoft YaHei UI",10);AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        Shown+=(_,_)=>{var area=Screen.FromControl(this).WorkingArea;MinimumSize=new Size(Math.Min(MinimumSize.Width,area.Width),Math.Min(MinimumSize.Height,area.Height));Size=new Size(Math.Min(Width,area.Width),Math.Min(Height,area.Height));Location=new Point(area.Left+(area.Width-Width)/2,area.Top+(area.Height-Height)/2);};
        using var icon=typeof(MainForm).Assembly.GetManifestResourceStream("UEFontTool.Resources.app.ico")!;Icon=new Icon(icon);
        var frame=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22,12,22,18),ColumnCount=1,RowCount=3,Margin=Padding.Empty};Controls.Add(frame);
        frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        frame.RowStyles.Add(new RowStyle(SizeType.Percent,100));frame.RowStyles.Add(new RowStyle(SizeType.AutoSize));frame.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Margin=Padding.Empty};frame.Controls.Add(scroll,0,0);
        var root=new TableLayoutPanel{MinimumSize=new Size(880,700),ColumnCount=1,RowCount=5,Margin=Padding.Empty,Padding=new Padding(0,0,8,0)};scroll.Controls.Add(root);
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        bool sizingContent=false;void SizeContent(){if(sizingContent)return;sizingContent=true;try{int width=scroll.ClientSize.Width-(root.MinimumSize.Height>scroll.ClientSize.Height?SystemInformation.VerticalScrollBarWidth:0);root.Size=new Size(Math.Max(root.MinimumSize.Width,width),Math.Max(root.MinimumSize.Height,scroll.ClientSize.Height));}finally{sizingContent=false;}}scroll.SizeChanged+=(_,_)=>SizeContent();Shown+=(_,_)=>SizeContent();
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Absolute,96));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var menu=new MenuStrip{Dock=DockStyle.Fill,AutoSize=true,BackColor=Color.Black,ForeColor=Color.Silver,Padding=new Padding(0,4,0,8),Font=Font,Renderer=new ToolStripProfessionalRenderer(new FontMenuColors())};
        foreach(var item in new[]{languageMenu,githubMenu,settingsMenu}){item.ForeColor=Color.Silver;item.Padding=new Padding(14,6,14,6);menu.Items.Add(item);}MainMenuStrip=menu;root.Controls.Add(menu,0,0);
        languageMenu.Click+=(_,_)=>{zh=!zh;Localize();};githubMenu.Click+=(_,_)=>Process.Start(new ProcessStartInfo("https://github.com/jakeouyang/UEFontTool"){UseShellExecute=true});settingsMenu.Click+=(_,_)=>ShowSettings();
        var inputs=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=3,RowCount=3,Margin=new Padding(0,0,0,8)};inputs.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));inputs.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for(int i=0;i<3;i++)inputs.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        foreach(var label in new[]{gameLabel,fontLabel,langLabel}){label.AutoSize=true;label.Dock=DockStyle.None;label.Anchor=AnchorStyles.Left;label.Margin=new Padding(0,6,10,8);}
        game.Dock=font.Dock=DockStyle.Fill;inputs.Controls.Add(gameLabel,0,0);inputs.Controls.Add(game,1,0);inputs.Controls.Add(gamePick,2,0);inputs.Controls.Add(fontLabel,0,1);inputs.Controls.Add(font,1,1);inputs.Controls.Add(fontPick,2,1);inputs.Controls.Add(langLabel,0,2);language.Width=300;language.Anchor=AnchorStyles.Left;inputs.Controls.Add(language,1,2);inputs.Controls.Add(analyze,2,2);root.Controls.Add(inputs,0,1);
        var statusPanel=new Panel{Dock=DockStyle.Fill,Padding=new Padding(12),BackColor=PanelColor,Margin=new Padding(0,4,0,8)};summary.Margin=Padding.Empty;statusPanel.Controls.Add(summary);root.Controls.Add(statusPanel,0,2);
        var toolbar=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,WrapContents=true,Margin=new Padding(0,8,0,8)};view.Width=260;search.Width=220;search.PlaceholderText=T("搜索字体","Search fonts");toolbar.Controls.AddRange(new Control[]{view,search,all,none,preview});root.Controls.Add(toolbar,0,3);
        grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=PanelColor;grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.Silver;grid.DefaultCellStyle.BackColor=PanelColor;grid.DefaultCellStyle.ForeColor=Color.Silver;grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(70,25,28);grid.DefaultCellStyle.SelectionForeColor=Color.White;grid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;
        grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.AutoSize;grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=PanelColor;grid.ColumnHeadersDefaultCellStyle.SelectionForeColor=Color.Silver;grid.DefaultCellStyle.Padding=new Padding(6,8,6,8);
        grid.Columns.Add(new DataGridViewCheckBoxColumn{Width=55});grid.Columns.Add(new DataGridViewTextBoxColumn{ReadOnly=true,AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,FillWeight=46,MinimumWidth=240});grid.Columns.Add(new DataGridViewTextBoxColumn{ReadOnly=true,Width=185});grid.Columns.Add(new DataGridViewTextBoxColumn{Visible=false});grid.Columns.Add(new DataGridViewTextBoxColumn{ReadOnly=true,AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,FillWeight=54,MinimumWidth=240});infoSplit.Panel1.Controls.Add(grid);
        var information=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=1,RowCount=2,BackColor=PanelColor};information.RowStyles.Add(new RowStyle(SizeType.AutoSize));information.RowStyles.Add(new RowStyle(SizeType.Percent,100));information.Controls.Add(detailsHeading,0,0);details.WordWrap=true;information.Controls.Add(details,0,1);infoSplit.Panel2.Controls.Add(information);root.Controls.Add(infoSplit,0,4);
        var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,WrapContents=true,Margin=new Padding(0,14,0,0)};build.MinimumSize=new Size(190,42);restoreFonts.MinimumSize=new Size(150,42);actions.Controls.AddRange(new Control[]{build,restoreFonts,moreActions});frame.Controls.Add(actions,0,1);selection.AutoSize=true;selection.Dock=DockStyle.None;selection.Margin=new Padding(0,8,0,0);frame.Controls.Add(selection,0,2);
        frame.SizeChanged+=(_,_)=>selection.MaximumSize=new Size(Math.Max(100,frame.ClientSize.Width-frame.Padding.Horizontal-8),0);
        Shown+=(_,_)=>{AlignFieldHeights();if(infoSplit.Height>infoSplit.Panel1MinSize+infoSplit.Panel2MinSize+infoSplit.SplitterWidth)infoSplit.SplitterDistance=Math.Clamp((int)(infoSplit.Height*.55),infoSplit.Panel1MinSize,infoSplit.Height-infoSplit.Panel2MinSize-infoSplit.SplitterWidth);};
        engine.Items.Add("Auto");engine.Items.AddRange(GuideHints.EngineVersions);engine.SelectedIndex=0;modName.Text="zz_UEFontTool_P";key.UseSystemPasswordChar=true;
        gamePick.Click+=(_,_)=>{using var dialog=new FolderBrowserDialog();if(dialog.ShowDialog(this)==DialogResult.OK)game.Text=dialog.SelectedPath;};fontPick.Click+=(_,_)=>ChooseFont(p=>font.Text=p);
        analyze.Click+=(_,_)=>Analyze();build.Click+=(_,_)=>{installAfterBuild=true;Build();};restoreFonts.Click+=async(_,_)=>await RestoreInstalled();preview.Click+=(_,_)=>PreviewOriginal();moreActions.Click+=(_,_)=>MoreMenu();
        language.SelectedIndexChanged+=(_,_)=>{if(!filling){Recommend();RefreshSummary();Fill();}};view.SelectedIndexChanged+=(_,_)=>{if(!filling)Fill();};search.TextChanged+=(_,_)=>Fill();all.Click+=(_,_)=>SelectVisible(true);none.Click+=(_,_)=>SelectVisible(false);
        game.TextChanged+=(_,_)=>InvalidateAnalysis();key.TextChanged+=(_,_)=>InvalidateAnalysis();engine.SelectedIndexChanged+=(_,_)=>InvalidateAnalysis();font.TextChanged+=(_,_)=>{output=null;Fill();};
        grid.CurrentCellDirtyStateChanged+=(_,_)=>{if(grid.IsCurrentCellDirty)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};grid.CellValueChanged+=(_,e)=>{if(!filling&&e.RowIndex>=0&&e.ColumnIndex==0&&grid.Rows[e.RowIndex].Tag is GuideTarget t){t.Selected=grid.Rows[e.RowIndex].Cells[0].Value is true;UpdateSelection();}};grid.SelectionChanged+=(_,_)=>ShowDetails();FormClosing+=(_,e)=>{if(busy)e.Cancel=true;};
        if(loadSettings)try{string file=Path.Combine(FontService.DataRoot,"settings.json");if(File.Exists(file)){var values=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(file));game.Text=values?.GetValueOrDefault("game")??"";font.Text=values?.GetValueOrDefault("font")??"";}}catch(IOException){}catch(JsonException){}
        Localize();summary.Text=T("选择游戏和新字体，检测后即可生成并安装。文件格式由工具自动处理。","Choose a game and font, detect, then build and install. Packaging is automatic.");
    }
    static Color PanelColor=>Color.FromArgb(22,22,22);
    void AlignFieldHeights()
    {
        int height=Math.Max(DeviceDpi*42/96,gamePick.Font.Height+16);
        foreach(var button in new[]{gamePick,fontPick,analyze,build,restoreFonts,moreActions,all,none,preview}){button.MinimumSize=new Size(button.MinimumSize.Width,height);button.Height=height;}
        foreach(var input in new[]{game,font,search}){input.MinimumSize=new Size(input.MinimumSize.Width,height);input.Height=height;}
    }
    protected override void OnDpiChanged(DpiChangedEventArgs e){base.OnDpiChanged(e);if(Visible)AlignFieldHeights();}
    void ShowSettings()
    {
        using var dialog=new DarkWindow{Text=T("设置","Settings"),Width=800,Height=480,StartPosition=FormStartPosition.CenterParent,BackColor=Color.Black,ForeColor=Color.Silver,Font=Font,MaximizeBox=false,MinimizeBox=false};
        var table=new TableLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(20),ColumnCount=2};table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));dialog.Controls.Add(table);
        var input=Input();input.Text=key.Text;input.UseSystemPasswordChar=true;var versions=Combo();versions.Items.Add(T("自动检测","Automatic"));versions.Items.AddRange(GuideHints.EngineVersions);versions.SelectedIndex=engine.SelectedIndex;var name=Input();name.Text=modName.Text;
        var labels=zh?new[]{"AES 密钥（需要时）","引擎版本","输出名称"}:new[]{"AES key (if required)","Engine version","Mod name"};Control[] fields={input,versions,name};for(int i=0;i<3;i++){table.RowStyles.Add(new RowStyle(SizeType.AutoSize));var label=new Label{Text=labels[i],AutoSize=true,Anchor=AnchorStyles.Left,Margin=new Padding(0,6,12,8)};if(fields[i] is FontInput field)field.FieldName=labels[i];table.Controls.Add(label,0,i);fields[i].Dock=DockStyle.Fill;table.Controls.Add(fields[i],1,i);}
        var risk=new CheckBox{Text=T("允许缺少特殊字符（可能影响图标）","Allow missing custom characters (may affect icons)"),Checked=allowSymbols.Checked,AutoSize=true,Margin=new Padding(0,10,0,10)};table.RowStyles.Add(new RowStyle(SizeType.AutoSize));table.Controls.Add(risk,0,3);table.SetColumnSpan(risk,2);
        var note=new Label{Text=T("通常保持自动即可。密钥仅用于本次会话，不保存到文件。","Usually keep Automatic. Keys are session-only and are not saved."),AutoSize=true,MaximumSize=new Size(650,0)};table.RowStyles.Add(new RowStyle(SizeType.AutoSize));table.Controls.Add(note,0,4);table.SetColumnSpan(note,2);
        var save=Button();save.Text=T("应用","Apply");table.RowStyles.Add(new RowStyle(SizeType.AutoSize));table.Controls.Add(save,1,5);save.Click+=(_,_)=>{try{PakTools.Keys(input.Text.Trim());IoStoreFonts.Name(name.Text.Trim());key.Text=input.Text.Trim();engine.SelectedIndex=versions.SelectedIndex;modName.Text=name.Text.Trim();allowSymbols.Checked=risk.Checked;dialog.DialogResult=DialogResult.OK;}catch(Exception ex){MessageBox.Show(dialog,ex.Message);}};dialog.AcceptButton=save;dialog.SetCaptionLanguage(zh);dialog.ShowDialog(this);input.Clear();
    }
    void MoreMenu()
    {
        var menu=new ContextMenuStrip();menu.Items.Add(T("仅生成，不安装","Build only"),null,(_,_)=>{installAfterBuild=false;Build();});menu.Items.Add(T("打开生成目录","Open output folder"),null,(_,_)=>{if(output!=null)Process.Start(new ProcessStartInfo(output){UseShellExecute=true});});menu.Items.Add(T("查看检测详情","Detection details"),null,(_,_)=>ShowDiagnostics());menu.Items.Add(T("为勾选项单独选新字体…","Choose another font for checked items…"),null,(_,_)=>ChooseFont(p=>{foreach(var t in targets.Where(t=>t.Selected))t.FontOverride=p;Fill();}));menu.Items.Add(T("勾选项恢复默认字体","Use default for checked items"),null,(_,_)=>{foreach(var t in targets.Where(t=>t.Selected))t.FontOverride=null;Fill();});menu.Items.Add(T("专家工具…","Expert tools…"),null,(_,_)=>{using var form=new MainForm();form.ShowDialog(this);});menu.Closed+=(_,_)=>menu.Dispose();menu.Show(moreActions,new Point(0,moreActions.Height));
    }
    void ShowDiagnostics(){using var dialog=new DarkWindow{Text=T("检测详情","Detection details"),Width=1050,Height=740,StartPosition=FormStartPosition.CenterParent};dialog.Controls.Add(new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,BackColor=PanelColor,ForeColor=Color.Silver,BorderStyle=BorderStyle.None,WordWrap=true,ScrollBars=ScrollBars.Vertical,Text=diagnosticText});dialog.SetCaptionLanguage(zh);dialog.ShowDialog(this);}
    bool ConfirmConflicts(string[] conflicts)
    {
        using var dialog=new DarkWindow{Text=T("已有字体 MOD 冲突","Existing font mod conflicts"),Width=1000,Height=650,StartPosition=FormStartPosition.CenterParent};var table=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(16),ColumnCount=1,RowCount=3};table.RowStyles.Add(new RowStyle(SizeType.Absolute,80));table.RowStyles.Add(new RowStyle(SizeType.Percent,100));table.RowStyles.Add(new RowStyle(SizeType.AutoSize));dialog.Controls.Add(table);table.Controls.Add(new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,BackColor=PanelColor,ForeColor=Color.Silver,BorderStyle=BorderStyle.None,ScrollBars=ScrollBars.Vertical,Text=T("这些文件修改相同字体。继续会整组暂存冲突 MOD，点击还原字体时恢复；不确定它们是否为 MOD 时请取消。","These files edit the same fonts. Continue to suspend their groups with backups; Restore brings them back. Cancel if unsure these are mods.")});table.Controls.Add(new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,BackColor=PanelColor,ForeColor=Color.Silver,BorderStyle=BorderStyle.None,WordWrap=true,ScrollBars=ScrollBars.Vertical,Text=string.Join(Environment.NewLine,conflicts)});var bar=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};var cancel=Button();cancel.Text=T("取消","Cancel");cancel.DialogResult=DialogResult.Cancel;var yes=Button();yes.Text=T("暂存冲突 MOD 并继续","Suspend conflicts and continue");yes.DialogResult=DialogResult.OK;bar.Controls.AddRange(new Control[]{cancel,yes});table.Controls.Add(bar);dialog.CancelButton=cancel;dialog.SetCaptionLanguage(zh);return dialog.ShowDialog(this)==DialogResult.OK;
    }
    async Task RestoreInstalled()
    {
        if(busy)return;SetBusy(true);try{string root=game.Text.Trim();await Task.Run(()=>ModInstaller.Restore(root));problem=null;summary.Text=T("已还原本工具的字体修改，并恢复此前暂存的冲突 MOD。","Tool font changes and previously suspended mods have been restored.");}catch(Exception ex){diagnosticText=ex.ToString();problem=T("还原未完成，请查看详情。","Restore incomplete; see details.");summary.Text=problem;}finally{SetBusy(false);}
    }
}
