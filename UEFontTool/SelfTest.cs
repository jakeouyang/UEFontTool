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
            var scan = new ScanReport { Game = Path.Combine(root, "game"), Paks = paks };
            scan.Fonts["font.ufont"] = new();
            scan.Fonts["b1/Content/Fonts/Font_SC_Regular.ufont"] = new();
            scan.Fonts["b1/Content/Fonts/Font_TC_Regular.ufont"] = new();
            scan.Fonts["Engine/Content/Fonts/Roboto.ufont"] = new();
            LanguageMap.Save(scan, Languages.Simplified, new[] { "font.ufont" });
            Assert(LanguageMap.Select(scan, Languages.Simplified, false).SetEquals(new[] { "font.ufont" }), "Remember selection overrides automatic language paths");
            LanguageMap.Forget(scan, Languages.Simplified);
            Assert(LanguageMap.Select(scan, Languages.Simplified, false).Contains("b1/Content/Fonts/Font_SC_Regular.ufont"), "Reset restores automatic language detection");
            using (var ui = new MainForm())
            {
                ui.Show(); Application.DoEvents(); ui.CheckLayout(); ui.CheckSelection(scan);
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
        }
        finally { Directory.Delete(root, true); }
        File.WriteAllLines(report, results);
    }
}
