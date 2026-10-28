using System.Text.Json;
using System.Diagnostics;

namespace UEFontTool;

public sealed partial class GuidedForm : DarkWindow
{
    readonly FontInput game=Input(),font=Input(),search=Input(),key=Input(),modName=Input();
    readonly ComboBox language=Combo(),view=Combo(),engine=Combo();
    readonly Label intro=Label(),gameLabel=Label(),fontLabel=Label(),langLabel=Label(),selection=Label(),engineNote=Label();
    readonly TextBox summary=new(){Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,WordWrap=true,ScrollBars=ScrollBars.Vertical,BackColor=Color.FromArgb(22,22,22),ForeColor=Color.Silver,BorderStyle=BorderStyle.None,Margin=new Padding(0,6,0,6)};
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
    static FontInput Input()=>new();
    static FontCombo Combo()=>new();
    static Label Label()=>new(){Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Margin=Padding.Empty};
    static Button Button()=>new FontActionButton();
    void ChooseFont(Action<string> apply){using var d=new OpenFileDialog{Filter="TTF / OTF|*.ttf;*.otf"};if(d.ShowDialog(this)==DialogResult.OK)apply(d.FileName);}
    void Localize()
    {
        filling=true;int l=language.SelectedIndex<0?0:language.SelectedIndex,v=view.SelectedIndex<0?0:view.SelectedIndex;language.Items.Clear();language.Items.AddRange(zh?new object[]{"简体中文","繁體中文","英文"}:new object[]{"Simplified Chinese","Traditional Chinese","English"});language.SelectedIndex=l;view.Items.Clear();view.Items.AddRange(zh?new object[]{"推荐替换项","全部游戏字体","语言待确认","引擎 / 回退字体"}:new object[]{"Recommended targets","All game fonts","Language unconfirmed","Engine / fallback fonts"});view.SelectedIndex=v;view.Width=Math.Max(230,view.Items.Cast<string>().Max(text=>TextRenderer.MeasureText(text,view.Font).Width)+40);view.DropDownWidth=view.Width;filling=false;
        SetCaptionLanguage(zh);detailsHeading.Text=T("字体详情 · 拖动上方分隔条可调整高度","Font details · drag the divider to resize");search.PlaceholderText=T("搜索字体","Search fonts");languageMenu.Text=T("English","中文");gameLabel.Text=T("游戏目录","Game folder");fontLabel.Text=T("新字体","New font");langLabel.Text=T("游戏语言","Game language");game.FieldName=gameLabel.Text;font.FieldName=fontLabel.Text;search.FieldName=T("搜索字体","Search fonts");gamePick.Text=fontPick.Text=T("浏览…","Browse…");analyze.Text=T("检测字体","Detect fonts");settingsMenu.Text=T("设置","Settings");restoreFonts.Text=T("还原字体","Restore fonts");moreActions.Text=T("更多操作 ▾","More ▾");
        all.Text=T("全选","Select all");none.Text=T("清空","Clear");build.Text=T("生成并安装","Build and install");
        allowSymbols.Text=T("允许缺少特殊字形（可能影响图标）","Allow missing custom symbols (may affect icons)");preview.Text=T("预览原字体","Preview original");string[] headers=zh?new[]{"✓","原字体名称","语言识别","处理方式","将使用的新字体"}:new[]{"Use","Original font","Language hint","Handling","Replacement font"};for(int i=0;i<headers.Length;i++)grid.Columns[i].HeaderText=headers[i];grid.Columns[2].Width=zh?185:Math.Max(220,TextRenderer.MeasureText("Traditional Chinese",grid.Font).Width+32);if(Visible)AlignFieldHeights();RefreshSummary();Fill();
    }
    void InvalidateAnalysis(){if(busy)return;scan=null;targets.Clear();cookedFolder=null;detectedVersion=null;hasAnalyzed=false;partial=false;problem=null;diagnosticText="";engineNote.Text="";allowSymbols.Checked=false;output=null;Fill();RefreshSummary();}
    void MarkAlternatives(){var embedded=targets.Where(t=>t.Inline).Select(t=>Path.ChangeExtension(t.Path,null)).ToHashSet(StringComparer.OrdinalIgnoreCase);foreach(var t in targets)t.AlternativeData=!t.Inline&&embedded.Contains(Path.ChangeExtension(t.Path,null));}
    void Recommend(){if(scan==null)return;MarkAlternatives();var saved=LanguageMap.Select(scan,ChosenLanguage,false);foreach(var t in targets){t.Recommended=!t.Engine&&!t.AlternativeData&&(t.Inline?(t.Language.Languages&ChosenLanguage)!=0:saved.Contains(t.Path)||(t.Language.Languages&ChosenLanguage)!=0);t.Selected=t.Recommended;}}
    void Fill()
    {
        if(grid.Columns.Count==0)return;filling=true;grid.Rows.Clear();
        var visible=targets.Where(t=>view.SelectedIndex switch{0=>t.Recommended,1=>!t.Engine,2=>!t.Engine&&t.Language.Languages==Languages.None,_=>t.Engine}).Where(t=>t.Path.Contains(search.Text.Trim(),StringComparison.OrdinalIgnoreCase));
        foreach(var t in visible.OrderByDescending(t=>t.Selected).ThenBy(t=>t.Path)){int i=grid.Rows.Add(t.Selected,Path.GetFileNameWithoutExtension(t.Path)+(t.AlternativeData?T("（备用数据）"," (alternative data)"):""),zh?t.Language.Zh:t.Language.En,t.Inline?T("自动处理封装","Embedded (auto)"):T("普通字体","Standalone"),t.FontOverride!=null?Path.GetFileName(t.FontOverride):string.IsNullOrWhiteSpace(font.Text)?T("请先选择新字体","Choose a font above"):T("上方新字体：","Default: ")+Path.GetFileName(font.Text));grid.Rows[i].Tag=t;grid.Rows[i].Cells[1].ToolTipText=t.Path;}
        filling=false;UpdateSelection();if(grid.Rows.Count>0){grid.CurrentCell=grid.Rows[0].Cells[1];ShowDetails();}else details.Text="";
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
        if(busy)return;summary.ForeColor=problem!=null?Color.Khaki:Color.Silver;summary.Text=problem??(!hasAnalyzed?T("选择游戏和新字体，再点击“检测字体”。文件格式由工具自动处理。","Choose a game and font, then Detect fonts. Packaging is automatic."):T($"已检测：{targets.Count(t=>!t.Inline)} 项普通字体，{targets.Count(t=>t.Inline)} 项可处理的封装字体。{targets.Count(t=>!t.Engine&&t.Language.Languages==Languages.None)} 项语言待确认（未自动勾选）。",$"Detected {targets.Count(t=>!t.Inline)} standalone and {targets.Count(t=>t.Inline)} supported embedded fonts. {targets.Count(t=>!t.Engine&&t.Language.Languages==Languages.None)} language-unconfirmed items were not automatically selected.")+(partial?Environment.NewLine+T("部分资源未能识别，请在“更多操作”查看检测详情。","Some resources were not inspected; see More → Detection details."):""));
        if(problem==null&&hasAnalyzed&&!targets.Any(t=>t.Recommended))summary.Text=T("未找到当前语言的可靠推荐。请切换“语言待确认”预览原字体，或查看“全部游戏字体”；有些游戏简繁共用，需手动确认。","No reliable recommendations for this language. Review Language unconfirmed or All game fonts and preview originals; some games share Chinese fonts.")+(partial?Environment.NewLine+T("另有部分资源未能识别，请查看检测详情。","Some resources could not be inspected; see Detection details."):"");
    }
    void SetBusy(bool value){busy=value;UseWaitCursor=value;languageMenu.Enabled=githubMenu.Enabled=settingsMenu.Enabled=!value;foreach(var c in new Control[]{game,font,gamePick,fontPick,language,analyze,advancedToggle,advanced,view,search,all,none,grid,custom,reset,toggleLanguage,preview,restoreFonts,moreActions})c.Enabled=!value;UpdateSelection();}
    async void PreviewOriginal()
    {
        if(busy||scan==null||grid.CurrentRow?.Tag is not GuideTarget target){details.Text=T("请先检测字体，再点击列表中的一项。","Analyze first, then click a font in the list.");return;}
        SetBusy(true);
        try
        {
            string folder=Path.Combine(FontService.DataRoot,"preview",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string file;
            if(target.Inline&&target.Cooked!.Faces.Count>0)
            {
                var bytes=await Task.Run(()=>CookedFonts.ExtractFace(Path.Combine(cookedFolder!,target.Path),detectedVersion!,target.Cooked!.Faces.First().Export));
                file=Path.Combine(folder,Path.GetFileNameWithoutExtension(target.Path)+(bytes.AsSpan(0,4).SequenceEqual("OTTO"u8)?".otf":".ttf"));File.WriteAllBytes(file,bytes);
            }
            else{string aes=key.Text;await Task.Run(()=>FontService.Export(scan,new[]{target.Inline?Path.ChangeExtension(target.Path,".ufont"):target.Path},folder,aes,_=>{}));file=Directory.GetFiles(folder).First();}
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
                if(detectedVersion==null){partial=true;advanced.Visible=true;problem=T("发现封装资源，但无法确定游戏版本。请在“设置”选择引擎版本，再次检测。普通字体结果仍可查看。","Embedded resources found, but engine version is unknown. Select a version in Settings and detect again; standalone results remain available.");}
                else
                {
                    cookedFolder=Path.Combine(FontService.DataRoot,"analysis",Guid.NewGuid().ToString("N"));Environment.SetEnvironmentVariable("UEFONTTOOL_AES_KEY",aes);
                    try{await Task.Run(()=>IoStoreFonts.Extract(root,cookedFolder,detectedVersion,"Font"));var reports=JsonSerializer.Deserialize<CookedReport[]>(File.ReadAllText(Path.Combine(cookedFolder,"font-analysis.json")))!;diagnosticText=engineNote.Text+Environment.NewLine+string.Join(Environment.NewLine,reports.Select(r=>Path.GetRelativePath(cookedFolder,r.Path)+": inline="+r.Faces.Count+", external="+r.ExternalFaces.Count+", source="+r.ResolvedContainer+(r.Error!=null?"; "+r.Error:"")));foreach(var r in reports){string p=Path.GetRelativePath(cookedFolder,r.Path).Replace('\\','/');if(r.Faces.Count>0||(r.ExternalFaces.Count>0&&scan.Fonts.ContainsKey(Path.ChangeExtension(p,".ufont"))))targets.Add(new(){Path=p,Inline=true,Cooked=r,Language=GuideHints.Language(p,scan.Profile)});}partial|=reports.Any(r=>r.Error!=null||(r.Classes.Contains("FontFace")&&r.Faces.Count==0&&r.ExternalFaces.Count==0));}
                    catch(Exception ex){partial=true;problem=T("普通字体已识别，但封装字体分析未完成。当前列表不完整，请在“更多操作”查看检测详情。","Standalone fonts were inspected, but embedded analysis did not finish. This list is incomplete; see More → Detection details.");diagnosticText=ex.Message;engineNote.Text=T("封装字体读取失败，请查看检测详情并确认版本或密钥。","Embedded analysis failed; check version/key in Detection details.");advanced.Visible=true;}
                }
            }
            hasAnalyzed=true;Recommend();if(scan.Errors.Count>0){advanced.Visible=true;diagnosticText+=Environment.NewLine+string.Join(Environment.NewLine,scan.Errors);engineNote.Text+=Environment.NewLine+T($"{scan.Errors.Count} 个资源包读取失败；如需其中字体，请查看检测详情并检查密钥或格式支持。",$"{scan.Errors.Count} archives could not be read; see Detection details to check key/format support.");}
        }
        catch(Exception ex){problem=T("检测未完成：","Analysis incomplete: ")+ex.Message;hasAnalyzed=false;}
        finally{Environment.SetEnvironmentVariable("UEFONTTOOL_AES_KEY",old);SetBusy(false);RefreshSummary();Fill();}
    }
    internal void Preview(string scanFile,string analysisFolder)
    {
        game.Text="GAME_DIR";font.Text="MY_FONT.ttf";
        scan=JsonSerializer.Deserialize<ScanReport>(File.ReadAllText(scanFile))!;targets.Clear();foreach(var p in scan.Fonts.Keys)targets.Add(new(){Path=p,Language=GuideHints.Language(p,scan.Profile)});
        foreach(var r in JsonSerializer.Deserialize<CookedReport[]>(File.ReadAllText(Path.Combine(analysisFolder,"font-analysis.json")))!.Where(r=>r.Faces.Count>0||r.ExternalFaces.Count>0)){string p=Path.GetRelativePath(analysisFolder,r.Path).Replace('\\','/');targets.Add(new(){Path=p,Inline=true,Cooked=r,Language=GuideHints.Language(p,scan.Profile)});}
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
