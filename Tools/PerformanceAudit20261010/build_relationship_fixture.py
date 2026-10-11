from pathlib import Path
import sys
root=Path.cwd(); before='--before' in sys.argv
base=root/'_Documentation/PerformanceAudit_20261010/before' if before else root
integration=(base/'Scripts/NPC/Replication/RebirthNpcReplicationPerformanceIntegration.cs').read_text(encoding='utf-8-sig')
scale=(base/'Scripts/NPC/Performance/RebirthNpcScaleHardening.cs').read_text(encoding='utf-8-sig')
def block(s, marker):
 a=s.index(marker);i=s.index('{',a)+1;d=1
 while d:
  if s[i]=='{':d+=1
  elif s[i]=='}':d-=1
  i+=1
 return s[a:i]
parts=['using System; using System.Collections.Generic;']
for typ in ('RebirthNpcRelationshipMutation','RebirthNpcRelationshipBatchSnapshot'):
 parts.append(block(scale,'public sealed class '+typ))
parts.append(block(scale,'public static class RebirthNpcRelationshipBatcher'))
parts.append('''public interface IAdapter { bool ApplyRelationshipMutation(RebirthNpcRelationshipMutation m); }
public static class Service {
private sealed class RelationshipEnvelope { public Guid ReplayId; public long Sequence; public RebirthNpcRelationshipMutation Mutation; }
private static readonly object Sync=new object();
private static readonly Dictionary<string,long> RelationshipSequences=new Dictionary<string,long>(StringComparer.Ordinal);
private static readonly Queue<RelationshipEnvelope> RelationshipOrder=new Queue<RelationshipEnvelope>();
private static readonly HashSet<Guid> RelationshipReplayIds=new HashSet<Guid>();
private static readonly Queue<Guid> RelationshipReplayOrder=new Queue<Guid>();
public static IAdapter Adapter;
private static long relationshipReplays,relationshipDeferred,relationshipQueued,relationshipApplied;
private static bool relationshipFlushActive;
public static void Reset(){RelationshipOrder.Clear();RelationshipSequences.Clear();RelationshipReplayIds.Clear();RelationshipReplayOrder.Clear();RebirthNpcRelationshipBatcher.ResetForWorldChange();}
''')
parts.append(block(integration,'public static bool QueueRelationship('));parts.append(block(integration,'public static int FlushRelationships('));parts.append('}')
parts.append('''class Sink:IAdapter {
 public int RejectAt=-1,Calls;public bool Throw,Reenter;public Action Callback;public readonly List<RebirthNpcRelationshipMutation> Received=new List<RebirthNpcRelationshipMutation>();
 public bool ApplyRelationshipMutation(RebirthNpcRelationshipMutation m){Calls++;var callback=Callback;Callback=null;callback?.Invoke();if(Throw)throw new Exception("fixture");if(Reenter){Reenter=false;Service.FlushRelationships(99);}if(Calls==RejectAt)return false;Received.Add(m);return true;}
}
class Program {
 static int Failed; static void Check(bool ok,string label){Console.WriteLine((ok?"PASS ":"FAIL ")+label);if(!ok)Failed++;}
 static Sink Reset(){Service.Reset();var sink=new Sink();Service.Adapter=sink;return sink;}
 static void Add(string source,int count){for(int i=0;i<count;i++)Service.QueueRelationship(Guid.NewGuid(),source,"target",i,"test");}
 static int Pending()=>RebirthNpcRelationshipBatcher.GetSnapshot().PendingItems;
 static int Main(){
 var s=Reset();Add("A",128);Add("B",128);Check(Service.FlushRelationships(200)==200 && Pending()==56,"Budget retains 56-item tail");Service.FlushRelationships(200);Check(s.Received.Count==256 && Pending()==0,"Next flush delivers retained tail");
 s=Reset();Add("A",4);s.RejectAt=1;Check(Service.FlushRelationships(10)==0 && Pending()==4,"Refusal retains entire source");s.RejectAt=-1;Service.FlushRelationships(10);Check(s.Received.Count==4 && Pending()==0,"Refusal retry delivers once");
 s=Reset();Add("A",4);s.RejectAt=3;Check(Service.FlushRelationships(10)==2 && Pending()==2,"Partial acceptance retains only unapplied tail");s.RejectAt=-1;Service.FlushRelationships(10);Check(s.Received.Count==4 && s.Received[2].Delta==2,"Partial retry preserves order");
 s=Reset();Add("A",2);Check(Service.FlushRelationships(0)==0 && Pending()==2 && s.Calls==0,"Zero budget preserves queue");Check(Service.FlushRelationships(-1)==0 && Pending()==2 && s.Calls==0,"Negative budget preserves queue");
 s=Reset();Add("A",2);s.Throw=true;try{Service.FlushRelationships(10);}catch(Exception){}Check(Pending()==2,"Throwing adapter retains queue");s.Throw=false;Service.FlushRelationships(10);Check(s.Received.Count==2,"Retry after throw remains available");
 s=Reset();Add("A",128);Guid retry=Guid.NewGuid();Check(!Service.QueueRelationship(retry,"A","target",999,"retry"),"Source capacity rejects admission");Service.FlushRelationships(128);Check(Service.QueueRelationship(retry,"A","target",999,"retry") && Pending()==1,"Rejected replay ID can retry after capacity frees");Service.FlushRelationships(10);Check(s.Received.Count==129,"Capacity retry actually delivers");
 s=Reset();Guid once=Guid.NewGuid();Service.QueueRelationship(once,"A","target",1,"once");Service.QueueRelationship(once,"A","target",1,"once");Service.FlushRelationships(10);Check(s.Received.Count==1,"Accepted replay suppressed");
 s=Reset();Add("A",2);s.Reenter=true;Service.FlushRelationships(10);Check(s.Received.Count==2 && Pending()==0,"Nested flush does not duplicate or lose updates");
 s=Reset();Service.QueueRelationship(Guid.NewGuid(),"A","target",1,"test");Service.QueueRelationship(Guid.NewGuid(),"B","target",2,"test");Service.QueueRelationship(Guid.NewGuid(),"A","target",3,"test");Service.FlushRelationships(2);Check(Pending()==1 && s.Received[0].Delta==1 && s.Received[1].Delta==2,"Interleaved sources retain admission order");Service.FlushRelationships(2);Check(Pending()==0 && s.Received[2].Delta==3,"Interleaved tail delivered once");
 s=Reset();Add("old",1);s.Callback=()=>{Service.Reset();Add("new",1);};Service.FlushRelationships(2);Check(s.Received.Count==2 && s.Received[0].SourceNpcId=="old" && s.Received[1].SourceNpcId=="new" && Pending()==0,"Callback reset cannot discard new queue head");
 s=Reset();Add("A",1);s.Callback=()=>Add("A",1);Service.FlushRelationships(2);Check(s.Received.Count==2 && Pending()==0,"Callback enqueue retains source head acknowledgement");
 Console.WriteLine("Failures="+Failed);return Failed==0?0:1;
 }
}''')
out=root/'Tools/PerformanceAudit20261010/RelationshipFixture';out.mkdir(exist_ok=True)
(out/'Program.cs').write_text('\n'.join(parts))
(out/'RelationshipFixture.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>')
print('Fixture extracted '+('before' if before else 'current')+' queue/flush and full relationship batcher; adapter/environment doubled.')
