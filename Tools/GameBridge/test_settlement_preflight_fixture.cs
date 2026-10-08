using System;using System.Collections.Generic;
struct RebirthNpcStableId {public int Id;public bool IsEmpty{get{return Id==0;}}}
enum RebirthNpcWorkState:byte {Available,Claimed,Running,Completed,Failed,Cancelled}
enum RebirthNpcResourceReservationState:byte {Reserved,Consumed}
class RebirthNpcNeedPersistentRecord {public RebirthNpcStableId NpcId;}
class RebirthNpcProfessionPersistentRecord {public RebirthNpcStableId NpcId;}
class RebirthNpcSettlementResourcePersistentRecord {public string SettlementId,ResourceKey;public long Quantity;}
class RebirthNpcResourceReservationPersistentRecord {public Guid WorkId;public string SettlementId,ResourceKey;public int Quantity;public RebirthNpcResourceReservationState State;}
class RebirthNpcWorkOrder {public Guid WorkId;public RebirthNpcWorkState State;public long ClaimExpiresUtcTicks;public RebirthNpcStableId ClaimedBy;public string SettlementId,ResourceKey;public int ResourceQuantity;}
class RebirthNpcSettlementPersistentState {public RebirthNpcNeedPersistentRecord[] Needs;public RebirthNpcProfessionPersistentRecord[] Professions;public RebirthNpcSettlementResourcePersistentRecord[] Resources;public RebirthNpcResourceReservationPersistentRecord[] ResourceReservations;public RebirthNpcWorkOrder[] WorkOrders;}
class Check {
 static string ResourceKey(string settlementId,string resourceKey){return settlementId+"\u001f"+resourceKey;}
// METHODS
 static void Expect(RebirthNpcSettlementPersistentState s,bool expected){string e;if(ValidatePersistentState(s,out e)!=expected)throw new Exception(e+" expected="+expected);}
 static void Main(){
 Expect(null,false);Expect(new RebirthNpcSettlementPersistentState(),true);var id=new RebirthNpcStableId{Id=1};
 var need=new RebirthNpcNeedPersistentRecord{NpcId=id};Expect(new RebirthNpcSettlementPersistentState{Needs=new[]{need,need}},false);
 var profession=new RebirthNpcProfessionPersistentRecord{NpcId=id};Expect(new RebirthNpcSettlementPersistentState{Professions=new[]{profession,profession}},false);
 Expect(new RebirthNpcSettlementPersistentState{Resources=new[]{new RebirthNpcSettlementResourcePersistentRecord{SettlementId="home",ResourceKey="Wood",Quantity=5},new RebirthNpcSettlementResourcePersistentRecord{SettlementId="HOME",ResourceKey="wood",Quantity=2}}},false);
 Guid workId=Guid.NewGuid();var w=new RebirthNpcWorkOrder{WorkId=workId,State=RebirthNpcWorkState.Running,ClaimedBy=id,SettlementId="home",ResourceKey="wood",ResourceQuantity=4};var r=new RebirthNpcResourceReservationPersistentRecord{WorkId=workId,SettlementId="home",ResourceKey="wood",Quantity=5};var state=new RebirthNpcSettlementPersistentState{WorkOrders=new[]{w},ResourceReservations=new[]{r}};
 Expect(state,false);if(w.State!=RebirthNpcWorkState.Running||r.Quantity!=5)throw new Exception("Mutated input");
 r.Quantity=4;Expect(state,true);w.State=RebirthNpcWorkState.Claimed;w.ClaimExpiresUtcTicks=1;r.Quantity=5;Expect(state,true);
 w.State=RebirthNpcWorkState.Available;Expect(state,true);w.State=(RebirthNpcWorkState)99;Expect(state,false);
 w.State=RebirthNpcWorkState.Running;state.WorkOrders=new[]{w,w};Expect(state,false);
 state.WorkOrders=null;Expect(state,true);r.State=(RebirthNpcResourceReservationState)99;Expect(state,false);
 Console.WriteLine("PASS: actual settlement preflight rejects duplicate records, case-folded resource collisions, invalid states and active claim mismatch; accepts matching/running, expired or orphan reservation cleanup; input unmodified. Domain records substituted, runtime restoration not executed.");
 }
}
