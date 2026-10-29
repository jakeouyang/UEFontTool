using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.UnrealTypes;
using Newtonsoft.Json;

namespace UEFontTool;

public record EmbeddedFace(int Export, string Object, int Offset, int Length, string Family, int Characters);
public record ExternalFace(int Export,string Object);
public sealed class CookedReport
{
    public string Path { get; set; } = "";
    public List<EmbeddedFace> Faces { get; set; } = new();
    public List<ExternalFace> ExternalFaces { get; set; } = new();
    public List<string> Classes { get; set; } = new();
    public List<string> PackageImports { get; set; } = new();
    public List<string> ImportedObjects { get; set; } = new();
    public string? ResolvedContainer { get; set; }
    public List<string> ContainerCandidates { get; set; } = new();
    public string ReferenceStatus { get; set; } = "Static package imports; runtime widget use is not proven.";
    public string? CompositeProperties { get; set; }
    public string? Error { get; set; }
}
public static class CookedFonts
{
    public static EngineVersion Version(string version) => Enum.Parse<EngineVersion>("VER_" + version.Replace("UE", "UE"));
    static UAsset Open(string path, string version)
    {
        string? mapping=Environment.GetEnvironmentVariable("UEFONTTOOL_USMAP");
        return new(path,Version(version),string.IsNullOrWhiteSpace(mapping)?null:new UAssetAPI.Unversioned.Usmap(mapping));
    }
    static byte[] ExportBytes(string path, Export export)
    {
        string exp = System.IO.Path.ChangeExtension(path, ".uexp");
        byte[] data = File.ReadAllBytes(File.Exists(exp) ? exp : path);
        long start = export.SerialOffset - (File.Exists(exp) ? new FileInfo(path).Length : 0);
        if (start < 0 || export.SerialSize < 0 || start > data.Length - export.SerialSize) throw new InvalidDataException("Export range outside package.");
        return data.AsSpan(checked((int)start), checked((int)export.SerialSize)).ToArray();
    }
    public static (int Offset, int Length, SfntFont Font)? Locate(byte[] data)
    {
        // FontFace's serialized TArray must finish this export. Never patch an arbitrary signature.
        for (int i = 4; i < Math.Min(data.Length - 12, 65536); i++)
        {
            if (!(data[i] == 0 && data[i+1] == 1 && data[i+2] == 0 && data[i+3] == 0) &&
                !(data[i] == 'O' && data[i+1] == 'T' && data[i+2] == 'T' && data[i+3] == 'O')) continue;
            int n = BitConverter.ToInt32(data, i - 4);
            int tail = data.Length - i - n;
            if (n < 12 || tail is not (0 or 4) || (tail == 4 && BitConverter.ToUInt32(data, data.Length - 4) != 0)) continue;
            try { return (i, n, new SfntFont(data.AsSpan(i,n).ToArray())); } catch (Exception ex) when (ex is IOException or ArgumentException or OverflowException) { }
        }
        return null;
    }
    public static bool SupportedExternal(byte[] data,string version)
    {
        // Observed UE5.6/5.7 unversioned FontFace: property 0 SourceFilename only,
        // no object GUID, cooked=true, saveInlineData=false. Other layouts are refused.
        if(version is not ("UE5_6" or "UE5_7")||data.Length<18||data[0]!=0||data[1]!=3)return false;
        int length=BitConverter.ToInt32(data,2);
        if(length==0||length==int.MinValue||Math.Abs((long)length)>32768)return false;
        long end=6+Math.Abs((long)length)*(length<0?2:1);
        return end==data.Length-12&&data[(int)end-1]==0&&(length>0||data[(int)end-2]==0)&&BitConverter.ToInt32(data,(int)end)==0&&BitConverter.ToInt32(data,(int)end+4)==1&&BitConverter.ToInt32(data,(int)end+8)==0;
    }
    public static byte[] InlineExternal(byte[] data,string version,byte[] replacement)
    {
        if(!SupportedExternal(data,version))throw new NotSupportedException("Unsupported external FontFace layout; refusing conversion.");
        using var output=new MemoryStream();using var writer=new BinaryWriter(output);
        // Two property fragments: SourceFilename at index 0 and LoadingPolicy at index 2.
        writer.Write((ushort)0x0200);writer.Write((ushort)0x0301);
        writer.Write(data.AsSpan(2,data.Length-14));writer.Write((byte)2); // EFontLoadingPolicy::Inline
        writer.Write(0);writer.Write(1);writer.Write(1); // no GUID, cooked, save inline
        writer.Write(replacement.Length);writer.Write(replacement);writer.Write(0); // no preprocessed geometry
        return output.ToArray();
    }
    public static CookedReport Inspect(string path, string version)
    {
        var report = new CookedReport { Path = path };
        try
        {
            var asset = Open(path, version);
            report.PackageImports = asset.Imports.Where(i => i.ObjectName.ToString().StartsWith('/')).Select(i => i.ObjectName.ToString()).Distinct().ToList();
            string ImportPath(int index,HashSet<int> seen)
            {
                if(index>=0 || !seen.Add(index) || -index>asset.Imports.Count)return "";
                var import=asset.Imports[-index-1];string outer=ImportPath(import.OuterIndex.Index,seen);
                return string.IsNullOrEmpty(outer)?import.ObjectName.ToString():outer+"."+import.ObjectName;
            }
            report.ImportedObjects=asset.Imports.Select((import,i)=>import.ClassName+": "+ImportPath(-i-1,new())).ToList();
            for (int i = 0; i < asset.Exports.Count; i++)
            {
                var e = asset.Exports[i]; string cls = e.GetExportClassType().ToString(); report.Classes.Add(cls);
                if (cls == "FontFace")
                {
                    var face = Locate(ExportBytes(path, e));
                    if (face != null) report.Faces.Add(new(i, e.ObjectName.ToString(), face.Value.Offset, face.Value.Length, face.Value.Font.Family, face.Value.Font.Codepoints.Count));
                    else if(asset.HasUnversionedProperties&&SupportedExternal(ExportBytes(path,e),version))report.ExternalFaces.Add(new(i,e.ObjectName.ToString()));
                }
                if (cls == "Font" && e is NormalExport normal)
                    report.CompositeProperties = JsonConvert.SerializeObject(normal.Data, Formatting.Indented);
                else if(cls=="Font")report.ReferenceStatus="Font imports resolved; property layout unresolved (matching .usmap may be required).";
            }
        }
        catch (Exception ex) { report.Error = ex.Message; }
        return report;
    }
    public static object Rewrite(string source, string destination, string version, string fontFile, bool allowPrivateUseLoss,byte[]? externalOriginal=null)
    {
        byte[] replacement = File.ReadAllBytes(fontFile); var newFont = new SfntFont(replacement);
        var asset = Open(source, version); var changed = new List<object>();
        for (int i = 0; i < asset.Exports.Count; i++)
        {
            var e = asset.Exports[i]; if (e.GetExportClassType().ToString() != "FontFace") continue;
            var data = ExportBytes(source, e); var face = Locate(data);
            bool external=face==null&&asset.HasUnversionedProperties&&SupportedExternal(data,version);
            if(face==null&&!external)continue;
            if(external&&externalOriginal==null)throw new InvalidDataException("Original external font data is required for coverage verification.");
            var originalFont=external?new SfntFont(externalOriginal!):face!.Value.Font;
            var missing = originalFont.Codepoints.Except(newFont.Codepoints).ToArray();
            var icons = missing.Where(IsPrivateUse).ToArray();
            if (icons.Length > 0 && !allowPrivateUseLoss) throw new PrivateUseGlyphLossException(source,icons);
            byte[] rewritten;
            if(external)rewritten=InlineExternal(data,version,replacement);
            else
            {
                int tail=data.Length-face!.Value.Offset-face.Value.Length;
                rewritten=new byte[face.Value.Offset+replacement.Length+tail];
                data.AsSpan(0,face.Value.Offset).CopyTo(rewritten);BitConverter.GetBytes(replacement.Length).CopyTo(rewritten,face.Value.Offset-4);replacement.CopyTo(rewritten,face.Value.Offset);
            }
            var raw = e.ConvertToChildExport<RawExport>(); raw.Data = rewritten; raw.Extras = Array.Empty<byte>(); asset.Exports[i] = raw;
            changed.Add(new { Export = i, Missing = missing.Select(c => $"U+{c:X}").ToArray(), PrivateUseMissing = icons.Length });
        }
        if (changed.Count == 0) throw new NotSupportedException("No supported terminal inline FontFace byte array found; package left untouched.");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!); asset.Write(destination);
        var check = Inspect(destination, version);
        if (check.Error != null || check.Faces.Count != changed.Count) throw new InvalidDataException("Rewritten package failed structural readback: " + check.Error);
        var reloaded = Open(destination, version);
        foreach (var face in check.Faces)
        {
            var data = ExportBytes(destination, reloaded.Exports[face.Export]);
            if (PakTools.Hash(data.AsSpan(face.Offset, face.Length).ToArray()) != PakTools.Hash(replacement)) throw new InvalidDataException("Inline font readback hash mismatch.");
        }
        return new { Source = source, Destination = destination, ReplacementSha256 = PakTools.Hash(replacement), Exports = changed, RuntimeVerified = false };
    }
    public static bool IsPrivateUse(int c) => c is >= 0xe000 and <= 0xf8ff or >= 0xf0000 and <= 0xffffd or >= 0x100000 and <= 0x10fffd;
    public static byte[] ExtractFace(string path,string version,int export)
    {
        var asset=Open(path,version);var bytes=ExportBytes(path,asset.Exports[export]);var face=Locate(bytes)??throw new InvalidDataException("Unsupported inline font layout.");
        return bytes.AsSpan(face.Offset,face.Length).ToArray();
    }
}
