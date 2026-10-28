namespace UEFontTool;

public sealed partial class GuidedForm
{
    async void Build()=>await BuildAsync();
    async Task BuildAsync()
    {
        if(busy)return;
        if(scan==null||!hasAnalyzed){summary.Text=T("请先检测字体。","Detect fonts first.");return;}
        grid.EndEdit();var chosen=targets.Where(t=>t.Selected).ToArray();if(chosen.Length==0){summary.Text=T("请先勾选要替换的字体。","Check the fonts to replace.");return;}
        string root=game.Text.Trim(),replacement=font.Text.Trim(),name=modName.Text.Trim();
        foreach(var t in chosen)if(!File.Exists(t.FontOverride??replacement)){summary.Text=T("请选择有效的新字体。","Choose a valid replacement font.");return;}
        bool doInstall=installAfterBuild,symbolLoss=allowSymbols.Checked;var current=scan;problem=null;SetBusy(true);string phase="preflight";string? old=Environment.GetEnvironmentVariable("UEFONTTOOL_AES_KEY");
        try
        {
            IoStoreFonts.Name(name);Environment.SetEnvironmentVariable("UEFONTTOOL_AES_KEY",key.Text.Trim());bool suspend=false;
            if(doInstall)
            {
                ModInstaller.CheckGameClosed(root);summary.Text=T("正在检查已有字体 MOD 冲突…","Checking existing font mod conflicts…");
                var candidate=new BuiltMod{Game=root,Name=name,Targets=chosen.Select(t=>new ModTarget(t.Path,t.Inline)).ToArray()};var conflicts=await Task.Run(()=>ModInstaller.Conflicts(root,candidate));
                if(conflicts.Length>0){if(!ConfirmConflicts(conflicts)){summary.Text=T("已取消；现有字体 MOD 未改变。","Cancelled; existing font mods are unchanged.");return;}suspend=true;}
            }
            phase="build";summary.Text=T("正在生成并校验字体文件…","Building and verifying font files…");string destination=Path.Combine(FontService.DataRoot,"guided-output",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..8]);Directory.CreateDirectory(destination);string aes=key.Text.Trim();
            await Task.Run(()=>GuideBuild.Build(root,current,chosen,replacement,aes,detectedVersion,name,destination,symbolLoss));output=Path.Combine(destination,"ReadyToInstall");problem=null;
            if(doInstall){phase="install";summary.Text=T("正在安装整组文件…","Installing the complete file group…");await Task.Run(()=>ModInstaller.Install(root,destination,suspend));summary.Text=T("字体 MOD 已安装。启动游戏检查菜单和字幕；需要撤销时点击“还原字体”。","Font mod installed. Check menus/subtitles in game; Restore fonts undoes this installation.");}
            else summary.Text=T("已生成并通过文件校验，尚未安装。可在“更多操作”打开生成目录。","Build verified, not installed. Open output from More.");
            details.Text="";
        }
        catch(Exception ex)
        {
            ReportFailure(ex,root,phase);
        }
        finally{Environment.SetEnvironmentVariable("UEFONTTOOL_AES_KEY",old);SetBusy(false);}
    }
    void ReportFailure(Exception error,string root,string phase)
    {
        string Redact(string value)=>System.Text.RegularExpressions.Regex.Replace(value,@"(?i)(?:0x)?(?<!\w)[a-f0-9]{64}(?!\w)","[redacted]");
        diagnosticText=Redact($"{DateTimeOffset.Now:O}\r\nGame: {root}\r\nPhase: {phase}\r\n\r\n{error}");
        summary.Text=error.Message.Contains("private-use",StringComparison.OrdinalIgnoreCase)?T("新字体缺少特殊字符。请换字体，或在“设置”确认允许缺失后重试。","Custom characters are missing. Choose another font or explicitly allow the loss in Settings."):T("操作未完成：","Operation incomplete: ")+Redact(error.Message).Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        problem=summary.Text;summary.ForeColor=Color.Khaki;detailsHeading.Text=T("本次操作错误 · 完整详情","Operation error · full details");details.Text=diagnosticText;
        try{Directory.CreateDirectory(FontService.DataRoot);File.WriteAllText(Path.Combine(FontService.DataRoot,"last-error.txt"),diagnosticText);}catch(IOException){}catch(UnauthorizedAccessException){}
    }
    internal async Task<bool> TestBuildWorkflow(string root,string replacement,string report,bool allowSymbolLoss)
    {
        game.Text=root;font.Text=replacement;await AnalyzeAsync();allowSymbols.Checked=allowSymbolLoss;installAfterBuild=true;await BuildAsync();
        bool success=problem==null&&output!=null;
        File.WriteAllText(report,System.Text.Json.JsonSerializer.Serialize(new{Success=success,Output=output,Message=summary.Text,Error=diagnosticText},FontService.Json));return success;
    }
}
