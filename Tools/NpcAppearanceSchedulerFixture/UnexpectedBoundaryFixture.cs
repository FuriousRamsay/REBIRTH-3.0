using System;
static class UnexpectedBoundaryFixture
{
 internal static int Run()
 {
  int count=0;Action<bool,string>check=(ok,name)=>{if(!ok)throw new Exception(name);count++;};var old=new World();var next=new World();GameManager.Instance.World=old;RebirthNpcPreparedSchedulerAdmission.RetireSavedWorld(old,true);var a=new EntityRebirthNPC{world=old,hasAI=true};RebirthNpcPreparedSchedulerAdmission.TrySchedule(a,"old",1);
  check(!RebirthNpcPreparedSchedulerAdmission.RetireUnexpectedWorld(old,next,"a","b"),"unbound current world refusal");check(!RebirthNpcPreparedSchedulerAdmission.RetireUnexpectedWorld(old,old,"a","A"),"same scope not retired");check(!RebirthNpcPreparedSchedulerAdmission.RetireUnexpectedWorld(old,old,"","b"),"unknown save not proof");GameManager.Instance.World=next;
  check(RebirthNpcPreparedSchedulerAdmission.RetireUnexpectedWorld(old,next,"",""),"concrete world replacement retirement");check(!a.hasAI,"departing AI remains off");GameManager.Instance.World=old;RebirthNpcPreparedSchedulerAdmission.Tick();check(a.Calls==0,"departing entry never readmitted");RebirthNpcPreparedSchedulerAdmission.TrySchedule(a,"sameworld",1);check(RebirthNpcPreparedSchedulerAdmission.RetireUnexpectedWorld(old,old,"saveA","saveB"),"concrete same-world save change retirement");RebirthNpcPreparedSchedulerAdmission.Tick();check(a.Calls==0,"same-world stale actor not enabled");GameManager.Instance.World=next;var b=new EntityRebirthNPC{world=next,Admit=true};RebirthNpcPreparedSchedulerAdmission.TrySchedule(b,"newworld",1);RebirthNpcPreparedSchedulerAdmission.RetireUnexpectedWorld(old,next,"a","b");RebirthNpcPreparedSchedulerAdmission.Tick();check(b.Calls==1,"new world original scheduling preserved");return count;
 }
}
