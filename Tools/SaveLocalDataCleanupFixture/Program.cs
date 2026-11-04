using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
class Program {
 static int checks, skipped;
 static void Check(bool pass,string why){checks++;if(!pass)throw new Exception(why);}
 static string player=new string('a',64);
 static string Dir(string user,string kind,string key)=>Path.Combine(user,"RebirthData",kind,key);
 static void Seed(string folder){Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,player+".xml"),"primary");File.WriteAllText(Path.Combine(folder,player+".xml.bak"),"backup");File.WriteAllText(Path.Combine(folder,player+".xml.guides.xml.tmp"),"guide");}
 static void Refused(Action action,string reason){bool failed=false;try{action();}catch(InvalidDataException){failed=true;}Check(failed,reason);}
 static void Main(){try{Run();}catch(Exception e){Console.Error.WriteLine(e);Environment.ExitCode=1;}}
 static void Run(){
  string root=Path.Combine(Path.GetTempPath(),"REBIRTH-SaveCleanupCases-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try{
   string user=Path.Combine(root,"User Data"),save=Path.Combine(root,"Saves","Test Save");Directory.CreateDirectory(save);
   Guid guid=Guid.Parse("34567890-1234-4321-abcd-123456789012");
   Check(RebirthSaveLocalDataCleanup.Capture(user,save,guid,false)==null,"Absent-before confirmation cannot delete");
   Check(RebirthSaveLocalDataCleanup.Capture(user,save,Guid.Empty,true)==null,"Unknown world identity cannot delete");
   var plan=RebirthSaveLocalDataCleanup.Capture(user,save,guid,true);
   foreach(var key in plan.WorldKeys)foreach(var kind in new[]{"HudTracking","Journal"})Seed(Dir(user,kind,key));
   string otherGuid=RebirthSaveLocalDataCleanup.WorldKey(save,Guid.NewGuid()), otherSave=RebirthSaveLocalDataCleanup.WorldKey(save+" Other",guid);
   Seed(Dir(user,"HudTracking",otherGuid));Seed(Dir(user,"Journal",otherSave));
   var retired=new List<string>();
   Check(!RebirthSaveLocalDataCleanup.DeleteAfterNativeConfirmation(plan,true,retired.Add,out int count)&&count==0&&retired.Count==0,"Native failure/cancel must retain preferences");
   Check(!RebirthSaveLocalDataCleanup.DeleteAfterNativeConfirmation(plan,false,retired.Add,out count),"Recreated or undeleted native directory must retain preferences");
   Directory.Delete(save);
   Check(RebirthSaveLocalDataCleanup.DeleteAfterNativeConfirmation(plan,false,key=>{
    Check(Directory.Exists(Dir(user,"HudTracking",key)),"Retire queued writes before removing files");retired.Add(key);
   },out count),"Confirmed absent native save cleanup succeeds");
   Check(count==12,"All current/legacy HUD and Journal primary/backup/temp files removed");
   Check(retired.SequenceEqual(plan.WorldKeys),"Only exact current GUID and legacy path contexts retired");
   foreach(var key in plan.WorldKeys)foreach(var kind in new[]{"HudTracking","Journal"})Check(!Directory.Exists(Dir(user,kind,key)),"Owned scope directory removed");
   Check(Directory.Exists(Dir(user,"HudTracking",otherGuid))&&Directory.Exists(Dir(user,"Journal",otherSave)),"Other world and same-prefix save preserved");
   Check(RebirthSaveLocalDataCleanup.DeleteAfterNativeConfirmation(plan,false,_=>{},out count)&&count==0,"Cleanup is idempotent");
   string own=Dir(user,"HudTracking",plan.WorldKeys[0]);Seed(own);File.WriteAllText(Path.Combine(own,"foreign.txt"),"protected");
   Refused(()=>RebirthSaveLocalDataCleanup.DeleteAfterNativeConfirmation(plan,false,_=>{},out _),"Unexpected file must be retained, not recursively removed");
   Check(File.Exists(Path.Combine(own,player+".xml")),"File preflight occurs before mutation");File.Delete(Path.Combine(own,"foreign.txt"));
   Directory.CreateDirectory(Path.Combine(own,"nested"));
   Refused(()=>RebirthSaveLocalDataCleanup.DeleteAfterNativeConfirmation(plan,false,_=>{},out _),"Unexpected subdirectory must be retained");Directory.Delete(Path.Combine(own,"nested"));
   string outside=Path.Combine(root,"outside");Directory.CreateDirectory(outside);string victim=Path.Combine(outside,"untouched.txt");File.WriteAllText(victim,"keep");
   string link=Path.Combine(own,player+".xml.tmp");bool made=false;
   try{File.CreateSymbolicLink(link,victim);made=true;}catch(UnauthorizedAccessException){skipped++;Console.WriteLine("SKIP symlink creation requires host privilege");}catch(IOException ex) when((ex.HResult & 0xffff)==1314){skipped++;Console.WriteLine("SKIP file symlink: Windows privilege1314 unavailable");}catch(PlatformNotSupportedException){skipped++;}
   if(made){Refused(()=>RebirthSaveLocalDataCleanup.DeleteAfterNativeConfirmation(plan,false,_=>{},out _),"Reparse file refused");Check(File.ReadAllText(victim)=="keep","Reparse target untouched");File.Delete(link);}
   RebirthSaveLocalDataCleanup.DeleteAfterNativeConfirmation(plan,false,_=>{},out _);
   string junctionUser=Path.Combine(root,"JunctionUser");
   var junctionPlan=RebirthSaveLocalDataCleanup.Capture(junctionUser,save,guid,true);
   string junction=Dir(junctionUser,"HudTracking",junctionPlan.WorldKeys[0]);
   Directory.CreateDirectory(Path.GetDirectoryName(junction));
   var psi=new System.Diagnostics.ProcessStartInfo("powershell.exe"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
   psi.ArgumentList.Add("-NoProfile");psi.ArgumentList.Add("-Command");
   psi.ArgumentList.Add("New-Item -ItemType Junction -Path '"+junction.Replace("'","''")+"' -Target '"+outside.Replace("'","''")+"' | Out-Null");
   using(var child=System.Diagnostics.Process.Start(psi)){
    if(!child.WaitForExit(10000)){child.Kill();throw new Exception("Owned junction fixture timed out");}
    Check(child.ExitCode==0,"Host junction fixture creation");
   }
   Refused(()=>RebirthSaveLocalDataCleanup.DeleteAfterNativeConfirmation(junctionPlan,false,_=>{},out _),"Scope directory junction refused");
   Check(File.ReadAllText(victim)=="keep","Junction target untouched");Directory.Delete(junction,false);
   var malformed=RebirthSaveLocalDataCleanup.Capture(user,save,guid,true);malformed.WorldKeys=new[]{"../outside"};
   Refused(()=>RebirthSaveLocalDataCleanup.DeleteAfterNativeConfirmation(malformed,false,_=>{},out _),"Non-hash scope refused");
   Check(File.ReadAllText(victim)=="keep","Malformed scope cannot escape owned directories");
   Check(RebirthSaveLocalDataCleanup.WorldKey(save,guid)==RebirthSaveLocalDataCleanup.WorldKey(save+Path.DirectorySeparatorChar,guid),"Historical trailing separator normalization retained");
   Console.WriteLine($"PASS {checks} checks; skipped={skipped}");
  }finally{Directory.Delete(root,true);}
 }
}
