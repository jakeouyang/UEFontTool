namespace UEFontTool;

public sealed class PrivateUseGlyphLossException : IOException
{
    public string Target {get;}
    public int[] Codepoints {get;}
    public string FontName=>Path.GetFileNameWithoutExtension(Target);
    public PrivateUseGlyphLossException(string target,int[] codepoints):base(Describe(target,codepoints))
    {Target=target;Codepoints=codepoints.Distinct().Order().ToArray();}
    static string Describe(string target,int[] codepoints)=>$"{Path.GetFileNameWithoutExtension(target)}: 新字体缺少 {codepoints.Distinct().Count()} 个私用区字符 / Replacement loses {codepoints.Distinct().Count()} private-use glyphs.\r\n无需专家模式：取消此字体、换用覆盖更完整的新字体，或勾选“游戏语言”右侧的“允许缺少特殊字符”后重试。\r\nNo Expert mode required: uncheck this target, choose a more complete font, or check Allow missing custom characters beside Game language and retry.\r\n允许缺失不会补字或保留原图标；这些字符可能是图标或其他自定义字符。 / Allowing loss does not merge glyphs or preserve icons.\r\n"+string.Join(" ",codepoints.Distinct().Order().Select(c=>$"U+{c:X}"));
}
