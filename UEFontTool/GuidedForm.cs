using System.Text.Json;
using System.Diagnostics;

namespace UEFontTool;

public sealed class GuidedForm : Form
{
    readonly TextBox game=Input(),font=Input(),search=Input(),key=Input(),modName=Input();
    readonly ComboBox language=Combo(),view=Combo(),engine=Combo();
    readonly Label intro=Label(),gameLabel=Label(),fontLabel=Label(),langLabel=Label(),summary=Label(),selection=Label(),engineNote=Label();
    readonly Button gamePick=Button(),fontPick=Button(),analyze=Button(),build=Button(),open=Button(),advancedToggle=Button(),toggleLanguage=Button(),all=Button(),none=Button(),custom=Button(),reset=Button(),expert=Button(),preview=Button();
    readonly DataGridView grid=new(){Dock=DockStyle.Fill,AllowUserToAddRows=false,AutoGenerateColumns=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=true,BackgroundColor=Color.FromArgb(18,18,18),AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells,BorderStyle=BorderStyle.None};
    readonly TextBox details=new(){Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=Color.FromArgb(18,18,18),ForeColor=Color.Silver,BorderStyle=BorderStyle.None};
    readonly FlowLayoutPanel advanced=new(){Dock=DockStyle.Fill,AutoSize=true,Visible=false,WrapContents=true};
    readonly Label keyLabel=Label(),versionLabel=Label(),nameLabel=Label();
    readonly CheckBox allowSymbols=new(){AutoSize=true,ForeColor=Color.Khaki};
    readonly Button diagnosticButton=Button();string diagnosticText="";
    readonly List<GuideTarget> targets=new();
    ScanReport? scan;string? cookedFolder,detectedVersion,output;
    bool zh=true,busy,filling,hasAnalyzed,partial;string? problem;
    string T(string cn,string en)=>zh?cn:en;
    Languages ChosenLanguage=>language.SelectedIndex switch{1=>Languages.Traditional,2=>Languages.English,_=>Languages.Simplified};
    public GuidedForm(bool loadSettings=true)
    {
        Text="UNREAL ENGINE Font Tool · v0.7";ClientSize=new Size(1180,900);MinimumSize=new Size(1020,800);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.Black;ForeColor=Color.Silver;Font=new Font("Microsoft YaHei UI",10);AutoScaleMode=AutoScaleMode.Dpi;
        using var icon=typeof(MainForm).Assembly.GetManifestResourceStream("UEFontTool.Resources.app.ico")!;Icon=new Icon(icon);
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24,14,24,14),ColumnCount=1,RowCount=12};Controls.Add(layout);
        foreach(var h in new[]{56,24,42,42,42})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,72));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,72));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,26));
        var header=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3};header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));
        var brand=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};using var stream=typeof(MainForm).Assembly.GetManifestResourceStream("UEFontTool.Resources.icon.jpg")!;using var src=Image.FromStream(stream);var picture=new PictureBox{Image=new Bitmap(src),SizeMode=PictureBoxSizeMode.Zoom,Size=new Size(40,46)};picture.Disposed+=(_,_)=>picture.Image?.Dispose();
        var title=new Label{Font=new Font("Segoe UI",19),ForeColor=Color.FromArgb(235,32,39),AutoSize=true,Margin=new Padding(8,6,0,0),Text="UNREAL ENGINE Font Tool"};brand.Controls.Add(picture);brand.Controls.Add(title);header.Controls.Add(brand);header.Controls.Add(toggleLanguage);var github=Button();github.Text="GitHub";github.Click+=(_,_)=>Process.Start(new ProcessStartInfo("https://github.com/jakeouyang/UEFontTool"){UseShellExecute=true});header.Controls.Add(github);layout.Controls.Add(header,0,0);layout.Controls.Add(intro,0,1);
        layout.Controls.Add(PathRow(gameLabel,game,gamePick),0,2);layout.Controls.Add(PathRow(fontLabel,font,fontPick),0,3);
        var options=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};langLabel.Width=155;langLabel.AutoSize=false;langLabel.Height=36;language.Width=230;analyze.Width=190;advancedToggle.Width=150;options.Controls.AddRange(new Control[]{langLabel,language,analyze,advancedToggle});layout.Controls.Add(options,0,4);
        key.UseSystemPasswordChar=true;key.Width=200;engine.Width=115;modName.Text="zz_UEFontTool_P";modName.Width=170;
        foreach(var label in new[]{keyLabel,versionLabel,nameLabel}){label.AutoSize=true;label.Margin=new Padding(4,8,6,0);}
        diagnosticButton.Width=170;advanced.MaximumSize=new Size(0,120);advanced.Controls.AddRange(new Control[]{keyLabel,key,versionLabel,engine,nameLabel,modName,expert,allowSymbols,diagnosticButton});advanced.SetFlowBreak(expert,true);engineNote.AutoSize=true;engineNote.MaximumSize=new Size(900,24);engineNote.ForeColor=Color.DarkGray;advanced.Controls.Add(engineNote);layout.Controls.Add(advanced,0,5);
        diagnosticButton.Click+=(_,_)=>{using var dialog=new Form{Text=T("检测详情","Detection details"),Width=920,Height=560,StartPosition=FormStartPosition.CenterParent};dialog.Controls.Add(new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,Text=diagnosticText});dialog.ShowDialog(this);};
        summary.Padding=new Padding(10,6,10,6);summary.BackColor=Color.FromArgb(22,22,22);layout.Controls.Add(summary,0,6);
        var tools=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};view.Width=220;search.Width=160;all.Width=140;none.Width=140;preview.Width=210;tools.Controls.AddRange(new Control[]{view,search,all,none,preview});layout.Controls.Add(tools,0,7);
        grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(24,24,24);grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.Silver;grid.DefaultCellStyle.BackColor=Color.FromArgb(18,18,18);grid.DefaultCellStyle.ForeColor=Color.Silver;grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(70,22,26);grid.DefaultCellStyle.SelectionForeColor=Color.White;grid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;
        grid.Columns.Add(new DataGridViewCheckBoxColumn{Width=64});grid.Columns.Add(new DataGridViewTextBoxColumn{ReadOnly=true,Width=400});grid.Columns.Add(new DataGridViewTextBoxColumn{ReadOnly=true,Width=180});grid.Columns.Add(new DataGridViewTextBoxColumn{ReadOnly=true,Width=120,Visible=false});grid.Columns.Add(new DataGridViewTextBoxColumn{ReadOnly=true,AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,MinimumWidth=220});layout.Controls.Add(grid,0,8);
        layout.Controls.Add(details,0,9);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};build.Width=200;open.Width=180;custom.Width=290;reset.Width=250;actions.Controls.AddRange(new Control[]{build,open,custom,reset});layout.Controls.Add(actions,0,10);layout.Controls.Add(selection,0,11);
        engine.Items.Add("Auto");engine.Items.AddRange(GuideHints.EngineVersions);engine.SelectedIndex=0;
        gamePick.Click+=(_,_)=>{using var d=new FolderBrowserDialog();if(d.ShowDialog(this)==DialogResult.OK)game.Text=d.SelectedPath;};fontPick.Click+=(_,_)=>ChooseFont(p=>font.Text=p);
        toggleLanguage.Click+=(_,_)=>{zh=!zh;Localize();};advancedToggle.Click+=(_,_)=>advanced.Visible=!advanced.Visible;expert.Click+=(_,_)=>{using var form=new MainForm();form.ShowDialog(this);};
        analyze.Click+=(_,_)=>Analyze();build.Click+=(_,_)=>Build();preview.Click+=(_,_)=>PreviewOriginal();open.Click+=(_,_)=>{if(output!=null)Process.Start(new ProcessStartInfo(output){UseShellExecute=true});else details.Text=T("生成成功后可在这里打开可安装文件。","This opens the installable files after a successful build.");};
        custom.Click+=(_,_)=>{if(!targets.Any(t=>t.Selected)){details.Text=T("请先勾选要单独设置的项目。","Check the items to customize first.");return;}ChooseFont(p=>{foreach(var t in targets.Where(t=>t.Selected))t.FontOverride=p;Fill();});};reset.Click+=(_,_)=>{foreach(var t in targets.Where(t=>t.Selected))t.FontOverride=null;Fill();};
        language.SelectedIndexChanged+=(_,_)=>{if(!filling){Recommend();RefreshSummary();Fill();}};view.SelectedIndexChanged+=(_,_)=>{if(!filling)Fill();};search.TextChanged+=(_,_)=>Fill();all.Click+=(_,_)=>SelectVisible(true);none.Click+=(_,_)=>SelectVisible(false);
        game.TextChanged+=(_,_)=>InvalidateAnalysis();key.TextChanged+=(_,_)=>InvalidateAnalysis();engine.SelectedIndexChanged+=(_,_)=>InvalidateAnalysis();font.TextChanged+=(_,_)=>{output=null;Fill();};
        grid.CurrentCellDirtyStateChanged+=(_,_)=>{if(grid.IsCurrentCellDirty)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};grid.CellValueChanged+=(_,e)=>{if(!filling&&e.RowIndex>=0&&e.ColumnIndex==0){((GuideTarget)grid.Rows[e.RowIndex].Tag!).Selected=grid.Rows[e.RowIndex].Cells[0].Value is true;UpdateSelection();}};grid.SelectionChanged+=(_,_)=>ShowDetails();FormClosing+=(_,e)=>{if(busy)e.Cancel=true;};
        if(loadSettings)try{var p=Path.Combine(FontService.DataRoot,"settings.json");if(File.Exists(p)){var values=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(p));game.Text=values?.GetValueOrDefault("game")??"";font.Text=values?.GetValueOrDefault("font")??"";}}catch(IOException){}catch(JsonException){}
        search.PlaceholderText="搜索名称 / Search name";intro.AutoEllipsis=true;all.Width=160;none.Width=160;
        Localize();
    }
    static TextBox Input()=>new(){BackColor=Color.FromArgb(18,18,18),ForeColor=Color.White,BorderStyle=BorderStyle.FixedSingle,Margin=new Padding(0,6,8,0)};
    static ComboBox Combo()=>new(){DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Color.FromArgb(24,24,24),ForeColor=Color.Silver,Margin=new Padding(0,5,10,0)};
    static Label Label()=>new(){Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Margin=Padding.Empty};
    static Button Button()=>new(){Height=36,Width=100,BackColor=Color.Black,ForeColor=Color.FromArgb(235,65,70),FlatStyle=FlatStyle.Flat,Margin=new Padding(0,1,8,2),Cursor=Cursors.Hand};
    static Control PathRow(Label label,TextBox input,Button browse){var row=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty};row.RowStyles.Add(new RowStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,155));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));row.Controls.Add(label);input.Dock=DockStyle.Fill;row.Controls.Add(input);row.Controls.Add(browse);return row;}
    void ChooseFont(Action<string> apply){using var d=new OpenFileDialog{Filter="TTF / OTF|*.ttf;*.otf"};if(d.ShowDialog(this)==DialogResult.OK)apply(d.FileName);}
    void Localize()
    {
        filling=true;int l=language.SelectedIndex<0?0:language.SelectedIndex,v=view.SelectedIndex<0?0:view.SelectedIndex;language.Items.Clear();language.Items.AddRange(zh?new object[]{"简体中文","繁體中文","英文"}:new object[]{"Simplified Chinese","Traditional Chinese","English"});language.SelectedIndex=l;view.Items.Clear();view.Items.AddRange(zh?new object[]{"推荐替换项","全部游戏字体","语言待确认","引擎 / 回退字体"}:new object[]{"Recommended targets","All game fonts","Language unconfirmed","Engine / fallback fonts"});view.SelectedIndex=v;filling=false;
        toggleLanguage.Text=T("English","中文");intro.Text=T("选择游戏和新字体，工具会自动检测处理方式。没有可靠语言依据的项目不会自动勾选。","Choose a game, font and language. Analyze to find recommended fonts automatically.");gameLabel.Text=T("1  游戏目录","1  Game folder");fontLabel.Text=T("2  新字体","2  New font");langLabel.Text=T("游戏显示语言","Game language");gamePick.Text=fontPick.Text=T("浏览","Browse");analyze.Text=T("3  自动检测字体","3  Analyze fonts");advancedToggle.Text=T("高级选项","Advanced");
        keyLabel.Text=T("解密密钥","AES key");versionLabel.Text=T("引擎版本","Engine");nameLabel.Text=T("输出名称","Mod name");expert.Text=T("专家模式","Expert mode");expert.Width=110;diagnosticButton.Text=T("查看检测详情","Detection details");all.Text=T("全选当前列表","Select visible");none.Text=T("取消当前选择","Clear visible");build.Text=T("4  生成字体 MOD","4  Build font mod");open.Text=T("打开生成结果","Open output folder");custom.Text=T("单独更换勾选项字体","Change checked fonts");reset.Text=T("勾选项使用默认字体","Use default font");
        allowSymbols.Text=T("允许缺少特殊字形（可能影响图标）","Allow missing custom symbols (may affect icons)");preview.Text=T("查看原字体样式","Preview original font");string[] headers=zh?new[]{"选中","原字体名称","语言识别","处理方式","将使用的新字体"}:new[]{"Use","Original font","Language hint","Handling","Replacement font"};for(int i=0;i<headers.Length;i++)grid.Columns[i].HeaderText=headers[i];RefreshSummary();Fill();
    }
    void InvalidateAnalysis(){if(busy)return;scan=null;targets.Clear();cookedFolder=null;detectedVersion=null;hasAnalyzed=false;partial=false;problem=null;diagnosticText="";engineNote.Text="";allowSymbols.Checked=false;output=null;Fill();RefreshSummary();}
    void MarkAlternatives(){var embedded=targets.Where(t=>t.Inline).Select(t=>Path.ChangeExtension(t.Path,null)).ToHashSet(StringComparer.OrdinalIgnoreCase);foreach(var t in targets)t.AlternativeData=!t.Inline&&embedded.Contains(Path.ChangeExtension(t.Path,null));}
    void Recommend(){if(scan==null)return;MarkAlternatives();var saved=LanguageMap.Select(scan,ChosenLanguage,false);foreach(var t in targets){t.Recommended=!t.Engine&&!t.AlternativeData&&(t.Inline?(t.Language.Languages&ChosenLanguage)!=0:saved.Contains(t.Path)||(t.Language.Languages&ChosenLanguage)!=0);t.Selected=t.Recommended;}}
    void Fill()
    {
        if(grid.Columns.Count==0)return;filling=true;grid.Rows.Clear();
        var visible=targets.Where(t=>view.SelectedIndex switch{0=>t.Recommended,1=>!t.Engine,2=>!t.Engine&&t.Language.Languages==Languages.None,_=>t.Engine}).Where(t=>t.Path.Contains(search.Text.Trim(),StringComparison.OrdinalIgnoreCase));
        foreach(var t in visible.OrderByDescending(t=>t.Selected).ThenBy(t=>t.Path)){int i=grid.Rows.Add(t.Selected,Path.GetFileNameWithoutExtension(t.Path)+(t.AlternativeData?T("（备用数据）"," (alternative data)"):""),zh?t.Language.Zh:t.Language.En,t.Inline?T("自动处理封装","Embedded (auto)"):T("普通字体","Standalone"),t.FontOverride!=null?Path.GetFileName(t.FontOverride):string.IsNullOrWhiteSpace(font.Text)?T("请先选择新字体","Choose a font above"):T("上方新字体：","Default: ")+Path.GetFileName(font.Text));grid.Rows[i].Tag=t;grid.Rows[i].Cells[1].ToolTipText=t.Path;}
        filling=false;UpdateSelection();
    }
    void SelectVisible(bool value){foreach(DataGridViewRow r in grid.Rows){((GuideTarget)r.Tag!).Selected=value;r.Cells[0].Value=value;}UpdateSelection();}
    void UpdateSelection(){int count=targets.Count(t=>t.Selected),visible=grid.Rows.Cast<DataGridViewRow>().Count(r=>((GuideTarget)r.Tag!).Selected);selection.Text=T($"已勾选 {count} 项，其中 {count-visible} 项被当前筛选隐藏。未单独设置的项目都使用上方的新字体。",$"{count} selected; {count-visible} hidden by the filter. Items without an override use the font chosen above.");build.Enabled=!busy;open.Enabled=!busy;}
    void ShowDetails()
    {
        if(grid.CurrentRow?.Tag is not GuideTarget t)return;
        details.Text=T("完整路径：","Full path: ")+t.Path+Environment.NewLine+T("识别依据：","Evidence: ")+(zh?t.Language.ReasonZh:t.Language.ReasonEn)+Environment.NewLine+T("处理方式：","Handling: ")+(t.Inline?T("工具自动修改封装字体并生成配套文件，无需自行选择文件格式。","Embedded font; matching container files are generated automatically."):T("工具自动生成独立字体替换包。","A standalone font replacement package is generated automatically."))+Environment.NewLine+T("使用字体：","Replacement: ")+(t.FontOverride??font.Text)+(t.Cooked?.ResolvedContainer!=null?Environment.NewLine+T("来源容器：","Source container: ")+t.Cooked.ResolvedContainer:"");
    }
    void RefreshSummary()
    {
        if(busy)return;summary.ForeColor=problem!=null?Color.Khaki:Color.Silver;summary.Text=problem??(!hasAnalyzed?T("先完成上方第 1、2 步，再点击“自动检测字体”。不需要知道游戏是否使用内联字体。","Complete steps 1 and 2, then Analyze. You do not need to know whether the game uses embedded fonts."):T($"已检测：{targets.Count(t=>!t.Inline)} 项普通字体，{targets.Count(t=>t.Inline)} 项可处理的封装字体。{targets.Count(t=>!t.Engine&&t.Language.Languages==Languages.None)} 项语言待确认（未自动勾选）。",$"Detected {targets.Count(t=>!t.Inline)} standalone and {targets.Count(t=>t.Inline)} supported embedded fonts. {targets.Count(t=>!t.Engine&&t.Language.Languages==Languages.None)} language-unconfirmed items were not automatically selected.")+(partial?Environment.NewLine+T("部分资源未能识别，请查看高级选项中的说明。","Some resources were not inspected; see Advanced for details."):""));
        if(problem==null&&hasAnalyzed&&!targets.Any(t=>t.Recommended))summary.Text=T("未找到当前语言的可靠推荐。请切换“语言待确认”预览原字体，或查看“全部游戏字体”；有些游戏简繁共用，需手动确认。","No reliable recommendations for this language. Review Language unconfirmed or All game fonts and preview originals; some games share Chinese fonts.")+(partial?Environment.NewLine+T("另有部分资源未能识别，请查看检测详情。","Some resources could not be inspected; see Detection details."):"");
    }
    void SetBusy(bool value){busy=value;foreach(var c in new Control[]{game,font,gamePick,fontPick,language,analyze,advancedToggle,advanced,view,search,all,none,grid,custom,reset,toggleLanguage,preview})c.Enabled=!value;UpdateSelection();}
    async void PreviewOriginal()
    {
        if(busy||scan==null||grid.CurrentRow?.Tag is not GuideTarget target){details.Text=T("请先检测字体，再点击列表中的一项。","Analyze first, then click a font in the list.");return;}
        SetBusy(true);
        try
        {
            string folder=Path.Combine(FontService.DataRoot,"preview",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string file;
            if(target.Inline)
            {
                var bytes=await Task.Run(()=>CookedFonts.ExtractFace(Path.Combine(cookedFolder!,target.Path),detectedVersion!,target.Cooked!.Faces.First().Export));
                file=Path.Combine(folder,Path.GetFileNameWithoutExtension(target.Path)+(bytes.AsSpan(0,4).SequenceEqual("OTTO"u8)?".otf":".ttf"));File.WriteAllBytes(file,bytes);
            }
            else{string aes=key.Text;await Task.Run(()=>FontService.Export(scan,new[]{target.Path},folder,aes,_=>{}));file=Directory.GetFiles(folder).First();}
            using var sample=new FontSampleForm(file,zh);sample.ShowDialog(this);
        }
        catch(Exception ex){details.Text=T("无法预览此原字体：","Could not preview the original font: ")+ex.Message;}
        finally{SetBusy(false);}
    }
    async void Analyze()=>await AnalyzeAsync();
    async Task AnalyzeAsync()
    {
        if(busy)return;string root=game.Text.Trim(),aes=key.Text.Trim(),manual=engine.SelectedIndex>0?engine.Text:"";SetBusy(true);summary.Text=T("正在检测游戏字体，首次分析封装资源可能需要一些时间…","Analyzing game fonts; the first embedded-resource analysis may take a while…");string? old=Environment.GetEnvironmentVariable("UEFONTTOOL_AES_KEY");
        try
        {
            PakTools.Keys(aes);FontService.LocatePaks(root);targets.Clear();hasAnalyzed=false;output=null;problem=null;diagnosticText="";engineNote.Text="";partial=false;Directory.CreateDirectory(FontService.DataRoot);File.WriteAllText(Path.Combine(FontService.DataRoot,"settings.json"),JsonSerializer.Serialize(new Dictionary<string,string>{{"game",root},{"font",font.Text}},FontService.Json));
            scan=await Task.Run(()=>FontService.Scan(root,aes,_=>{}));foreach(var p in scan.Fonts.Keys)targets.Add(new(){Path=p,Language=GuideHints.Language(p,scan.Profile)});partial=scan.Errors.Count>0;
            if(scan.IoStoreContainers>0)
            {
                var hint=GuideHints.Engine(root);detectedVersion=string.IsNullOrEmpty(manual)?hint.Version:manual;engineNote.Text=zh?hint.Zh:hint.En;
                if(detectedVersion==null){partial=true;advanced.Visible=true;problem=T("发现封装资源，但无法确定游戏版本。请在高级选项选择引擎版本，再点击自动检测。普通字体结果仍可查看。","Embedded resources found, but engine version is unknown. Select a version in Advanced and analyze again; standalone results remain available.");}
                else
                {
                    cookedFolder=Path.Combine(FontService.DataRoot,"analysis",Guid.NewGuid().ToString("N"));Environment.SetEnvironmentVariable("UEFONTTOOL_AES_KEY",aes);
                    try{await Task.Run(()=>IoStoreFonts.Extract(root,cookedFolder,detectedVersion,"Font"));var reports=JsonSerializer.Deserialize<CookedReport[]>(File.ReadAllText(Path.Combine(cookedFolder,"font-analysis.json")))!;foreach(var r in reports.Where(r=>r.Faces.Count>0)){string p=Path.GetRelativePath(cookedFolder,r.Path).Replace('\\','/');targets.Add(new(){Path=p,Inline=true,Cooked=r,Language=GuideHints.Language(p,scan.Profile)});}partial|=reports.Any(r=>r.Error!=null);}
                    catch(Exception ex){partial=true;problem=T("普通字体已识别，但封装字体分析未完成。当前列表不完整，可在高级选项查看检测详情。","Standalone fonts were inspected, but embedded analysis did not finish. This list is incomplete; see Detection details in Advanced.");diagnosticText=ex.Message;engineNote.Text=T("封装字体读取失败，请查看检测详情并确认版本或密钥。","Embedded analysis failed; check version/key in Detection details.");advanced.Visible=true;}
                }
            }
            hasAnalyzed=true;Recommend();if(scan.Errors.Count>0){advanced.Visible=true;diagnosticText+=Environment.NewLine+string.Join(Environment.NewLine,scan.Errors);engineNote.Text+=Environment.NewLine+T($"{scan.Errors.Count} 个资源包读取失败；如需其中字体，请查看检测详情并检查密钥或格式支持。",$"{scan.Errors.Count} archives could not be read; see Detection details to check key/format support.");}
        }
        catch(Exception ex){problem=T("检测未完成：","Analysis incomplete: ")+ex.Message;hasAnalyzed=false;}
        finally{Environment.SetEnvironmentVariable("UEFONTTOOL_AES_KEY",old);SetBusy(false);RefreshSummary();Fill();}
    }
    async void Build()
    {
        if(busy)return;if(scan==null||!hasAnalyzed){details.Text=T("请先点击第 3 步“自动检测字体”。","Run step 3, Analyze fonts, first.");return;}grid.EndEdit();var selected=targets.Where(t=>t.Selected).ToArray();if(selected.Length==0){details.Text=T("请先勾选要替换的字体。没有推荐项时，可切换到“语言待确认”查看识别依据。","Check fonts to replace. If there are no recommendations, review Language unconfirmed.");return;}string root=game.Text.Trim(),defaultFont=font.Text.Trim(),aes=key.Text.Trim(),name=modName.Text.Trim();string? version=detectedVersion;var current=scan;
        foreach(var t in selected)if(!File.Exists(t.FontOverride??defaultFont)){details.Text=T("请选择上方新字体，或为勾选项单独选择字体。","Choose a new font above, or choose another font for each checked item.");return;}
        bool symbolLoss=allowSymbols.Checked;SetBusy(true);summary.Text=T("正在生成并检查字体 MOD…","Building and checking the font mod…");string? old=Environment.GetEnvironmentVariable("UEFONTTOOL_AES_KEY");
        try{IoStoreFonts.Name(name);Environment.SetEnvironmentVariable("UEFONTTOOL_AES_KEY",aes);string destination=Path.Combine(FontService.DataRoot,"guided-output",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..8]);Directory.CreateDirectory(destination);await Task.Run(()=>GuideBuild.Build(root,current,selected,defaultFont,aes,version,name,destination,symbolLoss));output=Path.Combine(destination,"ReadyToInstall");problem=null;details.Text=T("生成完成，文件已通过回读检查。点击“打开生成结果”，按其中的安装说明复制文件。\r\n游戏内字体效果仍需实际确认。\r\n","Build complete and readback passed. Open the output folder and follow its installation instructions.\r\nIn-game appearance still needs verification.\r\n")+output;}
        catch(Exception ex){problem=T("生成未完成：","Build incomplete: ")+ex.Message;details.Text=problem;if(ex.Message.Contains("private-use",StringComparison.OrdinalIgnoreCase)){advanced.Visible=true;engineNote.Text=T("原字体的私用字符不一定都是图标。建议换字体或取消该项；了解风险后，也可勾选允许缺少特殊字形再生成。","Private-use characters are not always icons. Choose another font or uncheck the target; explicitly allow symbol loss only if you accept the risk.");}}
        finally{Environment.SetEnvironmentVariable("UEFONTTOOL_AES_KEY",old);SetBusy(false);RefreshSummary();}
    }
    internal void Preview(string scanFile,string analysisFolder)
    {
        game.Text="GAME_DIR";font.Text="MY_FONT.ttf";
        scan=JsonSerializer.Deserialize<ScanReport>(File.ReadAllText(scanFile))!;targets.Clear();foreach(var p in scan.Fonts.Keys)targets.Add(new(){Path=p,Language=GuideHints.Language(p,scan.Profile)});
        foreach(var r in JsonSerializer.Deserialize<CookedReport[]>(File.ReadAllText(Path.Combine(analysisFolder,"font-analysis.json")))!.Where(r=>r.Faces.Count>0)){string p=Path.GetRelativePath(analysisFolder,r.Path).Replace('\\','/');targets.Add(new(){Path=p,Inline=true,Cooked=r,Language=GuideHints.Language(p,scan.Profile)});}
        hasAnalyzed=true;MarkAlternatives();foreach(var t in targets){t.Recommended=!t.Engine&&!t.AlternativeData&&(t.Language.Languages&ChosenLanguage)!=0;t.Selected=t.Recommended;}RefreshSummary();Fill();
    }
    public void SelectLanguage(bool chinese){zh=chinese;Localize();}
    internal async Task TestAnalysis(string root,string replacement,string resultFile)
    {
        game.Text=root;font.Text=replacement;await AnalyzeAsync();
        File.WriteAllText(resultFile,JsonSerializer.Serialize(new{Completed=hasAnalyzed,Partial=partial,Problem=problem,Engine=detectedVersion,Standalone=targets.Count(t=>!t.Inline),Embedded=targets.Count(t=>t.Inline),Recommended=targets.Count(t=>t.Selected),Unconfirmed=targets.Count(t=>!t.Engine&&t.Language.Languages==Languages.None),Targets=targets.Select(t=>new{t.Path,t.Inline,t.Selected,t.AlternativeData,t.Language})},FontService.Json));
        using var bitmap=new Bitmap(Width,Height);DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(resultFile+".png");
    }
    internal void CheckExperience(ScanReport sample)
    {
        scan=sample;targets.Clear();foreach(var path in sample.Fonts.Keys)targets.Add(new(){Path=path,Language=GuideHints.Language(path,sample.Profile)});
        string loose=sample.Fonts.Keys.Single(p=>p.Contains("_SC_"));targets.Add(new(){Path=Path.ChangeExtension(loose,".uasset"),Inline=true,Language=GuideHints.Language(loose,sample.Profile)});
        hasAnalyzed=true;Recommend();Fill();
        if(targets.Count(t=>t.Selected)!=1 || !targets.Single(t=>t.Selected).Inline)throw new Exception("Guided recommendations should prefer the supported embedded representation.");
        if(grid.Rows.Cast<DataGridViewRow>().Any(r=>((GuideTarget)r.Tag!).Engine))throw new Exception("Guided recommendations must hide engine fonts.");
        view.SelectedIndex=2;
        if(grid.Rows.Count!=1 || ((GuideTarget)grid.Rows[0].Tag!).Selected)throw new Exception("Unknown language should be visible for review, not automatically selected.");
        SelectVisible(true);SelectVisible(false);
        if(targets.Count(t=>t.Selected)!=1)throw new Exception("Selection operations must preserve hidden recommendations.");
        view.SelectedIndex=0;font.Text="default.ttf";
        if(!Convert.ToString(grid.Rows[0].Cells[4].Value)!.Contains("default.ttf"))throw new Exception("The default replacement must be explicit.");
        targets.Single(t=>t.Selected).FontOverride="custom.ttf";Fill();
        if(Convert.ToString(grid.Rows[0].Cells[4].Value)!="custom.ttf")throw new Exception("A custom replacement must be visible.");
        advanced.Visible=true;engineNote.Text=new string('x',400);Size=MinimumSize;PerformLayout();
        if(grid.Height<100)throw new Exception($"Advanced diagnostics must not obscure the font list: grid={grid.Height}, client={ClientSize.Height}, advanced={advanced.Height}.");
        SelectLanguage(false);SelectLanguage(true);
    }
}
