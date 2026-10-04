using System.Text.Json;
using System.Runtime.InteropServices;

namespace UEFontTool;

internal static class Program
{
    [DllImport("kernel32.dll")] static extern bool AttachConsole(int id);
    [STAThread]
    static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length == 0) { Application.Run(new MainForm()); return 0; }
        AttachConsole(-1);
        string? resultFile = null;
        try
        {
            switch (args)
            {
                case ["scan", var game, var output]:
                    resultFile = output;
                    File.WriteAllText(output, JsonSerializer.Serialize(FontService.Scan(game, Environment.GetEnvironmentVariable("UEFONTTOOL_AES_KEY"), Console.WriteLine), FontService.Json)); break;
                case ["build", var game, var font, var output, .. var targets]:
                    resultFile = output;
                    var scan = FontService.Scan(game, Environment.GetEnvironmentVariable("UEFONTTOOL_AES_KEY"), Console.WriteLine);
                    var selected = targets.Length == 0 ? LanguageMap.Select(scan, Languages.Simplified, false).ToArray() : targets;
                    var manifest = FontService.Build(scan, font, selected, Environment.GetEnvironmentVariable("UEFONTTOOL_AES_KEY"), Console.WriteLine);
                    File.WriteAllText(output, JsonSerializer.Serialize(manifest, FontService.Json)); break;
                case ["restore", var game]: FontService.Restore(game, Console.WriteLine); break;
                case ["diagnose-wukong", var game]: WukongLoader.Diagnose(game, Console.WriteLine); break;
                case ["--render-ui", var output, .. var options]:
                    using (var form = new MainForm())
                    {
                        form.SelectLanguage(!options.Contains("--en")); form.Show(); Application.DoEvents();
                        int sample = Array.IndexOf(options, "--sample");
                        if (sample >= 0 && sample + 1 < options.Length) { form.LoadPreview(options[sample + 1]); Application.DoEvents(); }
                        using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, form.ClientRectangle); bitmap.Save(output);
                    }
                    break;
                case ["--self-test", var output]: resultFile = output; SelfTest.Run(output); break;
                default: throw new ArgumentException("scan <game> <report.json> | build <game> <font> <report.json> [targets...] | restore <game> | --render-ui <image.png> [--en] | --self-test <report.txt>");
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            if (resultFile != null) File.WriteAllText(resultFile + ".error.txt", ex.ToString());
            return 1;
        }
    }
}
