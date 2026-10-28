using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace UEFontTool;

public sealed class FontInput : UserControl
{
    readonly TextBox editor=new(){BorderStyle=BorderStyle.None,BackColor=Color.FromArgb(20,20,20),ForeColor=Color.White,AutoSize=true};
    public FontInput()
    {
        Height=42;MinimumSize=new Size(60,42);BackColor=editor.BackColor;Padding=new Padding(12,0,12,0);Margin=new Padding(0,4,8,4);AutoScaleMode=AutoScaleMode.Inherit;
        Controls.Add(editor);editor.TextChanged+=(_,_)=>OnTextChanged(EventArgs.Empty);editor.Enter+=(_,_)=>Invalidate();editor.Leave+=(_,_)=>Invalidate();
        Enter+=(_,_)=>editor.Focus();Click+=(_,_)=>editor.Focus();
    }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [AllowNull]
    public override string Text{get=>editor?.Text??"";set{if(editor!=null)editor.Text=value;}}
    public bool UseSystemPasswordChar{get=>editor.UseSystemPasswordChar;set=>editor.UseSystemPasswordChar=value;}
    public string PlaceholderText{get=>editor.PlaceholderText;set=>editor.PlaceholderText=value;}
    public string FieldName{set{AccessibleName=value;editor.AccessibleName=value;}}
    public void Clear()=>editor.Clear();
    void UpdateHeight(){int height=Math.Max(DeviceDpi*42/96,Font.Height+16);MinimumSize=new Size(MinimumSize.Width,height);Height=height;}
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);UpdateHeight();}
    protected override void OnDpiChangedAfterParent(EventArgs e){base.OnDpiChangedAfterParent(e);UpdateHeight();}
    protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);if(editor!=null){editor.Font=Font;PerformLayout();}}
    protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);if(editor!=null)editor.SetBounds(Padding.Left,Math.Max(1,(ClientSize.Height-editor.PreferredHeight)/2),Math.Max(1,ClientSize.Width-Padding.Horizontal),editor.PreferredHeight);}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using var pen=new Pen(ContainsFocus?Color.IndianRed:Color.FromArgb(75,75,75));e.Graphics.DrawRectangle(pen,0,0,Width-1,Height-1);}
}

public sealed class FontActionButton : TextButton
{
    public FontActionButton(){AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;MinimumSize=new Size(96,42);Padding=new Padding(14,6,14,6);BackColor=Color.Black;ForeColor=Color.FromArgb(235,65,70);FlatStyle=FlatStyle.Flat;Margin=new Padding(0,4,8,4);Cursor=Cursors.Hand;}
    void UpdateHeight(){int height=Math.Max(DeviceDpi*42/96,Font.Height+16);MinimumSize=new Size(MinimumSize.Width,height);Height=height;}
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);UpdateHeight();}
    protected override void OnDpiChangedAfterParent(EventArgs e){base.OnDpiChangedAfterParent(e);UpdateHeight();}
}

public sealed class FontCombo : ComboBox
{
    public FontCombo(){DropDownStyle=ComboBoxStyle.DropDownList;DrawMode=DrawMode.OwnerDrawFixed;FlatStyle=FlatStyle.Flat;BackColor=Color.FromArgb(20,20,20);ForeColor=Color.Silver;Margin=new Padding(0,4,8,4);UpdateItemHeight();}
    void UpdateItemHeight()=>ItemHeight=Math.Max(Font.Height+12,DeviceDpi*42/96-6);
    protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);UpdateItemHeight();}
    protected override void OnDpiChangedAfterParent(EventArgs e){base.OnDpiChangedAfterParent(e);UpdateItemHeight();}
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        using var background=new SolidBrush((e.State&DrawItemState.Selected)!=0?Color.FromArgb(60,28,30):BackColor);e.Graphics.FillRectangle(background,e.Bounds);
        var bounds=e.Bounds;bounds.Inflate(-8,0);string value=(e.Index>=0?GetItemText(Items[e.Index]):Text)??"";
        TextRenderer.DrawText(e.Graphics,value,Font,bounds,ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
    }
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if(!IsHandleCreated||m.Msg is not (0xF or 0x317 or 0x318))return;
        using var graphics=m.Msg is 0x317 or 0x318&&m.WParam!=IntPtr.Zero?Graphics.FromHdc(m.WParam):Graphics.FromHwnd(Handle);
        using var background=new SolidBrush(BackColor);graphics.FillRectangle(background,ClientRectangle);
        int arrowWidth=Math.Max(24,DeviceDpi*24/96);var textBounds=new Rectangle(12,0,Math.Max(1,Width-arrowWidth-20),Height);
        TextRenderer.DrawText(graphics,Text,Font,textBounds,Enabled?ForeColor:Color.Gray,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
        using var border=new Pen(Focused?Color.IndianRed:Color.FromArgb(75,75,75));graphics.DrawRectangle(border,0,0,Width-1,Height-1);
        int x=Width-arrowWidth/2,y=Height/2;using var arrow=new SolidBrush(Enabled?ForeColor:Color.Gray);graphics.FillPolygon(arrow,new[]{new Point(x-4,y-2),new Point(x+4,y-2),new Point(x,y+3)});
    }
}
