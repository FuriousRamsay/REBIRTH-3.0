using System;
using System.IO;
using System.Collections.Generic;
public class ItemClass {public string GetItemName(){return "cassette";}}
public class ItemValue {public int type=1;public ItemClass ItemClass=new ItemClass();public bool IsEmpty(){return type==0;}public static ItemValue ReadOrNull(BinaryReader r){return new ItemValue{type=r.ReadInt32()};}}
public class ItemStack {public ItemValue itemValue;public int count;public static ItemStack Empty=new ItemStack{itemValue=new ItemValue{type=0}};public bool IsEmpty(){return count<=0||itemValue==null||itemValue.IsEmpty();}public ItemStack Clone(){return new ItemStack{itemValue=new ItemValue{type=itemValue.type},count=count};}}
public class Slots {public ItemStack[] Items={new ItemStack{itemValue=new ItemValue(),count=2}};public int Writes;public bool ThrowAfter;public ItemStack[] GetSlots(){return Items;}public void SetSlot(int i,ItemStack s){Writes++;Items[i]=s;if(ThrowAfter)throw new Exception("listener");}public void SetItem(int i,ItemStack s){SetSlot(i,s);}}
public class Buffs {public float Receipt;public int Writes;public int ThrowBeforeWrite,ThrowAfterWrite;public float GetCustomVar(string id){return Receipt;}public void SetCustomVar(string id,float value,bool save){Writes++;if(Writes==ThrowBeforeWrite)throw new Exception("before receipt");Receipt=value;if(Writes==ThrowAfterWrite)throw new Exception("after receipt");}}
public class EntityPlayerLocal {public Buffs Buffs=new Buffs();public Slots bag=new Slots(),inventory=new Slots();public bool IsSpawned(){return true;}public bool IsDead(){return false;}}
public static class RebirthSurvivorMode {public static bool IsEnabledForCurrentWorld(){return true;}}
public static class RebirthToolbeltCapacity {public static int GetOwnedSlotCount(EntityPlayerLocal p,int count){return count;}}
public static class RebirthMusicLibraryService {
 public static bool Full;
 public static bool IsMusicCassette(string id){return id=="cassette";}
 public static string Encode(ItemValue v){return Convert.ToBase64String(BitConverter.GetBytes(v.type));}
 public static int FindReturnSlot(ItemStack[] slots,ItemValue value,out ItemStack result){result=new ItemStack{itemValue=value,count=slots[0].count+1};return Full?-1:0;}
}
// SCOPE
// STATE
// OWNER
public static class MusicOwnerChecks {
 static void A(bool value,string message){if(!value)throw new Exception(message);}
 public static void Run(){foreach(var creation in new[]{Guid.NewGuid().ToString("N"),"legacy-"+new string('a',64)}){
  var offer=new RebirthMusicTransferState{TransactionId=Guid.NewGuid().ToString("N"),CreationId=creation,Operation=1,SourceIsBag=true,SourceIndex=0,ItemId="cassette",ItemData=RebirthMusicLibraryService.Encode(new ItemValue())};
  foreach(float receipt in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,.5f,-.5f,2f,-2f}){
   var p=new EntityPlayerLocal();p.Buffs.Receipt=receipt;
   A(RebirthMusicOwnerTransfer.Apply(p,creation,offer)==RebirthMusicOwnerTransferResult.Pending&&p.bag.Writes==0&&p.inventory.Writes==0&&p.Buffs.Writes==0,"ambiguous receipt touched custody");
  }
  foreach(bool bag in new[]{true,false})foreach(int operation in new[]{1,2}){
   offer.SourceIsBag=bag;offer.Operation=operation;var uncertain=new EntityPlayerLocal();var inventory=operation==2||bag?uncertain.bag:uncertain.inventory;inventory.ThrowAfter=true;
   A(RebirthMusicOwnerTransfer.Apply(uncertain,creation,offer)==RebirthMusicOwnerTransferResult.Pending&&inventory.Writes==1&&uncertain.Buffs.Receipt==2f,"listener failure did not retain uncertainty");
   A(RebirthMusicOwnerTransfer.Apply(uncertain,creation,offer)==RebirthMusicOwnerTransferResult.Pending&&inventory.Writes==1,"uncertain transfer repeated inventory mutation");
  }
  offer.SourceIsBag=true;offer.Operation=1;
  foreach(int failAt in new[]{1,2})foreach(bool after in new[]{false,true}){
   var fault=new EntityPlayerLocal();if(after)fault.Buffs.ThrowAfterWrite=failAt;else fault.Buffs.ThrowBeforeWrite=failAt;
   A(RebirthMusicOwnerTransfer.Apply(fault,creation,offer)==RebirthMusicOwnerTransferResult.Pending,"receipt exception escaped");
   int writes=failAt==1?0:1;A(fault.bag.Writes==writes,"receipt failure crossed mutation boundary");
   fault.Buffs.ThrowAfterWrite=0;fault.Buffs.ThrowBeforeWrite=0;
   var retry=RebirthMusicOwnerTransfer.Apply(fault,creation,offer);
   bool safeNewAttempt=failAt==1&&!after;
   A(fault.bag.Writes==(safeNewAttempt?1:writes),"receipt failure repeated inventory");
   A(retry==((safeNewAttempt||(failAt==2&&after))?RebirthMusicOwnerTransferResult.Applied:RebirthMusicOwnerTransferResult.Pending),"wrong receipt recovery outcome");
  }
  var owner=new EntityPlayerLocal();A(RebirthMusicOwnerTransfer.Apply(owner,Guid.NewGuid().ToString("N"),offer)==RebirthMusicOwnerTransferResult.Pending&&owner.bag.Writes==0,"wrong owner applied");
  A(RebirthMusicOwnerTransfer.Apply(owner,creation,offer)==RebirthMusicOwnerTransferResult.Applied&&owner.bag.Items[0].count==1&&owner.bag.Writes==1&&owner.Buffs.Receipt==1,"first deposit failed");
  A(RebirthMusicOwnerTransfer.Apply(owner,creation,offer)==RebirthMusicOwnerTransferResult.Applied&&owner.bag.Writes==1,"duplicate deposit mutated inventory");
  owner.Buffs.Receipt=-1;A(RebirthMusicOwnerTransfer.Apply(owner,creation,offer)==RebirthMusicOwnerTransferResult.Rejected&&owner.bag.Writes==1,"rejected receipt retried");
  owner=new EntityPlayerLocal();offer.Operation=2;RebirthMusicLibraryService.Full=true;A(RebirthMusicOwnerTransfer.Apply(owner,creation,offer)==RebirthMusicOwnerTransferResult.Pending&&owner.bag.Writes==0&&owner.Buffs.Writes==0,"full bag changed receipt");
  RebirthMusicLibraryService.Full=false;A(RebirthMusicOwnerTransfer.Apply(owner,creation,offer)==RebirthMusicOwnerTransferResult.Applied&&owner.bag.Items[0].count==3,"withdraw failed");
  A(RebirthMusicOwnerTransfer.Apply(owner,creation,offer)==RebirthMusicOwnerTransferResult.Applied&&owner.bag.Writes==1,"duplicate withdrawal mutated inventory");
 }}
}