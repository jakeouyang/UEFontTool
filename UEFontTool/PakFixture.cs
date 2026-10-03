using System.Security.Cryptography;

namespace UEFontTool;

// Synthetic archives only: no game data or real game keys are used by these tests.
internal static class PakFixture
{
    public static void EncryptIndex(string source, string destination, byte[] key, bool dragon)
    {
        byte[] original = File.ReadAllBytes(source);
        byte[] footer = original[^221..];
        int offset = checked((int)BitConverter.ToInt64(footer, 25));
        int size = checked((int)BitConverter.ToInt64(footer, 33));
        byte[] primary = original.AsSpan(offset, size).ToArray();
        int p = 0;
        TransformString(primary, ref p, dragon);
        p += 12;
        int hasHash = BitConverter.ToInt32(primary, p); p += 4;
        if (hasHash != 0)
        {
            // The fixture uses only the full directory index; omit the optional path-hash index.
            BitConverter.GetBytes(0).CopyTo(primary, p - 4);
            primary = primary[..p].Concat(primary[(p + 36)..]).ToArray();
        }
        if (BitConverter.ToInt32(primary, p) != 1) throw new InvalidDataException("Fixture requires a directory index.");
        p += 4;
        int directoryFields = p;
        int dirOffset = checked((int)BitConverter.ToInt64(primary, p)), dirSize = checked((int)BitConverter.ToInt64(primary, p + 8));
        byte[] directory = original.AsSpan(dirOffset, dirSize).ToArray();
        if (dragon)
        {
            int d = 4, dirs = BitConverter.ToInt32(directory, 0);
            for (int i = 0; i < dirs; i++)
            {
                TransformString(directory, ref d, true);
                int files = BitConverter.ToInt32(directory, d); d += 4;
                for (int j = 0; j < files; j++) { TransformString(directory, ref d, true); d += 4; }
            }
            int encodedSize = BitConverter.ToInt32(primary, p + 36);
            int start = p + 40;
            if (encodedSize < 6 || primary[start + 5] != 0) throw new InvalidDataException("Unexpected fixture entry layout.");
            for (int i = 0; i < encodedSize; i++) primary[start + i] ^= 3;
        }
        byte[] paddedDirectory = Pad(directory);
        if (dragon) Xor(paddedDirectory);
        using var aes = Aes.Create(); aes.Key = key;
        byte[] encryptedDirectory = aes.EncryptEcb(paddedDirectory, PaddingMode.None);
        BitConverter.GetBytes((long)original.Length).CopyTo(primary, directoryFields);
        BitConverter.GetBytes((long)encryptedDirectory.Length).CopyTo(primary, directoryFields + 8);
        SHA1.HashData(paddedDirectory).CopyTo(primary, directoryFields + 16);
        byte[] paddedPrimary = Pad(primary);
        if (dragon) Xor(paddedPrimary);
        byte[] encryptedPrimary = aes.EncryptEcb(paddedPrimary, PaddingMode.None);
        footer[16] = 1;
        BitConverter.GetBytes(dragon ? 101 : 11).CopyTo(footer, 21);
        BitConverter.GetBytes((long)original.Length + encryptedDirectory.Length).CopyTo(footer, 25);
        BitConverter.GetBytes((long)encryptedPrimary.Length).CopyTo(footer, 33);
        SHA1.HashData(paddedPrimary).CopyTo(footer, 41);
        using var output = File.Create(destination);
        output.Write(original); output.Write(encryptedDirectory); output.Write(encryptedPrimary); output.Write(footer);
    }
    static byte[] Pad(byte[] data) { byte[] result = new byte[(data.Length + 15) & ~15]; data.CopyTo(result, 0); return result; }
    static void Xor(byte[] data) { for (int i = 0; i < data.Length; i++) data[i] ^= 0x1d; }
    static void TransformString(byte[] data, ref int p, bool transform)
    {
        int n = BitConverter.ToInt32(data, p); p += 4;
        if (n <= 0) throw new InvalidDataException("Fixture requires ASCII paths.");
        if (transform) for (int i = 0; i < n; i++) data[p + i] ^= 5;
        p += n;
    }
}
