using System.Runtime.InteropServices;

namespace UEFontTool;

public class DarkWindow : Form
{
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
    readonly Panel caption=new(){Dock=DockStyle.Top,Height=42,BackColor=Color.Black};
    readonly Label captionText=new(){Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Color.Silver,Padding=new Padding(14,0,0,0)};
    readonly Button minimize=CaptionButton("—"),maximize=CaptionButton("□"),close=CaptionButton("✕");
    public DarkWindow()
    {
        FormBorderStyle=FormBorderStyle.None;BackColor=Color.Black;ForeColor=Color.Silver;Padding=new Padding(1);DoubleBuffered=true;
        caption.Controls.Add(captionText);
        var picture=new PictureBox{Dock=DockStyle.Left,Width=40,BackColor=Color.Black};picture.Paint+=(_,e)=>{if(Icon!=null){int size=DeviceDpi*24/96;e.Graphics.DrawIcon(Icon,new Rectangle((picture.Width-size)/2,(picture.Height-size)/2,size,size));}};caption.Controls.Add(picture);picture.MouseDown+=DragWindow;captionText.Padding=new Padding(4,0,0,0);
        foreach(var button in new[]{minimize,maximize,close}){button.Dock=DockStyle.Right;caption.Controls.Add(button);}
        Controls.Add(caption);caption.AccessibleRole=AccessibleRole.TitleBar;
        minimize.Click+=(_,_)=>WindowState=FormWindowState.Minimized;maximize.Click+=(_,_)=>ToggleMaximize();close.Click+=(_,_)=>Close();
        foreach(Control control in new Control[]{caption,captionText}){control.MouseDown+=DragWindow;control.DoubleClick+=(_,_)=>{if(MaximizeBox)ToggleMaximize();};}
        TextChanged+=(_,_)=>captionText.Text=Text;
        SizeChanged+=(_,_)=>maximize.Text=WindowState==FormWindowState.Maximized?"❐":"□";
        SetCaptionLanguage(true);
    }
    static Button CaptionButton(string text)=>new(){Text=text,Width=48,FlatStyle=FlatStyle.Flat,BackColor=Color.Black,ForeColor=Color.Silver,TabStop=false,Font=new Font("Segoe UI",12),Cursor=Cursors.Hand};
    protected override void OnControlAdded(ControlEventArgs e){base.OnControlAdded(e);caption?.SendToBack();}
    protected override void OnShown(EventArgs e){base.OnShown(e);foreach(var button in new[]{minimize,maximize,close}){button.FlatAppearance.BorderSize=0;button.FlatAppearance.MouseOverBackColor=button==close?Color.FromArgb(150,25,35):Color.FromArgb(40,40,40);}minimize.Visible=MinimizeBox;maximize.Visible=MaximizeBox;}
    public void SetCaptionLanguage(bool chinese){minimize.AccessibleName=chinese?"最小化":"Minimize";maximize.AccessibleName=chinese?"最大化或还原":"Maximize or restore";close.AccessibleName=chinese?"关闭":"Close";}
    protected void SetCaptionMenu(MenuStrip menu)
    {
        menu.Dock=DockStyle.Right;menu.LayoutStyle=ToolStripLayoutStyle.HorizontalStackWithOverflow;menu.AutoSize=true;menu.Margin=Padding.Empty;caption.Controls.Add(menu);caption.Controls.SetChildIndex(menu,caption.Controls.IndexOf(minimize));
    }
    void DragWindow(object? sender,MouseEventArgs e){if(e.Button!=MouseButtons.Left)return;ReleaseCapture();SendMessage(Handle,0xA1,(IntPtr)2,IntPtr.Zero);}
    void ToggleMaximize(){MaximizedBounds=Screen.FromControl(this).WorkingArea;WindowState=WindowState==FormWindowState.Maximized?FormWindowState.Normal:FormWindowState.Maximized;}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using var pen=new Pen(Color.FromArgb(65,65,65));e.Graphics.DrawRectangle(pen,0,0,Width-1,Height-1);}
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if(m.Msg!=0x84||m.Result!=(IntPtr)1||WindowState==FormWindowState.Maximized)return;
        var point=PointToClient(new Point(unchecked((int)m.LParam.ToInt64())));int grip=Math.Max(6,DeviceDpi*6/96);
        bool left=point.X<grip,right=point.X>=ClientSize.Width-grip,top=point.Y<grip,bottom=point.Y>=ClientSize.Height-grip;
        m.Result=(IntPtr)(top?(left?13:right?14:12):bottom?(left?16:right?17:15):left?10:right?11:1);
    }
}

public sealed class FontMenuColors : ProfessionalColorTable
{
    public override Color MenuItemSelected=>Color.FromArgb(45,25,28);
    public override Color MenuItemSelectedGradientBegin=>MenuItemSelected;
    public override Color MenuItemSelectedGradientEnd=>MenuItemSelected;
    public override Color MenuItemBorder=>Color.FromArgb(75,40,45);
    public override Color MenuBorder=>Color.FromArgb(50,50,50);
    public override Color MenuStripGradientBegin=>Color.Black;
    public override Color MenuStripGradientEnd=>Color.Black;
    public override Color ToolStripDropDownBackground=>Color.Black;
    public override Color ImageMarginGradientBegin=>Color.Black;
    public override Color ImageMarginGradientMiddle=>Color.Black;
    public override Color ImageMarginGradientEnd=>Color.Black;
}
