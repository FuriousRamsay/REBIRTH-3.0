using System;using System.Collections.Generic;
class ItemValue {public string Name;public ItemValue Clone(){return new ItemValue {Name=Name};}}
class ItemStack {public ItemValue itemValue;public int count;public ItemStack(ItemValue v,int c){itemValue=v;count=c;}public ItemStack Clone(){return new ItemStack(itemValue.Clone(),count);}public static ItemStack Empty=new ItemStack(new ItemValue(),0);}
interface IRemoteResourceSource {string StableId {get;} ItemStack[] Slots {get;} void SetSlot(int i,ItemStack s);void MarkModified();}
class Source:IRemoteResourceSource {public string Id;public string StableId {get{return Id;}}public ItemStack[] Items;public ItemStack[] Slots {get{return Items;}}public void SetSlot(int i,ItemStack s){Items[i]=s;}public virtual void MarkModified(){}}
class RemoteNpcResourceSource:Source {public bool LastCommitSucceeded;public string LastCommitError="rejected";}
static class RemoteResourceLiveSync {public static void BeginBatch(){}public static void EndBatch(){}public static void NotifySourcesChanged(List<IRemoteResourceSource> s,string r){}}
class SlotTake {public IRemoteResourceSource Source;public int Slot,Count;}
class Plan {public List<SlotTake> Remote=new List<SlotTake>();}
class Subject {public class ConsumptionResult {public int RemoteItemsConsumed;public string FailureReason;}
// HELPER
public static ConsumptionResult Run(Plan plan,IList<ItemStack> removedItems){var result=new ConsumptionResult();
// BLOCK
return result;}}
class Check {static void Main(){foreach(bool success in new[]{false,true}){var p=new Plan();var npc=new RemoteNpcResourceSource {Id="npc",LastCommitSucceeded=success,Items=new[]{new ItemStack(new ItemValue {Name="npcItem"},5)}};var native=new Source {Id="native",Items=new[]{new ItemStack(new ItemValue {Name="nativeItem"},5)}};p.Remote.Add(new SlotTake {Source=npc,Count=2});p.Remote.Add(new SlotTake {Source=native,Count=1});var removed=new List<ItemStack>();var r=Subject.Run(p,removed);if(r.RemoteItemsConsumed!=(success?3:1)||removed.Count!=(success?2:1))throw new Exception("receipt counts");foreach(var s in removed)if(!success&&s.itemValue.Name=="npcItem")throw new Exception("uncommitted receipt");if((r.FailureReason!=null)==success)throw new Exception("failure outcome");}Console.WriteLine("PASS mixed native/NPC sources: rejected NPC receipts excluded, successful receipts retained");}}
