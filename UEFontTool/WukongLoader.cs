using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace UEFontTool;

public static class WukongLoader
{
    public const string Guide = "https://www.nexusmods.com/blackmythwukong/mods/1086";
    public static string? SteamPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        return key?.GetValue("SteamPath") as string;
    }
    public static string? LaunchOptions()
    {
        string? steam = SteamPath();
        using var active = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
        object? user = active?.GetValue("ActiveUser");
        if (steam == null || user == null) return null;
        string config = Path.Combine(steam, "userdata", Convert.ToUInt32(user).ToString(), "config", "localconfig.vdf");
        if (!File.Exists(config)) return null;
        return ParseLaunchOptions(File.ReadAllText(config));
    }
    internal static string? ParseLaunchOptions(string text)
    {
        var tokens = Regex.Matches(text, "\"(?:\\\\.|[^\"\\\\])*\"|[{}]");
        for (int i = 0; i + 1 < tokens.Count; i++)
        {
            if (tokens[i].Value != "\"2358720\"" || tokens[i + 1].Value != "{") continue;
            int depth = 1;
            for (int j = i + 2; j < tokens.Count && depth > 0; j++)
            {
                string value = tokens[j].Value;
                if (value == "{") depth++;
                else if (value == "}") depth--;
                else if (value.Equals("\"LaunchOptions\"", StringComparison.OrdinalIgnoreCase) && j + 1 < tokens.Count)
                    return tokens[j + 1].Value.Trim('"');
            }
            return "";
        }
        return null;
    }
    public static void Diagnose(string game, Action<string> log)
    {
        if (FontService.Profile(FontService.LocatePaks(game)) != "b1") return;
        string? options = LaunchOptions();
        bool present = options?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(v => v.Equals("-fileopenlog", StringComparison.OrdinalIgnoreCase)) == true;
        string bin = Path.Combine(Directory.GetParent(FontService.LocatePaks(game))!.Parent!.FullName, "Binaries", "Win64");
        if (File.Exists(Path.Combine(bin, "dsound.dll")) && File.Exists(Path.Combine(bin, "bitfix", "sig.lua")))
            log("检测到旧版签名绕过；当前版本游戏原生加载正确格式的 PAK，旧特征码补丁可能失配，建议移除 / Legacy signature bypass detected; current builds load correctly formatted PAKs natively and stale pattern patches may mismatch, so removing it is recommended.");
        if (present) log("Steam 仍设置 -fileopenlog；当前发行版已不实现该参数，可移除 / -fileopenlog is still set; the current shipping build no longer implements it and it can be removed.");
        log("构建时已自动转换为悟空运行时布局（自定义 footer 与条目对齐），无需其他加载条件 / Builds are converted automatically to the Wukong runtime layout (custom footer and entry alignment); no extra loading requirements.");
        log("参考 / Reference: " + Guide);
    }
}
