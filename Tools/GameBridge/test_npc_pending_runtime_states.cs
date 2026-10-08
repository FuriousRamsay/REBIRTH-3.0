using System;
using System.Collections.Generic;
namespace UnityEngine { public struct Vector3 {} }
internal struct RebirthNpcStableId { internal int Value; internal bool IsEmpty { get { return Value==0; } } }
internal enum RebirthNpcPresenceState { Active }
internal enum RebirthNpcOwnershipKind { Player }
internal enum RebirthNpcOrderState { Follow }
internal enum RebirthNpcTravelState { None }
internal class Entity { internal int entityId; }
internal class World { internal bool Remote=true; internal Dictionary<int,Entity> Entities=new Dictionary<int,Entity>(); internal bool IsRemote(){return Remote;} internal Entity GetEntity(int id){Entity e; return Entities.TryGetValue(id,out e)?e:null;} }
internal class Runtime { internal RebirthNpcStableId StableId; }
internal class EntityRebirthNPC:Entity { internal World world; internal Runtime RebirthRuntimeState=new Runtime(); internal uint Revision; internal string Owner; internal void ApplyReplicatedRuntimeState(RebirthNpcPresenceState p,RebirthNpcOwnershipKind k,string owner,RebirthNpcOrderState o,RebirthNpcTravelState t,UnityEngine.Vector3 g,bool h,uint r){if(r<Revision)return;Revision=r;Owner=owner;} }
internal static class RebirthNpcNetworkEpoch { internal static uint ClientEpoch=1,ClientConnectionEpoch=1; internal static bool AcceptClientEpoch(uint e){return e!=0&&e==ClientEpoch;} }
internal class PendingChecks {
static void Assert(bool value,string label){if(!value)throw new Exception(label);}
static RebirthNpcPendingRuntimeStates.Snapshot S(int id,int stable,uint revision){return new RebirthNpcPendingRuntimeStates.Snapshot {EntityId=id,StableId=new RebirthNpcStableId{Value=stable},Epoch=1,Revision=revision,Owner="owner"+revision};}
static EntityRebirthNPC N(World w,int id,int stable){return new EntityRebirthNPC{world=w,entityId=id,RebirthRuntimeState=new Runtime{StableId=new RebirthNpcStableId{Value=stable}}};}
static void Main(){
var w=new World(); var n=N(w,1,11);
RebirthNpcPendingRuntimeStates.Receive(w,S(1,11,3)); RebirthNpcPendingRuntimeStates.Receive(w,S(1,11,2)); RebirthNpcPendingRuntimeStates.ApplyOnAdded(n); Assert(n.Revision==3,"newest early snapshot");
n.Owner="changed";RebirthNpcPendingRuntimeStates.ApplyOnAdded(n);Assert(n.Owner=="changed","consume once");
RebirthNpcPendingRuntimeStates.Receive(w,S(2,22,3));var reused=N(w,2,23);RebirthNpcPendingRuntimeStates.ApplyOnAdded(reused);Assert(reused.Revision==0,"reused identity rejected");
RebirthNpcPendingRuntimeStates.Receive(w,S(3,33,3));RebirthNpcNetworkEpoch.ClientConnectionEpoch++;var reconnect=N(w,3,33);RebirthNpcPendingRuntimeStates.ApplyOnAdded(reconnect);Assert(reconnect.Revision==0,"connection reset");
RebirthNpcPendingRuntimeStates.Receive(w,S(4,44,3));var other=N(new World(),4,44);RebirthNpcPendingRuntimeStates.ApplyOnAdded(other);Assert(other.Revision==0,"world reset");
var expired=S(5,55,3);RebirthNpcPendingRuntimeStates.Receive(w,expired);expired.Expires=DateTime.UtcNow.AddSeconds(-1);var old=N(w,5,55);RebirthNpcPendingRuntimeStates.ApplyOnAdded(old);Assert(old.Revision==0,"expiry");
RebirthNpcPendingRuntimeStates.Receive(w,S(6,66,3));RebirthNpcPendingRuntimeStates.Reset();var cleared=N(w,6,66);RebirthNpcPendingRuntimeStates.ApplyOnAdded(cleared);Assert(cleared.Revision==0,"explicit reset");
w.Entities[1]=n;RebirthNpcPendingRuntimeStates.Receive(w,S(1,11,4));Assert(n.Revision==4,"live apply");RebirthNpcPendingRuntimeStates.Receive(w,S(1,99,5));Assert(n.Revision==4,"live identity mismatch");RebirthNpcPendingRuntimeStates.Receive(w,S(1,11,2));Assert(n.Revision==4,"stale revision");
var wrongEpoch=S(1,11,5);wrongEpoch.Epoch=2;RebirthNpcPendingRuntimeStates.Receive(w,wrongEpoch);Assert(n.Revision==4,"wrong epoch");w.Remote=false;RebirthNpcPendingRuntimeStates.Receive(w,S(1,11,5));Assert(n.Revision==4,"server excluded");w.Remote=true;
RebirthNpcPendingRuntimeStates.Reset();for(int i=100;i<16485;i++)RebirthNpcPendingRuntimeStates.Receive(w,S(i,i,1));var evicted=N(w,100,100);RebirthNpcPendingRuntimeStates.ApplyOnAdded(evicted);Assert(evicted.Revision==0,"bounded eviction");var kept=N(w,16484,16484);RebirthNpcPendingRuntimeStates.ApplyOnAdded(kept);Assert(kept.Revision==1,"newest retained");
Console.WriteLine("PASS: deferred NPC state ordering, identity, session, expiry, authority and capacity checks");
}}
