using System.Text.Json;

namespace UEFontTool;

public static class GuideBuild
{
    public static void Build(string game,ScanReport scan,GuideTarget[] selected,string defaultFont,string? key,string? version,string name,string destination,bool allowSymbolLoss=false)
    {
        IoStoreFonts.Name(name);
        var loose=selected.Where(t=>!t.Inline).ToArray();var inline=selected.Where(t=>t.Inline).ToArray();
        string ready=Path.Combine(destination,"ReadyToInstall");Directory.CreateDirectory(ready);
        try
        {
            BuildManifest? looseManifest=null;
            if(loose.Length>0)
            {
                var map=loose.ToDictionary(t=>t.Path,t=>t.FontOverride??defaultFont);
                looseManifest=FontService.Build(scan,map.Values.First(),map.Keys.ToArray(),key,_=>{},map,!allowSymbolLoss);
            }
            if(inline.Length>0)
            {
                if(version==null)throw new InvalidDataException("无法确定游戏版本，请重新检测 / Engine version required; analyze again.");
                var plan=new CookedPlan{Game=game,Version=version,OutputName=name,AllowPrivateUseLoss=allowSymbolLoss,Replacements=inline.ToDictionary(t=>t.Path,t=>t.FontOverride??defaultFont)};
                string planFile=Path.Combine(destination,"plan.json"),work=Path.Combine(destination,"build");
                File.WriteAllText(planFile,JsonSerializer.Serialize(plan,FontService.Json));IoStoreFonts.Build(planFile,work);
                foreach(var file in Directory.GetFiles(work,name+".*"))File.Copy(file,Path.Combine(ready,Path.GetFileName(file)));
                if(looseManifest!=null)MergeLoose(looseManifest,Path.Combine(ready,name+".pak"),Path.Combine(destination,"merge"));
            }
            else if(looseManifest!=null)File.Copy(looseManifest.Artifact,Path.Combine(ready,name+".pak"));
            else throw new InvalidDataException("未选择可替换的字体 / No font targets selected.");
            var artifacts=Directory.GetFiles(ready).Select(p=>new{File=Path.GetFileName(p),Sha256=PakTools.HashFile(p)}).ToArray();
            string installFolder=Path.Combine(scan.Paks,"~mods");
            File.WriteAllText(Path.Combine(ready,"INSTALL.txt"),"字体 MOD / Font mod\r\n\r\n推荐在主界面使用“生成并安装”和“还原字体”，工具会自动检查冲突、安装配套文件并记录备份。\r\nUse Build and install / Restore fonts in the main window for conflict checking, grouped installation and backups.\r\n\r\n若选择“仅生成”，本目录内同名的 .pak/.utoc/.ucas 需作为一组安装，不要复制分析目录。\r\nIf Build only was selected, matching .pak/.utoc/.ucas files form one installable group. Do not copy analysis directories.\r\n\r\n安装目录 / Destination:\r\n"+installFolder+"\r\n\r\n启动游戏检查菜单和字幕。文件校验通过不等于游戏内外观已验证。\r\nCheck menus/subtitles in game. File verification does not confirm runtime appearance.\r\n");
            File.WriteAllText(Path.Combine(destination,"result.json"),JsonSerializer.Serialize(new{Game=Path.GetFullPath(game),Name=name,Artifacts=artifacts,Targets=selected.Select(t=>new{t.Path,t.Inline,Font=t.FontOverride??defaultFont}),RuntimeVerified=false},FontService.Json));
        }
        catch(InvalidDataException ex) when(ex.Message.Contains("private-use",StringComparison.OrdinalIgnoreCase))
        {throw new InvalidDataException("新字体缺少特殊字符，已停止生成。无需专家模式：取消该字体、换用覆盖更完整的新字体，或勾选“游戏语言”右侧的“允许缺少特殊字符”后重试。允许缺失不会补字或保留原图标。 / Custom glyph loss blocked. No Expert mode required: keep this target unchanged, choose another font, or explicitly allow the loss beside Game language.\r\n"+ex.Message,ex);}
    }
    public static void MergeLoose(BuildManifest loose,string destination,string stage)
    {
        Directory.CreateDirectory(stage);
        if(File.Exists(destination))PakTools.Run("unpack",destination,"--output",stage);
        foreach(string target in loose.Targets)
        {
            string safe=PakTools.SafePath(target),path=Path.Combine(stage,safe.Replace('/',Path.DirectorySeparatorChar));
            if(File.Exists(path))throw new IOException("普通字体与封装输出包含相同路径 / Duplicate path in standalone and embedded outputs: "+target);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllBytes(path,PakTools.Run("get",loose.Artifact,target));
        }
        string packed=Path.Combine(Path.GetDirectoryName(stage)!,"merged.pak");PakTools.Run("pack","--version","V11",stage,packed);
        foreach(string target in loose.Targets)if(PakTools.Hash(PakTools.Run("get",packed,target))!=PakTools.Hash(PakTools.Run("get",loose.Artifact,target)))throw new InvalidDataException("Mixed output font readback failed.");
        File.Copy(packed,destination,true);
    }
}
