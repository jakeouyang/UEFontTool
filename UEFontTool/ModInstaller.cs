using System.Text;
using System.Text.Json;

namespace UEFontTool;

public record ModArtifact(string File,string Sha256);
public record ModTarget(string Path,bool Inline);
public sealed class BuiltMod
{
    public string Game {get;set;}="";
    public string Name {get;set;}="";
    public ModArtifact[] Artifacts {get;set;}=Array.Empty<ModArtifact>();
    public ModTarget[] Targets {get;set;}=Array.Empty<ModTarget>();
}
public sealed class InstalledMod
{
    public string Paks {get;set;}="";
    public string Name {get;set;}="";
    public ModArtifact[] Files {get;set;}=Array.Empty<ModArtifact>();
    public SuspendedModFile[] Suspended {get;set;}=Array.Empty<SuspendedModFile>();
}
public record SuspendedModFile(string Original,string Backup,string Sha256);
public sealed class ModConflictException : IOException
{
    public string[] Conflicts {get;}
    public ModConflictException(IEnumerable<string> paths):base("发现已有字体 MOD / Existing font mod conflicts:\n"+string.Join("\n",paths)){Conflicts=paths.Distinct().ToArray();}
}
public static class ModInstaller
{
    static string Canonical(string path)=>Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
    static string Record(string paks)=>Path.Combine(FontService.DataRoot,"guided-installed",PakTools.Hash(Encoding.UTF8.GetBytes(Canonical(paks).ToUpperInvariant()))+".json");
    static string Target(string paks,string name,ModArtifact file)
    {
        IoStoreFonts.Name(name);
        if(Path.GetFileName(file.File)!=file.File||!new[]{".pak",".utoc",".ucas"}.Contains(Path.GetExtension(file.File))||Path.GetFileNameWithoutExtension(file.File)!=name)throw new InvalidDataException("Invalid installation filename.");
        return Path.Combine(paks,"~mods",file.File);
    }
    static InstalledMod? Read(string paks)
    {
        if(!File.Exists(Record(paks)))return null;
        var record=JsonSerializer.Deserialize<InstalledMod>(File.ReadAllText(Record(paks)))??throw new InvalidDataException("Invalid installation record.");
        if(!Canonical(record.Paks).Equals(Canonical(paks),StringComparison.OrdinalIgnoreCase))throw new IOException("Installation record game mismatch.");
        if(record.Files.Length==0||record.Files.Select(f=>f.File).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=record.Files.Length)throw new IOException("Invalid installed file group.");
        foreach(var file in record.Files){string path=Target(paks,record.Name,file);if(File.Exists(path)&&PakTools.HashFile(path)!=file.Sha256)throw new IOException("本工具已安装文件被修改，未进行覆盖；请检查文件或安装记录 / Installed file changed; check the file or installation record: "+path);}
        return record;
    }
    public static BuiltMod Load(string buildDirectory,string game)
    {
        var mod=JsonSerializer.Deserialize<BuiltMod>(File.ReadAllText(Path.Combine(buildDirectory,"result.json")))??throw new InvalidDataException("Build record missing.");
        if(!Canonical(FontService.LocatePaks(mod.Game)).Equals(Canonical(FontService.LocatePaks(game)),StringComparison.OrdinalIgnoreCase))throw new IOException("生成包不属于此游戏 / Build belongs to another game.");
        if(mod.Artifacts.Length==0||mod.Artifacts.Select(f=>f.File).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=mod.Artifacts.Length)throw new IOException("Invalid artifact group.");
        foreach(var file in mod.Artifacts){Target(FontService.LocatePaks(game),mod.Name,file);string source=Path.Combine(buildDirectory,"ReadyToInstall",file.File);if(!File.Exists(source)||PakTools.HashFile(source)!=file.Sha256)throw new IOException("生成文件校验失败 / Build hash mismatch: "+file.File);}
        var extensions=mod.Artifacts.Select(f=>Path.GetExtension(f.File)).ToArray();
        if(!extensions.Contains(".pak")||extensions.Contains(".utoc")!=extensions.Contains(".ucas"))throw new IOException("配套容器文件不完整 / Incomplete container group.");
        return mod;
    }
    public static string[] Conflicts(string game,BuiltMod mod)
    {
        string paks=FontService.LocatePaks(game);var own=Read(paks);var owned=(own?.Files??Array.Empty<ModArtifact>()).Select(f=>Target(paks,own!.Name,f)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var targets=mod.Targets.Select(t=>PakTools.SafePath(t.Path)).ToHashSet(StringComparer.OrdinalIgnoreCase);var conflicts=new List<string>();
        foreach(var file in Directory.GetFiles(paks,"*",SearchOption.AllDirectories).Where(p=>new[]{".pak",".utoc"}.Contains(Path.GetExtension(p),StringComparer.OrdinalIgnoreCase)))
        {
            if(owned.Contains(file))continue;
            bool root=Path.GetDirectoryName(file)!.Equals(paks,StringComparison.OrdinalIgnoreCase);
            if(root&&!Path.GetFileNameWithoutExtension(file).EndsWith("_P",StringComparison.OrdinalIgnoreCase))continue;
            try
            {
                var paths=Path.GetExtension(file).Equals(".utoc",StringComparison.OrdinalIgnoreCase)?IoStoreFonts.Resources(file):FontService.ListPak(file);
                if(paths.Any(p=>targets.Contains(p)||targets.Contains(Path.ChangeExtension(p,".uasset"))||targets.Contains(Path.ChangeExtension(p,".ufont"))))conflicts.Add(file);
            }
            catch(Exception ex){throw new IOException("无法检查已有 MOD，已停止安装 / Cannot inspect existing mod: "+file+"\n"+ex.Message);}
        }
        foreach(var file in mod.Artifacts){var path=Target(paks,mod.Name,file);if(File.Exists(path)&&!owned.Contains(path))conflicts.Add(path);}
        return conflicts.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public static void CheckGameClosed(string game)
    {
        var root=Directory.GetParent(Directory.GetParent(FontService.LocatePaks(game))!.Parent!.FullName)!.FullName+Path.DirectorySeparatorChar;
        foreach(var process in System.Diagnostics.Process.GetProcesses())
        {
            using(process)try{if(process.Id==Environment.ProcessId)continue;string? path=process.MainModule?.FileName;if(path!=null&&path.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new IOException("请先正常退出游戏再安装或还原 / Exit the game before installing or restoring.");}catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}
        }
    }
    public static void Install(string game,string buildDirectory,bool suspendConflicts=false)
    {
        CheckGameClosed(game);var mod=Load(buildDirectory,game);string paks=FontService.LocatePaks(game);var previous=Read(paks);var conflicts=Conflicts(game,mod);if(conflicts.Length>0&&!suspendConflicts)throw new ModConflictException(conflicts);
        string folder=Path.Combine(paks,"~mods");Directory.CreateDirectory(folder);
        string backup=Path.Combine(FontService.DataRoot,"guided-backups",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(backup);
        var pending=new List<(string Temp,string Target,ModArtifact File)>();var saved=new List<(string Backup,string Target)>();var moved=new List<(string Path,string Hash)>();var suspended=new List<SuspendedModFile>();
        try
        {
            foreach(var file in mod.Artifacts){string target=Target(paks,mod.Name,file),temp=target+"."+Guid.NewGuid().ToString("N")+".tmp";pending.Add((temp,target,file));File.Copy(Path.Combine(buildDirectory,"ReadyToInstall",file.File),temp);if(PakTools.HashFile(temp)!=file.Sha256)throw new IOException("Installation staging hash mismatch.");}
            var group=conflicts.SelectMany(path=>new[]{".pak",".utoc",".ucas"}.Select(ext=>Path.ChangeExtension(path,ext))).Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach(string original in group)
            {
                if(!Canonical(original).StartsWith(Canonical(paks)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Conflict escaped game folder.");
                string hash=PakTools.HashFile(original),save=Path.Combine(backup,PakTools.Hash(Encoding.UTF8.GetBytes(original))[..12]+"-"+Path.GetFileName(original));
                File.Move(original,save);suspended.Add(new(original,save,hash));
            }
            if(previous!=null)foreach(var file in previous.Files){string target=Target(paks,previous.Name,file),save=Path.Combine(backup,file.File);if(File.Exists(target)){File.Move(target,save);saved.Add((save,target));}}
            foreach(var entry in pending){File.Move(entry.Temp,entry.Target,false);moved.Add((entry.Target,entry.File.Sha256));}
            string record=Record(paks);Directory.CreateDirectory(Path.GetDirectoryName(record)!);
            string tempRecord=record+".tmp";File.WriteAllText(tempRecord,JsonSerializer.Serialize(new InstalledMod{Paks=paks,Name=mod.Name,Files=mod.Artifacts,Suspended=(previous?.Suspended??Array.Empty<SuspendedModFile>()).Concat(suspended).ToArray()},FontService.Json));File.Move(tempRecord,record,true);
        }
        catch
        {
            foreach(var entry in moved)if(File.Exists(entry.Path)&&PakTools.HashFile(entry.Path)==entry.Hash)File.Delete(entry.Path);
            foreach(var entry in saved)if(!File.Exists(entry.Target))File.Move(entry.Backup,entry.Target);
            foreach(var entry in suspended)if(!File.Exists(entry.Original))File.Move(entry.Backup,entry.Original);
            throw;
        }
        finally{foreach(var entry in pending)if(File.Exists(entry.Temp))File.Delete(entry.Temp);}
    }
    public static void Restore(string game)
    {
        CheckGameClosed(game);string paks=FontService.LocatePaks(game);var record=Read(paks);
        if(record==null){FontService.Restore(game,_=>{});return;}
        var installedPaths=record.Files.Select(f=>Target(paks,record.Name,f)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var file in record.Suspended)
        {
            if(!Canonical(file.Original).StartsWith(Canonical(paks)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!Canonical(file.Backup).StartsWith(Canonical(Path.Combine(FontService.DataRoot,"guided-backups"))+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||(File.Exists(file.Original)&&!installedPaths.Contains(file.Original))||!File.Exists(file.Backup)||PakTools.HashFile(file.Backup)!=file.Sha256)throw new IOException("冲突 MOD 的恢复位置或备份发生变化，未还原 / Suspended mod backup or destination changed: "+file.Original);
        }
        string backup=Path.Combine(FontService.DataRoot,"guided-backups",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(backup);var moved=new List<(string From,string To)>();
        try
        {
            foreach(var file in record.Files){string source=Target(paks,record.Name,file),destination=Path.Combine(backup,file.File);if(File.Exists(source)){File.Move(source,destination);moved.Add((source,destination));}}
            foreach(var file in record.Suspended){File.Move(file.Backup,file.Original);moved.Add((file.Backup,file.Original));}
            File.Copy(Record(paks),Path.Combine(backup,"installation.json"));File.Delete(Record(paks));
        }
        catch{foreach(var entry in moved.AsEnumerable().Reverse())if(!File.Exists(entry.From))File.Move(entry.To,entry.From);throw;}
    }
}
