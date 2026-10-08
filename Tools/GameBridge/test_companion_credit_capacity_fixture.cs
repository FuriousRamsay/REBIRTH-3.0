using System;
class Count {public int Value=5;}
class ItemClass {public static ItemClass Item=new ItemClass();public Count Stacknumber=new Count();public static ItemClass GetForId(int t){return Item;}public static ItemValue GetItem(string k,bool x){return new ItemValue {type=k=="missing"?0:1};}}
class ItemValue {public int type=1;public string Variant="default";public bool IsEmpty(){return type==0;}}
class ItemStack {public ItemValue itemValue;public int count;public ItemStack(ItemValue v,int c){itemValue=v;count=c;}public bool IsEmpty(){return count<=0;}public bool CanStackPartlyWith(ItemStack other,out int amount){amount=itemValue.type==other.itemValue.type&&itemValue.Variant==other.itemValue.Variant?Math.Max(0,Math.Min(other.count,ItemClass.Item.Stacknumber.Value-count)):0;return amount>0;}}
class PackedBoolArray {public bool[] Values;public PackedBoolArray(params bool[] b){Values=b;}}
class Bag {public ItemStack[] Items;public PackedBoolArray LockedSlots;public ItemStack[] GetSlots(){return Items;}}
class EntityPlayer {public Bag bag=new Bag();}
static class LogisticsPreviewService {public static bool IsLocked(PackedBoolArray b,int i){return b!=null&&i<b.Values.Length&&b.Values[i];}}
class Subject {EntityPlayer player;public Subject(EntityPlayer p){player=p;}
// PRODUCTION_METHOD
}
class Check {static void Assert(bool b,string m){if(!b)throw new Exception(m);}static void Main(){var p=new EntityPlayer();p.bag.Items=new[]{new ItemStack(new ItemValue {Variant="partial liquid"},1),new ItemStack(new ItemValue(),3),null,null};p.bag.LockedSlots=new PackedBoolArray(false,true,true,false);var s=new Subject(p);Assert(s.GetAvailableCreditQuantity("item")==7,"incompatible variant or locks counted incorrectly");p.bag.Items=new[]{new ItemStack(new ItemValue {Variant="partial liquid"},1)};Assert(s.GetAvailableCreditQuantity("item")==0,"incompatible-only space accepted");p.bag.Items=new ItemStack[2];p.bag.LockedSlots=null;ItemClass.Item.Stacknumber.Value=int.MaxValue;Assert(s.GetAvailableCreditQuantity("item")==int.MaxValue,"capacity overflow");Assert(s.GetAvailableCreditQuantity("missing")==0,"invalid item accepted");p.bag=null;Assert(s.GetAvailableCreditQuantity("item")==0,"missing bag accepted");Assert(new Subject(null).GetAvailableCreditQuantity("item")==0,"missing player accepted");Console.WriteLine("PASS: metadata-aware capacity, locked top-ups, protected empties, incompatible-only rejection, overflow and unavailable inventory.");}}
