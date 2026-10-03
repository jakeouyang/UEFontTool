using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace UEFontTool;

[Flags] public enum Languages { None = 0, Simplified = 1, Traditional = 2, English = 4 }
public record FontSource(string Pak, bool Wukong, string Version = "V11", bool DragonSword = false);
public sealed class ScanReport
{
    public string Game { get; set; } = "";
    public string Paks { get; set; } = "";
    public string Profile { get; set; } = "";
    public int Containers { get; set; }
    public int IoStoreContainers { get; set; }
    public int SignatureFiles { get; set; }
    public string ScanCoverage { get; set; } = "PAK indexes only; IoStore assets are not inspected.";
    public List<string> FontAssetCandidates { get; set; } = new();
    public Dictionary<string, List<FontSource>> Fonts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> Conflicts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Errors { get; set; } = new();
    public List<string> ModErrors { get; set; } = new();
}
public record FontInfo(string Family, int Codepoints, string Sha256, int Bytes);
public record VerifiedFont(string Target, string Source, FontInfo Original, int MissingOriginalCodepoints, int MissingCjkCodepoints);
public sealed class BuildManifest
{
    public string Game { get; set; } = "";
    public string Artifact { get; set; } = "";
    public string ArtifactSha256 { get; set; } = "";
    public string[] Targets { get; set; } = Array.Empty<string>();
    public FontInfo Replacement { get; set; } = new("", 0, "", 0);
    public List<VerifiedFont> VerifiedOriginals { get; set; } = new();
    public string? InstalledPath { get; set; }
    public bool RuntimeVerified { get; set; }
    public bool LegacyPythonPackage { get; set; }
    public string OutputPakVersion { get; set; } = "";
}

