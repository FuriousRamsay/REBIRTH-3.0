// AUTHORED_NOT_RUN. Actual inventory consumers; native/source services are explicit adapters.
using System;using System.Collections.Generic;
class EntityPlayer{internal int entityId=1;}class EntityPlayerLocal:EntityPlayer{}
class ItemValue{}class ItemStack{}
class XUiM_PlayerInventory{internal EntityPlayer localPlayer=new EntityPlayerLocal();internal void dispatchBackpackItemsChanged(){}internal void dispatchToolbeltItemsChanged(){}}
static class RemoteResourcesRuntimePolicy{internal static bool Enabled;}
static class RemoteResourceDiagnostics{internal static void Write(string text){}}
static class RemoteResourceClientGrantContext
{
 internal static bool Active=true;internal static int Consumes,Queries,Counts,Appends;
 internal static bool ConsumeLocalRemainder(EntityPlayerLocal player,IList<ItemStack> needs,int multiplier,IList<ItemStack> removed){Consumes++;return true;}
 internal static bool HasItems(EntityPlayerLocal player,IList<ItemStack> needs,int multiplier){Queries++;return true;}
 internal static int GetCount(ItemValue value){Counts++;return 7;}internal static void AppendStacks(List<ItemStack> values){Appends++;values.Add(new ItemStack());}
}
static class RemoteResourceTransactions
{
 internal sealed class ConsumptionResult{internal bool Success;internal string FailureReason;internal int LocalItemsConsumed,RemoteItemsConsumed,SourcesChanged;}
 internal static int SourceCalls;internal static bool HasItems(EntityPlayer p,IList<ItemStack> needs,int multiplier,bool local){SourceCalls++;return false;}
 internal static bool HasLocalItems(EntityPlayer p,IList<ItemStack> needs,int multiplier){SourceCalls++;return false;}
 internal static ConsumptionResult TryConsume(EntityPlayer p,IList<ItemStack> needs,int multiplier,IList<ItemStack> removed,bool local){SourceCalls++;return new ConsumptionResult();}
 internal static int CountRemote(EntityPlayer p,ItemValue value){SourceCalls++;return 0;}
}
static class RemoteResourceClientAvailability{internal static bool HasItems(EntityPlayerLocal p,IList<ItemStack> n,int m)=>false;internal static int GetCount(EntityPlayerLocal p,ItemValue v)=>0;internal static void AppendStacks(EntityPlayerLocal p,List<ItemStack> s){}}
static class RemoteResourceSnapshotCache{internal sealed class Snapshot{internal void AppendClonedStacks(List<ItemStack> s){RemoteResourceTransactions.SourceCalls++;}}internal static Snapshot Get(EntityPlayer p)=>new Snapshot();internal static void InvalidatePlayer(int id){}}
class Consumers
{
 static bool Server;
 static bool IsAuthoritativeServer()=>Server;
 // ACTUAL_REMOVE
 // ACTUAL_HAS
 // ACTUAL_COUNT
 // ACTUAL_STACKS
 static void Need(bool value,string text){if(!value)throw new Exception(text);}
 static void Main(){foreach(bool server in new[]{false,true})foreach(bool enabled in new[]{false,true})
 {
  Server=server;RemoteResourcesRuntimePolicy.Enabled=enabled;RemoteResourceTransactions.SourceCalls=0;RemoteResourceClientGrantContext.Consumes=RemoteResourceClientGrantContext.Queries=RemoteResourceClientGrantContext.Counts=RemoteResourceClientGrantContext.Appends=0;
  var inventory=new XUiM_PlayerInventory();var needs=new List<ItemStack>{new ItemStack()};bool has=false;int count=2;List<ItemStack> stacks=new List<ItemStack>();
  Need(!BeforeRemoveItems(inventory,needs,2,new List<ItemStack>())&&RemoteResourceClientGrantContext.Consumes==1,"original grant must consume local remainder without another source withdrawal");AfterHasItems(inventory,needs,2,ref has);AfterGetItemCount(inventory,new ItemValue(),ref count);AfterGetAllItemStacks(inventory,ref stacks);
  Need(has&&count==9&&stacks.Count==1&&RemoteResourceClientGrantContext.Queries==1&&RemoteResourceClientGrantContext.Counts==1&&RemoteResourceClientGrantContext.Appends==1,"native original sees only exact already-paid grant");Need(RemoteResourceTransactions.SourceCalls==0,"host-local resume reentered source payment");
 }
 Console.WriteLine("MP042 actual native inventory grant consumers: 12 assertions authored for host/client and policy transitions; no source effect adapter invoked.");}
}
