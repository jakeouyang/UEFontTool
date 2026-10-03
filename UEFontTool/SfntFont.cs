using System.Buffers.Binary;
using System.Text;

namespace UEFontTool;

// Coverage reader, not an outline converter. Select the Unicode cmap rather than
// asking Windows to parse unrelated legacy/Mac cmap subtables in cooked fonts.
public sealed class SfntFont
{
    readonly byte[] data;
    public string Family { get; } = "Font";
    public HashSet<int> Codepoints { get; } = new();
    ReadOnlySpan<byte> Slice(int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > data.Length - count) throw new InvalidDataException("Font table outside file.");
        return data.AsSpan(offset, count);
    }
    int U16(int offset) => BinaryPrimitives.ReadUInt16BigEndian(Slice(offset, 2));
    uint U32(int offset) => BinaryPrimitives.ReadUInt32BigEndian(Slice(offset, 4));
    int I32(int offset) => checked((int)U32(offset));
    public SfntFont(byte[] bytes)
    {
        data = bytes;
        if (data.Length < 12 || U32(0) is not (0x00010000 or 0x4f54544f)) throw new InvalidDataException("仅支持独立 TTF/OTF 字体 / Only standalone TTF/OTF fonts are supported.");
        var tables = new Dictionary<string, (int Offset, int Length)>();
        int count = U16(4);
        if (count is 0 or > 4096) throw new InvalidDataException("Invalid font table count.");
        for (int i = 0; i < count; i++)
        {
            int record = 12 + 16 * i;
            string tag = Encoding.ASCII.GetString(Slice(record, 4));
            int offset = I32(record + 8), size = I32(record + 12);
            Slice(offset, size); tables.Add(tag, (offset, size));
        }
        foreach (string tag in new[] { "head", "maxp", "cmap" }) if (!tables.ContainsKey(tag)) throw new InvalidDataException("Missing font table: " + tag);
        if (!tables.ContainsKey("glyf") && !tables.ContainsKey("CFF ") && !tables.ContainsKey("CFF2")) throw new InvalidDataException("No font outlines.");
        int glyphs = U16(tables["maxp"].Offset + 4);
        var cmap = tables["cmap"];
        int cmapEnd = checked(cmap.Offset + cmap.Length);
        int maps = U16(cmap.Offset + 2);
        var candidates = new List<(int Rank, int Position)>();
        for (int i = 0; i < maps; i++)
        {
            int record = cmap.Offset + 4 + 8 * i;
            if (record + 8 > cmapEnd) throw new InvalidDataException("Invalid cmap record.");
            int platform = U16(record), encoding = U16(record + 2);
            int position = checked(cmap.Offset + I32(record + 4));
            if (position + 2 > cmapEnd) throw new InvalidDataException("Invalid cmap offset.");
            int format = U16(position);
            if ((platform == 0 || platform == 3 && encoding is 1 or 10) && format is 4 or 12 or 13)
            {
                int rank = platform == 3 && encoding == 10 ? 100 : platform == 0 && encoding == 6 ? 90 : platform == 0 && encoding == 4 ? 80 : platform == 3 && encoding == 1 ? 70 : 10;
                candidates.Add((rank, position));
            }
        }
        if (candidates.Count == 0) throw new NotSupportedException("No supported Unicode cmap (4/12/13).");
        int start = candidates.OrderByDescending(c => c.Rank).First().Position;
        int kind = U16(start);
        int length = kind == 4 ? U16(start + 2) : I32(start + 4);
        int end = checked(start + length);
        if (length < 16 || end > cmapEnd) throw new InvalidDataException("Invalid Unicode cmap size.");
        void Add(int code, uint glyph)
        {
            if (code > 0x10ffff || code is >= 0xd800 and <= 0xdfff) return;
            if (glyph >= glyphs) throw new InvalidDataException("Unicode cmap glyph exceeds maxp count.");
            if (glyph != 0) Codepoints.Add(code);
        }
        if (kind == 4)
        {
            int segments = U16(start + 6) / 2;
            if (segments < 1 || start + 16 + 8 * segments > end) throw new InvalidDataException("Invalid cmap segments.");
            int ends = start + 14, starts = ends + 2 * segments + 2, deltas = starts + 2 * segments, ranges = deltas + 2 * segments;
            for (int i = 0; i < segments; i++)
            {
                int a = U16(starts + 2 * i), b = U16(ends + 2 * i), delta = U16(deltas + 2 * i), range = U16(ranges + 2 * i);
                if (a > b) throw new InvalidDataException("Invalid cmap segment range.");
                for (int code = a; code <= b && code < 0xffff; code++)
                {
                    int glyph;
                    if (range == 0) glyph = (code + delta) & 65535;
                    else
                    {
                        int address = ranges + 2 * i + range + 2 * (code - a);
                        if (address + 2 > end) throw new InvalidDataException("Invalid cmap glyph pointer.");
                        glyph = U16(address); if (glyph != 0) glyph = (glyph + delta) & 65535;
                    }
                    Add(code, (uint)glyph);
                }
            }
        }
        else
        {
            int groups = I32(start + 12);
            if (groups < 0 || groups > (length - 16) / 12) throw new InvalidDataException("Invalid Unicode cmap groups.");
            int previousEnd = -1;
            for (int i = 0; i < groups; i++)
            {
                int group = start + 16 + 12 * i, first = I32(group), last = I32(group + 4); uint glyph = U32(group + 8);
                if (first <= previousEnd || first > last || last > 0x10ffff) throw new InvalidDataException("Invalid Unicode cmap range.");
                for (int code = first; code <= last; code++) Add(code, checked(glyph + (kind == 12 ? (uint)(code - first) : 0)));
                previousEnd = last;
            }
        }
        if (Codepoints.Count == 0) throw new InvalidDataException("Unicode cmap is empty.");
        if (tables.TryGetValue("name", out var names) && names.Length >= 6)
        {
            int num = U16(names.Offset + 2), storage = names.Offset + U16(names.Offset + 4);
            for (int i = 0; i < num; i++)
            {
                int record = names.Offset + 6 + 12 * i;
                if (record + 12 > names.Offset + names.Length) break;
                if (U16(record + 6) != 1 || U16(record) is not (0 or 3)) continue;
                int size = U16(record + 8), offset = storage + U16(record + 10);
                if (offset + size > names.Offset + names.Length || size % 2 != 0) continue;
                Family = Encoding.BigEndianUnicode.GetString(Slice(offset, size)); break;
            }
        }
    }
}
