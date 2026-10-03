using Microsoft.Win32;
using System.Diagnostics;
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
        string installed = Path.Combine(bin, "dsound.dll"), bypassLog = Path.Combine(bin, "bitfix.txt");
        if (File.Exists(installed) && File.Exists(Path.Combine(bin, "bitfix", "sig.lua")))
        {
            log("发现 dsound.dll 与 bitfix/sig.lua；未验证其版本或来源 / Loader files found; their version and origin are not verified.");
            if (File.Exists(bypassLog))
            {
                string recent = File.ReadAllText(bypassLog);
                if (recent.Contains("writing C3") && recent.Contains("done executing")) log("bitfix 历史日志包含补丁写入；不证明本次启动或字体已生效 / Historical log records patch execution, not current-launch or font success.");
            }
        }
        log(present ? "Steam 已设置 -fileopenlog / Steam launch flag is present." : options == null ? "无法读取当前账号的悟空启动参数 / Current Steam launch options unavailable." : "Steam 尚未设置 -fileopenlog / Steam launch flag is missing.");
        log("悟空无签名 PAK 需要 -fileopenlog 或有效的签名加载补丁；复制原版 .sig 不会给新包签名。 / Unsigned PAKs need the launch flag or a working signature bypass.");
        log("可点击“悟空测试启动”，或在 Steam 属性中添加 -fileopenlog。 / Use Wukong test launch, or add -fileopenlog in Steam Properties.");
        log("参考 / Reference: " + Guide);
    }
    public static void Launch(string game, Action<string> log)
    {
        if (FontService.Profile(FontService.LocatePaks(game)) != "b1") throw new InvalidOperationException("此测试入口仅用于悟空 / This launcher is only for Wukong.");
        if (Process.GetProcessesByName("b1-Win64-Shipping").Length != 0) throw new IOException("请先正常退出游戏再测试 / Exit the game normally before testing.");
        string steam = Path.Combine(SteamPath() ?? throw new FileNotFoundException("Steam installation not found."), "steam.exe");
        var start = new ProcessStartInfo(steam) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("-applaunch"); start.ArgumentList.Add("2358720"); start.ArgumentList.Add("-fileopenlog");
        Process.Start(start);
        log("已请求 Steam 带 -fileopenlog 启动；请检查游戏内字体。未修改永久启动设置。 / Requested a test launch; permanent Steam settings unchanged.");
    }
}
