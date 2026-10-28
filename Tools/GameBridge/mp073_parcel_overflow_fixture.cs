using System;using System.IO;using System.Linq;using System.Text;using System.Xml.Linq;using System.Collections.Generic;
// Actual overflow qualification code; native ECD/bag grammar, journal I/O,
// snapshot descriptor and final shared native-frame qualifier are explicit adapters.
class ItemStack{internal int count;internal ItemStack(int value){count=value;}internal bool IsEmpty()=>count==0;internal static ItemStack[] Clone(ItemStack[] items)=>items.Select(i=>new ItemStack(i.count)).ToArray();}
static class RebirthStationGridIngredients{internal static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a.count==b.count;}
static class RebirthCamperInventorySnapshot{internal static byte[] Capture(ItemStack[] items)=>items.Select(i=>(byte)i.count).ToArray();}
class RebirthCamperRemovalParcelPlan{internal Guid Operation=Guid.NewGuid(),Vehicle=Guid.NewGuid();internal string Creation="creation",Binding="binding";internal ItemStack[] Overflow=new[]{new ItemStack(3)};internal static byte[] Bytes(XElement node)=>Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting));}
static class GameIO{internal static string GetSaveGameDir()=>"save";}
class RebirthCamperTransferJournal{internal enum Phase{Prepared,Uncertain,Applied}internal class Record{internal Guid Operation,Vehicle;internal string OwnerCreation;internal int Revision;internal byte[] Before,After;internal Phase State;}string path;internal RebirthCamperTransferJournal(string value){path=value;}internal Record Read(Guid op)=>ReadExisting(path,op);internal Record Prepare(Guid op,Guid vehicle,string creation,int revision,byte[] before,byte[] after){var old=Read(op);if(old!=null)return old;return Records[path]=new Record{Operation=op,Vehicle=vehicle,OwnerCreation=creation,Revision=revision,Before=before,After=after};}internal static Dictionary<string,Record> Records=new Dictionary<string,Record>();internal static Record ReadExisting(string path,Guid op)=>Records.TryGetValue(path,out var record)?record:null;}
static class RebirthCookingRemoteAdmission{internal static XElement ReadProtocolXml(string text,int maximum){if(Encoding.UTF8.GetByteCount(text)>maximum)throw new Exception();return XElement.Parse(text);}}
static class RebirthStationPublicationRecord{internal static string Digest(byte[] bytes){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}}
static class RebirthCookingDropSnapshot{internal static bool Valid(XElement node,int length)=>node?.Name=="snapshot"&&(int?)node.Attribute("length")==length;}
enum StreamModeRead{Persistency}class XUiC_ItemStack{internal enum StackLocationTypes{Backpack}}
class Reader:IDisposable{internal Stream Stream;internal void SetBaseStream(Stream stream){Stream=stream;}internal byte ReadByte(){int b=Stream.ReadByte();if(b<0)throw new EndOfStreamException();return (byte)b;}public void Dispose(){}}
class Pool{internal Reader AllocSync(bool flag)=>new Reader();}static class MemoryPools{internal static Pool poolBinaryReader=new Pool();}
class EntityCreationData{internal int id,entityClass;internal MemoryStream entityData,bagData;internal void read(Reader reader,StreamModeRead mode){id=reader.ReadByte();entityClass=reader.ReadByte();int count=reader.ReadByte();byte[] bytes=new byte[count];for(int i=0;i<count;i++)bytes[i]=reader.ReadByte();bagData=new MemoryStream(bytes);}}
class Grid{internal ItemStack[] items;}class Bag{internal Grid ItemGrid;internal static Bag Read(Reader reader,XUiC_ItemStack.StackLocationTypes where,StreamModeRead mode,object ignored){int count=reader.ReadByte();var items=new ItemStack[count];for(int i=0;i<count;i++)items[i]=new ItemStack(reader.ReadByte());return new Bag{ItemGrid=new Grid{items=items}};}}
static class RebirthLogisticsBackpackPublication{internal static bool Ready=true;internal static object ReadSavedPlanImage(byte[] frame,ItemStack[] slots)=>Ready?new object():null;}
static class RebirthCamperRemovalParcelOverflow
{
 private static RebirthCamperTransferJournal Journal(string suffix)=>new RebirthCamperTransferJournal(Path.Combine("save","REBIRTH","RemovalParcelOverflow"+suffix));
 // ACTUAL_IDENTITIES
 // ACTUAL_QUALIFIERS
 internal static bool Retained(RebirthCamperRemovalParcelPlan plan)=>RetainedNonempty(plan)!=null;
 internal static byte[] Original(RebirthCamperRemovalParcelPlan plan)=>Image(plan);
 internal static bool Validate(RebirthCamperRemovalParcelPlan plan,RebirthCamperTransferJournal.Record record)=>Accepted(plan,record,out _);
}
static class Program
{
 static int count;static RebirthCamperRemovalParcelPlan plan=new RebirthCamperRemovalParcelPlan();
 static void Check(bool value){count++;if(!value)throw new Exception("Actual parcel overflow assertion "+count);}
 static RebirthCamperTransferJournal.Record Record(byte[] bytes)=>new RebirthCamperTransferJournal.Record{Operation=plan.Operation,Vehicle=plan.Vehicle,OwnerCreation=plan.Creation,Before=bytes,After=(byte[])bytes.Clone(),State=RebirthCamperTransferJournal.Phase.Applied};
 static XElement Accepted(byte[] frame)=>new XElement("removalParcelOverflowAccepted",new XAttribute("version",1),new XAttribute("operation",plan.Operation.ToString("N")),new XAttribute("binding",plan.Binding),new XAttribute("entity",7),new XAttribute("class",8),new XElement("native",Convert.ToBase64String(frame)),new XElement("snapshot",new XAttribute("length",frame.Length)));
 static void Save(string suffix,RebirthCamperTransferJournal.Record record)=>RebirthCamperTransferJournal.Records[Path.Combine("save","REBIRTH","RemovalParcelOverflow"+suffix)]=record;
 static void Main()
 {
  Check(!RebirthCamperRemovalParcelOverflow.Retained(plan));Save("",Record(RebirthCamperRemovalParcelOverflow.Original(plan)));Check(RebirthCamperRemovalParcelOverflow.Retained(plan));
  var original=RebirthCamperTransferJournal.Records[Path.Combine("save","REBIRTH","RemovalParcelOverflow")];original.Before=new byte[]{0};Check(!RebirthCamperRemovalParcelOverflow.Retained(plan));RebirthCamperTransferJournal.Records.Clear();
  Check(RebirthCamperRemovalParcelOverflow.RetainUnattempted(plan));Check(RebirthCamperRemovalParcelOverflow.Retained(plan));
  var retained=RebirthCamperTransferJournal.Records[Path.Combine("save","REBIRTH","RemovalParcelOverflow")];retained.State=RebirthCamperTransferJournal.Phase.Uncertain;Check(!RebirthCamperRemovalParcelOverflow.RetainUnattempted(plan));retained.State=RebirthCamperTransferJournal.Phase.Prepared;retained.After=new byte[]{1};Check(!RebirthCamperRemovalParcelOverflow.RetainUnattempted(plan));RebirthCamperTransferJournal.Records.Clear();
  byte[] native={7,8,3,2,3,0};var accepted=Record(RebirthCamperRemovalParcelPlan.Bytes(Accepted(native)));
  Check(RebirthCamperRemovalParcelOverflow.Validate(plan,accepted));
  var wrong=Accepted(native);wrong.SetAttributeValue("entity",9);Check(!RebirthCamperRemovalParcelOverflow.Validate(plan,Record(RebirthCamperRemovalParcelPlan.Bytes(wrong))));
  wrong=Accepted(new byte[]{7,8,3,2,3,1});Check(!RebirthCamperRemovalParcelOverflow.Validate(plan,Record(RebirthCamperRemovalParcelPlan.Bytes(wrong))));
  wrong=Accepted(new byte[]{7,8,3,2,2,0});Check(!RebirthCamperRemovalParcelOverflow.Validate(plan,Record(RebirthCamperRemovalParcelPlan.Bytes(wrong))));
  wrong=Accepted(native.Concat(new byte[]{99}).ToArray());Check(!RebirthCamperRemovalParcelOverflow.Validate(plan,Record(RebirthCamperRemovalParcelPlan.Bytes(wrong))));
  RebirthLogisticsBackpackPublication.Ready=false;Check(!RebirthCamperRemovalParcelOverflow.Validate(plan,accepted));RebirthLogisticsBackpackPublication.Ready=true;
  var bad=Record(accepted.Before);bad.Operation=Guid.NewGuid();Check(!RebirthCamperRemovalParcelOverflow.Validate(plan,bad));
  bad=Record(accepted.Before);bad.Revision=1;Check(!RebirthCamperRemovalParcelOverflow.Validate(plan,bad));
  Save("",Record(RebirthCamperRemovalParcelOverflow.Original(plan)));Save("Accepted",accepted);
  var publication=new XElement("removalParcelOverflowPublished",new XAttribute("operation",plan.Operation.ToString("N")),new XAttribute("binding",plan.Binding),new XAttribute("accepted",RebirthStationPublicationRecord.Digest(accepted.Before)),new XAttribute("region",Path.GetFullPath("native-region")),new XAttribute("payload",new string('a',64)),new XAttribute("x",1),new XAttribute("z",2),new XAttribute("generation",3));
  Save("Published",Record(RebirthCamperRemovalParcelPlan.Bytes(publication)));Check(RebirthCamperRemovalParcelOverflow.Terminal(plan,out var proof)&&proof.Length==64);
  publication.SetAttributeValue("generation",0);Save("Published",Record(RebirthCamperRemovalParcelPlan.Bytes(publication)));Check(!RebirthCamperRemovalParcelOverflow.Terminal(plan,out _));publication.SetAttributeValue("generation",3);
  publication.SetAttributeValue("x","01");Save("Published",Record(RebirthCamperRemovalParcelPlan.Bytes(publication)));Check(!RebirthCamperRemovalParcelOverflow.Terminal(plan,out _));publication.SetAttributeValue("x",1);
  publication.SetAttributeValue("payload",new string('z',64));Save("Published",Record(RebirthCamperRemovalParcelPlan.Bytes(publication)));Check(!RebirthCamperRemovalParcelOverflow.Terminal(plan,out _));
  plan.Overflow=new ItemStack[0];Save("",Record(RebirthCamperRemovalParcelOverflow.Original(plan)));Save("Published",Record(RebirthCamperRemovalParcelPlan.Bytes(new XElement("removalParcelOverflowPublished",new XAttribute("operation",plan.Operation.ToString("N")),new XAttribute("binding",plan.Binding),new XAttribute("empty",true)))));Check(RebirthCamperRemovalParcelOverflow.Terminal(plan,out _));
  RebirthCamperTransferJournal.Records[Path.Combine("save","REBIRTH","RemovalParcelOverflow")].State=RebirthCamperTransferJournal.Phase.Uncertain;Check(!RebirthCamperRemovalParcelOverflow.Terminal(plan,out _));
  Console.WriteLine(count+" actual parcel overflow assertions passed");
 }
}
