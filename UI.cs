using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FileTracker {
// Shared visual tokens keep the main window and dialogs consistent.
static class UI {
    public static Font TextFont(float size=10,FontStyle style=FontStyle.Regular){return new Font("Segoe UI",size,style,GraphicsUnit.Point);}
    // Use the native ClearType rasterizer; grayscale antialiasing softened small Segoe UI text.
    public static void DrawText(Graphics graphics,string text,Font font,Rectangle bounds,Color color,TextFormatFlags flags) {
        TextRenderingHint previous=graphics.TextRenderingHint;
        try {graphics.TextRenderingHint=TextRenderingHint.ClearTypeGridFit;TextRenderer.DrawText(graphics,text,font,bounds,color,flags|TextFormatFlags.PreserveGraphicsClipping|TextFormatFlags.PreserveGraphicsTranslateTransform);}
        finally {graphics.TextRenderingHint=previous;}
    }
    public static int Px(Control control,int value){return (int)Math.Round(value*control.DeviceDpi/96.0);}
    public static readonly Color Ink=Color.FromArgb(245,245,247),Muted=Color.FromArgb(183,185,193),Accent=Color.FromArgb(115,190,255);
    public static readonly Color Canvas=Color.FromArgb(38,38,38),Sidebar=Color.FromArgb(32,32,32),Card=Color.FromArgb(48,48,48),Input=Color.FromArgb(59,59,59);
    public static readonly Color Line=Color.FromArgb(68,68,70),Selection=Color.FromArgb(49,67,85),Hover=Color.FromArgb(69,69,72),Disabled=Color.FromArgb(46,46,48);
    public static readonly Color Success=Color.FromArgb(133,211,169),Warning=Color.FromArgb(245,199,115),Danger=Color.FromArgb(255,155,155);
    public static GraphicsPath Round(RectangleF r,float radius) {
        var p=new GraphicsPath();float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));
        p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);
        p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
    }
    public static void Symbol(Graphics g,string glyph,Rectangle bounds,Color color,int size) {
        using(var font=new Font("Segoe MDL2 Assets",size))
            DrawText(g,glyph,font,bounds,color,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
    }
}

class SmoothLabel : Label {
    protected override void OnPaint(PaintEventArgs e){var previous=e.Graphics.TextRenderingHint;try{e.Graphics.TextRenderingHint=TextRenderingHint.ClearTypeGridFit;base.OnPaint(e);}finally{e.Graphics.TextRenderingHint=previous;}}
}

