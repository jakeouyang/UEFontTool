using System.Diagnostics;
using System.Text.RegularExpressions;

namespace UEFontTool;

public record LanguageHint(Languages Languages,string Zh,string En,string ReasonZh,string ReasonEn);
public record EngineHint(string? Version,string Zh,string En);
public static class GuideHints
{
    public static LanguageHint Language(string path,string profile)
    {
        var value=path.ToLowerInvariant();
        var languages=LanguageMap.Infer(path,profile);
        string reasonZh="路径或字体系列标记，实际界面用途仍需验证",reasonEn="Path/family marker; runtime use still needs verification";
        if(profile.Equals("ProjectPBZ",StringComparison.OrdinalIgnoreCase)&&languages==(Languages.Simplified|Languages.Traditional))
        {reasonZh="PBZ 中文字体配置；复合字体另有 NoHan / 繁体回退，运行时使用分支仍需确认";reasonEn="PBZ Chinese font profile; composites have separate NoHan/TC fallbacks. Confirm the active runtime branch";}
        if(Regex.IsMatch(value,@"(?:^|/)l10n/zh-hans/") || value.Contains("/zh-cn/"))
        {languages=Languages.Simplified;reasonZh="资源位于简体中文本地化目录";reasonEn="Resource is in a Simplified Chinese localization directory";}
        else if(Regex.IsMatch(value,@"(?:^|/)l10n/zh-hant/") || value.Contains("/zh-tw/"))
        {languages=Languages.Traditional;reasonZh="资源位于繁体中文本地化目录";reasonEn="Resource is in a Traditional Chinese localization directory";}
        if(languages==Languages.None && (value.Contains("scgb") || (profile.Equals("FFRS",StringComparison.OrdinalIgnoreCase) && (value.Contains("arkaigb") || value.Contains("arshuyuansongh32")))))languages=Languages.Simplified;
        if(languages==Languages.None && (value.Contains("tcb5") || value.Contains("stdtc") || (profile.Equals("FFRS",StringComparison.OrdinalIgnoreCase) && (value.Contains("arstdkaib5") || value.Contains("arsysongb5")))))languages=Languages.Traditional;
        if(LanguageMap.IsEngine(path))return new(Languages.None,"引擎通用 / 回退字体","Engine / fallback","一般无需修改；仅在确认游戏使用它时选择","Usually leave unchanged unless its use is confirmed");
        if(languages==Languages.None)
        {
            if(value.Contains("hangul") || value.Contains("hangko") || Regex.IsMatch(value,@"(?:^|[/_.-])(ko|kr)(?:$|[/_.-])"))return new(Languages.None,"韩文候选","Korean candidate",reasonZh,reasonEn);
            if(value.Contains("pretendardjp") || Regex.IsMatch(value,@"(?:^|[/_.-])(ja|jp)(?:$|[/_.-])"))return new(Languages.None,"日文候选","Japanese candidate",reasonZh,reasonEn);
            if(value.Contains("thai"))return new(Languages.None,"泰文候选","Thai candidate",reasonZh,reasonEn);
            return new(Languages.None,"语言待确认","Language unconfirmed","未发现可靠语言标记，不自动勾选。可导出原字体确认","No reliable language marker; not selected automatically. Export to inspect the original");
        }
        return new(languages,languages switch{Languages.Simplified=>"简体中文",Languages.Traditional=>"繁體中文",Languages.English=>"英文",_=>"简体 / 繁体共用"},languages switch{Languages.Simplified=>"Simplified Chinese",Languages.Traditional=>"Traditional Chinese",Languages.English=>"English",_=>"Shared Chinese"},reasonZh,reasonEn);
    }
    public static EngineHint Engine(string game)
    {
        string paks=FontService.LocatePaks(game),profile=FontService.Profile(paks);
        // Only recognize UE-style versions, never reinterpret a game's 1.x release version.
        string root=Directory.GetParent(paks)!.Parent!.FullName;
        string binaries=Path.Combine(root,"Binaries","Win64");
        foreach(var exe in Directory.Exists(binaries)?Directory.GetFiles(binaries,"*.exe"):Array.Empty<string>())
        {
            var info=FileVersionInfo.GetVersionInfo(exe);
            var match=Regex.Match(info.ProductVersion??"",@"(?:^|\D)([45])\.(\d{1,2})\.\d+(?:\D|$)");
            if(match.Success)
            {
                string version="UE"+match.Groups[1].Value+"_"+match.Groups[2].Value;
                if(EngineVersions.Contains(version))return new(version,"游戏程序版本信息："+version,"Game executable version: "+version);
            }
        }
        string? known=profile.ToUpperInvariant() switch{"FFRS"=>"UE5_6","CODEVEIN2"=>"UE5_4","PHOENIX"=>"UE4_27","LOTF2"=>"UE5_1","PROJECTPBZ"=>"UE5_7",_=>null};
        return known!=null?new(known,"使用已验证的游戏配置："+known,"Using a tested game profile: "+known):new(null,"无法自动确定引擎版本，请在“设置”中选择后重新检测","Engine version unknown; select it in Settings and detect again");
    }
    public static readonly string[] EngineVersions={"UE4_26","UE4_27","UE5_0","UE5_1","UE5_2","UE5_3","UE5_4","UE5_5","UE5_6","UE5_7"};
}
public sealed class GuideTarget
{
    public string Path {get;set;}="";
    public bool Inline {get;set;}
    public LanguageHint Language {get;set;}=new(Languages.None,"","","","");
    public CookedReport? Cooked {get;set;}
    public string? FontOverride {get;set;}
    public bool Selected {get;set;}
    public bool Recommended {get;set;}
    public bool AlternativeData {get;set;}
    public bool Engine=>LanguageMap.IsEngine(Path);
    public string Id=>(Inline?"cooked:":"loose:")+Path;
}
