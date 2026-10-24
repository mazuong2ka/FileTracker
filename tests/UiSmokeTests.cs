using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Collections.Generic;
using System.Windows.Forms;

namespace FileTracker {
// Compile with /main:FileTracker.UiSmokeTests alongside the app sources.
// Uses only its own sample files and database. Screenshots go beside the test EXE.
static class UiSmokeTests {
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool AreDpiAwarenessContextsEqual(IntPtr left,IntPtr right);
    static MainForm form;static Settings config;static string output;static int stage,ticks;static Timer timer;static bool headless;
    static T Field<T>(string name){return (T)typeof(MainForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);}
    static void Call(string name,params object[] args){typeof(MainForm).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,args);}
    static void Check(bool value,string name){if(!value)throw new Exception(name);File.AppendAllText(Path.Combine(output,"checks.txt"),"PASS "+name+Environment.NewLine);}
    static void Capture(string name){form.PerformLayout();form.Update();using(var b=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(b,new Rectangle(Point.Empty,form.Size));b.Save(Path.Combine(output,name+".png"));}}
    [STAThread] static void Main(string[] args){
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        output=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"run-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"latest-run.txt"),output);
        string data=Path.Combine(output,"Data"),samples=Path.Combine(output,"Samples");Directory.CreateDirectory(data);Directory.CreateDirectory(samples);
        for(int i=1;i<=211;i++){string p=Path.Combine(samples,i==211?"Báo cáo môn học.md":"Study note "+i.ToString("000")+".txt");File.WriteAllText(p,"UI test sample");}
        config=new Settings{Roots=new List<string>{Settings.Normalize(samples)},Language="vi",CloseToTray=false};config.Save(data);
        form=new MainForm(data,config,false);form.Text="FileTracker · UI checks";
        headless=args.Contains("--headless");if(headless){form.ShowInTaskbar=false;form.Opacity=0;Field<NotifyIcon>("tray").Visible=false;}
        Application.ThreadException+=(o,e)=>Fail(e.Exception);
        timer=new Timer{Interval=1200};timer.Tick+=(o,e)=>Step();timer.Start();Application.Run(form);
    }
    static void Fail(Exception error){File.AppendAllText(Path.Combine(output,"checks.txt"),"FAIL "+error+Environment.NewLine);timer.Stop();form.Close();}
    static void Step(){
        try {
            if(++ticks>40)throw new Exception("UI checks timed out");
            if(Field<bool>("querying"))return;
            var list=Field<SmoothList>("list");
            switch(stage){
            case 0:
                if(list.Items.Count!=200)return;
                if(Environment.OSVersion.Version>=new Version(10,0,15063))Check(AreDpiAwarenessContextsEqual(GetWindowDpiAwarenessContext(form.Handle),new IntPtr(-4)),"Window runs with PerMonitorV2 DPI awareness");
                Check(Field<Button>("next").Enabled,"211 indexed files enable pagination");
                Check(Field<List<Button>>("fileActions").All(b=>!b.Enabled),"No selection disables file actions");
                list.Items[0].Selected=true;Call("UpdateSelection");
                Check(Field<List<Button>>("fileActions").All(b=>b.Enabled),"Selection enables file actions");
                CheckLayout(form);Capture("vi-default");Call("Star");stage++;break;
            case 1:Field<Button>("next").PerformClick();stage++;break;
            case 2:
                Check(list.Items.Count==11&&Field<Label>("paging").Text=="Trang 2","Next page shows remaining 11 files");
                Field<TextBox>("search").Text="Báo cáo";stage++;break;
            case 3:
                Check(list.Items.Count==1&&list.Items[0].Text.Contains("Báo cáo"),"Unicode search filters and returns to page 1");
                Check(Field<Label>("paging").Text=="Trang 1","Search resets pagination");
                Field<TextBox>("search").Text="no-such-file-xyz";stage++;break;
            case 4:
                Check(list.Items.Count==0&&Field<Panel>("emptyState").Visible,"Empty state appears for unmatched search");
                Check(Field<List<Button>>("fileActions").All(b=>!b.Enabled),"Empty result clears stale file actions");
                form.Size=form.MinimumSize;CheckLayout(form);Capture("vi-empty-minimum");Call("ClearFilters");stage++;break;
            case 5:
                Check(list.Items.Count==200,"Reset filters restores the file list");Call("SetMode","star");stage++;break;
            case 6:
                Check(list.Items.Count==1,"Starred view contains the starred sample");
                list.Items[0].Selected=true;Call("UpdateSelection");Check(Field<Button>("starAction").Text=="Bỏ đánh dấu","Selected star offers Unstar");
                Call("SetMode","events");stage++;break;
            case 7:
                Check(!Field<ComboBox>("sort").Enabled,"Activity view disables unsupported sorting");
                Call("TogglePause");config.Language="en";Call("ApplyLanguage");stage++;break;
            case 8:
                Check(Field<Button>("pause").Text=="Resume","Language rebuild preserves paused state");
                Check(Field<Button>("pause").AccessibleName=="Resume","Pause accessible name follows its current action");
                Check(Field<Label>("title").Text=="Activity history","Language rebuild preserves active view");
                Call("TogglePause");Call("SetMode","files");stage++;break;
            case 9:
                timer.Stop(); // Modal dialogs pump messages; prevent the test stage from reentering.
                Check(list.Items.Count==200,"Language rebuild restores results");
                form.Size=form.MinimumSize;CheckLayout(form);Capture("en-minimum");form.Size=new Size(1360,880);Capture("en-default");
                CheckLayout(form);Check(true,"Visible controls remain within their layout containers");
                using(var dialog=form.CreateSettingsDialog(()=>{})){
                    dialog.Opacity=0;dialog.ShowInTaskbar=false;dialog.Show();CreateHandles(dialog);dialog.PerformLayout();
                    Check(((ToggleSwitch)dialog.Controls.Find("close-to-tray",true)[0]).Checked==config.CloseToTray,"Settings preserve close-to-tray preference");
                    Check(((ToggleSwitch)dialog.Controls.Find("skip-dev",true)[0]).Checked==config.SkipDevFolders,"Settings preserve folder exclusions");
                    Check(((ComboBox)dialog.Controls.Find("settings-language",true)[0]).SelectedIndex==1,"Settings preserve selected language");
                    foreach(string name in new[]{"startup","close-to-tray","skip-dev","settings-language"}){var editor=dialog.Controls.Find(name,true)[0];Check(editor.Width>=80&&editor.Height>=20&&editor.Top>=0&&editor.Bottom<=editor.Parent.ClientSize.Height&&editor.Left>=0&&editor.Right<=editor.Parent.ClientSize.Width,"Settings editor fits: "+name);}
                    CheckLayout(dialog,true);using(var b=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(b,new Rectangle(Point.Empty,dialog.Size));b.Save(Path.Combine(output,"en-settings.png"));}
                }
                File.AppendAllText(Path.Combine(output,"checks.txt"),"COMPLETE\n");timer.Stop();form.Close();break;
            }
        }catch(Exception e){Fail(e);}
    }
    static void CreateHandles(Control control){var handle=control.Handle;foreach(Control c in control.Controls)CreateHandles(c);}
    static void CheckLayout(Control parent,bool includeHidden=false){
        foreach(Control c in parent.Controls){
            if((c.Visible||includeHidden)&&c is Button&&c.Parent is FlowLayoutPanel)Check(c.Right<=parent.ClientSize.Width+1&&c.Bottom<=parent.ClientSize.Height+1,"Button fits: "+c.Text);
            var rounded=c as RoundedButton;if((c.Visible||includeHidden)&&rounded!=null&&rounded.Text.Length>1){int available=c.Width-c.Padding.Horizontal-(rounded.Glyph.Length>0?28:0);int text=TextRenderer.MeasureText(c.Text,c.Font,Size.Empty,TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding).Width;Check(text<=available,"Button text fits: "+c.Text);}
            if(c.Visible||includeHidden)CheckLayout(c,includeHidden);
        }
    }
}
}
