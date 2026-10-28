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
        if (args.Length == 0) { Application.Run(new GuidedForm()); return 0; }
        AttachConsole(-1);
        string? resultFile = null;
        try
        {
            switch (args)
            {
                case ["--expert"]: Application.Run(new MainForm());break;
                case ["install-guided",var game,var directory,.. var options]:ModInstaller.Install(game,directory,options.Contains("--suspend-conflicts"));break;
                case ["restore-guided",var game]:ModInstaller.Restore(game);break;
                case ["conflicts-guided",var game,var directory]:Console.WriteLine(JsonSerializer.Serialize(ModInstaller.Conflicts(game,ModInstaller.Load(directory,game)),FontService.Json));break;
                case ["--test-guide",var game,var font,var output]:
                    using(var form=new GuidedForm(false))
                    {form.Shown+=async(_,_)=>{await form.TestAnalysis(game,font,output);form.Close();};Application.Run(form);}break;
                case ["--render-guide", var output, .. var options]:
                    using(var form=new GuidedForm(false))
                    {
                        form.SelectLanguage(!options.Contains("--en"));if(options.Contains("--small"))form.Size=form.MinimumSize;form.Show();Application.DoEvents();
                        int sample=Array.IndexOf(options,"--sample");if(sample>=0&&sample+2<options.Length)form.Preview(options[sample+1],options[sample+2]);
                        Application.DoEvents();using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save(output);
                    }break;
                case ["inspect-cooked", var input, var version, var output]:
                    resultFile = output;
                    var inspections = Directory.GetFiles(input, "*.uasset", SearchOption.AllDirectories).Select(p => CookedFonts.Inspect(p, version)).ToArray();
                    File.WriteAllText(output, JsonSerializer.Serialize(inspections, FontService.Json));
                    Console.WriteLine($"Assets: {inspections.Length}; faces: {inspections.Sum(r=>r.Faces.Count)}; failures: {inspections.Count(r=>r.Error!=null)}"); break;
                case ["scan-cooked", var game, var version, var output, .. var filters]:
                    IoStoreFonts.Extract(game, output, version, filters.FirstOrDefault() ?? "Font"); break;
                case ["build-cooked", var plan, var output]:
                    IoStoreFonts.Build(plan, output); break;
                case ["build-guided",var planFile,var output]:
                    var guidePlan=JsonSerializer.Deserialize<CookedPlan>(File.ReadAllText(planFile))!;
                    var guideScan=FontService.Scan(guidePlan.Game,Environment.GetEnvironmentVariable("UEFONTTOOL_AES_KEY"),Console.WriteLine);
                    var guideTargets=guidePlan.Replacements.Select(p=>new GuideTarget{Path=p.Key,Inline=p.Key.EndsWith(".uasset",StringComparison.OrdinalIgnoreCase),FontOverride=p.Value,Selected=true}).ToArray();
                    GuideBuild.Build(guidePlan.Game,guideScan,guideTargets,"",Environment.GetEnvironmentVariable("UEFONTTOOL_AES_KEY"),string.IsNullOrWhiteSpace(guidePlan.Version)?GuideHints.Engine(guidePlan.Game).Version:guidePlan.Version,guidePlan.OutputName,output,guidePlan.AllowPrivateUseLoss);break;
                case ["build-mapped", var planFile, var output]:
                    resultFile=output;
                    var mapped=JsonSerializer.Deserialize<CookedPlan>(File.ReadAllText(planFile))!;
                    var mappedScan=FontService.Scan(mapped.Game,Environment.GetEnvironmentVariable("UEFONTTOOL_AES_KEY"),Console.WriteLine);
                    var mappedBuild=FontService.Build(mappedScan,mapped.Replacements.Values.First(),mapped.Replacements.Keys.ToArray(),Environment.GetEnvironmentVariable("UEFONTTOOL_AES_KEY"),Console.WriteLine,mapped.Replacements,!mapped.AllowPrivateUseLoss);
                    File.WriteAllText(output,JsonSerializer.Serialize(mappedBuild,FontService.Json));break;
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
                case ["export", var game, var output]:
                    {
                        Directory.CreateDirectory(output);
                        var exportScan = FontService.Scan(game, Environment.GetEnvironmentVariable("UEFONTTOOL_AES_KEY"), Console.WriteLine);
                        int count = FontService.Export(exportScan, exportScan.Fonts.Keys.ToArray(), output, Environment.GetEnvironmentVariable("UEFONTTOOL_AES_KEY"), Console.WriteLine);
                        Console.WriteLine($"Exported {count} font(s) to {output}");
                        break;
                    }
                case ["export", var game, var output, .. var targets]:
                    {
                        Directory.CreateDirectory(output);
                        var exportScan = FontService.Scan(game, Environment.GetEnvironmentVariable("UEFONTTOOL_AES_KEY"), Console.WriteLine);
                        int count = FontService.Export(exportScan, targets, output, Environment.GetEnvironmentVariable("UEFONTTOOL_AES_KEY"), Console.WriteLine);
                        Console.WriteLine($"Exported {count} font(s) to {output}");
                        break;
                    }
                case ["diagnose-wukong", var game]: WukongLoader.Diagnose(game, Console.WriteLine); break;
                case ["--render-ui", var output, .. var options]:
                    using (var form = new MainForm(false))
                    {
                        form.SelectLanguage(!options.Contains("--en")); form.Show(); Application.DoEvents();
                        int sample = Array.IndexOf(options, "--sample");
                        if (sample >= 0 && sample + 1 < options.Length) { form.LoadPreview(options[sample + 1]); Application.DoEvents(); }
                        using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, form.ClientRectangle); bitmap.Save(output);
                    }
                    break;
                case ["--render-cooked", var output]:
                    using(var form=new CookedForm("GAME_DIR",null))
                    { form.Show();Application.DoEvents();using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,form.ClientRectangle);bitmap.Save(output); } break;
                case ["--self-test", var output]: resultFile = output; SelfTest.Run(output); break;
                default: throw new ArgumentException("scan <game> <report.json> | build <game> <font> <report.json> [targets...] | restore <game> | export <game> <folder> [targets...] | --render-ui <image.png> [--en] | --self-test <report.txt>");
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