class RoundedButton : Button {
    bool hover,pressed,chosen;
    string glyph="";
    string previousText="";
    public string Glyph {get{return glyph;}set{glyph=value??"";if(AutoSize)Size=GetPreferredSize(Size.Empty);if(Parent!=null)Parent.PerformLayout();Invalidate();}}
    public bool Primary;
    public bool Navigation;
    public bool Chosen { get{return chosen;} set{chosen=value;Invalidate();} }
    public RoundedButton() {
        Font=UI.TextFont();
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor,true);
        FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;BackColor=Color.Transparent;Cursor=Cursors.Hand;
        AutoSize=true;MinimumSize=new Size(76,36);Padding=new Padding(14,0,14,0);Margin=new Padding(0,0,8,0);
    }
    public override Size GetPreferredSize(Size proposedSize) {
        Size text=TextRenderer.MeasureText(Text,Font,Size.Empty,TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding);return new Size(Math.Max(MinimumSize.Width,text.Width+Padding.Horizontal+UI.Px(this,10)+(Glyph.Length>0?UI.Px(this,28):0)),Math.Max(MinimumSize.Height,text.Height+UI.Px(this,14)));
    }
    protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);if(String.IsNullOrEmpty(AccessibleName)||AccessibleName==previousText)AccessibleName=Text;previousText=Text;}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left)pressed=true;Invalidate();base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
    protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space)pressed=true;Invalidate();base.OnKeyDown(e);}
    protected override void OnKeyUp(KeyEventArgs e){pressed=false;Invalidate();base.OnKeyUp(e);}
    protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
    protected override void OnLostFocus(EventArgs e){pressed=false;Invalidate();base.OnLostFocus(e);}
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaintBackground(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        Color fill=Primary?UI.Accent:chosen?Color.FromArgb(55,55,58):Navigation?UI.Sidebar:UI.Input;
        if(hover)fill=Primary?Color.FromArgb(151,207,255):UI.Hover;
        if(pressed)fill=Primary?Color.FromArgb(91,166,235):UI.Card;
        if(!Enabled)fill=UI.Disabled;
        var rect=new RectangleF(1,1,Width-3,Height-3);
        using(var path=UI.Round(rect,5))using(var brush=new SolidBrush(fill))using(var pen=new Pen(Navigation||Primary?fill:UI.Line)) {g.FillPath(brush,path);g.DrawPath(pen,path);}
        if(Navigation&&chosen)using(var path=UI.Round(new RectangleF(2,Height/2-9,3,18),1.5f))using(var brush=new SolidBrush(UI.Accent))g.FillPath(brush,path);
        Color ink=!Enabled?Color.FromArgb(128,128,135):Primary?Color.FromArgb(18,35,50):UI.Ink;
        Rectangle content=new Rectangle(Padding.Left,0,Width-Padding.Horizontal,Height);
        if(Glyph.Length>0){UI.Symbol(g,Glyph,new Rectangle(content.X,0,UI.Px(this,20),Height),ink,13);int inset=UI.Px(this,28);content.X+=inset;content.Width-=inset;}
        UI.DrawText(g,Text,Font,content,ink,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding|(TextAlign==ContentAlignment.MiddleLeft?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter));
        if(Focused&&ShowFocusCues)using(var path=UI.Round(new RectangleF(3,3,Width-7,Height-7),7))using(var pen=new Pen(Primary?Color.White:UI.Accent,2)){pen.DashStyle=DashStyle.Dot;g.DrawPath(pen,path);}
    }
}

// Keep native dropdown behavior, keyboard navigation and accessibility; paint only the closed field.
class RoundedCombo : ComboBox {
    public RoundedCombo(){Font=UI.TextFont();}
    protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
    protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
    protected override void WndProc(ref Message m) {
        base.WndProc(ref m);
        if(m.Msg!=0xF&&m.Msg!=0x317&&m.Msg!=0x318)return;
        using(var g=m.Msg==0xF?Graphics.FromHwnd(Handle):Graphics.FromHdc(m.WParam)) {
            g.SetClip(ClientRectangle,CombineMode.Intersect);g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var background=new SolidBrush(UI.Canvas))g.FillRectangle(background,ClientRectangle);
            using(var path=UI.Round(new RectangleF(.7f,.7f,Width-1.4f,Height-1.4f),5))using(var brush=new SolidBrush(Enabled?UI.Input:UI.Disabled))using(var pen=new Pen(Focused?UI.Accent:UI.Line,Focused?1.5f:1f)){g.FillPath(brush,path);g.DrawPath(pen,path);}
            UI.DrawText(g,Text,Font,new Rectangle(UI.Px(this,10),0,Width-UI.Px(this,35),Height),Enabled?UI.Ink:UI.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding);
            UI.Symbol(g,"\uE70D",new Rectangle(Width-UI.Px(this,27),0,UI.Px(this,20),Height),UI.Muted,8);
        }
    }
}

class SurfacePanel : Panel {
    public bool Frosted;
    public int Radius=7;
    public SurfacePanel(){DoubleBuffered=true;BackColor=Color.Transparent;SetStyle(ControlStyles.ResizeRedraw,true);}
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        using(var path=UI.Round(new RectangleF(.5f,.5f,Width-1,Height-1),Radius)) {
            if(Frosted)using(var brush=new LinearGradientBrush(ClientRectangle,Color.FromArgb(44,44,48),Color.FromArgb(36,36,39),20f))g.FillPath(brush,path);
            else using(var brush=new SolidBrush(UI.Card))g.FillPath(brush,path);
            using(var pen=new Pen(UI.Line))g.DrawPath(pen,path);
        }
    }
}

