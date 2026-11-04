using System;
class Program {
 class History {internal int AuditSignature;internal float AuditStableSince;internal bool AuditWritten;}
 class View {internal int Rooms=18,Cleared=15,Enemies=4;}
 static class Time {internal static float realtimeSinceStartup;}
 static class RebirthPurgeSleeperAudit {internal static int Calls;internal static void Capture(object p,string reason){Calls++;}}
 static void Run(History history,View result,int remaining,int[] actors,object player){        int auditSignature=result.Rooms*397^result.Cleared*31^result.Enemies*17^actors.Length;
        if(history.AuditSignature!=auditSignature)
        {
            history.AuditSignature=auditSignature;
            history.AuditStableSince=Time.realtimeSinceStartup;
            history.AuditWritten=false;
        }
        // One snapshot per stable low-remaining state, never per-frame log spam.
        if(remaining>0&&remaining<=4&&!history.AuditWritten&&Time.realtimeSinceStartup-history.AuditStableSince>=15f)
        {
            history.AuditWritten=true;
            RebirthPurgeSleeperAudit.Capture(player,"scanner-stalled-15s");
        }
 }
 static void Main(){var h=new History();var v=new View();var actors=new int[4];
 void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);}
 Run(h,v,3,actors,null);Check(RebirthPurgeSleeperAudit.Calls==0,"no immediate log per scanner tick");
 Time.realtimeSinceStartup=15;Run(h,v,3,actors,null);Check(RebirthPurgeSleeperAudit.Calls==1,"stalled low-room state emits after fifteen seconds");
 Time.realtimeSinceStartup=100;Run(h,v,3,actors,null);Check(RebirthPurgeSleeperAudit.Calls==1,"unchanged state does not spam");
 v.Enemies=3;Run(h,v,3,actors,null);Check(RebirthPurgeSleeperAudit.Calls==1,"changed state resets wait");
 Time.realtimeSinceStartup=115;Run(h,v,3,actors,null);Check(RebirthPurgeSleeperAudit.Calls==2,"new stable state emits once");
 v.Enemies=0;v.Cleared=18;Run(h,v,0,actors,null);Time.realtimeSinceStartup=140;Run(h,v,0,actors,null);Check(RebirthPurgeSleeperAudit.Calls==2,"cleared state does not emit stalled audit");
 }
}