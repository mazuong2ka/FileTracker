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

namespace FileTracker {
static class Program {
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [STAThread] static void Main(string[] args) {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);SetProcessDPIAware();
        string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FileTracker");
        if(File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"FileTracker.portable"))) dir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Data");
        for(int i=0;i<args.Length-1;i++) if(args[i]=="--data-dir") dir=Path.GetFullPath(args[i+1]);
        try {
            Directory.CreateDirectory(dir);
            // Exclusive data lock prevents multiple writers and duplicate event streams.
            using(var guard=new FileStream(Path.Combine(dir,"instance.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)) {
                Settings s=Settings.Load(dir);s.Save(dir);
                Application.Run(new MainForm(dir,s,args.Contains("--tray")));
            }
        } catch(IOException e) {MessageBox.Show("Không mở được FileTracker. Nếu app đang chạy, mở từ biểu tượng ở khay hệ thống.\n\n"+e.Message,"FileTracker");}
          catch(Exception e) {MessageBox.Show(e.Message,"FileTracker · Lỗi");}
    }
}
class SmoothList : ListView { public SmoothList(){DoubleBuffered=true;} }
public class MainForm : Form {
    readonly string dataDir;readonly Settings config;readonly MonitorEngine engine;
    Color ink=Color.FromArgb(25,38,57),muted=Color.FromArgb(98,111,129),blue=Color.FromArgb(36,99,235);
    Label title,subtitle,status,totals,paging,detail;
    TextBox search;ComboBox roots,kinds,dates,sort;CheckBox hideTech;SmoothList list;
    Button pause,prev,next;NotifyIcon tray;
    System.Windows.Forms.Timer timer;List<Button> navigation=new List<Button>();
    List<string[]> rows=new List<string[]>();string mode="files";int page=0,queryVersion=0;bool querying=false,dirty=true,quitting=false;
    long lastRevision=-1;DateTime lastSearch=DateTime.MinValue,lastRefresh=DateTime.MinValue;
    public MainForm(string dir,Settings s,bool startHidden) {
        dataDir=dir;config=s;engine=new MonitorEngine(dir,s);
        Text="FileTracker";Size=new Size(1330,830);MinimumSize=new Size(1100,700);StartPosition=FormStartPosition.CenterScreen;
        Font=new Font("Segoe UI",10);BackColor=Color.FromArgb(245,247,251);ForeColor=ink;Icon=SystemIcons.Application;
        BuildUI();
        tray=new NotifyIcon{Icon=SystemIcons.Application,Text="FileTracker · Theo dõi file",Visible=true};
        var menu=new ContextMenuStrip();menu.Items.Add("Mở FileTracker",null,(o,e)=>ShowApp());menu.Items.Add("Tạm dừng / Tiếp tục",null,(o,e)=>TogglePause());menu.Items.Add("Thoát",null,(o,e)=>{quitting=true;Close();});
        tray.ContextMenuStrip=menu;tray.DoubleClick+=(o,e)=>ShowApp();
        timer=new System.Windows.Forms.Timer{Interval=650};timer.Tick+=(o,e)=>Tick();timer.Start();
        Shown+=(o,e)=>{engine.Start();if(startHidden) Hide();};
        FormClosing+=(o,e)=>{if(!quitting&&e.CloseReason==CloseReason.UserClosing&&config.CloseToTray){e.Cancel=true;Hide();tray.ShowBalloonTip(2500,"FileTracker","Vẫn theo dõi trong khay hệ thống. Nhấp đôi biểu tượng để mở lại.",ToolTipIcon.Info);}else{quitting=true;timer.Stop();tray.Dispose();engine.Dispose();}};
        KeyPreview=true;KeyDown+=(o,e)=>{if(e.Control&&e.KeyCode==Keys.F){search.Focus();e.SuppressKeyPress=true;}if(e.KeyCode==Keys.F5){engine.RequestScan();dirty=true;e.SuppressKeyPress=true;}};
    }
    Label LabelAt(string text,int size,bool bold) {return new Label{Text=text,AutoSize=true,Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular),ForeColor=ink};}
    Button Btn(string text,EventHandler click,bool primary=false) {
        var b=new Button{Text=text,AutoSize=true,Height=36,MinimumSize=new Size(70,36),FlatStyle=FlatStyle.Flat,BackColor=primary?blue:Color.White,ForeColor=primary?Color.White:ink,Padding=new Padding(8,2,8,2),Cursor=Cursors.Hand,Margin=new Padding(0,0,8,0)};
        b.FlatAppearance.BorderColor=primary?blue:Color.FromArgb(216,224,235);b.Click+=click;return b;
    }
    ComboBox Combo(string[] values,int width) {var c=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=width,Margin=new Padding(0,0,10,0)};c.Items.AddRange(values);c.SelectedIndex=0;c.SelectedIndexChanged+=(o,e)=>ResetQuery();return c;}
    void BuildUI() {
        var side=new Panel{Dock=DockStyle.Left,Width=218,BackColor=Color.FromArgb(19,32,53),Padding=new Padding(20)};
        Controls.Add(side);
        var brand=new Label{Text="FILE\nTRACKER",Location=new Point(22,28),Size=new Size(180,80),Font=new Font("Segoe UI",23,FontStyle.Bold),ForeColor=Color.White};side.Controls.Add(brand);
        side.Controls.Add(new Label{Text="LỊCH SỬ FILE TRÊN MÁY",Location=new Point(24,119),Size=new Size(180,24),Font=new Font("Segoe UI",8,FontStyle.Bold),ForeColor=Color.FromArgb(134,156,190)});
        string[] names={"File gần đây","Lịch sử thay đổi","File đã đánh dấu","File lớn ≥ 100 MB"};string[] modes={"files","events","star","large"};
        for(int i=0;i<names.Length;i++) {
            string id=modes[i];var b=new Button{Text=names[i],Tag=id,Location=new Point(12,174+i*49),Size=new Size(194,42),TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(10,0,0,0),FlatStyle=FlatStyle.Flat,ForeColor=Color.White,BackColor=i==0?blue:side.BackColor,Cursor=Cursors.Hand};
            b.FlatAppearance.BorderSize=0;b.Click+=(o,e)=>SetMode(id);side.Controls.Add(b);navigation.Add(b);
        }
        var note=new Label{Text="Dữ liệu lưu trên máy\nKhông tải nội dung file lên mạng\n\nCtrl + F  ·  Tìm kiếm\nF5  ·  Đồng bộ lại",Dock=DockStyle.Bottom,Height=120,ForeColor=Color.FromArgb(159,179,208),Font=new Font("Segoe UI",9)};side.Controls.Add(note);
        var main=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=8,Padding=new Padding(24,18,24,12),BackColor=BackColor};
        main.RowStyles.Add(new RowStyle(SizeType.Absolute,78));main.RowStyles.Add(new RowStyle(SizeType.Absolute,44));main.RowStyles.Add(new RowStyle(SizeType.Absolute,48));main.RowStyles.Add(new RowStyle(SizeType.Absolute,34));main.RowStyles.Add(new RowStyle(SizeType.Percent,100));main.RowStyles.Add(new RowStyle(SizeType.Absolute,52));main.RowStyles.Add(new RowStyle(SizeType.Absolute,44));main.RowStyles.Add(new RowStyle(SizeType.Absolute,32));Controls.Add(main);main.BringToFront();
        var heading=new Panel{Dock=DockStyle.Fill};title=LabelAt("File gần đây",25,true);title.Location=new Point(0,0);heading.Controls.Add(title);
        subtitle=LabelAt("File có sẵn được sắp theo thời gian Windows ghi nhận.",10,false);subtitle.ForeColor=muted;subtitle.Location=new Point(2,46);heading.Controls.Add(subtitle);main.Controls.Add(heading,0,0);
        var tools=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};
        tools.Controls.Add(Btn("Đồng bộ lại",(o,e)=>{engine.RequestScan();dirty=true;},true));
        pause=Btn("Tạm dừng",(o,e)=>TogglePause());tools.Controls.Add(pause);
        tools.Controls.Add(Btn("Thư mục & Cài đặt",(o,e)=>SettingsDialog()));
        tools.Controls.Add(Btn("Xuất CSV",(o,e)=>Export()));tools.Controls.Add(Btn("Trợ giúp",(o,e)=>Help()));main.Controls.Add(tools,0,1);
        var filters=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Padding=new Padding(0,7,0,0)};
        search=new TextBox{Width=225,Margin=new Padding(0,0,10,0),AccessibleName="Tìm tên file hoặc đường dẫn"};search.TextChanged+=(o,e)=>{lastSearch=DateTime.UtcNow;ResetQuery();};
        var tip=new ToolTip();tip.SetToolTip(search,"Tìm tên file hoặc đường dẫn (Ctrl+F)");filters.Controls.Add(search);
        roots=Combo(new[]{"Tất cả thư mục"}.Concat(config.Roots).ToArray(),190);filters.Controls.Add(roots);
        kinds=Combo(new[]{"Tất cả loại","Tài liệu","Hình ảnh","Video / Âm thanh","Ứng dụng","File nén","Mã nguồn","Kỹ thuật","Khác"},145);filters.Controls.Add(kinds);
        dates=Combo(new[]{"Mọi thời gian","Hôm nay","7 ngày qua","30 ngày qua"},135);filters.Controls.Add(dates);main.Controls.Add(filters,0,2);
        var infobar=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};
        hideTech=new CheckBox{Text="Ẩn file kỹ thuật",Checked=true,AutoSize=true,Margin=new Padding(0,3,15,0)};hideTech.CheckedChanged+=(o,e)=>ResetQuery();infobar.Controls.Add(hideTech);
        sort=Combo(new[]{"Ngày tạo mới nhất","Ngày sửa mới nhất","Dung lượng lớn nhất"},190);infobar.Controls.Add(sort);
        totals=new Label{Text="Đang lập chỉ mục…",AutoSize=true,ForeColor=muted,Margin=new Padding(5,4,0,0)};infobar.Controls.Add(totals);main.Controls.Add(infobar,0,3);
        list=new SmoothList{Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,HideSelection=false,MultiSelect=false,BorderStyle=BorderStyle.None,BackColor=Color.White,Font=new Font("Segoe UI",10),ShowItemToolTips=true};
        list.Columns.Add("Tên file",255);list.Columns.Add("Thời gian",145);list.Columns.Add("Loại / Hoạt động",150);list.Columns.Add("Dung lượng",100);list.Columns.Add("Đường dẫn",440);
        var im=new ImageList{ImageSize=new Size(1,29)};im.Images.Add(new Bitmap(1,29));list.SmallImageList=im;
        list.DoubleClick+=(o,e)=>OpenSelected(false);list.SelectedIndexChanged+=(o,e)=>{var r=Selected();detail.Text=r==null?"Chọn file để xem đường dẫn và thao tác.":r[5]+(r[6]!=""?"\nTrước đó: "+r[6]:"");};
        list.KeyDown+=(o,e)=>{if(e.KeyCode==Keys.Enter)OpenSelected(false);if(e.Control&&e.KeyCode==Keys.C)CopyPath();};
        var context=new ContextMenuStrip();context.Items.Add("Mở file",null,(o,e)=>OpenSelected(false));context.Items.Add("Mở thư mục chứa",null,(o,e)=>OpenSelected(true));context.Items.Add("Sao chép đường dẫn",null,(o,e)=>CopyPath());context.Items.Add("Đánh dấu / Bỏ đánh dấu",null,(o,e)=>Star());list.ContextMenuStrip=context;main.Controls.Add(list,0,4);
        detail=new Label{Dock=DockStyle.Fill,Text="Chọn file để xem đường dẫn và thao tác.",AutoEllipsis=true,ForeColor=muted,Padding=new Padding(0,8,0,0),Font=new Font("Segoe UI",9)};main.Controls.Add(detail,0,5);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};actions.Controls.Add(Btn("Mở file",(o,e)=>OpenSelected(false)));actions.Controls.Add(Btn("Mở thư mục",(o,e)=>OpenSelected(true)));actions.Controls.Add(Btn("Copy đường dẫn",(o,e)=>CopyPath()));actions.Controls.Add(Btn("Đánh dấu",(o,e)=>Star()));
        prev=Btn("‹",(o,e)=>{if(page>0){page--;dirty=true;queryVersion++;}});prev.MinimumSize=new Size(36,36);actions.Controls.Add(prev);
        paging=new Label{Text="Trang 1",AutoSize=true,Margin=new Padding(0,9,8,0)};actions.Controls.Add(paging);
        next=Btn("›",(o,e)=>{page++;dirty=true;queryVersion++;});next.MinimumSize=new Size(36,36);actions.Controls.Add(next);main.Controls.Add(actions,0,6);
        status=new Label{Dock=DockStyle.Fill,ForeColor=muted,AutoEllipsis=true,Text="Chuẩn bị theo dõi…",Padding=new Padding(0,7,0,0),Font=new Font("Segoe UI",9)};status.Click+=(o,e)=>{if(engine.Errors>0)MessageBox.Show(engine.LastError,"Chi tiết lần bỏ qua gần nhất");};main.Controls.Add(status,0,7);
    }
    void SetMode(string value) {
        mode=value;foreach(var b in navigation)b.BackColor=(string)b.Tag==mode?blue:Color.FromArgb(19,32,53);
        title.Text=navigation.First(b=>(string)b.Tag==mode).Text;
        subtitle.Text=mode=="events"?"Thay đổi được ghi nhận khi app chạy; kết quả quét lại có nhãn “Phát hiện”.":mode=="large"?"Các file từ 100 MB, ưu tiên dung lượng lớn nhất.":mode=="star"?"Những file bạn đánh dấu để mở lại nhanh.":"File có sẵn được sắp theo thời gian Windows ghi nhận.";
        sort.Enabled=mode!="events"&&mode!="large";ResetQuery();
    }
    void ResetQuery(){page=0;dirty=true;queryVersion++;}
    void TogglePause(){engine.SetPaused(!engine.Paused);pause.Text=engine.Paused?"Tiếp tục":"Tạm dừng";}
    void ShowApp(){Show();WindowState=FormWindowState.Normal;Activate();dirty=true;}
    void Tick() {
        status.Text=engine.Status+(engine.Errors>0?" · "+engine.Errors+" lần không đọc được (bấm xem)":"");
        long revision=Interlocked.Read(ref engine.Revision);
        if(revision!=lastRevision&&DateTime.UtcNow-lastRefresh>TimeSpan.FromSeconds(engine.Scanning?5:2)){dirty=true;lastRevision=revision;}
        if(dirty&&!querying&&DateTime.UtcNow-lastSearch>TimeSpan.FromMilliseconds(350)) RefreshRows();
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
        if(kinds.SelectedIndex>0){sql+=" AND category=?";args.Add(kinds.SelectedItem.ToString());}
        if(hideTech.Checked&&kinds.SelectedItem.ToString()!="Kỹ thuật")sql+=" AND category<>'Kỹ thuật'";
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
            var result=await Task.Run(()=>engine.Database.Query(spec.Sql,spec.Args.ToArray()));
            if(IsDisposed||quitting)return;if(version!=queryVersion){dirty=true;return;}
            next.Enabled=result.Count>200;prev.Enabled=page>0;rows=result.Take(200).ToList();
            list.BeginUpdate();list.Items.Clear();list.Groups.Clear();var groups=new Dictionary<string,ListViewGroup>();
            foreach(var r in rows) {
                DateTime t=new DateTime(Convert.ToInt64(r[2]),DateTimeKind.Utc).ToLocalTime();
                string group=mode=="large"||(!events&&sort.SelectedIndex==2)?"THEO DUNG LƯỢNG":t.Date==DateTime.Today?"HÔM NAY":t.Date==DateTime.Today.AddDays(-1)?"HÔM QUA":t.ToString("dd / MM / yyyy");
                if(!groups.ContainsKey(group)){var g=new ListViewGroup(group,HorizontalAlignment.Left);groups[group]=g;list.Groups.Add(g);}
                var item=new ListViewItem((r[7]=="1"?"★  ":"")+(events?Path.GetFileName(r[1]):r[1]),groups[group]);
                item.SubItems.Add(t.ToString("dd/MM/yyyy HH:mm:ss"));item.SubItems.Add(r[3]);item.SubItems.Add(SizeText(Convert.ToInt64(r[4])));item.SubItems.Add(r[5]);item.Tag=r;item.ToolTipText=r[5];
                if(events&&(r[3]=="Xóa"||r[3]=="Phát hiện mất"))item.ForeColor=Color.FromArgb(176,56,56);
                list.Items.Add(item);if(r[0]==selected)item.Selected=true;
            }
            list.EndUpdate();totals.Text=rows.Count==0?"Không có kết quả · thử đổi bộ lọc":rows.Count+" mục / trang · "+(events?"lịch sử ghi nhận":"file hiện có");paging.Text="Trang "+(page+1);lastRefresh=DateTime.UtcNow;
            if(rows.Count==0)detail.Text=engine.Scanning?"App đang quét lần đầu. Kết quả sẽ tự xuất hiện.":"Chưa có mục phù hợp. Thử bỏ bộ lọc hoặc chọn Đồng bộ lại.";
        } catch(Exception e) {if(!quitting)status.Text="Không tải được danh sách: "+e.Message;}
        finally{querying=false;}
    }
    public static string SizeText(long n){if(n>=1073741824)return(n/1073741824.0).ToString("0.##")+" GB";if(n>=1048576)return(n/1048576.0).ToString("0.##")+" MB";if(n>=1024)return(n/1024.0).ToString("0.#")+" KB";return n+" B";}
    string[] Selected(){return list.SelectedItems.Count>0?(string[])list.SelectedItems[0].Tag:null;}
    void OpenSelected(bool folder) {
        var r=Selected();if(r==null)return;string p=r[5];
        try {
            if(folder) {
                if(File.Exists(p)) Process.Start("explorer.exe","/select,\""+p+"\"");
                else {string dir=Path.GetDirectoryName(p);if(Directory.Exists(dir))Process.Start(new ProcessStartInfo(dir){UseShellExecute=true});else MessageBox.Show("Thư mục không còn tồn tại hoặc ổ đĩa chưa kết nối.");}
            } else {
                if(!File.Exists(p)){MessageBox.Show("File không còn ở đường dẫn này. Xem Lịch sử thay đổi hoặc đồng bộ lại.");return;}
                string ext=Path.GetExtension(p).ToLowerInvariant();
                if(new[]{".exe",".msi",".msix",".bat",".cmd",".ps1",".vbs",".js",".lnk",".com",".scr",".hta",".url"}.Contains(ext)&&MessageBox.Show("File này có thể chạy chương trình hoặc lệnh:\n\n"+p+"\n\nBạn muốn mở?","Mở file",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
                Process.Start(new ProcessStartInfo(p){UseShellExecute=true});
            }
        }catch(Exception e){MessageBox.Show(e.Message,"Không mở được file");}
    }
    void CopyPath(){var r=Selected();if(r==null)return;try{Clipboard.SetText(r[5]);status.Text="Đã sao chép đường dẫn.";}catch(Exception e){MessageBox.Show(e.Message);}}
    void Star(){var r=Selected();if(r==null)return;engine.Database.Exec("UPDATE files SET star=CASE WHEN star=1 THEN 0 ELSE 1 END WHERE path=?",r[5]);dirty=true;}
    static string Csv(string s){if(s.Length>0&&"=+-@\t\r\n".IndexOf(s[0])>=0)s="'"+s;return "\""+s.Replace("\"","\"\"")+"\"";}
    async void Export() {
        QuerySpec spec=BuildQuery(true);
        using(var dialog=new SaveFileDialog{Filter="CSV (*.csv)|*.csv",FileName="FileTracker-"+DateTime.Now.ToString("yyyy-MM-dd")+".csv"}) {
            if(dialog.ShowDialog()!=DialogResult.OK)return;
            try {
                var data=await Task.Run(()=>engine.Database.Query(spec.Sql,spec.Args.ToArray()));
                using(var w=new StreamWriter(dialog.FileName,false,new UTF8Encoding(true))){w.WriteLine("Tên file,Thời gian,Loại hoặc hoạt động,Dung lượng (byte),Đường dẫn,Đường dẫn cũ");foreach(var r in data.Take(50000))w.WriteLine(String.Join(",",new[]{Path.GetFileName(r[5]),new DateTime(Convert.ToInt64(r[2]),DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),r[3],r[4],r[5],r[6]}.Select(Csv)));}
                MessageBox.Show("Đã xuất "+Math.Min(50000,data.Count)+" mục theo bộ lọc hiện tại."+(data.Count>50000?"\nĐã chạm giới hạn 50.000 mục; hãy lọc theo thư mục/ngày để xuất tiếp.":""),"Xuất CSV");
            }catch(Exception e){MessageBox.Show(e.Message,"Không xuất được CSV");}
        }
    }
    void SettingsDialog() {
        using(var f=new Form{Text="Thư mục & Cài đặt",Size=new Size(690,510),StartPosition=FormStartPosition.CenterParent,Font=Font,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}) {
            var label=new Label{Text="Các thư mục được theo dõi (bao gồm thư mục con)",Location=new Point(20,18),AutoSize=true};f.Controls.Add(label);
            var box=new ListBox{Location=new Point(20,48),Size=new Size(630,155)};box.Items.AddRange(config.Roots.ToArray());f.Controls.Add(box);
            var add=Btn("Thêm thư mục",(o,e)=>{using(var d=new FolderBrowserDialog()){if(d.ShowDialog()==DialogResult.OK){string p=Settings.Normalize(d.SelectedPath);if(!box.Items.Cast<string>().Any(x=>p.StartsWith(x,StringComparison.OrdinalIgnoreCase)||x.StartsWith(p,StringComparison.OrdinalIgnoreCase)))box.Items.Add(p);else MessageBox.Show("Thư mục này đã được theo dõi hoặc trùng phạm vi với một thư mục đang có.");}}});add.Location=new Point(20,215);f.Controls.Add(add);
            var remove=Btn("Bỏ theo dõi mục chọn",(o,e)=>{if(box.SelectedIndex>=0)box.Items.RemoveAt(box.SelectedIndex);});remove.Location=new Point(180,215);f.Controls.Add(remove);
            var skip=new CheckBox{Text="Bỏ qua .git, node_modules, __pycache__, .venv và venv",Location=new Point(20,270),Width=630,Checked=config.SkipDevFolders};f.Controls.Add(skip);
            var close=new CheckBox{Text="Đóng cửa sổ → tiếp tục theo dõi dưới khay hệ thống",Location=new Point(20,303),Width=630,Checked=config.CloseToTray};f.Controls.Add(close);
            var startup=new CheckBox{Text="Khởi động cùng Windows (ẩn dưới khay hệ thống)",Location=new Point(20,336),Width=630};
            using(var k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))startup.Checked=k!=null&&k.GetValue("FileTracker")!=null;
            f.Controls.Add(startup);f.Controls.Add(new Label{Text="Thay đổi phạm vi áp dụng sau khi mở lại app. File gốc không bị thay đổi.",Location=new Point(20,373),Width=630,Height=30,ForeColor=muted});
            var save=Btn("Lưu cài đặt",(o,e)=>{
                try {
                    var updated=new Settings{Roots=box.Items.Cast<string>().ToList(),SkipDevFolders=skip.Checked,CloseToTray=close.Checked};updated.Save(dataDir);
                    using(var k=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) {
                        if(startup.Checked) k.SetValue("FileTracker","\""+Application.ExecutablePath+"\" --tray --data-dir \""+dataDir.TrimEnd('\\')+"\"");else k.DeleteValue("FileTracker",false);
                    }
                    config.CloseToTray=close.Checked;MessageBox.Show("Đã lưu. Chọn Thoát ở khay hệ thống rồi mở lại app để áp dụng phạm vi mới.","Cài đặt");f.Close();
                }catch(Exception ex){MessageBox.Show(ex.Message,"Không lưu được cài đặt");}
            },true);save.Location=new Point(20,415);f.Controls.Add(save);f.ShowDialog(this);
        }
    }
    void Help(){MessageBox.Show("FILE TIMELINE\n\n• File gần đây: thời gian tạo/sửa do Windows ghi nhận; không khẳng định đây là lúc tải file.\n• Lịch sử thay đổi: Thêm, Sửa, Xóa, Đổi tên khi app chạy. Nhãn 'Phát hiện' là kết quả so sánh khi quét lại, không biết chính xác lúc thao tác xảy ra.\n• Không khôi phục nội dung hoặc phiên bản file cũ.\n• Đổi tên thư mục được đối soát thành các file mất/thêm; chuyển giữa hai thư mục theo dõi có thể hiện là Xóa và Thêm.\n• Ẩn file kỹ thuật chỉ lọc giao diện. Bỏ qua thư mục lập trình là cài đặt phạm vi quét.\n• Bỏ qua junction/symlink, thùng rác và thư mục hệ thống không đọc được. Không cần quyền Administrator.\n• Giữ tối đa 100.000 sự kiện gần nhất; mỗi trang 200 mục.\n• App không ghi nhận lúc đã thoát/tạm dừng. Quét lại không thể tìm file đã tạo rồi xóa trong thời gian đó.\n• Database chỉ chứa tên, đường dẫn và thông tin file, ở:\n"+dataDir+"\n\nNhấp đôi file để mở. Ctrl+F: tìm kiếm; Ctrl+C trên dòng chọn: copy đường dẫn; F5: đồng bộ.","Hướng dẫn",MessageBoxButtons.OK,MessageBoxIcon.Information);}
}
}
