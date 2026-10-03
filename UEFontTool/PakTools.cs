using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.IO.Compression;

namespace UEFontTool;

public static class PakTools
{
    public static string Root => AppContext.BaseDirectory;
    public static byte[] Run(params string[] args)
    {
        var start = new ProcessStartInfo(Path.Combine(Root, "tools", "repak.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("Cannot start repak.");
        var errors = process.StandardError.ReadToEndAsync();
        using var output = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(output);
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidDataException(errors.GetAwaiter().GetResult());
        return output.ToArray();
    }
    public static string[] List(string pak) => Encoding.UTF8.GetString(Run("list", pak))
        .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(SafePath).ToArray();
    public static byte[] ReadWithKeys(List<byte[]> keys, params string[] args)
    {
        try { return Run(args); }
        catch (InvalidDataException)
        {
            foreach (var key in keys)
            {
                try { return Run(new[] { "--aes-key", Convert.ToHexString(key) }.Concat(args).ToArray()); }
                catch (InvalidDataException) { }
            }
            throw new InvalidDataException($"PAK 无法读取：已尝试 {keys.Count} 个 AES 密钥；请检查密钥及游戏格式适配。 / PAK unreadable after trying {keys.Count} AES key(s); check the key and game-specific format. " + FooterDiagnostic(args.LastOrDefault(File.Exists)));
        }
    }
    public static readonly string[] Versions = { "V0", "V1", "V2", "V3", "V4", "V5", "V6", "V7", "V8A", "V8B", "V9", "V10", "V11" };
    public static string OldestVersion(IEnumerable<string> versions)
    {
        var indices = versions.Select(v => Array.IndexOf(Versions, v)).ToArray();
        if (indices.Length == 0 || indices.Any(i => i < 0)) throw new NotSupportedException("Unknown PAK version; refusing to guess output format.");
        return Versions[indices.Min()];
    }
    public static string SafePath(string path)
    {
        path = path.Replace('\\', '/');
        if (path.StartsWith("../../../", StringComparison.Ordinal)) path = path[9..];
        if (string.IsNullOrWhiteSpace(path) || path.Any(char.IsControl) || path.StartsWith('/') || path.Contains(':') || path.Split('/').Any(p => p is ".." or "."))
            throw new InvalidDataException("Invalid virtual path: " + path);
        return string.Join('/', path.Split('/', StringSplitOptions.RemoveEmptyEntries));
    }
    public static bool IsFont(string path) => new[] { ".ufont", ".ttf", ".otf" }.Contains(Path.GetExtension(path).ToLowerInvariant());
    public static bool IsFontAssetCandidate(string path) => path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) && (path.Contains("/Fonts/", StringComparison.OrdinalIgnoreCase) || path.Contains("/Font/", StringComparison.OrdinalIgnoreCase));
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static string HashFile(string file) { using var stream = File.OpenRead(file); return Convert.ToHexString(SHA256.HashData(stream)); }
    public static string? FindLocal(string relative)
    {
        for (var dir = new DirectoryInfo(Root); dir != null; dir = dir.Parent)
        {
            string path = Path.Combine(dir.FullName, relative);
            if (File.Exists(path)) return path;
        }
        return null;
    }
    public static string FooterDiagnostic(string? file)
    {
        if (file == null) return "";
        using var stream = File.OpenRead(file);
        stream.Position = Math.Max(0, stream.Length - 4096);
        var bytes = new byte[checked((int)(stream.Length - stream.Position))]; stream.ReadExactly(bytes);
        for (int i = bytes.Length - 8; i >= 0; i--)
            if (BitConverter.ToUInt32(bytes, i) == 0x5a6f12e1)
                return $"PAK footer version={BitConverter.ToUInt32(bytes, i + 4)}, distance={bytes.Length - i}. 自定义版本需要专用读取器 / Custom versions require a dedicated reader.";
        return "未发现标准 PAK 尾部标记 / No standard PAK footer magic found; AES alone cannot repair a missing footer.";
    }
    public static List<byte[]> Keys(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return new();
        // File input is retained for local CLI callers. The UI passes key text directly.
        if (File.Exists(input)) input = File.ReadAllText(input);
        var tokens = input.Split(new[] { '\r', '\n', ';', ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new List<byte[]>();
        foreach (var token in tokens)
        {
            string hex = token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? token[2..] : token;
            byte[] key;
            if (Regex.IsMatch(hex, @"\A[0-9a-fA-F]{64}\z")) key = Convert.FromHexString(hex);
            else
            {
                try { key = Convert.FromBase64String(token); }
                catch (FormatException) { throw new InvalidDataException("AES 格式错误：请输入 64 位十六进制（可带 0x）或 32 字节 Base64。 / Enter 64 hex digits or a Base64-encoded 32-byte key."); }
                if (key.Length != 32) throw new InvalidDataException("AES 必须为 256 位 / AES key must be 256 bits.");
            }
            if (!result.Any(k => k.SequenceEqual(key))) result.Add(key);
        }
        return result;
    }
}

// Read-only adapters for observed Wukong and DragonSword layouts. See THIRD_PARTY.md.
public sealed class WukongPak
{
    readonly bool dragonSword;
    readonly string path;
    readonly bool encrypted;
    byte[]? key;
    readonly byte[] encoded;
    readonly string[] methods;
    public Dictionary<string, int> Entries { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> FontAssetCandidates { get; } = new();
    static byte[] Read(BinaryReader reader, int size)
    {
        if (size < 0 || size > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException("Invalid index range.");
        var data = reader.ReadBytes(size);
        if (data.Length != size) throw new EndOfStreamException();
        return data;
    }
    static int Count(BinaryReader r)
    {
        int n = r.ReadInt32();
        if (n < 0 || n > 20_000_000) throw new InvalidDataException("Invalid index count.");
        return n;
    }
    string Text(BinaryReader reader)
    {
        int n = reader.ReadInt32();
        if (n < -32768 || n > 32768) throw new InvalidDataException("Invalid index string.");
        var bytes = Read(reader, Math.Abs(n) * (n < 0 ? 2 : 1));
        if (dragonSword && bytes.Length > 0)
        {
            if (n > 0) { byte mask = bytes[^1]; for (int i = 0; i < bytes.Length; i++) bytes[i] ^= mask; }
            else { byte low = bytes[^2], high = bytes[^1]; for (int i = 0; i < bytes.Length; i++) bytes[i] ^= i % 2 == 0 ? low : high; }
        }
        return (n < 0 ? Encoding.Unicode : Encoding.UTF8).GetString(bytes).TrimEnd('\0');
    }
    public static bool Detect(string path)
    {
        using var f = File.OpenRead(path);
        if (f.Length < 223) return false;
        f.Seek(-206, SeekOrigin.End);
        using var r = new BinaryReader(f);
        return r.ReadUInt32() == 0x5a6f12e1 && r.ReadInt32() == 11;
    }
    public static bool DetectDragonSword(string path)
    {
        using var f = File.OpenRead(path);
        if (f.Length < 221) return false;
        f.Seek(-204, SeekOrigin.End);
        using var r = new BinaryReader(f);
        return r.ReadUInt32() == 0x5a6f12e1 && r.ReadInt32() == 101;
    }
    byte[] At(long offset, long size)
    {
        using var file = File.OpenRead(path);
        if (offset < 0 || size < 0 || size > 256 * 1024 * 1024 || offset > file.Length - size) throw new InvalidDataException("Invalid PAK range.");
        file.Position = offset;
        using var r = new BinaryReader(file);
        return Read(r, checked((int)size));
    }
    byte[] Decrypt(byte[] data, bool decrypt)
    {
        if (!decrypt) return data;
        using var aes = Aes.Create();
        aes.Key = key ?? throw new InvalidDataException("Missing AES key.");
        return aes.DecryptEcb(data, PaddingMode.None);
    }
    public WukongPak(string pak, List<byte[]> keys, bool dragonSword = false)
    {
        path = pak;
        this.dragonSword = dragonSword;
        if (!(dragonSword ? DetectDragonSword(pak) : Detect(pak))) throw new InvalidDataException("Unsupported game-specific PAK layout.");
        int footerSize = dragonSword ? 221 : 223, extra = dragonSword ? 0 : 2;
        var footer = At(new FileInfo(pak).Length - footerSize, footerSize);
        encrypted = footer[16] != 0;
        long offset = BitConverter.ToInt64(footer, 25 + extra), size = BitConverter.ToInt64(footer, 33 + extra);
        methods = new[] { "None" }.Concat(Enumerable.Range(0, 5).Select(i => Encoding.ASCII.GetString(footer, 61 + extra + 32 * i, 32).TrimEnd('\0'))).ToArray();
        byte[]? primary = null;
        var raw = At(offset, size);
        foreach (var candidate in encrypted ? keys : new List<byte[]> { new byte[32] })
        {
            key = candidate;
            var data = Decrypt(raw, encrypted);
            if (SHA1.HashData(data).AsSpan().SequenceEqual(footer.AsSpan(41 + extra, 20))) { primary = data; break; }
        }
        if (primary == null) throw new InvalidDataException($"索引校验失败：已尝试 {keys.Count} 个密钥，请输入匹配当前游戏版本的 AES。 / Index verification failed after {keys.Count} key(s); enter the matching AES key.");
        UnmaskIndex(primary);
        using var r = new BinaryReader(new MemoryStream(primary));
        string mount = Text(r);
        Count(r); Read(r, 8);
        if (r.ReadInt32() != 0) Read(r, 36);
        if (r.ReadInt32() != 1) throw new InvalidDataException("PAK has no full directory index.");
        long directoryOffset = r.ReadInt64(), directorySize = r.ReadInt64();
        var hash = Read(r, 20);
        encoded = Read(r, Count(r));
        if (dragonSword && encoded.Length > 0)
        {
            if (encoded.Length < 6) throw new InvalidDataException("Invalid encoded entry table.");
            byte mask = encoded[5]; for (int i = 0; i < encoded.Length; i++) encoded[i] ^= mask;
        }
        var directory = Decrypt(At(directoryOffset, directorySize), encrypted);
        // DragonSword's directory hash does not cover the stored transformed bytes.
        // Its primary index hash is checked above; directory strings/ranges are validated below.
        if (!dragonSword && !SHA1.HashData(directory).SequenceEqual(hash)) throw new InvalidDataException("Directory index checksum mismatch.");
        UnmaskIndex(directory);
        using var d = new BinaryReader(new MemoryStream(directory));
        int dirs = Count(d);
        for (int i = 0; i < dirs; i++)
        {
            string folder = Text(d); int files = Count(d);
            for (int j = 0; j < files; j++)
            {
                string name = Text(d); int location = d.ReadInt32();
                if (location == int.MinValue) continue;
                string full = PakTools.SafePath(mount + folder.TrimStart('/') + name);
                if (PakTools.IsFont(full)) Entries[full] = location;
                else if (PakTools.IsFontAssetCandidate(full)) FontAssetCandidates.Add(full);
            }
        }
    }
    void UnmaskIndex(byte[] bytes)
    {
        if (!dragonSword || !encrypted || bytes.Length < 16) return;
        byte mask = bytes[2]; for (int i = 0; i < bytes.Length; i++) bytes[i] ^= mask;
    }
    public byte[] Extract(string target)
    {
        int location = Entries[target];
        if (location < 0) throw new NotSupportedException("Non-encoded font entries are not supported.");
        using var r = new BinaryReader(new MemoryStream(encoded)); r.BaseStream.Position = location;
        uint flags = r.ReadUInt32();
        uint blockSize = (flags & 63) == 63 ? r.ReadUInt32() : (flags & 63) << 11;
        string method = methods[(flags >> 23) & 63];
        long offset = (flags & (1u << 31)) != 0 ? r.ReadUInt32() : r.ReadInt64();
        long rawSize = (flags & (1u << 30)) != 0 ? r.ReadUInt32() : r.ReadInt64();
        long compressedSize = method == "None" ? rawSize : (flags & (1u << 29)) != 0 ? r.ReadUInt32() : r.ReadInt64();
        if (rawSize < 0 || rawSize > 128 * 1024 * 1024) throw new InvalidDataException("Invalid font size.");
        bool encryptPayload = (flags & (1u << 22)) != 0;
        int blocks = (int)((flags >> 6) & 65535);
        long position = offset + (dragonSword ? 53 : 54) + (method == "None" ? 0 : 4 + blocks * 16);
        long[] lengths = method == "None" ? new[] { rawSize } : blocks == 1 && !encryptPayload ? new[] { compressedSize } : Enumerable.Range(0, blocks).Select(_ => (long)r.ReadUInt32()).ToArray();
        using var output = new MemoryStream();
        foreach (long length in lengths)
        {
            long stored = encryptPayload ? (length + 15) & ~15L : length;
            var data = Decrypt(At(position, stored), encryptPayload).AsSpan(0, checked((int)length)).ToArray(); position += stored;
            int expected = checked((int)Math.Min(rawSize - output.Length, blocks == 1 ? rawSize : blockSize));
            if (method == "Oodle") data = Oodle(data, expected);
            else if (method is "Zlib" or "Gzip")
            {
                using Stream decode = method == "Zlib" ? new ZLibStream(new MemoryStream(data), CompressionMode.Decompress) : new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
                using var expanded = new MemoryStream(); decode.CopyTo(expanded); data = expanded.ToArray();
            }
            else if (method != "None") throw new NotSupportedException("Unsupported compression: " + method);
            output.Write(data);
        }
        if (output.Length != rawSize) throw new InvalidDataException("Extracted payload size mismatch.");
        return output.ToArray();
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate long OodleFn(byte[] src, long srcSize, byte[] dst, long dstSize, int fuzz, int crc, int verbosity, IntPtr basePtr, long baseSize, IntPtr callback, IntPtr user, IntPtr scratch, long scratchSize, int phase);
    static byte[] Oodle(byte[] data, int expected)
    {
        string path = Path.Combine(PakTools.Root, "tools", "oo2core_9_win64.dll");
        if (!File.Exists(path)) throw new FileNotFoundException("Oodle 字体需自行提供合法来源的 tools/oo2core_9_win64.dll / Supply your own Oodle DLL in tools/oo2core_9_win64.dll to extract this font.");
        IntPtr library = NativeLibrary.Load(path);
        try
        {
            var decode = Marshal.GetDelegateForFunctionPointer<OodleFn>(NativeLibrary.GetExport(library, "OodleLZ_Decompress"));
            byte[] output = new byte[expected];
            if (decode(data, data.Length, output, expected, 1, 0, 0, IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, 3) != expected) throw new InvalidDataException("Oodle decompression failed.");
            return output;
        }
        finally { NativeLibrary.Free(library); }
    }
}
