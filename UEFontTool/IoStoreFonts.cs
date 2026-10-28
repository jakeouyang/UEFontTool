using System.Diagnostics;
using System.Text.Json;

namespace UEFontTool;

public sealed class CookedPlan
{
    public string Game { get; set; } = "";
    public string Version { get; set; } = "";
    public string Filter { get; set; } = "Font";
    public string OutputName { get; set; } = "zz_UEFontTool_P";
    public Dictionary<string,string> Replacements { get; set; } = new();
    public bool AllowPrivateUseLoss { get; set; }
}
public static class IoStoreFonts
{
    public static string Name(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains('.') || !name.EndsWith("_P", StringComparison.Ordinal))
            throw new InvalidDataException("Mod basename must contain no extension/path and end in _P. / Mod 文件名须以 _P 结尾，不含扩展名和路径。");
        return name;
    }
    public static string Retoc(params string[] args)
    {
        var psi = new ProcessStartInfo(Path.Combine(PakTools.Root,"tools","retoc.exe")) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
        string? key = Environment.GetEnvironmentVariable("UEFONTTOOL_AES_KEY");
        if (!string.IsNullOrWhiteSpace(key)) { psi.ArgumentList.Add("--aes-key"); psi.ArgumentList.Add(Convert.ToHexString(PakTools.Keys(key).First())); }
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var process = Process.Start(psi)!; var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync(); process.WaitForExit();
        string result = stdout.GetAwaiter().GetResult()+stderr.GetAwaiter().GetResult();
        if (process.ExitCode != 0 || System.Text.RegularExpressions.Regex.IsMatch(result,@"\([1-9][0-9]* failed\)")) throw new IOException("retoc failed. Verify engine version, AES, Oodle and container overrides. " + System.Text.RegularExpressions.Regex.Replace(result,@"(?:0x)?[a-fA-F0-9]{64}","[redacted]"));
        return result;
    }
    public static void Extract(string game, string output, string version, string filter)
    {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Use an empty analysis directory.");
        Directory.CreateDirectory(output);
        Console.WriteLine(Retoc("to-legacy",FontService.LocatePaks(game),output,"--version",version,"--filter",filter,"--no-shaders"));
        var files = Directory.GetFiles(output,"*.uasset",SearchOption.AllDirectories);
        var reports = files.Select(f=>CookedFonts.Inspect(f,version)).ToArray();
        var selected=Origins(Retoc("list",FontService.LocatePaks(game),"--path"));
        var candidates=Origins(Retoc("list",FontService.LocatePaks(game),"--path","--all"));
        foreach(var report in reports)
        {
            string relative=Path.GetRelativePath(output,report.Path).Replace('\\','/');
            report.ResolvedContainer=selected.FirstOrDefault(p=>p.Path.Equals(relative,StringComparison.OrdinalIgnoreCase)).Container;
            report.ContainerCandidates=candidates.Where(p=>p.Path.Equals(relative,StringComparison.OrdinalIgnoreCase)).Select(p=>p.Container).Distinct().ToList();
        }
        File.WriteAllText(Path.Combine(output,"font-analysis.json"),JsonSerializer.Serialize(reports,FontService.Json));
        Console.WriteLine($"Assets: {files.Length}; inline faces: {reports.Sum(r=>r.Faces.Count)}; parse failures: {reports.Count(r=>r.Error!=null)}");
    }
    static List<(string Path,string Container)> Origins(string listing)
    {
        var result=new List<(string,string)>();
        foreach(var line in listing.Split('\n'))
        {
            var match=System.Text.RegularExpressions.Regex.Match(line,@"^(.*?)\s+[0-9a-fA-F]{24}\s+ExportBundleData\s+(.+?)\s*$");
            if(match.Success)result.Add((PakTools.SafePath(match.Groups[2].Value),match.Groups[1].Value.Trim()));
        }
        return result;
    }
    public static string[] Resources(string container)=>Origins(Retoc("list",container,"--path")).Select(p=>p.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public static void Build(string planFile, string output)
    {
        var plan = JsonSerializer.Deserialize<CookedPlan>(File.ReadAllText(planFile))!; Name(plan.OutputName); CookedFonts.Version(plan.Version);
        if (plan.Replacements.Count == 0) throw new InvalidDataException("Plan contains no replacements.");
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Use an empty build directory.");
        Directory.CreateDirectory(output);
        string original=Path.Combine(output,"original"),modified=Path.Combine(output,"modified"),verified=Path.Combine(output,"verified");
        Extract(plan.Game,original,plan.Version,plan.Filter);
        var changes=new List<object>();
        ScanReport? externalScan=null;
        foreach(var (path,font) in plan.Replacements)
        {
            string safe=PakTools.SafePath(path); if (!safe.EndsWith(".uasset",StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Expected a uasset path.");
            var inspection=CookedFonts.Inspect(Path.Combine(original,safe),plan.Version);
            byte[]? externalBytes=null;
            if(inspection.ExternalFaces.Count>0)
            {
                externalScan??=FontService.Scan(plan.Game,Environment.GetEnvironmentVariable("UEFONTTOOL_AES_KEY"),_=>{});
                externalBytes=FontService.ReadOriginal(externalScan,Path.ChangeExtension(safe,".ufont"),Environment.GetEnvironmentVariable("UEFONTTOOL_AES_KEY"));
            }
            changes.Add(CookedFonts.Rewrite(Path.Combine(original,safe),Path.Combine(modified,safe),plan.Version,font,plan.AllowPrivateUseLoss,externalBytes));
            foreach(string ext in new[]{".ubulk",".uptnl"})
            { string side=Path.ChangeExtension(Path.Combine(original,safe),ext); if(File.Exists(side)) throw new NotSupportedException("External bulk data is not yet rewritten; refusing to package it."); }
        }
        string container=Path.Combine(output,plan.OutputName+".utoc");
        string scriptObjects=Path.Combine(original,"scriptobjects.bin");
        if(File.Exists(scriptObjects)) File.Copy(scriptObjects,Path.Combine(modified,"scriptobjects.bin"));
        Console.WriteLine(Retoc("to-zen",modified,container,"--version",plan.Version));
        Console.WriteLine(Retoc("verify",container));
        string context=Path.Combine(output,"verification-context"); Directory.CreateDirectory(context);
        foreach(string file in Directory.GetFiles(output,plan.OutputName+".*").Concat(Directory.GetFiles(FontService.LocatePaks(plan.Game),"global.*")))
        {
            if(new FileInfo(file).Length > 256L*1024*1024) throw new IOException("Global verification context exceeds 256 MiB copy limit.");
            File.Copy(file,Path.Combine(context,Path.GetFileName(file)));
        }
        Console.WriteLine(Retoc("to-legacy",context,verified,"--version",plan.Version,"--no-shaders","--no-script-objects"));
        foreach(var (path,font) in plan.Replacements)
        {
            string safe=PakTools.SafePath(path); var check=CookedFonts.Inspect(Path.Combine(verified,safe),plan.Version);
            if(check.Error!=null || check.Faces.Count==0) throw new InvalidDataException("IoStore roundtrip lost font: "+path+" "+check.Error);
            // Compare against the already hash-verified legacy rewrite after both Zen conversions.
            string a=Path.ChangeExtension(Path.Combine(modified,safe),".uexp"),b=Path.ChangeExtension(Path.Combine(verified,safe),".uexp");
            if(PakTools.HashFile(a)!=PakTools.HashFile(b)) throw new InvalidDataException("IoStore roundtrip changed export bytes: "+path);
        }
        var artifacts=Directory.GetFiles(output,plan.OutputName+".*").Select(p=>new{Path=p,Sha256=PakTools.HashFile(p)}).ToArray();
        File.WriteAllText(Path.Combine(output,"manifest.json"),JsonSerializer.Serialize(new{plan.Version,plan.OutputName,Changes=changes,Artifacts=artifacts,RuntimeVerified=false},FontService.Json));
        Console.WriteLine("IoStore package verified; test in game before claiming visual success.");
    }
}
