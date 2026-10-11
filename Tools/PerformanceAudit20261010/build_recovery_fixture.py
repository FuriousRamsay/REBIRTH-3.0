from pathlib import Path
s=Path('Scripts/NPC/Activity/RebirthNpcActivityResultPolicy.cs').read_text(encoding='utf-8-sig');a=s.index('    public static void Tick()',s.index('public static class RebirthNpcActivityRecoveryRegistry'));z=s.index('    public static void ReleaseForLease',a);method=s[a:z]
stub='''using System;
using System.Collections.Generic;
enum RebirthNpcActivityRecoveryStatus { Pending, Cancelled }
sealed class RebirthNpcActivityRecoveryTicket { public ulong RecoveryId,LeaseId; public int Order,TargetEntityId; public long ExpiresUtcTicks; public RebirthNpcActivityRecoveryStatus Status; }
sealed class RebirthNpcExecutionLease { public ulong LeaseId; public int AcceptedRevision; }
static class RebirthNpcWorkReleaseGate { public static int Visits; public static bool Hold(int order){Visits++;return order==1;} }
static class RebirthNpcExecutionLeaseRegistry { public static int Checks,Fails; public static bool TryGetActive(int id,out RebirthNpcExecutionLease l){Checks++;l=new(){LeaseId=(ulong)id};return true;} public static void TryFail(int id,ulong lease,int revision,string detail){Fails++;} }
static class RebirthNpcActivityRecoveryDiagnostics { public static void RecordCancelled(){} }
static class Program {
static object Sync=new();static LinkedList<ulong> PendingOrder=new();static Dictionary<ulong,LinkedListNode<ulong>> PendingNodes=new();static Dictionary<ulong,RebirthNpcActivityRecoveryTicket> ByRecoveryId=new();static Dictionary<ulong,int> AttemptsByLease=new();static LinkedListNode<ulong> recoveryCursor;const int RecoverySweepBudget=16;
static void TerminalizeLocked(RebirthNpcActivityRecoveryTicket t,RebirthNpcActivityRecoveryStatus status,string detail){t.Status=status;}
static void Add(ulong id,bool held,bool expired=false){var node=PendingOrder.AddLast(id);PendingNodes[id]=node;ByRecoveryId[id]=new(){RecoveryId=id,LeaseId=id,TargetEntityId=(int)id,Order=held?1:0,ExpiresUtcTicks=expired?0:DateTime.MaxValue.Ticks};}
static void Check(bool test,string name){if(!test)throw new Exception(name);Console.WriteLine("PASS "+name);}
static void Main(){
 for(ulong i=1;i<=1000;i++)Add(i,true);
 Add(1001,false,true);var original=PendingOrder.First;var order=new List<ulong>(PendingOrder);int max=0;
 for(int i=0;i<64;i++){RebirthNpcWorkReleaseGate.Visits=0;Tick();max=Math.Max(max,RebirthNpcWorkReleaseGate.Visits);}
 Check(max<=16,"Every tick inspects at most 16 nodes, including held entries");
 Check(RebirthNpcExecutionLeaseRegistry.Fails==1,"Expired active ticket after 1000 held tickets is reached and failed exactly once");
 Check(ReferenceEquals(original,PendingOrder.First),"Held node identity preserved");
 Check(PendingOrder.Count==1000 && new List<ulong>(PendingOrder).TrueForAll(id=>ByRecoveryId[id].Status==RebirthNpcActivityRecoveryStatus.Pending),"Held states unchanged");
 Check(new List<ulong>(PendingOrder).SequenceEqual(order.Take(1000)),"Held relative order preserved");
 var removed=recoveryCursor; if(removed!=null && removed.List==PendingOrder){PendingOrder.Remove(removed);PendingNodes.Remove(removed.Value);} Tick();
 Check(recoveryCursor==null || recoveryCursor.List==PendingOrder,"Removed cursor recovers to attached node");
 PendingOrder.Clear();PendingNodes.Clear();ByRecoveryId.Clear();recoveryCursor=null;Add(2001,false);int before=RebirthNpcExecutionLeaseRegistry.Checks;Tick();
 Check(RebirthNpcExecutionLeaseRegistry.Checks-before==1,"Single live ticket visited once instead of 16 repeats");
 Check(PendingOrder.Count==1 && PendingNodes[2001].List==PendingOrder,"Live ticket remains scheduled");
 PendingOrder.Clear();Tick();Console.WriteLine("PASS empty queue");
}
'''
Path('Tools/PerformanceAudit20261010/RecoveryFixture/Program.cs').write_text(stub+method+'}\n')
