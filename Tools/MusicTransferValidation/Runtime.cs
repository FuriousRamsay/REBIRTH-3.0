using System;
using System.IO;
using System.Collections.Generic;
class Program
{
 static void Check(bool b,string message){if(!b)throw new Exception(message);}
 static void Main(){
  string origin=Guid.NewGuid().ToString("N");
  var player=new EntityPlayerLocal();player.bag.slots[0]=new ItemStack(new ItemValue(),2);
  var offer=new RebirthMusicTransferState{TransactionId=Guid.NewGuid().ToString("N"),CreationId=origin,Operation=1,SourceIsBag=true,ItemId="tape",ItemData="AQAAAA=="};
  Check(RebirthMusicOwnerTransfer.Apply(player,Guid.NewGuid().ToString(),offer)==RebirthMusicOwnerTransferResult.Pending&&player.bag.slots[0].count==2,"wrong character mutated");
  Check(RebirthMusicOwnerTransfer.Apply(player,origin,offer)==RebirthMusicOwnerTransferResult.Applied&&player.bag.slots[0].count==1,"insert failed");
  Check(RebirthMusicOwnerTransfer.Apply(player,origin,offer)==RebirthMusicOwnerTransferResult.Applied&&player.bag.slots[0].count==1,"duplicate removed twice");
  offer.TransactionId=Guid.NewGuid().ToString("N");offer.SourceIndex=1;
  Check(RebirthMusicOwnerTransfer.Apply(player,origin,offer)==RebirthMusicOwnerTransferResult.Rejected,"missing source accepted");
  player.bag.slots[1]=new ItemStack(new ItemValue(),1);
  Check(RebirthMusicOwnerTransfer.Apply(player,origin,offer)==RebirthMusicOwnerTransferResult.Rejected&&player.bag.slots[1].count==1,"rejected retry mutated");
  offer.TransactionId=Guid.NewGuid().ToString("N");offer.Operation=2;
  Check(RebirthMusicOwnerTransfer.Apply(player,origin,offer)==RebirthMusicOwnerTransferResult.Pending,"full bag discarded transfer");
  player.bag.slots[1]=null;
  Check(RebirthMusicOwnerTransfer.Apply(player,origin,offer)==RebirthMusicOwnerTransferResult.Applied&&player.bag.slots[1].count==1,"return retry failed");
  player.bag.slots[1]=null;
  Check(RebirthMusicOwnerTransfer.Apply(player,origin,offer)==RebirthMusicOwnerTransferResult.Applied&&player.bag.slots[1]==null,"duplicate return granted twice");
  player.inventory.slots=new ItemStack[4];
  offer.Operation=1;offer.SourceIsBag=false;
  foreach(int excluded in new[]{2,3}){
   offer.TransactionId=Guid.NewGuid().ToString("N");offer.SourceIndex=excluded;
   player.inventory.slots[excluded]=new ItemStack(new ItemValue(),1);
   Check(RebirthMusicOwnerTransfer.Apply(player,origin,offer)==RebirthMusicOwnerTransferResult.Rejected&&player.inventory.slots[excluded].count==1,"locked/dummy slot consumed");
  }
  offer.TransactionId=Guid.NewGuid().ToString("N");offer.SourceIndex=1;
  player.inventory.slots[1]=new ItemStack(new ItemValue(),2);
  Check(RebirthMusicOwnerTransfer.Apply(player,origin,offer)==RebirthMusicOwnerTransferResult.Applied&&player.inventory.slots[1].count==1,"last usable belt slot rejected");
  Console.WriteLine("PASS: 11 production owner-transfer checks; inventory/save/network adapters stubbed.");
  ReceiptChecks();
 }
 static byte[] Receipt(string id,float value,bool duplicate=false){
  using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){
   writer.Write((byte)EntityBuffs.Version);writer.Write((ushort)0);writer.Write((ushort)(duplicate?2:1));
   writer.Write("rbMusic_"+id);writer.Write(value);
   if(duplicate){writer.Write("rbMusic_"+id);writer.Write(value);}
   return stream.ToArray();
  }
 }
 static void ReceiptChecks(){
  string id=Guid.NewGuid().ToString("N");byte[] bytes=Receipt(id,1);
  Check(RebirthMusicReceiptReader.Contains(bytes,id,true),"valid applied receipt");
  Check(RebirthMusicReceiptReader.Contains(Receipt(id,-1),id,false),"valid rejected receipt");
  Check(!RebirthMusicReceiptReader.Contains(bytes,id,false),"wrong outcome accepted");
  Check(!RebirthMusicReceiptReader.Contains(bytes,Guid.NewGuid().ToString("N"),true),"wrong transaction accepted");
  Check(!RebirthMusicReceiptReader.Contains(Receipt(id,1,true),id,true),"duplicate receipt accepted");
  Check(!RebirthMusicReceiptReader.Contains(Receipt(id,float.NaN),id,true),"NaN accepted");
  for(int size=0;size<bytes.Length;size++)Check(!RebirthMusicReceiptReader.Contains(bytes.AsSpan(0,size).ToArray(),id,true),"truncation accepted");
  var trailing=new byte[bytes.Length+1];Array.Copy(bytes,trailing,bytes.Length);
  Check(!RebirthMusicReceiptReader.Contains(trailing,id,true),"trailing bytes accepted");
  bytes[0]=255;Check(!RebirthMusicReceiptReader.Contains(bytes,id,true),"unknown version accepted");
  Console.WriteLine("PASS: receipt outcomes, identity, duplicates, NaN, all truncations, trailing bytes and version. Buff decoder stubbed; no disk/network.");
 }
}
public static class EntityBuffs{public const byte Version=3;}
public class BuffValue{public void Read(BinaryReader reader,int version){throw new InvalidDataException("Active buffs not used by this fixture");}}
public class ItemClass{public string GetItemName()=>"tape";}
public class ItemValue{public ItemClass ItemClass=new ItemClass();public bool IsEmpty()=>false;public static ItemValue ReadOrNull(BinaryReader r){if(r.ReadInt32()!=1)throw new InvalidDataException();return new ItemValue();}}
public class ItemStack{public ItemValue itemValue;public int count;public static ItemStack Empty=new ItemStack(null,0);public ItemStack(ItemValue v,int n){itemValue=v;count=n;}public bool IsEmpty()=>count<=0;public ItemStack Clone()=>new ItemStack(itemValue,count);}
public class Slots{public ItemStack[] slots=new ItemStack[2];public ItemStack[] GetSlots()=>slots;public void SetSlot(int i,ItemStack s){slots[i]=s;}public void SetItem(int i,ItemStack s){slots[i]=s;}}
public class Buffs{Dictionary<string,float> values=new Dictionary<string,float>();public float GetCustomVar(string k)=>values.TryGetValue(k,out var v)?v:0;public void SetCustomVar(string k,float v,bool sync){values[k]=v;}}
public class EntityPlayerLocal{public Buffs Buffs=new Buffs();public Slots bag=new Slots(),inventory=new Slots();public bool IsSpawned()=>true;public bool IsDead()=>false;}
public static class RebirthSurvivorMode{public static bool IsEnabledForCurrentWorld()=>true;}
public static class RebirthToolbeltCapacity{public static int GetOwnedSlotCount(EntityPlayerLocal player,int count)=>Math.Max(0,Math.Min(2,count-1));}
public static class RebirthMusicLibraryService{
 public static bool IsMusicCassette(string id)=>id=="tape";
 public static string Encode(ItemValue value)=>"AQAAAA==";
 public static int FindReturnSlot(ItemStack[] slots,ItemValue value,out ItemStack replacement){replacement=null;for(int i=0;i<slots.Length;i++)if(slots[i]==null||slots[i].IsEmpty()){replacement=new ItemStack(value,1);return i;}return -1;}
}
