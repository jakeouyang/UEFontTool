namespace UEFontTool;

public static class SelfTest
{
    public static void Run(string report)
    {
        var results = new List<string>();
        void Assert(bool condition, string name) { if (!condition) throw new Exception(name); results.Add("PASS " + name); }
        void Reject(Action action, string name)
        {
            bool rejected = false;
            try { action(); } catch (IOException) { rejected = true; } catch (InvalidDataException) { rejected = true; }
            Assert(rejected, name);
        }
        foreach (var path in new[] { "../escape", "b1/../../escape", "C:/escape", "/escape" })
            Reject(() => PakTools.SafePath(path), "Reject virtual traversal: " + path);
        Assert(PakTools.SafePath("../../../b1/Content/font.ufont") == "b1/Content/font.ufont", "Normalize mount point");
        Assert(PakTools.SafePath("b1/Content//font.ufont") == "b1/Content/font.ufont", "Normalize repeated game path separators");
        Assert(GuideHints.Language("Game/Content/L10N/zh-Hans/Font/NotoSansTC.uasset","Game").Languages==Languages.Simplified,"Prefer localization directory over conflicting font family label");
        Assert(GuideHints.Language("Game/Content/Font/BO-UDReiminSCGb4-Bold.uasset","Game").Languages==Languages.Simplified,"Recognize Chinese font family markers in beginner mode");
        Assert(GuideHints.Language("Game/Content/Font/Unknown.uasset","Game").Languages==Languages.None,"Do not invent a language for unmarked fonts");
        Assert(IoStoreFonts.Name("zz_Test_P")=="zz_Test_P","Preserve required _P basename");
        foreach(var bad in new[]{"Test","../Test_P","Test_P.utoc","Test_p"})Reject(()=>IoStoreFonts.Name(bad),"Reject invalid IoStore mod basename: "+bad);
        Assert(CookedFonts.IsPrivateUse(0xe000)&&CookedFonts.IsPrivateUse(0x100000)&&!CookedFonts.IsPrivateUse(0x4e00),"Recognize private-use glyph ranges without treating CJK as icons");
        byte[] testFont=File.ReadAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"arial.ttf"));
        byte[] inline=new byte[testFont.Length+24];BitConverter.GetBytes(testFont.Length).CopyTo(inline,16);testFont.CopyTo(inline,20);
        Assert(CookedFonts.Locate(inline)?.Length==testFont.Length,"Locate bounded terminal inline font with padding");
        BitConverter.GetBytes(testFont.Length-1).CopyTo(inline,16);
        Assert(CookedFonts.Locate(inline)==null,"Reject inconsistent inline byte-array length");
        using var nativeBytes=new MemoryStream();using(var nativeWriter=new BinaryWriter(nativeBytes,System.Text.Encoding.UTF8,true)){nativeWriter.Write((ushort)0x0300);nativeWriter.Write(6);nativeWriter.Write(System.Text.Encoding.ASCII.GetBytes("a.ttf\0"));nativeWriter.Write(0);nativeWriter.Write(1);nativeWriter.Write(0);}
        byte[] native=nativeBytes.ToArray();Assert(CookedFonts.SupportedExternal(native,"UE5_6"),"Recognize bounded external UE5.6 FontFace layout");
        byte[] converted=CookedFonts.InlineExternal(native,"UE5_6",testFont);Assert(CookedFonts.Locate(converted)?.Length==testFont.Length&&converted[14]==2,"Convert external FontFace to inline and set LoadingPolicy");
        byte[] corruptExternal=(byte[])native.Clone();corruptExternal[11]=1;Assert(!CookedFonts.SupportedExternal(corruptExternal,"UE5_6"),"Reject unterminated external FontFace source filename");
        Assert(!CookedFonts.SupportedExternal(native,"UE5_4"),"Do not guess external FontFace layouts for another engine version");
        byte[] fixtureKey = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        Assert(PakTools.Keys("0X" + Convert.ToHexString(fixtureKey)).Single().SequenceEqual(fixtureKey), "Parse prefixed AES hex input");
        Assert(PakTools.Keys(Convert.ToBase64String(fixtureKey)).Single().SequenceEqual(fixtureKey), "Parse AES Base64 input");
        Assert(PakTools.Keys("").Count == 0, "Empty AES input clears keys");
        Reject(() => PakTools.Keys("invalid-secret-value"), "Reject invalid AES input without echoing it");
        Reject(() => PakTools.Keys("0x" + new string('A', 63)), "Reject truncated AES key");
        Assert(PakTools.OldestVersion(new[] { "V11", "V8B" }) == "V8B", "Choose a PAK version no newer than the source archives");
        Assert(PakTools.OldestVersion(new[] { "V3" }) == "V3", "Retain legacy PAK version support");
        Assert(FontService.Recommend("Phoenix/Content/UI/Fonts/UI_TTF_SimplifiedChinese.ufont", "Phoenix", Languages.Simplified), "Hogwarts SC mapping");
        Assert(!FontService.Recommend("Phoenix/Content/UI/Fonts/UI_TTF_TraditionalChinese.ufont", "Phoenix", Languages.Simplified), "Do not select another language");
        Assert(!FontService.Recommend("Engine/Content/Roboto.ufont", "b1", Languages.English), "Do not select engine fallback");
        Assert(!FontService.Recommend("LOTF2/Content/NotoSerifSC-Regular.ufont", "LOTF2", Languages.Traditional), "Do not guess missing TC mapping");
        Assert(FontService.Recommend("Other/Content/Font/FontF_SC_Regular.ufont", "Other", Languages.Simplified), "Generic SC path mapping");
        Assert(FontService.Recommend("Other/Content/Font/NotoSansTC-Regular.ufont", "Other", Languages.Traditional), "Generic TC family mapping");
        Assert(FontService.Recommend("Other/Content/Font/zh-Hans/Regular.ufont", "Other", Languages.Simplified), "Generic zh-Hans folder mapping");
        Assert(!FontService.Recommend("Other/Content/Font/NotoSerifJP-Regular.ufont", "Other", Languages.Simplified), "Do not confuse Japanese with simplified Chinese");
        Assert(!FontService.Recommend("Other/Content/Font/Unknown-Regular.ufont", "Other", Languages.English), "Do not guess English from Latin file names");
        Assert(WukongLoader.ParseLaunchOptions("\"apps\" { \"2358720\" { \"nested\" { \"x\" \"y\" } \"LaunchOptions\" \"-fileopenlog\" } }") == "-fileopenlog", "Read nested Steam launch options");
        Assert(WukongLoader.ParseLaunchOptions("\"2358720\" { \"nested\" {} } \"other\" { \"LaunchOptions\" \"other\" }") == "", "Do not read another game's launch options");
        string root = Path.Combine(Path.GetTempPath(), "UEFontTool-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string paks = Path.Combine(root, "game", "b1", "Content", "Paks"), stage = Path.Combine(root, "stage");
            Directory.CreateDirectory(paks); Directory.CreateDirectory(stage);
            string mappedGame=Path.Combine(root,"mapped-game"),mappedPaks=Path.Combine(mappedGame,"Test","Content","Paks"),mappedStage=Path.Combine(root,"mapped-stage");
            Directory.CreateDirectory(mappedPaks);Directory.CreateDirectory(mappedStage);
            File.WriteAllBytes(Path.Combine(mappedStage,"A.ttf"),testFont);File.WriteAllBytes(Path.Combine(mappedStage,"B.ttf"),testFont);
            PakTools.Run("pack","--version","V11",mappedStage,Path.Combine(mappedPaks,"source.pak"));
            string arial=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"arial.ttf"),times=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"times.ttf");
            var mappedScan=FontService.Scan(mappedGame,null,_=>{});
            var mappedBuild=FontService.Build(mappedScan,arial,new[]{"A.ttf","B.ttf"},null,_=>{},new(){{"A.ttf",arial},{"B.ttf",times}});
            Assert(PakTools.Hash(PakTools.Run("get",mappedBuild.Artifact,"A.ttf"))==PakTools.Hash(testFont)&&PakTools.Hash(PakTools.Run("get",mappedBuild.Artifact,"B.ttf"))==PakTools.HashFile(times),"Apply distinct replacement fonts per resource and verify both payloads");
            Assert(mappedBuild.Replacements.Count==2&&mappedBuild.MissingCodepoints.Count==2,"Record per-resource font metadata and missing codepoints");
            string companionStage=Path.Combine(root,"companion"),companion=Path.Combine(root,"mixed.pak");Directory.CreateDirectory(companionStage);File.WriteAllText(Path.Combine(companionStage,"metadata.bin"),"container metadata");PakTools.Run("pack","--version","V11",companionStage,companion);
            GuideBuild.MergeLoose(mappedBuild,companion,Path.Combine(root,"merge-stage"));
            Assert(System.Text.Encoding.UTF8.GetString(PakTools.Run("get",companion,"metadata.bin"))=="container metadata"&&PakTools.Hash(PakTools.Run("get",companion,"B.ttf"))==PakTools.HashFile(times),"Merge mixed font output without losing companion PAK metadata");
            File.WriteAllText(Path.Combine(stage, "font.ufont"), "test payload");
            string artifact = Path.Combine(root, "source.pak");
            PakTools.Run("pack", "--version", "V11", stage, artifact);
            string encrypted = Path.Combine(root, "encrypted.pak"), dragon = Path.Combine(root, "dragon.pak");
            PakFixture.EncryptIndex(artifact, encrypted, fixtureKey, false);
            Assert(System.Text.Encoding.UTF8.GetString(PakTools.ReadWithKeys(new() { fixtureKey }, "get", encrypted, "font.ufont")) == "test payload", "Read standard AES-encrypted PAK using supplied key");
            Reject(() => PakTools.ReadWithKeys(new() { new byte[32] }, "list", encrypted), "Reject wrong standard PAK AES key");
            PakFixture.EncryptIndex(artifact, dragon, fixtureKey, true);
            Assert(WukongPak.DetectDragonSword(dragon), "Detect DragonSword v101 footer");
            Assert(System.Text.Encoding.UTF8.GetString(new WukongPak(dragon, new() { fixtureKey }, true).Extract("font.ufont")) == "test payload", "Decode DragonSword encrypted and masked indexes and extract payload");
            Reject(() => new WukongPak(dragon, new(), true), "Reject missing DragonSword AES key");
            Reject(() => new WukongPak(dragon, new() { new byte[32] }, true), "Reject wrong DragonSword AES key");
            string wukong = Path.Combine(root, "wukong.pak");
            File.Copy(artifact, wukong, true);
            WukongPakWriter.Convert(wukong);
            Assert(WukongPak.Detect(wukong), "Convert to the Wukong footer layout");
            Assert(System.Text.Encoding.UTF8.GetString(new WukongPak(wukong, new()).Extract("font.ufont")) == "test payload", "Round-trip the Wukong layout with runtime data offsets");
            Assert(FontService.ListPak(wukong).Contains("font.ufont")&&FontService.ListPak(encrypted,Convert.ToHexString(fixtureKey)).Contains("font.ufont")&&FontService.ListPak(dragon,Convert.ToHexString(fixtureKey)).Contains("font.ufont"),"Read custom and encrypted existing font mods for conflict checking");
            var exportScan = new ScanReport { Game = Path.Combine(root, "game"), Paks = paks };
            exportScan.Fonts["font.ufont"] = new() { new FontSource(artifact, false) };
            string exportDir = Path.Combine(root, "export"); Directory.CreateDirectory(exportDir);
            Assert(FontService.Export(exportScan, new[] { "font.ufont" }, exportDir, null, _ => { }) == 1
                && File.ReadAllBytes(Path.Combine(exportDir, "font.ufont")).SequenceEqual(System.Text.Encoding.UTF8.GetBytes("test payload")), "Export checked fonts as real font files");
            string system = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
            if (File.Exists(system))
            {
                byte[] plain = File.ReadAllBytes(system);
                var wrapped = new byte[plain.Length + 8];
                BitConverter.GetBytes((uint)plain.Length).CopyTo(wrapped, 0);
                plain.CopyTo(wrapped, 4);
                var plainFont = new SfntFont(plain);
                var wrappedFont = new SfntFont(wrapped);
                Assert(wrappedFont.Base == 4 && plainFont.Family == wrappedFont.Family && plainFont.Codepoints.SetEquals(wrappedFont.Codepoints), "Read size-prefixed loose fonts transparently");
            }
            var scan = new ScanReport { Game = Path.Combine(root, "game"), Paks = paks };
            scan.Fonts["font.ufont"] = new();
            scan.Fonts["b1/Content/Fonts/Font_SC_Regular.ufont"] = new();
            scan.Fonts["b1/Content/Fonts/Font_TC_Regular.ufont"] = new();
            scan.Fonts["Engine/Content/Fonts/Roboto.ufont"] = new();
            LanguageMap.Save(scan, Languages.Simplified, new[] { "font.ufont" });
            Assert(LanguageMap.Select(scan, Languages.Simplified, false).SetEquals(new[] { "font.ufont" }), "Remember selection overrides automatic language paths");
            LanguageMap.Forget(scan, Languages.Simplified);
            Assert(LanguageMap.Select(scan, Languages.Simplified, false).Contains("b1/Content/Fonts/Font_SC_Regular.ufont"), "Reset restores automatic language detection");
            using(var guided=new GuidedForm(false)){guided.Show();Application.DoEvents();guided.CheckExperience(scan);Assert(true,"Guided UI: recommendations, unknown languages, hidden selections and explicit replacement fonts");}
            using (var ui = new MainForm())
            {
                ui.Show(); Application.DoEvents(); ui.CheckLayout(); ui.CheckSelection(scan);
                IEnumerable<Control> All(Control c) { foreach (Control child in c.Controls) { yield return child; foreach (var grandChild in All(child)) yield return grandChild; } }
                Assert(All(ui).All(control => control.Right <= ui.ClientSize.Width), "All controls including the export row fit the window width");
                Assert(All(ui).OfType<Button>().Any(button => button.Text.Contains("Export") || button.Text.Contains("导出")), "Export button is present");
                Assert(true, "Bilingual layout, icon, language selection, Engine toggle");
            }
            var manifest = new BuildManifest { Game = scan.Game, Artifact = artifact, ArtifactSha256 = PakTools.HashFile(artifact), Targets = new[] { "font.ufont" } };
            var mods = Path.Combine(paks, "~mods"); Directory.CreateDirectory(mods);
            string conflict = Path.Combine(mods, "existing.pak"); File.Copy(artifact, conflict);
            Reject(() => FontService.Install(manifest, scan, _ => { }), "Reject an overlapping existing mod");
            Assert(File.Exists(conflict), "Preserve existing mod"); File.Delete(conflict);
            FontService.Install(manifest, scan, _ => { });
            Assert(File.Exists(manifest.InstalledPath), "Install verified artifact");
            Reject(() => FontService.Install(manifest, scan, _ => { }), "Reject duplicate install");
            File.AppendAllText(manifest.InstalledPath!, "changed");
            Reject(() => FontService.Restore(scan.Game, _ => { }), "Refuse to remove changed installed file");
            File.Copy(artifact, manifest.InstalledPath!, true);
            FontService.Restore(scan.Game, _ => { });
            Assert(!File.Exists(manifest.InstalledPath), "Restore exact owned file");
            string groupBuild=Path.Combine(root,"group-build"),ready=Path.Combine(groupBuild,"ReadyToInstall"),groupName="zz_TestGroup_P";Directory.CreateDirectory(ready);
            File.Copy(artifact,Path.Combine(ready,groupName+".pak"));File.WriteAllText(Path.Combine(ready,groupName+".utoc"),"test index");File.WriteAllText(Path.Combine(ready,groupName+".ucas"),"test data");
            var group=new BuiltMod{Game=scan.Game,Name=groupName,Targets=new[]{new ModTarget("font.ufont",false)},Artifacts=Directory.GetFiles(ready).Select(f=>new ModArtifact(Path.GetFileName(f),PakTools.HashFile(f))).ToArray()};
            File.WriteAllText(Path.Combine(groupBuild,"result.json"),System.Text.Json.JsonSerializer.Serialize(group,FontService.Json));
            string thirdParty=Path.Combine(mods,"OtherFont_P.pak");File.Copy(artifact,thirdParty);string thirdPartyHash=PakTools.HashFile(thirdParty);
            Assert(ModInstaller.Conflicts(scan.Game,group).Contains(thirdParty),"Detect existing font mod conflicts before group installation");
            Reject(()=>ModInstaller.Install(scan.Game,groupBuild),"Block conflict installation without explicit suspension");
            ModInstaller.Install(scan.Game,groupBuild,true);
            Assert(group.Artifacts.All(f=>File.Exists(Path.Combine(mods,f.File)))&&!File.Exists(thirdParty),"Install all three files and back up the conflicting mod as a group");
            ModInstaller.Install(scan.Game,groupBuild);
            Assert(group.Artifacts.All(f=>PakTools.HashFile(Path.Combine(mods,f.File))==f.Sha256),"Safely replace an owned installation without losing suspended-mod records");
            string changed=Path.Combine(mods,groupName+".ucas");File.AppendAllText(changed,"changed");Reject(()=>ModInstaller.Restore(scan.Game),"Refuse group restoration when any installed hash changed");
            Assert(group.Artifacts.All(f=>File.Exists(Path.Combine(mods,f.File))),"A failed restore leaves every installed file intact");File.Copy(Path.Combine(ready,groupName+".ucas"),changed,true);
            ModInstaller.Restore(scan.Game);
            Assert(group.Artifacts.All(f=>!File.Exists(Path.Combine(mods,f.File)))&&PakTools.HashFile(thirdParty)==thirdPartyHash,"Restore all owned files and the exact suspended third-party mod");
        }
        finally { Directory.Delete(root, true); }
        File.WriteAllLines(report, results);
    }
}
