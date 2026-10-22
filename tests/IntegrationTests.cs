using System;
using System.IO;
using System.Threading;
using FileTracker;
class Tests {
 static void Assert(bool ok,string msg){if(!ok)throw new Exception("FAIL: "+msg);Console.WriteLine("PASS: "+msg);}
 static bool Wait(Func<bool> test,int ms=12000){var until=DateTime.UtcNow.AddMilliseconds(ms);while(DateTime.UtcNow<until){if(test())return true;Thread.Sleep(100);}return false;}
 static int Main(string[] args){try{
  string home=Path.GetFullPath(args[0]);Directory.CreateDirectory(home);string root=Settings.Normalize(Path.Combine(home,"watched"));Directory.CreateDirectory(root);
  string data=Path.Combine(home,"data");Directory.CreateDirectory(data);string original=Path.Combine(root,"existing.txt");File.WriteAllText(original,"baseline");
  var s=new Settings();s.Roots.Add(root);s.CloseToTray=false;
  using(var e=new MonitorEngine(data,s)) {
   e.Start();Assert(Wait(()=>e.Database.Count("SELECT COUNT(*) FROM roots WHERE baseline=1")==1),"initial scan completes");
   Assert(e.Database.Count("SELECT COUNT(*) FROM events")==0,"existing files do not become historical events");
   Assert(e.Database.Count("SELECT COUNT(*) FROM files WHERE present=1")==1,"baseline indexed");
   string added=Path.Combine(root,"Tiếng Việt 100%_ ' report.txt");File.WriteAllText(added,"one");
   Assert(Wait(()=>e.Database.Count("SELECT COUNT(*) FROM events WHERE kind='Thêm' AND path=?",added)>0),"live create and Unicode path");
   Thread.Sleep(2100);File.AppendAllText(added," two");
   Assert(Wait(()=>e.Database.Count("SELECT COUNT(*) FROM events WHERE kind='Sửa' AND path=?",added)>0),"live modify");
   e.Database.Exec("UPDATE files SET star=1 WHERE path=?",added);
   string renamed=Path.Combine(root,"renamed.txt");File.Move(added,renamed);
   Assert(Wait(()=>e.Database.Count("SELECT COUNT(*) FROM events WHERE kind='Đổi tên / Di chuyển' AND oldpath=?",added)>0),"rename preserves old path");
   Assert(e.Database.Count("SELECT star FROM files WHERE path=?",renamed)==1,"bookmark follows rename");
   File.Delete(renamed);Assert(Wait(()=>e.Database.Count("SELECT COUNT(*) FROM files WHERE path=? AND present=0",renamed)==1),"deleted file remains in history, not current list");
   string ignored=Path.Combine(root,"node_modules");Directory.CreateDirectory(ignored);File.WriteAllText(Path.Combine(ignored,"ignored.txt"),"no");e.RequestScan();Thread.Sleep(600);
   Assert(e.Database.Count("SELECT COUNT(*) FROM files WHERE name='ignored.txt'")==0,"excluded developer folders");
   e.SetPaused(true);File.WriteAllText(Path.Combine(root,"paused.txt"),"pause");Thread.Sleep(600);e.SetPaused(false);
   Assert(Wait(()=>e.Database.Count("SELECT COUNT(*) FROM events WHERE kind='Phát hiện thêm' AND path LIKE '%paused.txt'")>0),"resume labels offline changes as detected");
  }
  File.Delete(original);
  using(var e=new MonitorEngine(data,s)){
   e.ScanRoot(root);Assert(e.Database.Count("SELECT COUNT(*) FROM events WHERE kind='Phát hiện mất' AND path=?",original)==1,"restart reconciliation finds missing file");
   long before=e.Database.Count("SELECT COUNT(*) FROM files WHERE present=1");string moved=root.TrimEnd('\\')+"-offline";Directory.Move(root,moved);e.ScanRoot(root);
   Assert(e.Database.Count("SELECT COUNT(*) FROM files WHERE present=1")==before,"offline root does not mark files deleted");Directory.Move(moved,root);
   Assert(e.Database.Query("PRAGMA integrity_check")[0][0]=="ok","SQLite integrity");
  }
  s.Save(data);Assert(Settings.Load(data).Roots[0]==root,"settings persist");
  Console.WriteLine("ALL TESTS PASSED");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
