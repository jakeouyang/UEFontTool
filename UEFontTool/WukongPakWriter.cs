using System.Security.Cryptography;

namespace UEFontTool;

// Wukong's runtime only mounts paks with its 223-byte footer (two extra bytes
// after the version field) and locates entry data at a fixed 54-byte local
// header, while repak writes 53 bytes. Convert() repoints a repak V11 pak to
// that layout: one pad byte per entry, patched index offsets, recomputed
// primary-index SHA-1 and a Wukong footer. Uncompressed entries only until
// compressed ones are validated in game.
public static class WukongPakWriter
{
    const int StandardFooter = 221, WukongFooter = 223, LocalHeaderSize = 53, RuntimeHeaderSize = 54;

    record Entry(long OffsetFieldIndex, int Width, long Offset);

    public static void Convert(string pakPath)
    {
        byte[] pak = File.ReadAllBytes(pakPath);
        int fileLength = pak.Length;
        long indexOffset = ReadInt64(pak, fileLength - StandardFooter + 25);
        long indexSize = ReadInt64(pak, fileLength - StandardFooter + 33);
        byte[] methods = Slice(pak, fileLength - StandardFooter + 61, 160);
        byte[] index = Slice(pak, indexOffset, checked((int)indexSize));

        using var reader = new BinaryReader(new MemoryStream(index));
        ReadText(reader);
        reader.ReadInt32(); reader.ReadBytes(8);
        if (reader.ReadInt32() != 0) reader.ReadBytes(36);
        if (reader.ReadInt32() != 1) throw new InvalidDataException("PAK has no full directory index.");
        long directoryOffsetField = reader.BaseStream.Position;
        long directoryOffset = reader.ReadInt64(), directorySize = reader.ReadInt64();
        reader.ReadBytes(20);
        long encodedBase = reader.BaseStream.Position + 4;
        byte[] directory = Slice(pak, directoryOffset, checked((int)directorySize));

        var entries = new List<Entry>();
        using (var dirs = new BinaryReader(new MemoryStream(directory)))
        {
            int folderCount = dirs.ReadInt32();
            for (int i = 0; i < folderCount; i++)
            {
                ReadText(dirs); int files = dirs.ReadInt32();
                for (int j = 0; j < files; j++)
                {
                    ReadText(dirs); int location = dirs.ReadInt32();
                    if (location == int.MinValue) continue;
                    uint flags = BitConverter.ToUInt32(index, checked((int)(encodedBase + location)));
                    if (((flags >> 23) & 63) != 0) throw new InvalidDataException("Compressed entries are not validated for the Wukong layout yet.");
                    int field = checked((int)(encodedBase + location + 4 + ((flags & 63) == 63 ? 4 : 0)));
                    entries.Add(new Entry(field, (flags & (1u << 31)) != 0 ? 4 : 8,
                        (flags & (1u << 31)) != 0 ? BitConverter.ToUInt32(index, field) : ReadInt64(index, field)));
                }
            }
        }

        var ordered = entries.OrderBy(e => e.Offset).ToList();
        var data = new MemoryStream();
        long cursor = 0;
        for (int i = 0; i < ordered.Count; i++)
        {
            var entry = ordered[i];
            long insert = entry.Offset + LocalHeaderSize;
            data.Write(pak, checked((int)cursor), checked((int)(insert - cursor)));
            data.WriteByte(0);
            cursor = insert;
            byte[] relocated = entry.Width == 4 ? BitConverter.GetBytes((uint)(entry.Offset + i)) : BitConverter.GetBytes(entry.Offset + i);
            Array.Copy(relocated, 0, index, (int)entry.OffsetFieldIndex, entry.Width);
        }
        data.Write(pak, checked((int)cursor), checked((int)(indexOffset - cursor)));
        byte[] dataRegion = data.ToArray();
        Array.Copy(BitConverter.GetBytes((long)dataRegion.Length + index.Length), 0, index, (int)directoryOffsetField, 8);

        byte[] footer = new byte[WukongFooter];
        BitConverter.GetBytes(0x5A6F12E1u).CopyTo(footer, 17);
        BitConverter.GetBytes(11).CopyTo(footer, 21);
        footer[25] = 0x00; footer[26] = 0x01;
        BitConverter.GetBytes((long)dataRegion.Length).CopyTo(footer, 27);
        BitConverter.GetBytes((long)index.Length).CopyTo(footer, 35);
        SHA1.HashData(index).CopyTo(footer, 43);
        methods.CopyTo(footer, 63);

        using var output = File.Create(pakPath);
        output.Write(dataRegion);
        output.Write(index);
        output.Write(directory);
        output.Write(footer);
    }

    static byte[] Slice(byte[] source, long offset, int size)
    {
        if (offset < 0 || size < 0 || offset > source.Length - size) throw new InvalidDataException("PAK range outside file.");
        var result = new byte[size];
        Array.Copy(source, offset, result, 0, size);
        return result;
    }
    static long ReadInt64(byte[] source, long offset) => BitConverter.ToInt64(source, checked((int)offset));
    static void ReadText(BinaryReader reader)
    {
        int size = reader.ReadInt32();
        if (size < -32768 || size > 32768) throw new InvalidDataException("Invalid index string.");
        reader.BaseStream.Position += Math.Abs(size) * (size < 0 ? 2L : 1L);
    }
}
