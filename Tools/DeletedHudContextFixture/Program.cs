using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Reflection;
static class Program {
 static int checks;
 static void Check(bool pass,string why){checks++;if(!pass)throw new Exception(why);}
 static object Field(string name)=>typeof(RebirthHudTrackingPreferenceService).GetField(name,BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
 static RebirthHudTrackingPreferenceContext Enter(string root,string name){
  GameIO.Save=Path.Combine(root,"Saves",name);Directory.CreateDirectory(GameIO.Save);
  GameManager.Instance.World=new World{worldState=new WorldState{Guid=Guid.NewGuid().ToString()}};
  UnityEngine.Time.realtimeSinceStartup+=2;
  Check(RebirthHudTrackingPreferenceService.TryGetCurrent(out _,out _),"Production context load "+name);
  return (RebirthHudTrackingPreferenceContext)Field("context");
 }
 static void Mutate()=>Check(RebirthHudTrackingPreferenceService.Mutate(p=>p.Enabled=!p.Enabled,out _),"Production mutation admitted");
 static int Pending=>((IList)Field("pendingPreviousWrites")).Count;
 static void Main(){try{Run();}catch(Exception e){Console.Error.WriteLine(e);Environment.ExitCode=1;}}
 static void Run(){
  string root=Path.Combine(Path.GetTempPath(),"REBIRTH-DeletedHud-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);GameIO.User=Path.Combine(root,"User");
  try{
   var a=Enter(root,"A");
   Check(a.WorldKey==RebirthSaveLocalDataCleanup.WorldKey(GameIO.Save,Guid.Parse(GameManager.Instance.World.worldState.Guid)),"Cleanup derives exact production GUID storage key");
   Mutate();RebirthAtomicXmlFile.Fail=true;RebirthHudTrackingPreferenceService.ResetRuntime(true);
   Check(Pending==1,"Failed departure retains A snapshot");
   var b=Enter(root,"B");Mutate();RebirthHudTrackingPreferenceService.ResetRuntime(true);
   Check(Pending==2,"Independent failed departure retains B too");
   var c=Enter(root,"C");Mutate();
   RebirthHudTrackingPreferenceService.RetireDeletedWorld(a.WorldKey);
   Check(a.Retired&&Pending==1,"Deleted A marked retired and detached retry removed");
   Check(ReferenceEquals(Field("context"),c)&&(bool)Field("dirty"),"Other active world C remains dirty and intact");
   RebirthAtomicXmlFile.Fail=false;RebirthAtomicXmlFile.Writes.Clear();UnityEngine.Time.realtimeSinceStartup+=2;
   RebirthHudTrackingPreferenceService.FlushPendingIfDue(out _);
   Check(!RebirthAtomicXmlFile.Writes.Contains(a.FilePath),"Deleted A is never retried/recreated");
   Check(RebirthAtomicXmlFile.Writes.Contains(b.FilePath)&&Pending==0,"Unrelated pending B retries normally");
   Check(RebirthHudTrackingPreferenceService.SaveNow(out _),"Unrelated current C still saves normally");
   Check(File.Exists(c.FilePath),"C file published");
   int before=RebirthAtomicXmlFile.Writes.Count;
   Check(!RebirthHudTrackingPreferenceStore.TrySave(a,new RebirthHudTrackingPreferences(),out _),"Stale detached reference cannot save after retirement");
   Check(before==RebirthAtomicXmlFile.Writes.Count,"Retired context refused before IO");
   Mutate();RebirthHudTrackingPreferenceService.RetireDeletedWorld(c.WorldKey);
   Check(c.Retired&&!(bool)Field("dirty")&&Field("context")==null,"Deleted current C discarded without flush");
   Check(before==RebirthAtomicXmlFile.Writes.Count,"Retiring dirty current context performs no write");
   GameManager.Instance.World=null;GameIO.Save=null;
   RebirthHudTrackingPreferenceService.SaveNow(out _);RebirthHudTrackingPreferenceService.FlushPendingIfDue(out _);
   Check(before==RebirthAtomicXmlFile.Writes.Count,"Menu lifecycle cannot resurrect deleted context");
   var next=Enter(root,"A");Check(next.WorldKey!=a.WorldKey&&!next.Retired,"New GUID at reused save path remains writable and isolated");
   Mutate();RebirthHudTrackingPreferenceService.RetireDeletedWorld(new string('f',64));
   Check(ReferenceEquals(Field("context"),next)&&(bool)Field("dirty"),"Deleting unrelated world preserves active preferences");
   Check(RebirthHudTrackingPreferenceService.SaveNow(out _),"Recreated save can persist its own new GUID context");
   Console.WriteLine($"PASS {checks} checks on linked production store/service/models; native platform and atomic IO doubled");
  }finally{RebirthHudTrackingPreferenceService.ResetRuntime(false);Directory.Delete(root,true);}
 }
}