public static class FontService
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static string DataRoot => Path.Combine(PakTools.Root, "data");
    public static string LocatePaks(string game)
    {
        game = Path.GetFullPath(game);
        if (!Directory.Exists(game)) throw new DirectoryNotFoundException("游戏目录不存在 / Game folder does not exist.");
        var dirs = Path.GetFileName(game).Equals("Paks", StringComparison.OrdinalIgnoreCase) ? new[] { game } :
            Directory.GetDirectories(game).Select(d => Path.Combine(d, "Content", "Paks")).Where(Directory.Exists).ToArray();
        if (dirs.Length != 1) throw new InvalidDataException("请选择游戏根目录或准确的 Content/Paks 目录 / Select the game root or exact Content/Paks folder.");
        return dirs[0];
    }
    public static string Profile(string paks) => new DirectoryInfo(paks).Parent?.Parent?.Name ?? "Unknown";
    public static bool Recommend(string target, string profile, Languages languages)
    {
        return (LanguageMap.Infer(target, profile) & languages) != 0;
    }
    public static ScanReport Scan(string game, string? keyFile, Action<string> log)
    {
        string paks = LocatePaks(game);
        var result = new ScanReport { Game = Path.GetFullPath(game), Paks = paks, Profile = Profile(paks), IoStoreContainers = Directory.GetFiles(paks, "*.utoc").Length, SignatureFiles = Directory.GetFiles(paks, "*.sig").Length };
        var keys = PakTools.Keys(keyFile);
        foreach (string pak in Directory.GetFiles(paks, "*.pak").Order())
        {
            log("扫描 / Scan: " + Path.GetFileName(pak));
            try
            {
                bool special = WukongPak.Detect(pak);
                bool dragon = result.Profile.Equals("DS", StringComparison.OrdinalIgnoreCase) && WukongPak.DetectDragonSword(pak);
                string version = "V11";
                IEnumerable<string> names;
                if (special || dragon) { var reader = new WukongPak(pak, keys, dragon); names = reader.Entries.Keys; result.FontAssetCandidates.AddRange(reader.FontAssetCandidates); }
                else
                {
                    names = Encoding.UTF8.GetString(PakTools.ReadWithKeys(keys, "list", pak)).Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(PakTools.SafePath).ToArray();
                    string info = Encoding.UTF8.GetString(PakTools.ReadWithKeys(keys, "info", pak));
                    var match = Regex.Match(info, @"(?m)^version:\s*(V\d+[AB]?)\s*$");
                    if (!match.Success) throw new InvalidDataException("Cannot identify PAK version.");
                    version = match.Groups[1].Value;
                    result.FontAssetCandidates.AddRange(names.Where(PakTools.IsFontAssetCandidate));
                }
                foreach (string name in names.Where(PakTools.IsFont))
                {
                    if (!result.Fonts.TryGetValue(name, out var sources)) result.Fonts[name] = sources = new();
                    sources.Add(new(pak, special, version, dragon));
                }
                result.Containers++;
            }
            catch (Exception ex) { result.Errors.Add(Path.GetFileName(pak) + ": " + ex.Message); }
        }
        InspectMods(result);
        result.FontAssetCandidates = result.FontAssetCandidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Directory.CreateDirectory(DataRoot);
        File.WriteAllText(Path.Combine(DataRoot, "scan-" + result.Profile + ".json"), JsonSerializer.Serialize(result, Json));
        log($"字体 / Fonts: {result.Fonts.Count}; PAK: {result.Containers}; IoStore: {result.IoStoreContainers}; 读取失败 / failures: {result.Errors.Count}");
        if (result.IoStoreContainers > 0) log("扫描范围：PAK 中的独立字体；IoStore 内部资源尚未解析，列表不是游戏所有字体的保证。 / IoStore resources are not yet inspected; this is not an exhaustive font inventory.");
        if (result.FontAssetCandidates.Count > 0) log($"字体目录下还发现 {result.FontAssetCandidates.Count} 个资源包候选；仅凭文件名无法确认是否内嵌字体。 / Font-folder asset candidates require resource parsing.");
        if (result.SignatureFiles > 0) log($"发现 {result.SignatureFiles} 个 .sig；是否强制校验及加载方法需按游戏确认，不能使用通用签名绕过。 / Signature sidecars detected; loading requirements are game-specific.");
        if (result.Profile == "b1") WukongLoader.Diagnose(game, log);
        return result;
    }
    public static void InspectMods(ScanReport result)
    {
        result.Conflicts.Clear(); result.ModErrors.Clear();
        foreach (var sub in Directory.GetDirectories(result.Paks))
        foreach (var pak in Directory.GetFiles(sub, "*.pak", SearchOption.AllDirectories))
        {
            try
            {
                foreach (var path in PakTools.List(pak).Where(result.Fonts.ContainsKey))
                {
                    if (!result.Conflicts.TryGetValue(path, out var mods)) result.Conflicts[path] = mods = new();
                    mods.Add(pak);
                }
            }
            catch (Exception ex) { result.ModErrors.Add(Path.GetFileName(pak) + ": " + ex.Message); }
        }
    }
    static (FontInfo Info, HashSet<int> Map) InspectFont(byte[] bytes)
    {
        var font = new SfntFont(bytes);
        return (new(font.Family, font.Codepoints.Count, PakTools.Hash(bytes), bytes.Length), font.Codepoints);
    }
    public static BuildManifest Build(ScanReport scan, string fontFile, string[] targets, string? keyFile, Action<string> log)
    {
        targets = targets.Select(PakTools.SafePath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (targets.Length == 0) throw new InvalidDataException("请扫描并勾选至少一个字体 / Scan and select at least one font.");
        if (new FileInfo(fontFile).Length > 128 * 1024 * 1024) throw new InvalidDataException("Font exceeds 128 MB.");
        byte[] bytes = File.ReadAllBytes(fontFile);
        var replacement = InspectFont(bytes);
        var manifest = new BuildManifest { Game = scan.Game, Targets = targets, Replacement = replacement.Info };
        var keys = PakTools.Keys(keyFile);
        var readers = new Dictionary<string, WukongPak>();
        foreach (var target in targets)
        {
            if (!scan.Fonts.TryGetValue(target, out var sources)) throw new InvalidDataException("Target absent from scan: " + target);
            log("校验原字体 / Verify original: " + Path.GetFileName(target));
            foreach (var source in sources)
            {
                byte[] original;
                if (source.Wukong || source.DragonSword)
                {
                    if (!readers.TryGetValue(source.Pak, out var reader)) readers[source.Pak] = reader = new WukongPak(source.Pak, keys, source.DragonSword);
                    original = reader.Extract(target);
                }
                else original = PakTools.ReadWithKeys(keys, "get", source.Pak, target);
                var inspected = InspectFont(original);
                var missing = inspected.Map.Except(replacement.Map).ToArray();
                int cjk = missing.Count(c => c is >= 0x3400 and <= 0x9fff or >= 0x20000 and <= 0x323af);
                manifest.VerifiedOriginals.Add(new(target, source.Pak, inspected.Info, missing.Length, cjk));
                log($"字符覆盖差异 / Coverage difference: {missing.Length}; 汉字 / CJK: {cjk}（不代表游戏实际使用这些字 / actual game usage unknown）");
            }
        }
        string output = Path.Combine(DataRoot, "output", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(output);
        string stage = Path.Combine(Path.GetTempPath(), "UEFontTool-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            foreach (var target in targets)
            {
                string destination = Path.Combine(stage, target.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.WriteAllBytes(destination, bytes);
            }
            manifest.Artifact = Path.Combine(output, "zz_UEFontTool_P.pak");
            manifest.OutputPakVersion = PakTools.OldestVersion(targets.SelectMany(t => scan.Fonts[t]).Select(s => s.Version));
            log("正在打包并回读校验 / Packing and verifying…");
            log("输出 PAK 版本 / Output PAK version: " + manifest.OutputPakVersion);
            var arguments = new List<string> { "pack", "--version", manifest.OutputPakVersion };
            if (Array.IndexOf(PakTools.Versions, manifest.OutputPakVersion) >= 3) arguments.AddRange(new[] { "--compression", "Zlib" });
            arguments.AddRange(new[] { stage, manifest.Artifact });
            PakTools.Run(arguments.ToArray());
            if (!PakTools.List(manifest.Artifact).Order().SequenceEqual(targets.Order())) throw new InvalidDataException("PAK path verification failed.");
            foreach (var target in targets)
                if (PakTools.Hash(PakTools.Run("get", manifest.Artifact, target)) != replacement.Info.Sha256) throw new InvalidDataException("PAK font hash verification failed.");
            manifest.ArtifactSha256 = PakTools.HashFile(manifest.Artifact);
            WriteManifest(manifest);
            log("已生成 / Created: " + manifest.Artifact);
            log("游戏内效果尚未验证 / In-game appearance has not been verified.");
            return manifest;
        }
        finally { Directory.Delete(stage, true); }
    }
    static string Record(string game)
    {
        string paks = LocatePaks(game).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
        return Path.Combine(DataRoot, "installed", PakTools.Hash(Encoding.UTF8.GetBytes(paks)) + ".json");
    }
    public static string ManifestPath(BuildManifest manifest) => Path.Combine(Path.GetDirectoryName(manifest.Artifact)!, "manifest.json");
    static void WriteManifest(BuildManifest manifest) => File.WriteAllText(ManifestPath(manifest), JsonSerializer.Serialize(manifest, Json));
    public static void Install(BuildManifest manifest, ScanReport scan, Action<string> log)
    {
        if (LocatePaks(manifest.Game) != scan.Paks) throw new InvalidDataException("Game scan mismatch.");
        if (PakTools.HashFile(manifest.Artifact) != manifest.ArtifactSha256) throw new InvalidDataException("Artifact hash mismatch.");
        InspectMods(scan);
        var conflicts = manifest.Targets.Where(scan.Conflicts.ContainsKey).SelectMany(t => scan.Conflicts[t]).Distinct().ToArray();
        if (conflicts.Length != 0) throw new IOException("检测到字体覆盖冲突；测试包已保留。请先移走冲突 Mod 再安装 / Conflicting font mods; output preserved:\n" + string.Join('\n', conflicts));
        if (scan.ModErrors.Count != 0) throw new IOException("部分现有 Mod 无法读取，无法完成冲突检查；测试包已保留 / Cannot inspect existing mods:\n" + string.Join('\n', scan.ModErrors));
        string destination = Path.Combine(scan.Paks, "~mods", "zz_UEFontTool_P.pak");
        if (File.Exists(destination) || File.Exists(Record(manifest.Game))) throw new IOException("请先还原本工具已安装字体 / Restore the previous tool installation first.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string pending = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        bool moved = false;
        try
        {
            File.Copy(manifest.Artifact, pending, false);
            if (PakTools.HashFile(pending) != manifest.ArtifactSha256) throw new IOException("Install copy failed verification.");
            File.Move(pending, destination, false); moved = true;
            manifest.InstalledPath = destination;
            Directory.CreateDirectory(Path.GetDirectoryName(Record(manifest.Game))!);
            File.WriteAllText(Record(manifest.Game), JsonSerializer.Serialize(manifest, Json));
            WriteManifest(manifest);
        }
        catch
        {
            if (moved && File.Exists(destination) && PakTools.HashFile(destination) == manifest.ArtifactSha256) File.Delete(destination);
            // Record only belongs to this operation; pre-existing records were rejected above.
            if (moved && File.Exists(Record(manifest.Game))) File.Delete(Record(manifest.Game));
            manifest.InstalledPath = null;
            throw;
        }
        finally { if (File.Exists(pending)) File.Delete(pending); }
        log("已安装 / Installed: " + destination);
    }
    public static void Restore(string game, Action<string> log)
    {
        string record = Record(game);
        if (!File.Exists(record)) ImportLegacyRecord(game);
        if (!File.Exists(record)) throw new IOException("当前游戏没有本工具的安装记录 / No installation record for this game.");
        var manifest = JsonSerializer.Deserialize<BuildManifest>(File.ReadAllText(record)) ?? throw new InvalidDataException("Invalid record.");
        string expected = Path.Combine(LocatePaks(game), "~mods", manifest.LegacyPythonPackage ? "zz_FontTool_Jiangxi_P.pak" : "zz_UEFontTool_P.pak");
        if (!string.Equals(manifest.InstalledPath, expected, StringComparison.OrdinalIgnoreCase)) throw new IOException("Installation path mismatch.");
        if (File.Exists(expected))
        {
            if (PakTools.HashFile(expected) != manifest.ArtifactSha256) throw new IOException("安装文件已被修改，未删除 / Installed file changed; refusing to remove it.");
            File.Delete(expected);
        }
        File.Delete(record);
        log("已还原，仅移除本工具的包 / Restored; removed only this tool's package.");
    }
    static void ImportLegacyRecord(string game)
    {
        string? script = PakTools.FindLocal("font_mod.py");
        if (script == null) return;
        string output = Path.Combine(Path.GetDirectoryName(script)!, "output");
        if (!Directory.Exists(output)) return;
        string installed = Path.Combine(LocatePaks(game), "~mods", "zz_FontTool_Jiangxi_P.pak");
        if (!File.Exists(installed)) return;
        foreach (string file in Directory.GetFiles(output, "manifest.json", SearchOption.AllDirectories))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(file)); var m = json.RootElement;
            if (!m.TryGetProperty("game", out var root) || !m.TryGetProperty("artifact_sha256", out var hash)) continue;
            if (!string.Equals(LocatePaks(root.GetString()!), LocatePaks(game), StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(PakTools.HashFile(installed), hash.GetString(), StringComparison.OrdinalIgnoreCase)) continue;
            var manifest = new BuildManifest { Game = game, InstalledPath = installed, ArtifactSha256 = PakTools.HashFile(installed), LegacyPythonPackage = true };
            Directory.CreateDirectory(Path.GetDirectoryName(Record(game))!);
            File.WriteAllText(Record(game), JsonSerializer.Serialize(manifest, Json));
            return;
        }
    }
}
