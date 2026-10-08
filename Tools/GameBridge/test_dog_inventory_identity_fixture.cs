using System;
public struct RebirthNpcStableId {}
public class ItemValue { public int type; }
public class ItemStack {public static ItemStack Empty=new ItemStack();public ItemValue itemValue=new ItemValue();public int count;public bool IsEmpty(){return count<=0;}public ItemStack Clone(){return new ItemStack {count=count,itemValue=new ItemValue {type=itemValue.type}};}}
public class RebirthDogInventorySnapshot {public uint Revision=1;public ItemStack[] Slots=new ItemStack[1];public bool[] Locks=new bool[1];}
public static class Subject {
 const int Capacity=1;public static RebirthDogInventorySnapshot State;public static int Commits;public static bool Accept=true;
 static RebirthDogInventorySnapshot GetSnapshot(RebirthNpcStableId id){return new RebirthDogInventorySnapshot {Slots=new[]{State.Slots[0].Clone()},Locks=State.Locks};}
 static bool CommitWorkingSlots(RebirthNpcStableId id,uint r,ItemStack[] slots,bool[] locks,out string error){error=null;Commits++;if(Accept)State.Slots=slots;return Accept;}
 // PRODUCTION_METHOD
}
class Check {
 static void Assert(bool ok,string m){if(!ok)throw new Exception(m);}
 static void Main(){var id=new RebirthNpcStableId();ItemStack removed;Subject.State=new RebirthDogInventorySnapshot {Slots=new[]{new ItemStack {count=5,itemValue=new ItemValue {type=10}}}};
 Assert(!Subject.TryRemove(id,0,1,true,out removed,11)&&Subject.Commits==0&&removed.IsEmpty()&&Subject.State.Slots[0].count==5,"stale item mutated");
 Assert(Subject.TryRemove(id,0,2,true,out removed,10)&&removed.count==2&&removed.itemValue.type==10&&Subject.State.Slots[0].count==3,"matching transfer failed");
 Subject.State.Locks[0]=true;Assert(!Subject.TryRemove(id,0,1,true,out removed,10)&&Subject.Commits==1,"lock ignored");Subject.State.Locks[0]=false;
 Subject.Accept=false;Assert(!Subject.TryRemove(id,0,1,true,out removed,10)&&removed.IsEmpty()&&Subject.State.Slots[0].count==3,"rejected commit removed item");Subject.Accept=true;
 Assert(!Subject.TryRemove(id,0,0,true,out removed,10)&&!Subject.TryRemove(id,1,1,true,out removed,10),"invalid count/slot accepted");
 Assert(Subject.TryRemove(id,0,99,false,out removed)&&removed.count==3&&Subject.State.Slots[0].IsEmpty(),"existing internal untyped removal broken");Console.WriteLine("PASS: stale item rejection, matching bounded removal, locks, revision failure, invalid requests, existing internal caller.");}
}
