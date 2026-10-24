using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8",FrameworkDisplayName=".NET Framework 4.8")]

namespace FileTracker {
static class Program {
    [STAThread] static void Main(string[] args) {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FileTracker");
        if(File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"FileTracker.portable"))) dir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Data");
        for(int i=0;i<args.Length-1;i++) if(args[i]=="--data-dir") dir=Path.GetFullPath(args[i+1]);
        try {
            Directory.CreateDirectory(dir);
            // Exclusive data lock prevents multiple writers and duplicate event streams.
            using(var guard=new FileStream(Path.Combine(dir,"instance.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)) {
                Settings s=Settings.Load(dir);Loc.Language=s.Language;s.Save(dir);
                Application.Run(new MainForm(dir,s,args.Contains("--tray")));
            }
        } catch(IOException e) {MessageBox.Show(Loc.T("Không mở được FileTracker. Nếu app đang chạy, mở từ biểu tượng ở khay hệ thống.\n\n")+e.Message,"FileTracker");}
          catch(Exception e) {MessageBox.Show(e.Message,Loc.T("FileTracker · Lỗi"));}
    }
}
class SmoothList : ListView { public SmoothList(){DoubleBuffered=true;} }
public class MainForm : Form {
    readonly string dataDir;readonly Settings config;readonly MonitorEngine engine;
    Color ink=UI.Ink,muted=UI.Muted;
    Label title,subtitle,status,totals,paging,detail;
    TextBox search;ComboBox roots,kinds,dates,sort;CheckBox hideTech;SmoothList list;
    Button pause,prev,next;NotifyIcon tray;
    readonly List<Button> fileActions=new List<Button>();
    Label emptyTitle,emptyHint,monitorBadge;Panel emptyState;Button starAction;
    System.Windows.Forms.Timer timer;List<Button> navigation=new List<Button>();
    List<string[]> rows=new List<string[]>();string mode="files",renderedMode="";int page=0,queryVersion=0;bool querying=false,dirty=true,quitting=false,shutdownComplete=false,rebuilding=false;
    long lastRevision=-1;DateTime lastSearch=DateTime.MinValue,lastRefresh=DateTime.MinValue;
    public MainForm(string dir,Settings s,bool startHidden) {
        dataDir=dir;config=s;Loc.Language=s.Language;engine=new MonitorEngine(dir,s);
        Text="FileTracker";Size=new Size(1360,880);MinimumSize=new Size(1100,760);StartPosition=FormStartPosition.CenterScreen;
        AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);
        Font=UI.TextFont();BackColor=UI.Canvas;ForeColor=ink;Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        BuildUI();
        tray=new NotifyIcon{Icon=Icon,Text=Loc.T("FileTracker · Theo dõi file"),Visible=true};
        UpdateTray();tray.DoubleClick+=(o,e)=>ShowApp();
        timer=new System.Windows.Forms.Timer{Interval=650};timer.Tick+=(o,e)=>Tick();timer.Start();
        Shown+=(o,e)=>{engine.Start();if(startHidden) Hide();};
        FormClosing+=async(o,e)=>{
            if(shutdownComplete)return;
            e.Cancel=true;
            if(!quitting&&e.CloseReason==CloseReason.UserClosing&&config.CloseToTray){Hide();tray.ShowBalloonTip(2500,"FileTracker",Loc.T("Vẫn theo dõi trong khay hệ thống. Nhấp đôi biểu tượng để mở lại."),ToolTipIcon.Info);return;}
            if(engine.Stopping)return;
            quitting=true;timer.Stop();status.Text=Loc.T("Đang dừng theo dõi…");
            await Task.Run(()=>engine.Dispose());
            shutdownComplete=true;tray.Dispose();Close();
        };
        KeyPreview=true;KeyDown+=(o,e)=>{if(e.Control&&e.KeyCode==Keys.F){search.Focus();e.SuppressKeyPress=true;}if(e.KeyCode==Keys.F5){engine.RequestScan();dirty=true;e.SuppressKeyPress=true;}};
    }
    void UpdateTray() {
        var old=tray.ContextMenuStrip;
        var menu=new ContextMenuStrip{Font=UI.TextFont(9),BackColor=UI.Card,ForeColor=UI.Ink,Renderer=new ToolStripProfessionalRenderer(new DarkMenuColors())};
        menu.Items.Add(Loc.T("Mở FileTracker"),null,(o,e)=>ShowApp());
        menu.Items.Add(Loc.T("Tạm dừng / Tiếp tục"),null,(o,e)=>TogglePause());
        menu.Items.Add(Loc.T("Thoát"),null,(o,e)=>{quitting=true;Close();});
        tray.Text=Loc.T("FileTracker · Theo dõi file");tray.ContextMenuStrip=menu;
        if(old!=null)old.Dispose();
    }
    void ApplyLanguage() {
        string term=search.Text,currentMode=mode;
        int rootIndex=roots.SelectedIndex,kindIndex=kinds.SelectedIndex,dateIndex=dates.SelectedIndex,sortIndex=sort.SelectedIndex;
        bool hide=hideTech.Checked;
        queryVersion++;rebuilding=true;SuspendLayout();
        try {
            foreach(Control control in Controls.Cast<Control>().ToArray())control.Dispose();
            navigation.Clear();fileActions.Clear();rows.Clear();renderedMode="";
            Loc.Language=config.Language;BuildUI();
            search.Text=term;roots.SelectedIndex=rootIndex;kinds.SelectedIndex=kindIndex;dates.SelectedIndex=dateIndex;sort.SelectedIndex=sortIndex;hideTech.Checked=hide;
            SetMode(currentMode);pause.Text=Loc.T(engine.Paused?"Tiếp tục":"Tạm dừng");((RoundedButton)pause).Glyph=engine.Paused?"\uE768":"\uE769";UpdateMonitorBadge();UpdateTray();
        }finally{rebuilding=false;ResumeLayout(true);}
        dirty=true;
    }
    Label LabelAt(string text,int size,bool bold) {return new SmoothLabel{Text=text,AutoSize=true,UseMnemonic=false,Font=UI.TextFont(size,bold?FontStyle.Bold:FontStyle.Regular),ForeColor=ink};}
    Button Btn(string text,EventHandler click,bool primary=false) {
        var b=new RoundedButton{Text=text,Primary=primary,AccessibleName=text};b.Click+=click;return b;
    }
    ComboBox Combo(string[] values,int width,bool reset=true) {
        var c=new RoundedCombo{DropDownStyle=ComboBoxStyle.DropDownList,Width=width,FlatStyle=FlatStyle.Flat,BackColor=UI.Card,ForeColor=ink,Margin=new Padding(0),IntegralHeight=false,DropDownHeight=260,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=24};
        c.DrawItem+=(o,e)=>{if(e.Index<0)return;bool selected=(e.State&DrawItemState.Selected)!=0&&(e.State&DrawItemState.ComboBoxEdit)==0;using(var b=new SolidBrush(selected?UI.Selection:UI.Input))e.Graphics.FillRectangle(b,e.Bounds);UI.DrawText(e.Graphics,c.Items[e.Index].ToString(),c.Font,new Rectangle(e.Bounds.X+4,e.Bounds.Y,e.Bounds.Width-8,e.Bounds.Height),c.Enabled?ink:muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);if((e.State&DrawItemState.Focus)!=0)e.DrawFocusRectangle();};
        c.Items.AddRange(values);c.SelectedIndex=0;if(reset)c.SelectedIndexChanged+=(o,e)=>ResetQuery();return c;
    }
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);WindowMaterial.Apply(this);}
    protected override void OnLoad(EventArgs e){
        base.OnLoad(e);var area=Screen.FromControl(this).WorkingArea;
        MinimumSize=new Size(Math.Min(MinimumSize.Width,area.Width),Math.Min(MinimumSize.Height,area.Height));
        Size=new Size(Math.Min(Width,area.Width),Math.Min(Height,area.Height));ResizeColumns();
    }
    void FilterField(TableLayoutPanel host,int column,string caption,ComboBox combo) {
        var cell=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Margin=new Padding(0,0,column==2?0:12,0)};
        cell.RowStyles.Add(new RowStyle(SizeType.Absolute,24));cell.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        cell.Controls.Add(new SmoothLabel{Text=caption,Dock=DockStyle.Fill,ForeColor=muted,Font=UI.TextFont(9),Margin=Padding.Empty},0,0);
        combo.Dock=DockStyle.Top;combo.AccessibleName=caption;cell.Controls.Add(combo,0,1);host.Controls.Add(cell,column,0);
    }
    void BuildUI() {
        var shell=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=Padding.Empty,Padding=Padding.Empty};
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,218));shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute,66));shell.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(shell);

        var menu=new SurfacePanel{Dock=DockStyle.Fill,Frosted=true,Radius=1,Margin=Padding.Empty,Padding=new Padding(18,13,20,12)};
        shell.Controls.Add(menu,0,0);shell.SetColumnSpan(menu,2);
        var brand=new SmoothLabel{Text="FileTracker",AutoSize=false,Size=new Size(164,38),Location=new Point(52,13),TextAlign=ContentAlignment.MiddleLeft,Font=UI.TextFont(14,FontStyle.Bold)};
        var logo=new PictureBox{Image=Icon.ToBitmap(),SizeMode=PictureBoxSizeMode.Zoom,Size=new Size(30,30),Location=new Point(17,17)};
        logo.Disposed+=(o,e)=>logo.Image.Dispose();menu.Controls.Add(logo);menu.Controls.Add(brand);
        var toolbar=new FlowLayoutPanel{Dock=DockStyle.Right,AutoSize=true,WrapContents=false,BackColor=Color.Transparent,Padding=Padding.Empty};
        var rescan=Btn(Loc.T("Đồng bộ lại"),(o,e)=>{engine.RequestScan();dirty=true;},true);((RoundedButton)rescan).Glyph="\uE72C";toolbar.Controls.Add(rescan);
        pause=Btn(Loc.T("Tạm dừng"),(o,e)=>TogglePause());((RoundedButton)pause).Glyph="\uE769";toolbar.Controls.Add(pause);
        var export=Btn(Loc.T("Xuất CSV"),(o,e)=>Export());((RoundedButton)export).Glyph="\uE74E";export.Margin=Padding.Empty;toolbar.Controls.Add(export);menu.Controls.Add(toolbar);

        var side=new Panel{Dock=DockStyle.Fill,BackColor=UI.Sidebar,Margin=Padding.Empty,Padding=new Padding(14,26,14,16)};shell.Controls.Add(side,0,1);
        side.Paint+=(o,e)=>{using(var pen=new Pen(UI.Line))e.Graphics.DrawLine(pen,side.Width-1,0,side.Width-1,side.Height);};
        side.Controls.Add(new SmoothLabel{Text=Loc.T("KHÔNG GIAN LÀM VIỆC"),Location=new Point(23,27),Size=new Size(190,22),ForeColor=muted,Font=UI.TextFont(8,FontStyle.Bold)});
        string[] names={Loc.T("File gần đây"),Loc.T("Lịch sử thay đổi"),Loc.T("File đã đánh dấu"),Loc.T("File lớn ≥ 100 MB")};
        string[] modes={"files","events","star","large"},glyphs={"\uE8A5","\uE81C","\uE734","\uE8B7"};
        for(int i=0;i<names.Length;i++) {
            string id=modes[i];var b=new RoundedButton{Navigation=true,Text=names[i],Tag=id,Glyph=glyphs[i],Chosen=id==mode,AutoSize=false,Location=new Point(12,62+i*49),Size=new Size(194,41),TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(11,0,6,0),Font=UI.TextFont(9.5f),AccessibleName=names[i]};
            b.Click+=(o,e)=>SetMode(id);side.Controls.Add(b);navigation.Add(b);
        }
        var bottom=new TableLayoutPanel{Dock=DockStyle.Bottom,Height=212,ColumnCount=1,RowCount=4,BackColor=Color.Transparent,Margin=Padding.Empty};
        bottom.RowStyles.Add(new RowStyle(SizeType.Absolute,42));bottom.RowStyles.Add(new RowStyle(SizeType.Absolute,42));bottom.RowStyles.Add(new RowStyle(SizeType.Absolute,84));bottom.RowStyles.Add(new RowStyle(SizeType.Absolute,44));side.Controls.Add(bottom);
        var settings=Btn(Loc.T("Thư mục & Cài đặt"),(o,e)=>SettingsDialog());((RoundedButton)settings).Navigation=true;((RoundedButton)settings).Glyph="\uE713";settings.Dock=DockStyle.Fill;settings.Font=UI.TextFont(9);settings.Margin=new Padding(0,0,0,6);bottom.Controls.Add(settings,0,0);
        var help=Btn(Loc.T("Trợ giúp"),(o,e)=>Help());((RoundedButton)help).Navigation=true;((RoundedButton)help).Glyph="\uE897";help.Dock=DockStyle.Fill;help.Margin=new Padding(0,0,0,6);bottom.Controls.Add(help,0,1);
        bottom.Controls.Add(new SmoothLabel{Text="Ctrl + F  ·  "+Loc.T("Tìm kiếm")+"\nF5  ·  "+Loc.T("Đồng bộ lại"),Dock=DockStyle.Fill,ForeColor=muted,Font=UI.TextFont(9),Padding=new Padding(9,16,0,0)},0,2);
        bottom.Controls.Add(new SmoothLabel{Text="●  "+Loc.T("Chỉ lưu trên máy"),Dock=DockStyle.Fill,ForeColor=UI.Success,Font=UI.TextFont(9),Padding=new Padding(9,12,0,0)},0,3);

        var main=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=8,Padding=new Padding(26,22,26,12),Margin=Padding.Empty,BackColor=BackColor};shell.Controls.Add(main,1,1);
        int[] heights={76,52,69,42,0,59,48,27};
        for(int i=0;i<heights.Length;i++)main.RowStyles.Add(heights[i]==0?new RowStyle(SizeType.Percent,100):new RowStyle(SizeType.Absolute,heights[i]));
        var heading=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};
        title=LabelAt(Loc.T("File gần đây"),25,true);title.Location=new Point(-2,-3);heading.Controls.Add(title);
        subtitle=new SmoothLabel{Text=Loc.T("File có sẵn được sắp theo thời gian Windows ghi nhận."),Location=new Point(0,43),Height=28,Width=600,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,ForeColor=muted,AutoEllipsis=true};heading.Controls.Add(subtitle);
        monitorBadge=new SmoothLabel{Text="●  "+Loc.T("Đang theo dõi"),AutoSize=true,ForeColor=UI.Success,Font=UI.TextFont(9),Padding=new Padding(0,10,0,0),Anchor=AnchorStyles.Top|AnchorStyles.Right};heading.Controls.Add(monitorBadge);
        heading.Resize+=(o,e)=>{monitorBadge.Location=new Point(heading.Width-monitorBadge.PreferredWidth,0);subtitle.Width=heading.Width;};main.Controls.Add(heading,0,0);
        var searchField=new SearchField(Loc.T("Tìm tên file hoặc đường dẫn (Ctrl+F)")){Dock=DockStyle.Fill,Margin=new Padding(0,0,0,10)};
        search=searchField.Input;search.TextChanged+=(o,e)=>{lastSearch=DateTime.UtcNow;ResetQuery();};main.Controls.Add(searchField,0,1);
        var filters=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty};
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,32));filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,28));
        roots=Combo(new[]{Loc.T("Tất cả thư mục")}.Concat(config.Roots).ToArray(),190);FilterField(filters,0,Loc.T("Thư mục"),roots);
        kinds=Combo(new[]{Loc.T("Tất cả loại"),Loc.T("Tài liệu"),Loc.T("Hình ảnh"),Loc.T("Video / Âm thanh"),Loc.T("Ứng dụng"),Loc.T("File nén"),Loc.T("Mã nguồn"),Loc.T("Kỹ thuật"),Loc.T("Khác")},145);FilterField(filters,1,Loc.T("Loại file"),kinds);
        dates=Combo(new[]{Loc.T("Mọi thời gian"),Loc.T("Hôm nay"),Loc.T("7 ngày qua"),Loc.T("30 ngày qua")},135);FilterField(filters,2,Loc.T("Thời gian"),dates);main.Controls.Add(filters,0,2);
        var info=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,Margin=Padding.Empty,Padding=new Padding(0,6,0,0)};
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));info.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));info.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,190));info.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,42));
        totals=new SmoothLabel{Text=Loc.T("Đang lập chỉ mục…"),Dock=DockStyle.Fill,ForeColor=muted,Font=UI.TextFont(9),AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft,Margin=Padding.Empty};info.Controls.Add(totals,0,0);
        hideTech=new CheckBox{Text=Loc.T("Ẩn file kỹ thuật"),Checked=true,AutoSize=true,Anchor=AnchorStyles.Left,Font=UI.TextFont(9),Margin=new Padding(8,0,14,0)};hideTech.CheckedChanged+=(o,e)=>ResetQuery();info.Controls.Add(hideTech,1,0);
        sort=Combo(new[]{Loc.T("Ngày tạo mới nhất"),Loc.T("Ngày sửa mới nhất"),Loc.T("Dung lượng lớn nhất")},190);sort.AccessibleName=Loc.T("Sắp xếp");sort.Anchor=AnchorStyles.Left|AnchorStyles.Right;sort.Font=UI.TextFont(9);info.Controls.Add(sort,2,0);
        var reset=Btn("",(o,e)=>ClearFilters());((RoundedButton)reset).Glyph="\uE711";reset.AutoSize=false;reset.MinimumSize=new Size(34,30);reset.Size=new Size(34,30);reset.Padding=new Padding(6,0,0,0);reset.Margin=new Padding(8,0,0,0);reset.AccessibleName=Loc.T("Đặt lại bộ lọc");info.Controls.Add(reset,3,0);
        var tips=new ToolTip();tips.SetToolTip(reset,Loc.T("Đặt lại bộ lọc"));tips.SetToolTip(search,Loc.T("Tìm tên file hoặc đường dẫn (Ctrl+F)"));reset.Disposed+=(o,e)=>tips.Dispose();main.Controls.Add(info,0,3);

        var listCard=new SurfacePanel{Dock=DockStyle.Fill,Padding=new Padding(1,1,1,6),Margin=Padding.Empty};main.Controls.Add(listCard,0,4);
        list=new SmoothList{Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,HideSelection=false,MultiSelect=false,BorderStyle=BorderStyle.None,BackColor=UI.Card,ForeColor=ink,Font=UI.TextFont(9.5f),ShowItemToolTips=true,OwnerDraw=true,AccessibleName=Loc.T("Danh sách file"),ShowGroups=false};
        list.Columns.Add(Loc.T("Tên file"),255);list.Columns.Add(Loc.T("Thời gian"),148);list.Columns.Add(Loc.T("Loại / Hoạt động"),145);list.Columns.Add(Loc.T("Dung lượng"),92,HorizontalAlignment.Right);list.Columns.Add(Loc.T("Đường dẫn"),300);
        var im=new ImageList{ImageSize=new Size(1,36)};im.Images.Add(new Bitmap(1,36));list.SmallImageList=im;
        list.DrawColumnHeader+=(o,e)=>{using(var b=new SolidBrush(UI.Input))e.Graphics.FillRectangle(b,e.Bounds);using(var f=UI.TextFont(9))UI.DrawText(e.Graphics,e.Header.Text,f,new Rectangle(e.Bounds.X+12,e.Bounds.Y,e.Bounds.Width-24,e.Bounds.Height),muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|(e.ColumnIndex==3?TextFormatFlags.Right:TextFormatFlags.Left));using(var p=new Pen(UI.Line))e.Graphics.DrawLine(p,e.Bounds.Left,e.Bounds.Bottom-1,e.Bounds.Right,e.Bounds.Bottom-1);};
        list.DrawItem+=(o,e)=>{};list.DrawSubItem+=DrawFileCell;
        list.SizeChanged+=(o,e)=>ResizeColumns();
        list.DpiChangedAfterParent+=(o,e)=>{ResizeColumns();list.Invalidate();};
        list.DoubleClick+=(o,e)=>OpenSelected(false);list.SelectedIndexChanged+=(o,e)=>{if(rebuilding)return;UpdateSelection();};
        list.KeyDown+=(o,e)=>{if(e.KeyCode==Keys.Enter){OpenSelected(false);e.Handled=true;}if(e.Control&&e.KeyCode==Keys.C){CopyPath();e.SuppressKeyPress=true;}};
        var context=new ContextMenuStrip{Font=UI.TextFont(9),BackColor=UI.Card,ForeColor=UI.Ink,Renderer=new ToolStripProfessionalRenderer(new DarkMenuColors())};context.Items.Add(Loc.T("Mở file"),null,(o,e)=>OpenSelected(false));context.Items.Add(Loc.T("Mở thư mục chứa"),null,(o,e)=>OpenSelected(true));context.Items.Add(Loc.T("Sao chép đường dẫn"),null,(o,e)=>CopyPath());context.Items.Add(Loc.T("Đánh dấu / Bỏ đánh dấu"),null,(o,e)=>Star());context.Opening+=(o,e)=>e.Cancel=Selected()==null;list.ContextMenuStrip=context;
        listCard.Controls.Add(list);
        emptyState=new Panel{BackColor=UI.Card,Visible=false,Size=new Size(490,160)};
        var emptyIcon=new SmoothLabel{Text="\uE8B7",Font=new Font("Segoe MDL2 Assets",30),ForeColor=UI.Accent,Dock=DockStyle.Top,Height=57,TextAlign=ContentAlignment.MiddleCenter};emptyState.Controls.Add(emptyIcon);
        emptyTitle=new SmoothLabel{Text=Loc.T("Đang lập chỉ mục…"),Location=new Point(0,67),Size=new Size(490,30),Font=UI.TextFont(13,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter};emptyState.Controls.Add(emptyTitle);
        emptyHint=new SmoothLabel{Location=new Point(20,105),Size=new Size(450,50),ForeColor=muted,Font=UI.TextFont(10),TextAlign=ContentAlignment.TopCenter};emptyState.Controls.Add(emptyHint);listCard.Controls.Add(emptyState);emptyState.BringToFront();
        listCard.Resize+=(o,e)=>{emptyState.Width=Math.Min(490,Math.Max(0,listCard.Width-20));emptyTitle.Width=emptyState.Width;emptyHint.Width=Math.Max(0,emptyState.Width-40);emptyState.Location=new Point((listCard.Width-emptyState.Width)/2,Math.Max(36,(listCard.Height-emptyState.Height)/2));};
        list.Disposed+=(o,e)=>{im.Dispose();context.Dispose();};
        detail=new SmoothLabel{Dock=DockStyle.Fill,Text=Loc.T("Chọn file để xem đường dẫn và thao tác."),AutoEllipsis=true,ForeColor=muted,Padding=new Padding(2,12,0,0),Margin=Padding.Empty,Font=UI.TextFont(9)};main.Controls.Add(detail,0,5);

        var footer=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};
        string[] actionNames={Loc.T("Mở file"),Loc.T("Mở thư mục"),Loc.T("Copy đường dẫn"),Loc.T("Đánh dấu")};
        EventHandler[] handlers={(o,e)=>OpenSelected(false),(o,e)=>OpenSelected(true),(o,e)=>CopyPath(),(o,e)=>Star()};
        for(int i=0;i<actionNames.Length;i++){var b=Btn(actionNames[i],handlers[i]);b.Font=UI.TextFont(9);b.Padding=new Padding(10,0,10,0);b.Enabled=false;fileActions.Add(b);actions.Controls.Add(b);if(i==3)starAction=b;}footer.Controls.Add(actions,0,0);
        var pages=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};
        prev=Btn("‹",(o,e)=>{if(page>0){page--;dirty=true;queryVersion++;}});prev.MinimumSize=new Size(34,36);prev.Padding=Padding.Empty;prev.AccessibleName=Loc.T("Trang trước");prev.Enabled=false;pages.Controls.Add(prev);
        paging=new SmoothLabel{Text=Loc.T("Trang 1"),AutoSize=true,Margin=new Padding(0,9,8,0),ForeColor=muted,Font=UI.TextFont(9)};pages.Controls.Add(paging);
        next=Btn("›",(o,e)=>{page++;dirty=true;queryVersion++;});next.MinimumSize=new Size(34,36);next.Padding=Padding.Empty;next.AccessibleName=Loc.T("Trang sau");next.Enabled=false;next.Margin=Padding.Empty;pages.Controls.Add(next);footer.Controls.Add(pages,1,0);main.Controls.Add(footer,0,6);
        status=new SmoothLabel{Dock=DockStyle.Fill,ForeColor=muted,AutoEllipsis=true,Text=Loc.T("Chuẩn bị theo dõi…"),Padding=new Padding(0,6,0,0),Margin=Padding.Empty,Font=UI.TextFont(8.5f)};status.Click+=(o,e)=>{if(engine.Errors>0)MessageBox.Show(engine.LastError,Loc.T("Chi tiết lần bỏ qua gần nhất"));};main.Controls.Add(status,0,7);
    }
    void ResizeColumns() {
        if(list==null||list.Columns.Count<5)return;int available=Math.Max(UI.Px(list,710),list.ClientSize.Width-UI.Px(list,24));
        list.Columns[0].Width=(int)(available*.28);list.Columns[1].Width=UI.Px(list,148);list.Columns[2].Width=UI.Px(list,139);list.Columns[3].Width=UI.Px(list,90);
        list.Columns[4].Width=Math.Max(UI.Px(list,135),available-list.Columns[0].Width-list.Columns[1].Width-list.Columns[2].Width-list.Columns[3].Width);
    }
    void DrawFileCell(object sender,DrawListViewSubItemEventArgs e) {
        var g=e.Graphics;bool selected=e.Item.Selected;
        using(var brush=new SolidBrush(selected?UI.Selection:e.ItemIndex%2==0?UI.Card:Color.FromArgb(51,51,53)))g.FillRectangle(brush,e.Bounds);
        var bounds=new Rectangle(e.Bounds.X+UI.Px(list,12),e.Bounds.Y,e.Bounds.Width-UI.Px(list,24),e.Bounds.Height);
        if(e.ColumnIndex==0){
            string[] r=(string[])e.Item.Tag;Color accent=r[7]=="1"?UI.Warning:UI.Accent;
            UI.Symbol(g,r[7]=="1"?"\uE734":"\uE8A5",new Rectangle(bounds.X,e.Bounds.Y,UI.Px(list,20),e.Bounds.Height),accent,13);int inset=UI.Px(list,29);bounds.X+=inset;bounds.Width-=inset;
        }
        string cellText=e.SubItem.Text;if(e.ColumnIndex==0&&cellText.StartsWith("★  "))cellText=cellText.Substring(3);
        UI.DrawText(g,cellText,list.Font,bounds,e.Item.ForeColor!=ink?e.Item.ForeColor:e.ColumnIndex==0?ink:muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|(e.ColumnIndex==3?TextFormatFlags.Right:TextFormatFlags.Left));
        if(selected&&list.Focused&&e.ColumnIndex==0)using(var p=new Pen(UI.Accent,3))g.DrawLine(p,e.Bounds.Left+1,e.Bounds.Top+6,e.Bounds.Left+1,e.Bounds.Bottom-6);
    }
    void UpdateSelection() {
        var r=Selected();foreach(var b in fileActions)b.Enabled=r!=null;
        if(starAction!=null)starAction.Text=Loc.T(r!=null&&r[7]=="1"?"Bỏ đánh dấu":"Đánh dấu");
        detail.Text=r==null?Loc.T("Chọn file để xem đường dẫn và thao tác."):r[5]+(r[6]!=""?Loc.T("\nTrước đó: ")+r[6]:"");
    }
    void ClearFilters() {search.Clear();roots.SelectedIndex=0;kinds.SelectedIndex=0;dates.SelectedIndex=0;hideTech.Checked=true;sort.SelectedIndex=0;ResetQuery();}
    void SetMode(string value) {
        mode=value;foreach(var b in navigation)((RoundedButton)b).Chosen=(string)b.Tag==mode;
        title.Text=navigation.First(b=>(string)b.Tag==mode).Text;
        subtitle.Text=mode=="events"?Loc.T("Thay đổi được ghi nhận khi app chạy; kết quả quét lại có nhãn “Phát hiện”."):mode=="large"?Loc.T("Các file từ 100 MB, ưu tiên dung lượng lớn nhất."):mode=="star"?Loc.T("Những file bạn đánh dấu để mở lại nhanh."):Loc.T("File có sẵn được sắp theo thời gian Windows ghi nhận.");
        sort.Enabled=mode!="events"&&mode!="large";ResetQuery();
    }
    void ResetQuery(){page=0;dirty=true;queryVersion++;}
    void TogglePause(){engine.SetPaused(!engine.Paused);pause.Text=engine.Paused?Loc.T("Tiếp tục"):Loc.T("Tạm dừng");((RoundedButton)pause).Glyph=engine.Paused?"\uE768":"\uE769";UpdateMonitorBadge();}
    void UpdateMonitorBadge(){monitorBadge.Text="●  "+Loc.T(engine.Paused?"Đã tạm dừng":engine.Scanning?"Đang đồng bộ…":"Đang theo dõi");monitorBadge.ForeColor=engine.Paused?UI.Warning:UI.Success;monitorBadge.Left=monitorBadge.Parent.ClientSize.Width-monitorBadge.PreferredWidth;}
    void UpdateEmptyState(){bool empty=rows.Count==0;emptyState.Visible=empty;list.Visible=!empty;emptyTitle.Text=Loc.T(engine.Scanning?"Đang lập chỉ mục…":"Không có file phù hợp");emptyHint.Text=Loc.T(engine.Scanning?"App đang quét lần đầu. Kết quả sẽ tự xuất hiện.":"Thử đổi từ khóa hoặc đặt lại bộ lọc.");}
    void ShowApp(){Show();WindowState=FormWindowState.Normal;Activate();dirty=true;}
    void Tick() {
        UpdateMonitorBadge();UpdateEmptyState();
        status.Text=engine.Status+(engine.Errors>0?" · "+engine.Errors+Loc.T(" lần không đọc được (bấm xem)"):"");
        long revision=Interlocked.Read(ref engine.Revision);
        if(revision!=lastRevision&&DateTime.UtcNow-lastRefresh>TimeSpan.FromSeconds(engine.Scanning?5:2)){dirty=true;lastRevision=revision;}
        if(Visible&&WindowState!=FormWindowState.Minimized&&dirty&&!querying&&DateTime.UtcNow-lastSearch>TimeSpan.FromMilliseconds(350)) RefreshRows();
    }
    class QuerySpec {public string Sql;public List<object> Args;}
    QuerySpec BuildQuery(bool export) {
        var args=new List<object>();bool events=mode=="events";
        string time=events?"time":sort.SelectedIndex==1?"modified":"created";
        string sql=events?"SELECT id,path,time,kind,size,path,oldpath,0 FROM events WHERE 1=1":"SELECT path,name,"+time+",category,size,path,'',star FROM files WHERE present=1";
        if(config.Roots.Count==0)sql+=" AND 0=1";
        else {sql+=" AND root IN ("+String.Join(",",config.Roots.Select(x=>"?"))+")";args.AddRange(config.Roots.Cast<object>());}
        if(roots.SelectedIndex>0){sql+=" AND root=?";args.Add(roots.SelectedItem.ToString());}
        if(search.Text.Trim()!=""){sql+=" AND path LIKE ? ESCAPE '\\'";args.Add("%"+search.Text.Trim().Replace("\\","\\\\").Replace("%","\\%").Replace("_","\\_")+"%");}
        if(kinds.SelectedIndex>0){sql+=" AND category=?";args.Add(Loc.CategoryKeys[kinds.SelectedIndex]);}
        if(hideTech.Checked&&kinds.SelectedIndex!=7)sql+=" AND category<>'Kỹ thuật'";
        if(mode=="star")sql+=" AND star=1";
        if(mode=="large")sql+=" AND size>=104857600";
        if(dates.SelectedIndex>0){DateTime cutoff=dates.SelectedIndex==1?DateTime.Today:DateTime.Now.AddDays(dates.SelectedIndex==2?-7:-30);sql+=" AND "+time+">=?";args.Add(cutoff.ToUniversalTime().Ticks);}
        sql+=" ORDER BY "+(!events&&(mode=="large"||sort.SelectedIndex==2)?"size":time)+" DESC,"+(events?"id":"path")+" DESC";
        if(export)sql+=" LIMIT 50001";else{sql+=" LIMIT 201 OFFSET ?";args.Add(page*200);}
        return new QuerySpec{Sql=sql,Args=args};
    }
    async void RefreshRows() {
        querying=true;dirty=false;int version=queryVersion;QuerySpec spec=BuildQuery(false);bool events=mode=="events";string selected=Selected()==null?null:Selected()[0];
        try {
            var result=await Task.Run(()=>engine.Reader.Query(spec.Sql,spec.Args.ToArray()));
            if(IsDisposed||quitting)return;if(version!=queryVersion){dirty=true;return;}
            next.Enabled=result.Count>200;prev.Enabled=page>0;var newRows=result.Take(200).ToList();
            paging.Text=Loc.T("Trang ")+(page+1);
            if(renderedMode==mode&&rows.Count==newRows.Count&&rows.Zip(newRows,(a,b)=>a.SequenceEqual(b)).All(same=>same)){lastRefresh=DateTime.UtcNow;return;}
            int top=list.TopItem==null?0:list.TopItem.Index;rows=newRows;renderedMode=mode;
            list.BeginUpdate();list.Items.Clear();list.Groups.Clear();var groups=new Dictionary<string,ListViewGroup>();
            foreach(var r in rows) {
                DateTime t=new DateTime(Convert.ToInt64(r[2]),DateTimeKind.Utc).ToLocalTime();
                string group=mode=="large"||(!events&&sort.SelectedIndex==2)?Loc.T("THEO DUNG LƯỢNG"):t.Date==DateTime.Today?Loc.T("HÔM NAY"):t.Date==DateTime.Today.AddDays(-1)?Loc.T("HÔM QUA"):t.ToString("dd / MM / yyyy");
                if(!groups.ContainsKey(group)){var g=new ListViewGroup(group,HorizontalAlignment.Left);groups[group]=g;list.Groups.Add(g);}
                var item=new ListViewItem((r[7]=="1"?"★  ":"")+(events?Path.GetFileName(r[1]):r[1]),groups[group]);
                item.SubItems.Add(t.ToString("dd/MM/yyyy HH:mm"));item.SubItems.Add(Loc.T(r[3]));item.SubItems.Add(SizeText(Convert.ToInt64(r[4])));item.SubItems.Add(r[5]);item.Tag=r;item.ToolTipText=r[5]+"\n"+t.ToString("dd/MM/yyyy HH:mm:ss");
                if(events&&(r[3]=="Xóa"||r[3]=="Phát hiện mất"))item.ForeColor=UI.Danger;
                list.Items.Add(item);if(r[0]==selected)item.Selected=true;
            }
            list.EndUpdate();if(top>0&&list.Items.Count>top)list.TopItem=list.Items[top];totals.Text=rows.Count==0?Loc.T("Không có kết quả · thử đổi bộ lọc"):rows.Count+Loc.T(" mục / trang · ")+(events?Loc.T("lịch sử ghi nhận"):Loc.T("file hiện có"));paging.Text=Loc.T("Trang ")+(page+1);lastRefresh=DateTime.UtcNow;
            UpdateSelection();UpdateEmptyState();
            if(rows.Count==0)detail.Text=engine.Scanning?Loc.T("App đang quét lần đầu. Kết quả sẽ tự xuất hiện."):Loc.T("Chưa có mục phù hợp. Thử bỏ bộ lọc hoặc chọn Đồng bộ lại.");
        } catch(Exception e) {if(!quitting)status.Text=Loc.T("Không tải được danh sách: ")+e.Message;}
        finally{querying=false;}
    }
    public static string SizeText(long n){if(n>=1073741824)return(n/1073741824.0).ToString("0.##")+" GB";if(n>=1048576)return(n/1048576.0).ToString("0.##")+" MB";if(n>=1024)return(n/1024.0).ToString("0.#")+" KB";return n+" B";}
    string[] Selected(){return list.SelectedItems.Count>0?(string[])list.SelectedItems[0].Tag:null;}
    void OpenSelected(bool folder) {
        var r=Selected();if(r==null)return;string p=r[5];
        try {
            if(folder) {
                if(File.Exists(p)) Process.Start("explorer.exe","/select,\""+p+"\"");
                else {string dir=Path.GetDirectoryName(p);if(Directory.Exists(dir))Process.Start(new ProcessStartInfo(dir){UseShellExecute=true});else MessageBox.Show(Loc.T("Thư mục không còn tồn tại hoặc ổ đĩa chưa kết nối."));}
            } else {
                if(!File.Exists(p)){MessageBox.Show(Loc.T("File không còn ở đường dẫn này. Xem Lịch sử thay đổi hoặc đồng bộ lại."));return;}
                string ext=Path.GetExtension(p).ToLowerInvariant();
                if(new[]{".exe",".msi",".msix",".bat",".cmd",".ps1",".vbs",".js",".lnk",".com",".scr",".hta",".url"}.Contains(ext)&&MessageBox.Show(Loc.T("File này có thể chạy chương trình hoặc lệnh:\n\n")+p+Loc.T("\n\nBạn muốn mở?"),Loc.T("Mở file"),MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
                Process.Start(new ProcessStartInfo(p){UseShellExecute=true});
            }
        }catch(Exception e){MessageBox.Show(e.Message,Loc.T("Không mở được file"));}
    }
    void CopyPath(){var r=Selected();if(r==null)return;try{Clipboard.SetText(r[5]);status.Text=Loc.T("Đã sao chép đường dẫn.");}catch(Exception e){MessageBox.Show(e.Message);}}
    async void Star(){var r=Selected();if(r==null)return;try{await Task.Run(()=>engine.Database.Exec("UPDATE files SET star=CASE WHEN star=1 THEN 0 ELSE 1 END WHERE path=?",r[5]));dirty=true;}catch(Exception e){if(!quitting)MessageBox.Show(e.Message,Loc.T("Không đánh dấu được file"));}}
    static string Csv(string s){if(s.Length>0&&"=+-@\t\r\n".IndexOf(s[0])>=0)s="'"+s;return "\""+s.Replace("\"","\"\"")+"\"";}
    async void Export() {
        QuerySpec spec=BuildQuery(true);
        using(var dialog=new SaveFileDialog{Filter="CSV (*.csv)|*.csv",FileName="FileTracker-"+DateTime.Now.ToString("yyyy-MM-dd")+".csv"}) {
            if(dialog.ShowDialog()!=DialogResult.OK)return;
            try {
                var data=await Task.Run(()=>engine.Reader.Query(spec.Sql,spec.Args.ToArray()));
                using(var w=new StreamWriter(dialog.FileName,false,new UTF8Encoding(true))){w.WriteLine(Loc.T("Tên file,Thời gian,Loại hoặc hoạt động,Dung lượng (byte),Đường dẫn,Đường dẫn cũ"));foreach(var r in data.Take(50000))w.WriteLine(String.Join(",",new[]{Path.GetFileName(r[5]),new DateTime(Convert.ToInt64(r[2]),DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),Loc.T(r[3]),r[4],r[5],r[6]}.Select(Csv)));}
                MessageBox.Show(Loc.T("Đã xuất ")+Math.Min(50000,data.Count)+Loc.T(" mục theo bộ lọc hiện tại.")+(data.Count>50000?Loc.T("\nĐã chạm giới hạn 50.000 mục; hãy lọc theo thư mục/ngày để xuất tiếp."):""),Loc.T("Xuất CSV"));
            }catch(Exception e){MessageBox.Show(e.Message,Loc.T("Không xuất được CSV"));}
        }
    }
    void SettingsDialog() {
        bool changed=false;
        using(var f=CreateSettingsDialog(()=>changed=true))f.ShowDialog(this);
        if(changed)ApplyLanguage();
    }
    SurfacePanel SettingCard(string name,string description,Control editor,int height) {
        var card=new SurfacePanel{Dock=DockStyle.Fill,Height=height,Margin=new Padding(0,0,0,8),Padding=new Padding(18,12,18,12),Radius=6};
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty,BackColor=UI.Card};
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,230));card.Controls.Add(layout);
        var text=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Margin=new Padding(0,0,20,0),BackColor=UI.Card};text.RowStyles.Add(new RowStyle(SizeType.Absolute,24));text.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        text.Controls.Add(new SmoothLabel{Text=name,Dock=DockStyle.Fill,ForeColor=ink,Font=UI.TextFont(10),Margin=Padding.Empty,AutoEllipsis=true},0,0);
        text.Controls.Add(new SmoothLabel{Text=description,Dock=DockStyle.Fill,ForeColor=muted,Font=UI.TextFont(9),Margin=Padding.Empty},0,1);layout.Controls.Add(text,0,0);
        editor.Anchor=AnchorStyles.Right;editor.Margin=Padding.Empty;layout.Controls.Add(editor,1,0);return card;
    }
    internal Form CreateSettingsDialog(Action languageChanged) {
        Settings saved=Settings.Load(dataDir);
        var f=new Form{Text=Loc.T("Thư mục & Cài đặt"),Icon=Icon,Size=new Size(1160,810),MinimumSize=new Size(1000,680),StartPosition=FormStartPosition.CenterParent,Font=Font,BackColor=UI.Canvas,ForeColor=ink,AutoScaleMode=AutoScaleMode.Dpi,MaximizeBox=true,MinimizeBox=false,ShowInTaskbar=false};
            f.HandleCreated+=(o,e)=>WindowMaterial.Apply(f);
            var shell=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,204));shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));f.Controls.Add(shell);
            var sidebar=new Panel{Dock=DockStyle.Fill,BackColor=UI.Sidebar,Margin=Padding.Empty};shell.Controls.Add(sidebar,0,0);
            sidebar.Controls.Add(new SmoothLabel{Text="FileTracker",Location=new Point(22,27),Size=new Size(170,30),Font=UI.TextFont(16,FontStyle.Bold)});
            sidebar.Controls.Add(new SmoothLabel{Text=Loc.T("Cài đặt"),Location=new Point(24,66),Size=new Size(170,24),ForeColor=muted});
            sidebar.Controls.Add(new SmoothLabel{Text=Loc.T("Chỉ lưu trên máy"),Dock=DockStyle.Bottom,Height=52,ForeColor=UI.Success,Padding=new Padding(23,12,0,0),Font=UI.TextFont(9)});
            var main=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Padding=new Padding(30,22,30,0),Margin=Padding.Empty};main.RowStyles.Add(new RowStyle(SizeType.Absolute,82));main.RowStyles.Add(new RowStyle(SizeType.Percent,100));main.RowStyles.Add(new RowStyle(SizeType.Absolute,74));shell.Controls.Add(main,1,0);
            var heading=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};var h=LabelAt(Loc.T("Thư mục & Cài đặt"),22,true);heading.Controls.Add(h);
            heading.Controls.Add(new SmoothLabel{Text=Loc.T("Tùy chỉnh cách FileTracker hoạt động trên máy bạn."),Location=new Point(0,44),Height=28,Width=750,ForeColor=muted});main.Controls.Add(heading,0,0);
            var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Margin=Padding.Empty};main.Controls.Add(scroll,0,1);
            var cards=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,RowCount=6,Margin=Padding.Empty,Padding=new Padding(0,0,10,0)};scroll.Controls.Add(cards);
            int[] heights={90,90,90,90,240,60};foreach(int height in heights)cards.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
            var startup=new ToggleSwitch{AccessibleName=Loc.T("Khởi động cùng Windows"),Name="startup"};
            using(var k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))startup.Checked=k!=null&&k.GetValue("FileTracker")!=null;
            bool wasStartup=startup.Checked;
            var startupCard=SettingCard(Loc.T("Khởi động cùng Windows"),Loc.T("Tự mở FileTracker dưới khay hệ thống khi đăng nhập."),startup,82);cards.Controls.Add(startupCard,0,0);
            var close=new ToggleSwitch{Checked=config.CloseToTray,AccessibleName=Loc.T("Tiếp tục chạy khi đóng cửa sổ"),Name="close-to-tray"};cards.Controls.Add(SettingCard(Loc.T("Tiếp tục chạy khi đóng cửa sổ"),Loc.T("Giữ theo dõi file dưới khay hệ thống."),close,82),0,1);
            var skip=new ToggleSwitch{Checked=saved.SkipDevFolders,AccessibleName=Loc.T("Bỏ qua thư mục lập trình"),Name="skip-dev"};cards.Controls.Add(SettingCard(Loc.T("Bỏ qua thư mục lập trình"),Loc.T("Bỏ qua .git, node_modules, __pycache__, .venv và venv."),skip,82),0,2);
            var language=Combo(new[]{"Tiếng Việt","English"},230,false);language.Name="settings-language";language.AccessibleName="Language / Ngôn ngữ";language.SelectedIndex=config.Language=="en"?1:0;
            var languageCard=SettingCard(Loc.T("Ngôn ngữ / Language"),Loc.T("Áp dụng ngay cho giao diện và menu của ứng dụng."),language,82);cards.Controls.Add(languageCard,0,3);
            var foldersCard=new SurfacePanel{Dock=DockStyle.Fill,Padding=new Padding(18,12,18,12),Margin=new Padding(0,0,0,8)};
            var foldersLayout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Margin=Padding.Empty};foldersLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,32));foldersLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));foldersLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,46));foldersCard.Controls.Add(foldersLayout);
            foldersLayout.Controls.Add(new SmoothLabel{Text=Loc.T("Các thư mục được theo dõi (bao gồm thư mục con)"),Dock=DockStyle.Fill,ForeColor=ink,Margin=Padding.Empty},0,0);
            var box=new ListBox{Dock=DockStyle.Fill,BackColor=UI.Input,ForeColor=ink,BorderStyle=BorderStyle.None,IntegralHeight=false,Name="monitored-folders",Margin=new Padding(0,0,0,8)};box.Items.AddRange(saved.Roots.ToArray());foldersLayout.Controls.Add(box,0,1);
            var folderActions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty,Padding=new Padding(0,6,0,0)};foldersLayout.Controls.Add(folderActions,0,2);
            var add=Btn(Loc.T("Thêm thư mục"),(o,e)=>{using(var d=new FolderBrowserDialog()){if(d.ShowDialog(f)==DialogResult.OK){string p=Settings.Normalize(d.SelectedPath);if(!box.Items.Cast<string>().Any(x=>p.StartsWith(x,StringComparison.OrdinalIgnoreCase)||x.StartsWith(p,StringComparison.OrdinalIgnoreCase)))box.Items.Add(p);else MessageBox.Show(Loc.T("Thư mục này đã được theo dõi hoặc trùng phạm vi với một thư mục đang có."));}}});((RoundedButton)add).Glyph="\uE710";folderActions.Controls.Add(add);
            var remove=Btn(Loc.T("Bỏ theo dõi mục chọn"),(o,e)=>{if(box.SelectedIndex>=0)box.Items.RemoveAt(box.SelectedIndex);});remove.Enabled=false;box.SelectedIndexChanged+=(o,e)=>remove.Enabled=box.SelectedIndex>=0;folderActions.Controls.Add(remove);cards.Controls.Add(foldersCard,0,4);
            cards.Controls.Add(new SmoothLabel{Text=Loc.T("Thay đổi phạm vi áp dụng sau khi mở lại app. File gốc không bị thay đổi."),Dock=DockStyle.Fill,ForeColor=muted,Font=UI.TextFont(9),Padding=new Padding(2,10,0,0)},0,5);
            string[] sections={Loc.T("Chung"),Loc.T("Ngôn ngữ"),Loc.T("Thư mục")};string[] icons={"\uE713","\uE774","\uE8B7"};Control[] targets={startupCard,languageCard,foldersCard};var nav=new List<RoundedButton>();
            for(int i=0;i<sections.Length;i++){Control target=targets[i];var b=new RoundedButton{Text=sections[i],Navigation=true,Chosen=i==0,Glyph=icons[i],AutoSize=false,Size=new Size(188,42),Location=new Point(8,120+i*48),TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(12,0,3,0),Font=UI.TextFont(9)};nav.Add(b);b.Click+=(o,e)=>{scroll.ScrollControlIntoView(target);foreach(var n in nav)n.Chosen=n==b;};sidebar.Controls.Add(b);}
            var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Margin=Padding.Empty,Padding=new Padding(0,18,0,0)};main.Controls.Add(footer,0,2);
            var cancel=Btn(Loc.T("Hủy thay đổi"),(o,e)=>f.Close());cancel.DialogResult=DialogResult.Cancel;footer.Controls.Add(cancel);f.CancelButton=cancel;
            var save=Btn(Loc.T("Lưu cài đặt"),(o,e)=>{
                try {
                    var updated=new Settings{Roots=box.Items.Cast<string>().ToList(),SkipDevFolders=skip.Checked,CloseToTray=close.Checked,Language=language.SelectedIndex==1?"en":"vi"};
                    if(startup.Checked!=wasStartup)using(var k=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) {
                        if(startup.Checked) k.SetValue("FileTracker","\""+Application.ExecutablePath+"\" --tray --data-dir \""+dataDir.TrimEnd('\\')+"\"");else k.DeleteValue("FileTracker",false);
                    }
                    updated.Save(dataDir);
                    bool scopeChanged=!config.Roots.SequenceEqual(updated.Roots,StringComparer.OrdinalIgnoreCase)||config.SkipDevFolders!=updated.SkipDevFolders;
                    bool changed=config.Language!=updated.Language;config.Language=updated.Language;Loc.Language=updated.Language;config.CloseToTray=close.Checked;if(changed)languageChanged();
                    MessageBox.Show(Loc.T(scopeChanged?"Đã lưu. Chọn Thoát ở khay hệ thống rồi mở lại app để áp dụng phạm vi mới.":"Đã lưu cài đặt."),Loc.T("Cài đặt"));f.Close();
                }catch(Exception ex){MessageBox.Show(ex.Message,Loc.T("Không lưu được cài đặt"));}
            },true);footer.Controls.Add(save);
            f.Load+=(o,e)=>{var area=Screen.FromControl(this).WorkingArea;f.MinimumSize=new Size(Math.Min(f.MinimumSize.Width,area.Width),Math.Min(f.MinimumSize.Height,area.Height));f.Size=new Size(Math.Min(f.Width,area.Width),Math.Min(f.Height,area.Height));};
        return f;
    }
    void Help(){MessageBox.Show(Loc.T("FILETRACKER\n\n• File gần đây: thời gian tạo/sửa do Windows ghi nhận; không khẳng định đây là lúc tải file.\n• Lịch sử thay đổi: Thêm, Sửa, Xóa, Đổi tên khi app chạy. Nhãn 'Phát hiện' là kết quả so sánh khi quét lại, không biết chính xác lúc thao tác xảy ra.\n• Không khôi phục nội dung hoặc phiên bản file cũ.\n• Đổi tên thư mục được đối soát thành các file mất/thêm; chuyển giữa hai thư mục theo dõi có thể hiện là Xóa và Thêm.\n• Ẩn file kỹ thuật chỉ lọc giao diện. Bỏ qua thư mục lập trình là cài đặt phạm vi quét.\n• Bỏ qua junction/symlink, thùng rác và thư mục hệ thống không đọc được. Không cần quyền Administrator.\n• Giữ tối đa 100.000 sự kiện gần nhất; mỗi trang 200 mục.\n• App không ghi nhận lúc đã thoát/tạm dừng. Quét lại không thể tìm file đã tạo rồi xóa trong thời gian đó.\n• Database chỉ chứa tên, đường dẫn và thông tin file, ở:\n")+dataDir+Loc.T("\n\nNhấp đôi file để mở. Ctrl+F: tìm kiếm; Ctrl+C trên dòng chọn: copy đường dẫn; F5: đồng bộ."),Loc.T("Hướng dẫn"),MessageBoxButtons.OK,MessageBoxIcon.Information);}
}
}