class SearchField : SurfacePanel {
    public readonly TextBox Input;
    readonly Button clear;
    readonly SmoothLabel placeholder;
    public SearchField(string hint) {
        Height=42;Padding=new Padding(40,11,42,8);
        Input=new TextBox{Font=UI.TextFont(),BorderStyle=BorderStyle.None,Dock=DockStyle.Fill,BackColor=UI.Card,ForeColor=UI.Ink,AccessibleName=hint};
        placeholder=new SmoothLabel{Font=UI.TextFont(),Text=hint,ForeColor=UI.Muted,BackColor=UI.Card,Location=new Point(40,11),Height=23,AutoEllipsis=true,Cursor=Cursors.IBeam};
        placeholder.Click+=(o,e)=>Input.Focus();
        Input.GotFocus+=(o,e)=>{placeholder.Visible=false;Invalidate();};Input.LostFocus+=(o,e)=>{placeholder.Visible=Input.TextLength==0;Invalidate();};
        clear=new Button{Font=UI.TextFont(),Text="×",FlatStyle=FlatStyle.Flat,Size=new Size(30,30),Anchor=AnchorStyles.Top|AnchorStyles.Right,BackColor=UI.Card,ForeColor=UI.Muted,Cursor=Cursors.Hand,AccessibleName=Loc.T("Xóa tìm kiếm"),TabStop=false,Visible=false};
        clear.FlatAppearance.BorderSize=0;clear.Click+=(o,e)=>{Input.Clear();Input.Focus();};
        Input.TextChanged+=(o,e)=>{clear.Visible=Input.TextLength>0;placeholder.Visible=Input.TextLength==0&&!Input.Focused;};
        Controls.Add(Input);Controls.Add(clear);Controls.Add(placeholder);placeholder.BringToFront();Resize+=(o,e)=>{clear.Location=new Point(Width-36,5);placeholder.Width=Math.Max(0,Width-82);};Click+=(o,e)=>Input.Focus();
    }
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);UI.Symbol(e.Graphics,"\uE721",new Rectangle(10,0,24,Height),UI.Muted,13);if(Input.Focused){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var p=UI.Round(new RectangleF(1,1,Width-3,Height-3),11))using(var pen=new Pen(UI.Accent,1.5f))e.Graphics.DrawPath(pen,p);}}
}

class ToggleSwitch : CheckBox {
    public ToggleSwitch(){Font=UI.TextFont();SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);AutoSize=false;Size=new Size(92,32);Cursor=Cursors.Hand;}
    protected override void OnCheckedChanged(EventArgs e){base.OnCheckedChanged(e);Invalidate();}
    protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
    protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaintBackground(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        UI.DrawText(g,Loc.T(Checked?"Bật":"Tắt"),Font,new Rectangle(0,0,43,Height),UI.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.Right);
        var track=new RectangleF(48,6,40,20);using(var p=UI.Round(track,10))using(var b=new SolidBrush(Checked?UI.Accent:UI.Input))using(var pen=new Pen(Checked?UI.Accent:UI.Muted)){g.FillPath(b,p);g.DrawPath(pen,p);}
        using(var b=new SolidBrush(Checked?Color.FromArgb(20,35,50):UI.Ink))g.FillEllipse(b,Checked?70:52,10,12,12);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,ClientRectangle,UI.Accent,BackColor);
    }
}

class DarkMenuColors : ProfessionalColorTable {
    public override Color ToolStripDropDownBackground {get{return UI.Card;}}
    public override Color ImageMarginGradientBegin {get{return UI.Card;}}
    public override Color ImageMarginGradientMiddle {get{return UI.Card;}}
    public override Color ImageMarginGradientEnd {get{return UI.Card;}}
    public override Color MenuItemSelected {get{return UI.Selection;}}
    public override Color MenuItemBorder {get{return UI.Line;}}
    public override Color MenuBorder {get{return UI.Line;}}
}

static class WindowMaterial {
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h,int attribute,ref int value,int size);
    public static void Apply(Form form) {
        if(SystemInformation.HighContrast)return;
        try {
            int round=2;DwmSetWindowAttribute(form.Handle,33,ref round,4);
            // Documented Windows 11 backdrop; older Windows keeps its native caption.
            int acrylic=3;DwmSetWindowAttribute(form.Handle,38,ref acrylic,4);
            int dark=1;DwmSetWindowAttribute(form.Handle,20,ref dark,4);
        }catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
    }
}
}
