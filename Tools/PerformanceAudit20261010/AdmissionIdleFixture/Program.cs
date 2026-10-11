using System;
class World { public bool IsRemote()=>false; }
class GameManager { public static GameManager Instance=new(); public World World=new(); }
class EntityRebirthNPC { public World world=GameManager.Instance.World; public bool hasAI=true; public int Calls; public Func<bool> Admit=()=>true; public bool TryAdmitPreparedAi(string r,uint g){Calls++;return Admit();} }
class Log { public static void Warning(string s){} }
class Program {
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main(){
  for(int i=0;i<1000;i++)RebirthNpcPreparedSchedulerAdmission.Tick();
  long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++)RebirthNpcPreparedSchedulerAdmission.Tick();
  Check(GC.GetAllocatedBytesForCurrentThread()==before,"idle allocation");
  var a=new EntityRebirthNPC();var child=new EntityRebirthNPC();a.Admit=()=>{Check(RebirthNpcPreparedSchedulerAdmission.TrySchedule(child,"child",1),"child schedule");return true;};
  Check(RebirthNpcPreparedSchedulerAdmission.TrySchedule(a,"a",1),"schedule");RebirthNpcPreparedSchedulerAdmission.Tick();Check(a.Calls==1&&child.Calls==0,"reentrant admission deferred");RebirthNpcPreparedSchedulerAdmission.Tick();Check(child.Calls==1,"child admitted next tick");
  var retry=new EntityRebirthNPC();retry.Admit=()=>retry.Calls>1;RebirthNpcPreparedSchedulerAdmission.TrySchedule(retry,"retry",1);RebirthNpcPreparedSchedulerAdmission.Tick();RebirthNpcPreparedSchedulerAdmission.Tick();Check(retry.Calls==2,"retry retained");
  var actors=new EntityRebirthNPC[65];for(int i=0;i<65;i++){actors[i]=new();RebirthNpcPreparedSchedulerAdmission.TrySchedule(actors[i],"batch"+i,1);}RebirthNpcPreparedSchedulerAdmission.Tick();Check(actors[63].Calls==1&&actors[64].Calls==0,"64 limit");RebirthNpcPreparedSchedulerAdmission.Tick();Check(actors[64].Calls==1,"tail admitted");
  var throws=new EntityRebirthNPC();throws.Admit=()=>{if(throws.Calls==1)throw new Exception("transient");return true;};RebirthNpcPreparedSchedulerAdmission.TrySchedule(throws,"throws",1);RebirthNpcPreparedSchedulerAdmission.Tick();RebirthNpcPreparedSchedulerAdmission.Tick();Check(throws.Calls==2,"exception retry");
  var held=new EntityRebirthNPC();RebirthNpcPreparedSchedulerAdmission.TrySchedule(held,"held",1);var original=GameManager.Instance.World;GameManager.Instance.World=new();RebirthNpcPreparedSchedulerAdmission.Tick();Check(held.Calls==0,"foreign world held");GameManager.Instance.World=original;RebirthNpcPreparedSchedulerAdmission.Tick();Check(held.Calls==1,"original world resumed");
  var retired=new EntityRebirthNPC();RebirthNpcPreparedSchedulerAdmission.TrySchedule(retired,"retire",1);RebirthNpcPreparedSchedulerAdmission.RetireSavedWorld(original,true);RebirthNpcPreparedSchedulerAdmission.Tick();Check(retired.Calls==0,"retired admission absent");
  Console.WriteLine("PASS production admission: 10000 idle ticks allocate zero bytes; reentrant deferral, retry and 64-entry budget preserved. Native admission collaborators are doubles.");
 }
}
