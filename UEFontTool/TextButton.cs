namespace UEFontTool;

public class TextButton : Button
{
    Color normalText=Color.Silver,hoverText=Color.WhiteSmoke;
    bool hovered,settingColor;
    public TextButton()
    {
        FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;Cursor=Cursors.Hand;UpdateBackground();
    }
    public Color HoverTextColor{get=>hoverText;set{hoverText=value;UpdateText();}}
    protected override bool ShowFocusCues=>false;
    public override void NotifyDefault(bool value)=>base.NotifyDefault(false);
    void UpdateBackground(){FlatAppearance.MouseOverBackColor=BackColor;FlatAppearance.MouseDownBackColor=BackColor;}
    void UpdateText()
    {
        if(IsDisposed||settingColor)return;settingColor=true;
        try{base.ForeColor=Enabled&&(hovered||Focused)?hoverText:normalText;}finally{settingColor=false;}
    }
    protected override void OnForeColorChanged(EventArgs e){if(!settingColor)normalText=base.ForeColor;base.OnForeColorChanged(e);if(!settingColor)UpdateText();}
    protected override void OnBackColorChanged(EventArgs e){base.OnBackColorChanged(e);UpdateBackground();}
    protected override void OnMouseEnter(EventArgs e){hovered=true;UpdateText();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hovered=false;UpdateText();base.OnMouseLeave(e);}
    protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);UpdateText();}
    protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);UpdateText();}
    protected override void OnEnabledChanged(EventArgs e){base.OnEnabledChanged(e);UpdateText();}
}

public sealed class TextMenuRenderer : ToolStripProfessionalRenderer
{
    public TextMenuRenderer():base(new FontMenuColors()){RoundedEdges=false;}
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {using var brush=new SolidBrush(e.Item.BackColor);e.Graphics.FillRectangle(brush,new Rectangle(Point.Empty,e.Item.Size));}
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {e.TextColor=!e.Item.Enabled?Color.Gray:e.Item.Selected||e.Item.Pressed?Color.WhiteSmoke:e.Item.ForeColor;base.OnRenderItemText(e);}
}
