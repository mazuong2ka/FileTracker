using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace FileTracker {
public class Settings {
    public List<string> Roots = new List<string>();
    public bool SkipDevFolders = true;
    public bool CloseToTray = true;
    public static string Normalize(string p) { return Path.GetFullPath(Environment.ExpandEnvironmentVariables(p)).TrimEnd('\\') + "\\"; }
    public static Settings Defaults() {
        var s = new Settings();
        s.Roots.Add(Environment.GetEnvironmentVariable("ProgramW6432") ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        s.Roots.Add(Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)");
        string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        using(var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders")) {
            if(k != null) downloads = Convert.ToString(k.GetValue("{374DE290-123F-4565-9164-39C4925E467B}", downloads));
        }
        s.Roots.Add(downloads); s.Roots.Add(@"D:\");
        s.Roots = s.Roots.Where(p=>!String.IsNullOrWhiteSpace(p)).Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return s;
    }
    public static Settings Load(string dir) {
        string p = Path.Combine(dir,"settings.json");
        if(!File.Exists(p)) return Defaults();
        var s = new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(p));
        if(s == null || s.Roots == null) throw new InvalidDataException("settings.json không hợp lệ.");
        s.Roots = s.Roots.Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase).ToList(); return s;
    }
    public void Save(string dir) {
        string p = Path.Combine(dir,"settings.json"), tmp=p+".tmp";
        File.WriteAllText(tmp,new JavaScriptSerializer().Serialize(this),new UTF8Encoding(false));
        if(File.Exists(p)) File.Replace(tmp,p,null); else File.Move(tmp,p);
    }
}
public sealed class Db : IDisposable {
    internal readonly object Gate = new object(); IntPtr db;
    [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode)] static extern int sqlite3_open16(string file,out IntPtr db);
    [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_close(IntPtr db);
    [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode)] static extern int sqlite3_prepare16_v2(IntPtr db,string sql,int n,out IntPtr stmt,IntPtr tail);
    [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode)] static extern int sqlite3_bind_text16(IntPtr stmt,int index,string value,int n,IntPtr destructor);
    [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_bind_int64(IntPtr stmt,int index,long value);
    [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_step(IntPtr stmt);
    [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_finalize(IntPtr stmt);
    [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_column_count(IntPtr stmt);
    [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_column_text16(IntPtr stmt,int i);
    [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_errmsg16(IntPtr db);
    public Db(string path) {
        if(sqlite3_open16(path,out db)!=0) throw new IOException("Không mở được cơ sở dữ liệu.");
        Exec("PRAGMA journal_mode=WAL"); Exec("PRAGMA synchronous=NORMAL"); Exec("PRAGMA busy_timeout=5000");
        Exec("CREATE TABLE IF NOT EXISTS files(path TEXT PRIMARY KEY COLLATE NOCASE,root TEXT COLLATE NOCASE,name TEXT,category TEXT,size INTEGER,created INTEGER,modified INTEGER,seen INTEGER,present INTEGER,star INTEGER DEFAULT 0)");
        Exec("CREATE TABLE IF NOT EXISTS events(id INTEGER PRIMARY KEY,time INTEGER,kind TEXT,path TEXT,oldpath TEXT,root TEXT,category TEXT,size INTEGER)");
        Exec("CREATE TABLE IF NOT EXISTS roots(path TEXT PRIMARY KEY COLLATE NOCASE,baseline INTEGER)");
        Exec("CREATE INDEX IF NOT EXISTS files_created ON files(present,created DESC)");
        Exec("CREATE INDEX IF NOT EXISTS files_root ON files(root)");
        Exec("CREATE INDEX IF NOT EXISTS files_modified ON files(present,modified DESC)");
        Exec("CREATE INDEX IF NOT EXISTS events_time ON events(time DESC)");
    }
    public List<string[]> Query(string sql,params object[] args) {
        lock(Gate) {
            IntPtr stmt; if(sqlite3_prepare16_v2(db,sql,-1,out stmt,IntPtr.Zero)!=0) throw new IOException(Marshal.PtrToStringUni(sqlite3_errmsg16(db)));
            try {
                for(int i=0;i<args.Length;i++) {
                    int rc = args[i] is string ? sqlite3_bind_text16(stmt,i+1,(string)args[i],-1,new IntPtr(-1)) : sqlite3_bind_int64(stmt,i+1,Convert.ToInt64(args[i]));
                    if(rc!=0) throw new IOException("SQL bind error "+rc);
                }
                var result=new List<string[]>(); int status;
                while((status=sqlite3_step(stmt))==100) {
                    var row=new string[sqlite3_column_count(stmt)];
                    for(int i=0;i<row.Length;i++) row[i]=Marshal.PtrToStringUni(sqlite3_column_text16(stmt,i)) ?? "";
                    result.Add(row);
                }
                if(status!=101) throw new IOException(Marshal.PtrToStringUni(sqlite3_errmsg16(db)));
                return result;
            } finally { sqlite3_finalize(stmt); }
        }
    }
    public void Exec(string sql,params object[] args) { Query(sql,args); }
    public long Count(string sql,params object[] args) { var r=Query(sql,args); return r.Count==0?0:Convert.ToInt64(r[0][0]); }
    public void Dispose() { lock(Gate) { if(db!=IntPtr.Zero) {sqlite3_close(db);db=IntPtr.Zero;} } }
}
public class Change { public string Kind,Path,Old,Root; public long Time; }
public sealed class MonitorEngine : IDisposable {
    public readonly Db Database;
    public readonly Settings Config;
    public volatile bool Paused,Stopping,Scanning;
    public volatile string Status="Chuẩn bị…";
    public long Revision;
    public int Errors;
    public string LastError="";
    public int WatcherCount { get { lock(watchers) return watchers.Count; } }
    readonly string dataDir;
    readonly List<FileSystemWatcher> watchers=new List<FileSystemWatcher>();
    readonly ConcurrentQueue<Change> queue=new ConcurrentQueue<Change>();
    readonly AutoResetEvent wake=new AutoResetEvent(false);
    readonly HashSet<string> skipNames=new HashSet<string>(new[]{"$recycle.bin","system volume information"},StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> devNames=new HashSet<string>(new[]{".git","node_modules","__pycache__",".venv","venv"},StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,long> debounce=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
    Thread worker; int scanRequested=1;
    public static long Now { get { return DateTime.UtcNow.Ticks; } }
    public MonitorEngine(string dir,Settings s) { dataDir=Settings.Normalize(dir); Config=s; Database=new Db(Path.Combine(dir,"timeline.db")); }
    public void Start() { worker=new Thread(Run){IsBackground=true,Name="FileTracker monitor"};worker.Start(); }
    public void RequestScan() { Interlocked.Exchange(ref scanRequested,1);wake.Set(); }
    public void SetPaused(bool paused) { Paused=paused; if(!paused) RequestScan(); }
    public bool Excluded(string p) {
        if(p.StartsWith(dataDir,StringComparison.OrdinalIgnoreCase)) return true;
        foreach(string part in p.Split('\\')) if(skipNames.Contains(part)||(Config.SkipDevFolders&&devNames.Contains(part))) return true;
        return false;
    }
    public static string Category(string p) {
        string e=Path.GetExtension(p).ToLowerInvariant();
        if(new[]{".pdf",".doc",".docx",".txt",".rtf",".xlsx",".xls",".csv",".pptx",".ppt",".md",".tex"}.Contains(e)) return "Tài liệu";
        if(new[]{".png",".jpg",".jpeg",".gif",".webp",".svg",".bmp",".heic"}.Contains(e)) return "Hình ảnh";
        if(new[]{".mp4",".mkv",".mov",".mp3",".wav",".flac",".avi"}.Contains(e)) return "Video / Âm thanh";
        if(new[]{".exe",".msi",".msix",".lnk",".appx"}.Contains(e)) return "Ứng dụng";
        if(new[]{".zip",".rar",".7z",".tar",".gz",".iso"}.Contains(e)) return "File nén";
        if(new[]{".cs",".py",".js",".ts",".tsx",".jsx",".cpp",".c",".h",".java",".html",".css",".v",".sv"}.Contains(e)) return "Mã nguồn";
        if(new[]{".dll",".sys",".pdb",".log",".tmp",".cache",".dat",".pak",".bin",".obj",".pyc"}.Contains(e)) return "Kỹ thuật";
        return "Khác";
    }
    void Warn(string msg) { Interlocked.Increment(ref Errors);LastError=msg; }
    void Attach() {
        lock(watchers) {
            foreach(var w in watchers) w.Dispose(); watchers.Clear();
            foreach(string root in Config.Roots) {
                if(!Directory.Exists(root)) {Warn("Không truy cập được: "+root);continue;}
                try {
                    var w=new FileSystemWatcher(root){IncludeSubdirectories=true,InternalBufferSize=65536,NotifyFilter=NotifyFilters.FileName|NotifyFilters.DirectoryName|NotifyFilters.LastWrite|NotifyFilters.Size};
                    string r=root;
                    w.Created+=(o,e)=>Enqueue("Thêm",e.FullPath,"",r);
                    w.Changed+=(o,e)=>Enqueue("Sửa",e.FullPath,"",r);
                    w.Deleted+=(o,e)=>Enqueue("Xóa",e.FullPath,"",r);
                    w.Renamed+=(o,e)=>Enqueue("Đổi tên / Di chuyển",e.FullPath,e.OldFullPath,r);
                    w.Error+=(o,e)=>{Warn("Bộ theo dõi cần đồng bộ lại: "+r);RequestScan();};
                    w.EnableRaisingEvents=true;watchers.Add(w);
                } catch(Exception e) {Warn(root+": "+e.Message);}
            }
        }
    }
    void Enqueue(string kind,string path,string old,string root) {
        if(Stopping||Paused||(Excluded(path)&&(old==""||Excluded(old)))) return;
        if(queue.Count>20000) {RequestScan();return;}
        queue.Enqueue(new Change{Kind=kind,Path=path,Old=old,Root=root,Time=Now});wake.Set();
    }
    void AddEvent(string kind,string path,string old,string root,long size,long time) {
        string key=kind+"|"+path;long last;
        if(debounce.TryGetValue(key,out last)&&time-last<TimeSpan.TicksPerSecond*2) return;
        debounce[key]=time;
        if(debounce.Count>10000) debounce.Clear();
        Database.Exec("INSERT INTO events(time,kind,path,oldpath,root,category,size) VALUES(?,?,?,?,?,?,?)",time,kind,path,old,root,Category(path),size);
    }
    public void IndexFile(string path,string root,bool baseline,string eventKind,long time) {
        if(Excluded(path)) return;
        var f=new FileInfo(path); if(!f.Exists) return;
        long size=f.Length,created=f.CreationTimeUtc.Ticks,modified=f.LastWriteTimeUtc.Ticks;
        lock(Database.Gate) {
            var prior=Database.Query("SELECT size,modified,present,star FROM files WHERE path=?",path);
            bool exists=prior.Count>0&&prior[0][2]=="1";
            bool changed=!exists||prior[0][0]!=size.ToString()||prior[0][1]!=modified.ToString();
            long star=prior.Count>0?Convert.ToInt64(prior[0][3]):0;
            Database.Exec("INSERT OR REPLACE INTO files(path,root,name,category,size,created,modified,seen,present,star) VALUES(?,?,?,?,?,?,?,?,1,?)",path,root,f.Name,Category(path),size,created,modified,time,star);
            if(!baseline&&changed) AddEvent(eventKind??(exists?"Phát hiện sửa":"Phát hiện thêm"),path,"",root,size,time);
            if(changed) Interlocked.Increment(ref Revision);
        }
    }
    void RemovePath(string path,string root,string kind,long time) {
        lock(Database.Gate) {
            var rows=Database.Query("SELECT path,size FROM files WHERE present=1 AND (path=? OR substr(path,1,?)=? COLLATE NOCASE)",path,path.TrimEnd('\\').Length+1,path.TrimEnd('\\')+"\\");
            foreach(var r in rows) {Database.Exec("UPDATE files SET present=0 WHERE path=?",r[0]);AddEvent(kind,r[0],"",root,Convert.ToInt64(r[1]),time);}
            if(rows.Count>0) Interlocked.Increment(ref Revision);
        }
    }
    void Drain() {
        Change c; int n=0;
        while(n++<500&&queue.TryDequeue(out c)) {
            if(Paused||Stopping) continue;
            try {
                if(c.Kind=="Xóa") {RemovePath(c.Path,c.Root,c.Kind,c.Time);continue;}
                if(c.Old!="") {
                    // Directory moves are reconciled as detected additions/removals on the next scan.
                    if(Directory.Exists(c.Path)) {RequestScan();continue;}
                    lock(Database.Gate) {
                        var old=Database.Query("SELECT size,star FROM files WHERE path=?",c.Old);
                        Database.Exec("UPDATE files SET present=0 WHERE path=?",c.Old);
                        if(!Excluded(c.Path)) {
                            IndexFile(c.Path,c.Root,true,null,c.Time);
                            if(old.Count>0) Database.Exec("UPDATE files SET star=? WHERE path=?",Convert.ToInt64(old[0][1]),c.Path);
                            AddEvent(c.Kind,c.Path,c.Old,c.Root,old.Count>0?Convert.ToInt64(old[0][0]):0,c.Time);
                        } else if(old.Count>0) AddEvent("Ra khỏi phạm vi",c.Old,"",c.Root,Convert.ToInt64(old[0][0]),c.Time);
                        Interlocked.Increment(ref Revision);
                    }
                } else if(Directory.Exists(c.Path)) {if(c.Kind=="Thêm") RequestScan();}
                else IndexFile(c.Path,c.Root,false,c.Kind,c.Time);
            } catch(IOException e) {Warn(e.Message);RequestScan();} catch(UnauthorizedAccessException e) {Warn(e.Message);}
        }
    }
    public void ScanRoot(string root) {
        if(!Directory.Exists(root)) {Warn("Thư mục không sẵn sàng: "+root);return;}
        long started=Now;
        bool baseline=Database.Count("SELECT COUNT(*) FROM roots WHERE path=? AND baseline=1",root)==0;
        var stack=new Stack<string>();stack.Push(root);
        var protectedPrefixes=new List<string>();long count=0;
        while(stack.Count>0&&!Stopping&&!Paused) {
            string dir=stack.Pop();
            if(Excluded(dir)) {protectedPrefixes.Add(Settings.Normalize(dir));continue;}
            try {
                // Never follow junctions/symlinks into another scope or a loop.
                if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0) {protectedPrefixes.Add(Settings.Normalize(dir));continue;}
                foreach(string sub in Directory.GetDirectories(dir)) stack.Push(sub);
                string[] files=Directory.GetFiles(dir);
                for(int start=0;start<files.Length;start+=150) {
                    if(Stopping||Paused) break;
                    lock(Database.Gate) {
                        Database.Exec("BEGIN");
                        try {
                            foreach(string p in files.Skip(start).Take(150)) {
                                try {IndexFile(p,root,baseline,null,Now);count++;}
                                catch(IOException e) {protectedPrefixes.Add(p);Warn(e.Message);}
                                catch(UnauthorizedAccessException e) {protectedPrefixes.Add(p);Warn(e.Message);}
                            }
                            Database.Exec("COMMIT");
                        } catch {Database.Exec("ROLLBACK");throw;}
                    }
                    Status="Đang quét "+root+"  ·  "+count.ToString("N0")+" file"; Drain();Thread.Sleep(1);
                }
            } catch(IOException e) {protectedPrefixes.Add(Settings.Normalize(dir));Warn(e.Message);}
              catch(UnauthorizedAccessException e) {protectedPrefixes.Add(Settings.Normalize(dir));Warn(e.Message);}
        }
        if(Stopping||Paused) return;
        // A missing/offline drive or unreadable subtree is not evidence of deletion.
        if(!Directory.Exists(root)) {Warn("Mất kết nối thư mục trong lúc quét: "+root);return;}
        if(!baseline) {
            var missing=Database.Query("SELECT path,size FROM files WHERE root=? AND present=1 AND seen<?",root,started);
            foreach(var row in missing) {
                if(Stopping||Paused) return;
                string p=row[0];
                if(Excluded(p)||protectedPrefixes.Any(prefix=>p.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))) continue;
                try {File.GetAttributes(p);continue;}
                catch(FileNotFoundException) {} catch(DirectoryNotFoundException) {}
                catch(IOException) {continue;} catch(UnauthorizedAccessException) {continue;}
                RemovePath(p,root,"Phát hiện mất",Now);
            }
        }
        Database.Exec("INSERT OR REPLACE INTO roots(path,baseline) VALUES(?,1)",root);
        Interlocked.Increment(ref Revision);
    }
    void Run() {
        DateTime nextScan=DateTime.UtcNow;
        try {
            Attach();
            while(!Stopping) {
                if(Paused) {Status="Đã tạm dừng · Tiếp tục để đồng bộ lại";wake.WaitOne(500);continue;}
                if(Interlocked.Exchange(ref scanRequested,0)==1||DateTime.UtcNow>=nextScan) {
                    Scanning=true;Errors=0;Attach();
                    foreach(string root in Config.Roots) {if(Stopping||Paused) break;ScanRoot(root);}
                    Database.Exec("DELETE FROM events WHERE id NOT IN (SELECT id FROM events ORDER BY id DESC LIMIT 100000)");
                    nextScan=DateTime.UtcNow.AddMinutes(30);Scanning=false;
                }
                Drain();if(!Scanning) Status="Đang theo dõi "+WatcherCount+" / "+Config.Roots.Count+" thư mục · Đồng bộ mỗi 30 phút";
                wake.WaitOne(400);
            }
        } catch(Exception e) {Status="Theo dõi đã dừng: "+e.Message;Warn(e.ToString());}
    }
    public void Dispose() {Stopping=true;wake.Set();lock(watchers){foreach(var w in watchers) w.Dispose();watchers.Clear();}if(worker!=null) worker.Join();Database.Dispose();wake.Dispose();}
}
}
