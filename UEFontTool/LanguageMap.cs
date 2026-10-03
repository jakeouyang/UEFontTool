using System.Text.Json;
using System.Text.RegularExpressions;

namespace UEFontTool;

public static class LanguageMap
{
    static string FilePath => Path.Combine(FontService.DataRoot, "language-mappings.json");
    static Dictionary<string, string[]> ReadSaved() => File.Exists(FilePath)
        ? JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(FilePath)) ?? new()
        : new();
    static string Key(string game, Languages language) => Path.GetFullPath(FontService.LocatePaks(game)).TrimEnd('\\').ToUpperInvariant() + "|" + language;
    public static bool IsEngine(string path) => path.StartsWith("Engine/", StringComparison.OrdinalIgnoreCase);
    public static Languages Infer(string path, string profile)
    {
        if (IsEngine(path)) return Languages.None;
        string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        if (profile.Equals("b1", StringComparison.OrdinalIgnoreCase))
        {
            if (name is "hykaitiwukong" or "hykaitiwukongendingcredits") return Languages.Simplified | Languages.Traditional;
            if (name is "crimsonprowukong" or "serifwukong") return Languages.English;
        }
        // Explicit labels only. Supporting Latin glyphs does not make a font an English UI font.
        string value = path.ToLowerInvariant();
        Languages result = Languages.None;
        bool Token(string pattern) => Regex.IsMatch(value, @"(?:^|[/_.\-])(?:" + pattern + @")(?:$|[/_.\-])");
        if (value.Contains("simplifiedchinese") || value.Contains("simplified_chinese") || value.Contains("notoserifsc") || value.Contains("notosanssc") || value.Contains("sourcehansanssc") || value.Contains("sourcehanserifsc") || Token("sc|cn|chs|zh[-_]cn|zh[-_]hans|chinese[-_]simplified")) result |= Languages.Simplified;
        if (value.Contains("traditionalchinese") || value.Contains("traditional_chinese") || value.Contains("notoseriftc") || value.Contains("notosanstc") || value.Contains("sourcehansanstc") || value.Contains("sourcehanseriftc") || Token("tc|tw|cht|zh[-_]tw|zh[-_]hant|chinese[-_]traditional")) result |= Languages.Traditional;
        if (Token("en|eng|english|en[-_]us|en[-_]gb")) result |= Languages.English;
        return result;
    }
    public static HashSet<string> Select(ScanReport report, Languages languages, bool includeEngine)
    {
        var saved = ReadSaved();
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Languages language in new[] { Languages.Simplified, Languages.Traditional, Languages.English })
        {
            if (!languages.HasFlag(language)) continue;
            IEnumerable<string> matches = saved.TryGetValue(Key(report.Game, language), out var paths)
                ? paths.Where(report.Fonts.ContainsKey)
                : report.Fonts.Keys.Where(path => Infer(path, report.Profile).HasFlag(language));
            foreach (var path in matches.Where(p => includeEngine || !IsEngine(p))) result.Add(path);
        }
        return result;
    }
    public static void Save(ScanReport report, Languages language, IEnumerable<string> chosen)
    {
        if (language is not (Languages.Simplified or Languages.Traditional or Languages.English)) throw new InvalidOperationException("请只选择一种语言，再记住该语言的字体 / Choose one language before saving its selection.");
        var paths = chosen.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (paths.Length == 0 || paths.Any(p => !report.Fonts.ContainsKey(p))) throw new InvalidDataException("请先勾选有效字体 / Select valid font paths first.");
        var saved = ReadSaved(); saved[Key(report.Game, language)] = paths;
        Directory.CreateDirectory(FontService.DataRoot);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(saved, FontService.Json));
        File.Move(temp, FilePath, true);
    }
    public static void Forget(ScanReport report, Languages languages)
    {
        var saved = ReadSaved();
        foreach (var language in new[] { Languages.Simplified, Languages.Traditional, Languages.English })
            if (languages.HasFlag(language)) saved.Remove(Key(report.Game, language));
        Directory.CreateDirectory(FontService.DataRoot);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(saved, FontService.Json));
        File.Move(temp, FilePath, true);
    }
}
