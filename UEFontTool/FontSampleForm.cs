using System.Drawing.Text;

namespace UEFontTool;

public sealed class FontSampleForm : DarkWindow
{
    readonly PrivateFontCollection collection=new();
    Font? displayFont;
    public FontSampleForm(string path,bool zh)
    {
        Text=zh?"原字体样式与字符覆盖":"Original font and character coverage";Width=1000;Height=700;MinimumSize=new Size(800,550);SetCaptionLanguage(zh);StartPosition=FormStartPosition.CenterParent;BackColor=Color.Black;ForeColor=Color.Silver;
        var data=File.ReadAllBytes(path);var info=new SfntFont(data);
        var panel=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=1,RowCount=3};panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));panel.RowStyles.Add(new RowStyle(SizeType.Percent,100));panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(panel);
        var note=new Label{Dock=DockStyle.Top,AutoSize=true,Margin=new Padding(0,0,0,14),Text=(zh?"字体家族：":"Family: ")+info.Family+Environment.NewLine+(zh?"字符覆盖：":"Mapped characters: ")+info.Codepoints.Count+Environment.NewLine+(zh?"支持字符不等于游戏语言；缺少的示例字显示为 □。可据样式判断是否需要替换。":"Glyph coverage does not establish game language. Missing sample characters are shown as □.")};panel.Controls.Add(note);panel.SizeChanged+=(_,_)=>note.MaximumSize=new Size(Math.Max(200,panel.ClientSize.Width-panel.Padding.Horizontal-8),0);
        var sample=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,BackColor=Color.FromArgb(18,18,18),ForeColor=Color.Silver,ScrollBars=ScrollBars.Vertical};
        string text="简体中文：汉字 龙门 风云 游戏 菜单 字幕\r\n繁體中文：漢字 龍門 風雲 遊戲 選單 字幕\r\nEnglish: The quick brown fox\r\n0123456789 !? +- ABC abc";
        sample.Text=string.Concat(text.Select(c=>c is '\r' or '\n' or ' '||info.Codepoints.Contains(c)?c:'□'));
        var symbols=info.Codepoints.Where(CookedFonts.IsPrivateUse).Order().ToArray();
        if(symbols.Length>0)
        {
            note.Text+=Environment.NewLine+(zh?$"另含 {symbols.Length} 个私用字符，可能是图标或自定义字。":$"Includes {symbols.Length} private-use characters: possible icons or custom glyphs.");
            sample.Text+=Environment.NewLine+string.Concat(symbols.Take(16).Select(char.ConvertFromUtf32));
        }
        try{collection.AddFontFile(path);var family=collection.Families.First();var style=new[]{FontStyle.Regular,FontStyle.Bold,FontStyle.Italic,FontStyle.Bold|FontStyle.Italic}.First(family.IsStyleAvailable);displayFont=new Font(family,24,style);sample.Font=displayFont;}
        catch(ArgumentException){note.Text+=Environment.NewLine+(zh?"此字体无法由 Windows 预览，但仍可导出查看。":"Windows cannot preview this font; you can still export it.");}
        panel.Controls.Add(sample);
        var export=new FontActionButton{Text=zh?"导出此原字体…":"Export this original font…",Dock=DockStyle.Right,MinimumSize=new Size(240,42),BackColor=Color.Black,ForeColor=Color.IndianRed};export.Click+=(_,_)=>{using var dialog=new SaveFileDialog{FileName=Path.GetFileName(path),Filter="Font file|*"};if(dialog.ShowDialog(this)==DialogResult.OK)File.Copy(path,dialog.FileName,true);};panel.Controls.Add(export);
        FormClosed+=(_,_)=>{displayFont?.Dispose();collection.Dispose();};
    }
}
