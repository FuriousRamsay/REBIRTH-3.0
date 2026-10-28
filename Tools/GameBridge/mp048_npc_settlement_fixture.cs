// AUTHORED_NOT_RUN. Entire production NPC trade settlement partial. Native
// SavedAfter, full plan codec, persistence backing and capability are adapters.
using System;using System.Collections.Generic;using System.Linq;using System.Threading;
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
static class RebirthNpcPersistenceCoordinator{internal static bool IsRecoveryInstalling;}
enum RebirthNpcInventoryTransactionResult{AuthorityDenied,NotFound,Replayed,RevisionConflict,Applied}
class Snapshot{internal uint Revision;internal object[] NativeStacks;internal Dictionary<string,int> Quantities=new Dictionary<string,int>(),Reservations=new Dictionary<string,int>(),LogisticsEffects=new Dictionary<string,int>();}
class RebirthNpcTradeNpcPlan{
 internal Snapshot Before,After;internal Snapshot CopySnapshot(bool after)=>Clone(after?After:Before);
 internal bool Matches(Snapshot value,bool after){var expected=after?After:Before;return value.Revision==expected.Revision&&value.NativeStacks.SequenceEqual(expected.NativeStacks)&&Equal(value.Quantities,expected.Quantities)&&Equal(value.Reservations,expected.Reservations)&&Equal(value.LogisticsEffects,expected.LogisticsEffects);}
 static bool Equal(Dictionary<string,int> a,Dictionary<string,int> b)=>a.Count==b.Count&&a.All(p=>b.TryGetValue(p.Key,out var value)&&value==p.Value);
 internal static Snapshot Clone(Snapshot value)=>new Snapshot{Revision=value.Revision,NativeStacks=(object[])value.NativeStacks.Clone(),Quantities=new Dictionary<string,int>(value.Quantities),Reservations=new Dictionary<string,int>(value.Reservations),LogisticsEffects=new Dictionary<string,int>(value.LogisticsEffects)};
}
class RebirthNpcTradeOriginal{internal Guid Operation=Guid.NewGuid(),Npc=Guid.NewGuid();internal Quote CopyQuote()=>new Quote{NpcId=Npc};internal class Quote{internal Guid NpcId;}}
static class RebirthNpcTradeService{
 internal static bool Blocked=true;internal static bool BlocksOwnerPreparedNpc(Guid npc)=>Blocked;
 internal class HostPrepared{internal RebirthNpcTradeOriginal Original;internal RebirthNpcTradeNpcPlan NpcPlan;}
 internal class HostGrant{internal HostPrepared Prepared;}
 internal class OwnerAdmission{internal class SavedAfter{internal HostGrant Grant;internal int Calls,FailAt=int.MaxValue;internal bool Current()=>++Calls<FailAt;}}
}
static class RebirthNpcInventoryPersistenceStore{internal static bool Saved=true;internal static int Dirty;internal static object CaptureTradeSaved(RebirthNpcTradeOriginal original,RebirthNpcTradeNpcPlan plan,bool after)=>Saved?new object():null;internal static void MarkDirty(){Dirty++;}}
public static partial class RebirthNpcInventoryTransactionService{
 class InventoryState{internal uint Revision;internal object[] NativeStacks;internal Dictionary<string,int> Quantities=new Dictionary<string,int>(),Reservations=new Dictionary<string,int>(),LogisticsEffects=new Dictionary<string,int>();internal Queue<Guid> AppliedTransactionOrder=new Queue<Guid>();internal HashSet<Guid> AppliedTransactions=new HashSet<Guid>();}
 static readonly object Sync=new object();static Dictionary<Guid,InventoryState> States=new Dictionary<Guid,InventoryState>();static object generationEpoch=new object();static long applied;static bool Capability=true;
 static bool HasInventoryCapability(Guid npc)=>Capability;
 static Snapshot GetSnapshot(Guid npc){var value=States[npc];return new Snapshot{Revision=value.Revision,NativeStacks=(object[])value.NativeStacks.Clone(),Quantities=new Dictionary<string,int>(value.Quantities),Reservations=new Dictionary<string,int>(value.Reservations),LogisticsEffects=new Dictionary<string,int>(value.LogisticsEffects)};}
 static void RememberTransaction(InventoryState state,Guid operation){state.AppliedTransactions.Add(operation);state.AppliedTransactionOrder.Enqueue(operation);}
 static RebirthNpcTradeService.OwnerAdmission.SavedAfter source;static RebirthNpcTradeOriginal original;static RebirthNpcTradeNpcPlan plan;static InventoryState prior;static object epoch;static Guid earlier;static int checks;
 static void Seed(bool sell=false){ThreadManager.Main=true;RebirthNpcPersistenceCoordinator.IsRecoveryInstalling=false;RebirthNpcTradeService.Blocked=Capability=RebirthNpcInventoryPersistenceStore.Saved=true;RebirthNpcInventoryPersistenceStore.Dirty=0;applied=0;
  original=new RebirthNpcTradeOriginal();earlier=Guid.NewGuid();prior=new InventoryState{Revision=10,NativeStacks=new[]{new object()}};prior.Quantities.Add("goods",20);prior.Quantities.Add("coin",100);prior.Quantities.Add("unrelated",9);prior.LogisticsEffects.Add("existing",4);RememberTransaction(prior,earlier);States=new Dictionary<Guid,InventoryState>{{original.Npc,prior}};generationEpoch=epoch=new object();
  var before=GetSnapshot(original.Npc);var after=RebirthNpcTradeNpcPlan.Clone(before);after.Revision++;after.Quantities["goods"]+=sell?2:-2;after.Quantities["coin"]+=sell?-6:6;
  plan=new RebirthNpcTradeNpcPlan{Before=before,After=after};source=new RebirthNpcTradeService.OwnerAdmission.SavedAfter{Grant=new RebirthNpcTradeService.HostGrant{Prepared=new RebirthNpcTradeService.HostPrepared{Original=original,NpcPlan=plan}}};
 }
 static RebirthNpcInventoryTransactionResult Apply(out uint revision)=>ApplyTradeOwnerOriginal(source,out revision);
 static void Need(bool value){if(!value)throw new Exception("NPC settlement "+checks);checks++;}
 public static void Main(){
  foreach(bool sell in new[]{false,true}){Seed(sell);Need(Apply(out var revision)==RebirthNpcInventoryTransactionResult.Applied&&revision==11);var next=States[original.Npc];Need(!ReferenceEquals(next,prior)&&!ReferenceEquals(generationEpoch,epoch)&&plan.Matches(GetSnapshot(original.Npc),true));Need(prior.Quantities["goods"]==20&&prior.Quantities["coin"]==100&&next.Quantities["unrelated"]==9&&next.LogisticsEffects["existing"]==4);Need(next.AppliedTransactions.SetEquals(new[]{earlier,original.Operation})&&next.AppliedTransactionOrder.SequenceEqual(new[]{earlier,original.Operation})&&RebirthNpcInventoryPersistenceStore.Dirty==1&&applied==1);Need(Apply(out revision)==RebirthNpcInventoryTransactionResult.Replayed&&revision==11&&RebirthNpcInventoryPersistenceStore.Dirty==1&&applied==1);}
  Seed();ThreadManager.Main=false;Need(Apply(out _)==RebirthNpcInventoryTransactionResult.AuthorityDenied&&ReferenceEquals(States[original.Npc],prior));
  Seed();RebirthNpcPersistenceCoordinator.IsRecoveryInstalling=true;Need(Apply(out _)==RebirthNpcInventoryTransactionResult.AuthorityDenied);
  Seed();source.FailAt=1;Need(Apply(out _)==RebirthNpcInventoryTransactionResult.AuthorityDenied);
  Seed();Capability=false;Need(Apply(out _)==RebirthNpcInventoryTransactionResult.AuthorityDenied);
  Seed();RebirthNpcTradeService.Blocked=false;Need(Apply(out _)==RebirthNpcInventoryTransactionResult.AuthorityDenied);
  Seed();States.Clear();Need(Apply(out _)==RebirthNpcInventoryTransactionResult.NotFound);
  Seed();prior.Reservations.Add("goods",1);Need(Apply(out _)==RebirthNpcInventoryTransactionResult.RevisionConflict&&ReferenceEquals(States[original.Npc],prior));
  Seed();prior.Reservations.Add("goods",1);plan.Before.Reservations.Add("goods",1);plan.After.Reservations.Add("goods",1);Need(Apply(out _)==RebirthNpcInventoryTransactionResult.RevisionConflict&&ReferenceEquals(States[original.Npc],prior)&&RebirthNpcInventoryPersistenceStore.Dirty==0);
  Seed();prior.NativeStacks=new[]{new object()};Need(Apply(out _)==RebirthNpcInventoryTransactionResult.RevisionConflict&&ReferenceEquals(States[original.Npc],prior));
  Seed();prior.Quantities["unrelated"]++;Need(Apply(out _)==RebirthNpcInventoryTransactionResult.RevisionConflict&&ReferenceEquals(generationEpoch,epoch));
  Seed();RebirthNpcInventoryPersistenceStore.Saved=false;Need(Apply(out _)==RebirthNpcInventoryTransactionResult.RevisionConflict&&RebirthNpcInventoryPersistenceStore.Dirty==0);
  Seed();source.FailAt=2;Need(Apply(out _)==RebirthNpcInventoryTransactionResult.RevisionConflict&&ReferenceEquals(States[original.Npc],prior)&&RebirthNpcInventoryPersistenceStore.Dirty==0);
  Seed();source.FailAt=3;Need(Apply(out _)==RebirthNpcInventoryTransactionResult.RevisionConflict&&ReferenceEquals(States[original.Npc],prior)&&ReferenceEquals(generationEpoch,epoch)&&RebirthNpcInventoryPersistenceStore.Dirty==0&&applied==0);
  Seed();RememberTransaction(prior,original.Operation);Need(Apply(out _)==RebirthNpcInventoryTransactionResult.RevisionConflict&&RebirthNpcInventoryPersistenceStore.Dirty==0);
  Console.WriteLine("PASS "+checks+" complete production NPC trade settlement assertions; native plan/source/backing adapters excluded");
 }
}
// ACTUAL_NPC_TRADE_SETTLEMENT
