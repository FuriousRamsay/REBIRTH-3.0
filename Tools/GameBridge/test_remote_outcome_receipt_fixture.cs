using System;
using System.Collections.Generic;
class World {} class EntityPlayer {}
class PlatformUserIdentifierAbs {public string CombinedString="owner";}
enum RemoteResourceClientOperation:byte {Craft=1,CookingPull=5}
enum RemoteResourceTransactionOutcomeState:byte {Unknown,Success,Failed}
class ItemStack {public int count;public ItemStack Clone(){return new ItemStack{count=count};}}
static class RebirthSurvivorMode {public static bool Enabled=true;public static bool IsEnabledForCurrentWorld(){return Enabled;}}
static class RemoteResourcesRuntimePolicy {public static bool Enabled=true;}
static class RemoteResourceTransactions {
public static int Calls; public static int Mode;
public class ConsumptionResult {public bool Success;public string FailureReason="";}
public static ConsumptionResult TryConsume(EntityPlayer p,IList<ItemStack> needs,int multiplier,List<ItemStack> removed,bool local){Calls++;if(Mode==0)throw new Exception("before debit");removed.Add(new ItemStack{count=3});if(Mode==1)throw new Exception("after debit");return new ConsumptionResult{Success=Mode==2,FailureReason="failed"};}}
// PRODUCTION_METHOD
class Check {static void Main(){var world=new World();var p=new EntityPlayer();var uid=new PlatformUserIdentifierAbs();var needs=new List<ItemStack>{new ItemStack{count=3}};
foreach(var operation in new[]{RemoteResourceClientOperation.Craft,RemoteResourceClientOperation.CookingPull})
for(int mode=0;mode<4;mode++){RemoteResourceTransactionOutcomeJournal.ResetForWorldUnload();RemoteResourceTransactions.Calls=0;RemoteResourceTransactions.Mode=mode;var first=RemoteResourceTransactionOutcomeJournal.Resolve(world,p,uid,1,1,operation,needs,false);if(first.State!=(mode==2?RemoteResourceTransactionOutcomeState.Success:RemoteResourceTransactionOutcomeState.Failed)||first.Removed.Count!=(mode==0?0:1))throw new Exception("Outcome");if(first.Removed.Count>0)first.Removed[0].count=999;foreach(bool query in new[]{true,false}){var replay=RemoteResourceTransactionOutcomeJournal.Resolve(world,p,uid,1,1,operation,needs,query);if(RemoteResourceTransactions.Calls!=1||(mode!=0&&replay.Removed[0].count!=3))throw new Exception("Replay");}}
RemoteResourceTransactionOutcomeJournal.ResetForWorldUnload();RemoteResourceTransactions.Calls=0;RebirthSurvivorMode.Enabled=false;var disabled=RemoteResourceTransactionOutcomeJournal.Resolve(world,p,uid,1,99,RemoteResourceClientOperation.CookingPull,needs,false);if(disabled.State!=RemoteResourceTransactionOutcomeState.Failed||RemoteResourceTransactions.Calls!=0)throw new Exception("Disabled cooking debited");RebirthSurvivorMode.Enabled=true;
Console.WriteLine("PASS: cooking and crafting receipts, disabled cooking guard; pre/post-debit exception receipts, success/failure, replay without repeat debit, receipt clone isolation");}}
