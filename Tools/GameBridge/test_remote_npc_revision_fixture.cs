using System;
using System.Collections.Generic;
struct RebirthNpcStableId {public bool IsEmpty;}
class Inv {public uint InventoryRevision;}
class RebirthNpcPersistentRecordView {public Inv Inventory;}
class RebirthDogPersistentRecordView {}
class RebirthNpcInventorySnapshot {public uint Revision;}
static class RebirthDogStateService {public static RebirthNpcPersistentRecordView Record;public static bool TryGetView(RebirthNpcStableId id,out RebirthNpcPersistentRecordView r,out RebirthDogPersistentRecordView d){r=Record;d=null;return r!=null;}}
static class RebirthNpcInventoryPersistenceStore {public static void EnsureLoaded(){}}
class InventoryState {public uint Revision;}
static class RebirthNpcInventoryTransactionService {public static object Sync=new object();public static Dictionary<RebirthNpcStableId,InventoryState> States=new Dictionary<RebirthNpcStableId,InventoryState>();
// REVISION
}
class Subject {public RebirthNpcStableId npcId;public bool dogStorage;public uint startingRevision;
// CHECK
}
class Check {static void A(bool b){if(!b)throw new Exception("revision preflight");}static void Main(){var s=new Subject {dogStorage=true,startingRevision=2};RebirthDogStateService.Record=new RebirthNpcPersistentRecordView {Inventory=new Inv {InventoryRevision=2}};A(s.IsSnapshotCurrent());RebirthDogStateService.Record.Inventory.InventoryRevision=3;A(!s.IsSnapshotCurrent());RebirthDogStateService.Record.Inventory=null;A(!s.IsSnapshotCurrent());s.startingRevision=0;A(s.IsSnapshotCurrent());RebirthDogStateService.Record=null;A(!s.IsSnapshotCurrent());s.dogStorage=false;A(RebirthNpcInventoryTransactionService.GetRevision(s.npcId)==0 && RebirthNpcInventoryTransactionService.States.Count==0);RebirthNpcInventoryTransactionService.States[s.npcId]=new InventoryState {Revision=0};A(s.IsSnapshotCurrent());RebirthNpcInventoryTransactionService.States[s.npcId].Revision=1;A(!s.IsSnapshotCurrent());s.npcId=new RebirthNpcStableId {IsEmpty=true};A(!s.IsSnapshotCurrent());Console.WriteLine("PASS current/stale dog and generic NPC snapshots, absent inventory, missing entity identity");}}
