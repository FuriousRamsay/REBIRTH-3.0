using System;
public class RebirthGearInventoryPlan{public class Stack{public string ItemData;public int Count;}}
public class RebirthGearInventorySnapshot{public RebirthGearInventoryPlan.Stack[] Bag,Belt;public int OwnedBeltSlots=4;}
static class Program{
 static int n;static RebirthGearInventoryPlan.Stack Cell(string data=null,int count=0)=>new(){ItemData=data,Count=count};
 static RebirthGearInventorySnapshot Snapshot()=>new(){Bag=new[]{Cell("same",1),Cell(),Cell("other",3)},Belt=new[]{Cell("held",1),Cell()}};
 static void Check(bool yes,string label){if(!yes)throw new Exception(label);n++;}
 static bool Locate(RebirthGearInventorySnapshot before,RebirthGearInventorySnapshot after,int count,out int index)=>RebirthGearCursorDepositWitness.TryLocate(before,after,"same",count,out index);
 static void Main(){
 var before=Snapshot();var after=Snapshot();after.Bag[1]=Cell("same",1);Check(Locate(before,after,1,out var index)&&index==1,"new slot selected instead of existing identical stack");
 after=Snapshot();after.Bag[0].Count=2;Check(Locate(before,after,1,out index)&&index==0,"exact merge destination accepted");
 after=Snapshot();after.Bag[0].Count=2;after.Bag[1]=Cell("same",2);Check(Locate(before,after,3,out index)&&index==0,"split exact deposit accounts all cells");
 Check(!Locate(before,after,4,out index)&&index==-1,"wrong deposited count refused");
 after=Snapshot();Check(!Locate(before,after,1,out index),"no change cannot select old identical item");
 after=Snapshot();after.Bag[0].Count=0;after.Bag[1]=Cell("same",2);Check(!Locate(before,after,1,out index),"move masquerading as addition refused");
 after=Snapshot();after.Bag[2]=Cell("same",4);Check(!Locate(before,after,1,out index),"replaced unrelated item refused");
 after=Snapshot();after.Bag[1]=Cell("wrong",1);Check(!Locate(before,after,1,out index),"different metadata refused");
 after=Snapshot();after.Bag[1]=Cell("same",1);after.Belt[0].Count=2;Check(!Locate(before,after,1,out index),"concurrent belt change refused");
 after=Snapshot();after.Bag[1]=Cell("same",1);after.OwnedBeltSlots=5;Check(!Locate(before,after,1,out index),"capacity ownership change refused");
 after=Snapshot();after.Bag=new[]{Cell("same",1)};Check(!Locate(before,after,1,out index),"changed bag geometry refused");
 after=Snapshot();after.Bag[1]=null;Check(!Locate(before,after,1,out index),"null cell refused");
 after=Snapshot();after.Bag[1]=Cell("same",-1);Check(!Locate(before,after,1,out index),"negative count refused");
 after=Snapshot();Check(!Locate(before,after,0,out index),"empty deposit refused");Check(!Locate(null,after,1,out index),"missing preimage refused");Check(!RebirthGearCursorDepositWitness.TryLocate(before,after,null,1,out index),"missing full item identity refused");
 Console.WriteLine($"PASS {n} actual cursor deposit witness checks; snapshot DTO boundary doubled");
 }
}