using System;using System.IO;using System.Linq;using System.Text;using System.Xml.Linq;using System.Globalization;
// Explicit adapters: rich item codec/equivalence, native grid grammar, native
// owner file I/O and authoritative capacity. No actual game save is claimed.
class ItemClass{internal int MaxCount=5;}
class ItemValue{internal string Name="item";internal ItemClass ItemClass=new ItemClass();internal ItemValue Clone()=>new ItemValue{Name=Name};}
class ItemStack
{internal ItemValue itemValue;internal int count;internal ItemStack(ItemValue item,int quantity){itemValue=item;count=quantity;}internal ItemStack Clone()=>new ItemStack(itemValue.Clone(),count);internal bool IsEmpty()=>count<1||itemValue.Name=="none";internal bool CanMoveTo(XUiC_ItemStack.StackLocationTypes where)=>true;internal bool CanStackPartlyWith(ItemStack other,out int available){available=5-count;return itemValue.Name==other.itemValue.Name&&available>0;}}
class XUiC_ItemStack{internal enum StackLocationTypes{Backpack,ToolBelt}}
static class RebirthNativeItemCodec{internal static string Encode(ItemValue item)=>item.Name;}
class EntityPlayer{internal int Owned=1;}
class PlayerDataFile{internal MemoryStream inventoryData;}
// Detached native header/dimension admission adapter; production uses the bounded real native reader.
static class RebirthPlayerDataInventory{internal static ItemStack[] ReadSlots(PlayerDataFile saved,bool bag){var bytes=saved.inventoryData.ToArray();return bytes.Length>=2&&bytes[0]==1&&bytes[1]<=20?new ItemStack[bytes[1]]:null;}}
static class RebirthToolbeltCapacity{internal static bool IsRejectedIncoming(ItemStack stack)=>false;internal static int GetOwnedSlotCount(EntityPlayer actor,int width)=>Math.Min(width,actor.Owned);}
class RebirthCamperEnergyActorImage{internal ItemStack[] Bag,Belt;internal bool[] BagLocks;internal RebirthCamperEnergyActorImage Copy()=>new RebirthCamperEnergyActorImage{Bag=Bag.Select(x=>x.Clone()).ToArray(),Belt=Belt.Select(x=>x.Clone()).ToArray(),BagLocks=(bool[])BagLocks.Clone()};}
static class RebirthCamperEnergyItems{internal static bool[] Locks(bool[] value)=>value==null?null:(bool[])value.Clone();}
enum StreamModeRead{Persistency}
class Reader:IDisposable{internal Stream BaseStream;internal void SetBaseStream(Stream stream){BaseStream=stream;}internal byte ReadByte(){int value=BaseStream.ReadByte();if(value<0)throw new EndOfStreamException();return (byte)value;}public void Dispose(){}}
class Pool{internal Reader AllocSync(bool flag)=>new Reader();}
static class MemoryPools{internal static Pool poolBinaryReader=new Pool();}
class ItemStackGrid{internal static bool NoLocks;internal int Length;internal bool[] SlotLocks;internal static ItemStackGrid Read(Reader reader,StreamModeRead mode){int width=reader.ReadByte();var locks=new bool[width];for(int i=0;i<width;i++)locks[i]=reader.ReadByte()!=0;return new ItemStackGrid{Length=width,SlotLocks=NoLocks?null:locks};}}
static class RebirthRemoteResourceBlockRefund
{
 internal static bool Plan(RebirthCamperEnergyActorImage before,ItemStack[] payload,out RebirthCamperEnergyActorImage after){after=before.Copy();foreach(var stack in payload){var copy=stack.Clone();Insert(after.Bag,before.BagLocks,after.Bag.Length,copy);if(copy.count!=0){after=null;return false;}}return true;}
 // ACTUAL_POLICY
 internal static bool Saved(EntityPlayer actor,PlayerDataFile saved,XElement policy)=>SavedBeltPolicy(actor,saved,policy);
}
static class RebirthRemoteResourceJournalFormat{internal static int FieldLimit(string name)=>24*1024*1024;}
static class RebirthRemoteResourcePlacedStationReference{internal class Image{internal Guid Job;}internal static bool TryRead(string s,out Image image){image=null;return false;}}
static class RebirthRemoteResourceDurableJournal
{
 static bool Hex(string text,int length)=>text?.Length==length&&text.All(Uri.IsHexDigit);
 // ACTUAL_SCHEMA
}
static class Program
{
 static int count;static ItemStack Stack(int n,string name="item")=>new ItemStack(new ItemValue{Name=name},n);
 static RebirthCamperEnergyActorImage Frame()=>new RebirthCamperEnergyActorImage{Bag=new[]{Stack(5),Stack(5)},Belt=new[]{Stack(0),Stack(0)},BagLocks=new[]{false,false}};
 static void Check(bool value){count++;if(!value)throw new Exception("Actual Block belt/schema assertion "+count);}
 static XElement Policy(int owned=1,bool first=false)=>RebirthRemoteResourceBlockRefund.BeltPolicy(2,owned,new[]{first,false});
 static XElement Refund(bool block=true,bool adopted=false,XElement policy=null){var body=new XElement(block?"blockRefund":"repairRefund",new XAttribute("original",new string('a',64)),new XAttribute("source",Guid.NewGuid().ToString("N")),new XAttribute("child",Guid.NewGuid().ToString("N")),new XAttribute("binding",new string('b',64)),new XAttribute("noUseProof",new string('c',64)),new XElement("beforeActor","before"),new XElement("afterActor","after"));if(adopted)body.SetAttributeValue("deaths",1);if(policy!=null)body.Add(new XElement(policy));return new XElement("ownerRefund",new XAttribute("nativeBeforeProof",new string('d',64)),body);}
 static bool Schema(XElement node){try{RebirthRemoteResourceDurableJournal.ValidateSettlementElement(node);return true;}catch(InvalidDataException){return false;}}
 static PlayerDataFile Saved(params byte[] bytes)=>new PlayerDataFile{inventoryData=new MemoryStream(bytes)};
 static void Main()
 {
  Check(RebirthRemoteResourceBlockRefund.Plan(Frame(),new[]{Stack(3)},Policy(),out var after)&&after.Belt[0].count==3&&after.Bag[0].count==5);
  var nativeEmpty=Frame();nativeEmpty.Belt[0]=Stack(1,"none");Check(RebirthRemoteResourceBlockRefund.Plan(nativeEmpty,new[]{Stack(3)},Policy(),out after)&&after.Belt[0].count==3&&after.Belt[0].itemValue.Name=="item");
  Check(!RebirthRemoteResourceBlockRefund.Plan(Frame(),new[]{Stack(3)},Policy(0),out after));
  Check(!RebirthRemoteResourceBlockRefund.Plan(Frame(),new[]{Stack(3)},Policy(1,true),out after));
  var frame=Frame();frame.Belt[0]=Stack(5);Check(!RebirthRemoteResourceBlockRefund.Plan(frame,new[]{Stack(3)},Policy(1),out after));
  frame=Frame();frame.Bag[0]=Stack(0);frame.Belt[0]=Stack(4);Check(RebirthRemoteResourceBlockRefund.Plan(frame,new[]{Stack(3)},Policy(),out after)&&after.Belt[0].count==5&&after.Bag[0].count==2);
  frame=Frame();frame.BagLocks[0]=true;Check(RebirthRemoteResourceBlockRefund.Plan(frame,new[]{Stack(2)},Policy(),out after)&&after.Belt[0].count==2);
  Check(!RebirthRemoteResourceBlockRefund.Plan(Frame(),new[]{Stack(3)},null,out after));
  var bad=Policy();bad.SetAttributeValue("width","02");Check(!RebirthRemoteResourceBlockRefund.ReadBeltPolicy(bad,out _,out _,out _));
  bad=Policy();bad.SetAttributeValue("extra",1);Check(!RebirthRemoteResourceBlockRefund.ReadBeltPolicy(bad,out _,out _,out _));
  Check(RebirthRemoteResourceBlockRefund.Saved(new EntityPlayer(),Saved(1,2,0,0,0),Policy()));
  ItemStackGrid.NoLocks=true;Check(RebirthRemoteResourceBlockRefund.Saved(new EntityPlayer(),Saved(1,2,0,0,0),Policy()));
  Check(!RebirthRemoteResourceBlockRefund.Saved(new EntityPlayer(),Saved(1,2,0,0,0),Policy(1,true)));ItemStackGrid.NoLocks=false;
  Check(!RebirthRemoteResourceBlockRefund.Saved(new EntityPlayer(),Saved(1,2,1,0,0),Policy()));
  Check(!RebirthRemoteResourceBlockRefund.Saved(new EntityPlayer{Owned=0},Saved(1,2,0,0,0),Policy()));
  Check(!RebirthRemoteResourceBlockRefund.Saved(new EntityPlayer(),Saved(1,2,0,0,0,99),Policy()));
  Check(!RebirthRemoteResourceBlockRefund.Saved(new EntityPlayer(),Saved(2,2,0,0,0),Policy()));
  Check(Schema(Refund()));
  Check(Schema(Refund(true,true,Policy())));
  Check(!Schema(Refund(false,false,Policy())));
  bad=Refund(true,true,Policy());bad.Element("blockRefund").SetAttributeValue("deaths",-1);Check(!Schema(bad));
  bad=Refund(true,true,Policy());bad.Element("blockRefund").Element("beltPolicy").Value="00x";Check(!Schema(bad));
  bad=Refund(true,true,Policy());bad.Element("blockRefund").Add(new XElement("unexpected"));Check(!Schema(bad));
  Console.WriteLine(count+" actual Block belt/native-policy/schema assertions passed");
 }
}
