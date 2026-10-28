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
            File.WriteAllText(Path.Combine(ready,"INSTALL.txt"),"字体 MOD 已生成并通过文件检查 / Font mod generated and readback checked\r\n\r\n1. 正常退出游戏 / Exit the game.\r\n2. 将本文件夹内所有 .pak/.utoc/.ucas 一起复制到 / Copy every matching .pak/.utoc/.ucas file together to:\r\n"+installFolder+"\r\n3. 不要与其他修改相同字体的 MOD 同时使用 / Avoid other mods editing the same fonts.\r\n4. 启动游戏检查菜单和字幕 / Launch the game and check menus/subtitles.\r\n\r\n卸载：只删除此次复制的同名文件 / Uninstall: remove only the files copied for this mod.\r\n不要复制上级分析目录；本轮未自动安装 / Do not copy the parent analysis directories. This build has not been auto-installed.\r\n");
            File.WriteAllText(Path.Combine(destination,"result.json"),JsonSerializer.Serialize(new{Artifacts=artifacts,Targets=selected.Select(t=>new{t.Path,t.Inline,Font=t.FontOverride??defaultFont}),RuntimeVerified=false},FontService.Json));
        }
        catch(InvalidDataException ex) when(ex.Message.Contains("private-use",StringComparison.OrdinalIgnoreCase))
        {throw new InvalidDataException("新字体缺少原字体中的特殊符号。为避免按键图标等变成方框，已停止生成。请在列表取消该字体，或换用覆盖更完整的新字体。专家模式可查看详细缺字。 / Replacement loses private-use symbols; build stopped to protect possible icons. Uncheck that target or choose a more complete font.\r\n"+ex.Message);}
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
