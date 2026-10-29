namespace UEFontTool;

public enum AesPrompt { None, Missing, CheckKeyOrFormat, InvalidFormat }

public static class EncryptionHints
{
    public static string[] EncryptedArchives(string paks)=>Directory.GetFiles(paks)
        .Where(p=>Path.GetExtension(p).Equals(".pak",StringComparison.OrdinalIgnoreCase)||Path.GetExtension(p).Equals(".utoc",StringComparison.OrdinalIgnoreCase))
        .Where(IsEncrypted).ToArray();

    public static bool IsEncrypted(string path)
    {
        try
        {
            using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
            if(Path.GetExtension(path).Equals(".utoc",StringComparison.OrdinalIgnoreCase))
            {
                if(file.Length<144)return false;
                var header=new byte[144];file.ReadExactly(header);
                return header.AsSpan(0,16).SequenceEqual("-==--==--==--==-"u8)&&header[16] is >=1 and <=8&&BitConverter.ToUInt32(header,20)>=144&&(header[80]&2)!=0;
            }
            if(!Path.GetExtension(path).Equals(".pak",StringComparison.OrdinalIgnoreCase))return false;
            file.Position=Math.Max(0,file.Length-4096);var tail=new byte[checked((int)(file.Length-file.Position))];file.ReadExactly(tail);
            for(int i=tail.Length-44;i>=1;i--)
            {
                if(BitConverter.ToUInt32(tail,i)!=0x5a6f12e1)continue;
                uint version=BitConverter.ToUInt32(tail,i+4);int distance=tail.Length-i;
                bool known=version switch{>=4 and <=7=>distance==44,8=>distance is 172 or 204,9=>distance==205,10=>distance==204,11=>distance is 204 or 206,101=>distance==204,_=>false};
                if(known)return tail[i-1]==1;
            }
        }
        catch(IOException){}catch(UnauthorizedAccessException){}
        return false;
    }

    public static AesPrompt Failure(string message,bool hasKey,bool encryptedContext=false)
    {
        if(message.Contains("AES 格式错误")||message.Contains("AES 必须为 256")||message.Contains("AES key must be 256",StringComparison.OrdinalIgnoreCase))return AesPrompt.InvalidFormat;
        bool explicitKey=message.Contains("missing encryption key",StringComparison.OrdinalIgnoreCase)||message.Contains("missing AES key",StringComparison.OrdinalIgnoreCase)||message.Contains("encrypted but no key",StringComparison.OrdinalIgnoreCase)||message.Contains("Encrypted archive could not be read",StringComparison.OrdinalIgnoreCase)||message.Contains("Index verification failed after",StringComparison.OrdinalIgnoreCase);
        return explicitKey||encryptedContext?hasKey?AesPrompt.CheckKeyOrFormat:AesPrompt.Missing:AesPrompt.None;
    }
}
