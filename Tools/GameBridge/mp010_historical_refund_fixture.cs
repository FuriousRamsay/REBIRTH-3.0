// AUTHORED_NOT_RUN. Actual historical witness; saved native owner/codec/journal are explicit adapters.
using System;using System.IO;using System.Linq;using System.Text;using System.Xml.Linq;using System.Collections.Generic;
class ItemStack{internal int count;internal string Rich="rich";internal bool IsEmpty()=>count==0;}
class World{internal bool Remote;internal EntityPlayer Player;internal State worldState=new State();internal bool IsRemote()=>Remote;internal object GetEntity(int id)=>Player;}
class State{internal string Guid="native-world";}
class EntityPlayer{internal World world;internal int entityId=1,Died=2;internal string Owner="owner",Creation="creation";internal bool Dead;internal bool IsDead()=>Dead;}
class RebirthStablePlayerIdentity{internal string StorageKey="owner-key",CanonicalId="owner";internal static bool TryResolveServerEntity(EntityPlayer actor,out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity{CanonicalId=actor.Owner};return true;}}
static class RebirthWorldCharacterService{internal static bool TryGetIdentity(EntityPlayer actor,out RebirthStablePlayerIdentity identity)=>RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out identity);}
static class RemoteResourceWorldIdentity{internal static bool TryGet(World world,out string id){id="source-world";return world!=null;}}
static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayer actor)=>actor.Creation;}
static class RebirthOriginalActionScope{internal static bool Matches(EntityPlayer actor,string owner,string creation)=>owner=="owner-key"&&actor.Owner=="owner"&&actor.Creation==creation;}
class RebirthRemoteResourceNetworkScope{internal static bool Valid=true;internal bool Current=>Valid;internal static RebirthRemoteResourceNetworkScope Capture(EntityPlayer actor)=>new RebirthRemoteResourceNetworkScope();}
static class RebirthCookingRemoteAdmission{internal static XElement ReadProtocolXml(string text,int limit){if(text.Length>limit)throw new Exception();return XElement.Parse(text);}}
enum RemoteResourceClientOperation{CookingPull=5}
class RebirthCamperEnergyActorImage
{
 internal ItemStack[] Bag,Belt=new ItemStack[0];internal ItemStack Cursor=new ItemStack();internal bool[] BagLocks={false};
 internal static RebirthCamperEnergyActorImage Read(BinaryReader reader)=>new RebirthCamperEnergyActorImage{Bag=new[]{new ItemStack{count=reader.ReadInt32()}}};
}
static class RebirthRemoteResourceDurableJournal
{
 internal sealed class Record{internal string Owner="owner-key",Creation="creation",World="source-world";internal Guid Operation;internal int Phase=2;internal XElement Root;internal XElement Copy()=>new XElement(Root);}
 internal static Record Saved;internal static int Advances;internal static bool Fail,ChangeScope;
 internal static Guid Operation(string world,string owner,string creation,ulong epoch,ulong request,RemoteResourceClientOperation kind)=>world=="source-world"&&owner=="owner-key"&&creation=="creation"&&request==5?Saved.Operation:Guid.Empty;
 internal static bool TryRead(string owner,Guid operation,out Record record,out bool unknown){record=Saved;unknown=false;return operation==Saved.Operation;}
 internal static string Digest(string value)=>"proof:"+value;
 internal static bool TryAdvance(Record original,Action<XElement> write,Func<bool> current,out Record next){next=null;Advances++;if(ChangeScope)RebirthRemoteResourceNetworkScope.Valid=false;if(Fail||!current())return false;write(original.Root);original.Phase=(int)original.Root.Attribute("phase");next=original;return true;}
}
internal static partial class RebirthRemoteResourceDurableOutcome
{
 static ItemStack[] ReadSlots(XElement node)=>node.Value.Split(',').Select(value=>new ItemStack{count=int.Parse(value)}).ToArray();
 static int Totals(IEnumerable<ItemStack> items)=>items.Sum(item=>item.count);static bool SameTotals(int left,int right)=>left==right;
}
// ACTUAL_HISTORICAL_WITNESS
class Program
{
 static string Actor(int count){using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){writer.Write(count);writer.Flush();return Convert.ToBase64String(stream.ToArray());}}
 static void Need(bool value,string reason){if(!value)throw new Exception(reason);}
 static void Main(){foreach(int sourcePhase in new[]{2,3})for(int variant=0;variant<16;variant++)
 {
  var world=new World();var actor=new EntityPlayer{world=world};world.Player=actor;RebirthRemoteResourceNetworkScope.Valid=true;RebirthRemoteResourceDurableJournal.Fail=RebirthRemoteResourceDurableJournal.ChangeScope=false;RebirthRemoteResourceDurableJournal.Advances=0;
  var root=new XElement("source",new XAttribute("deaths",1),new XAttribute("kind",5),new XAttribute("phase",sourcePhase),new XElement("removed","3"));var record=new RebirthRemoteResourceDurableJournal.Record{Operation=Guid.NewGuid(),Phase=sourcePhase,Root=root};RebirthRemoteResourceDurableJournal.Saved=record;
  var child=Guid.NewGuid();var image=new XElement("cookingGridOriginal",new XAttribute("version",1),new XAttribute("action","fetched-return"),new XAttribute("fetchId",5),new XAttribute("sourceDeaths",1),new XAttribute("deaths",2),new XAttribute("world","native-world"),new XAttribute("owner","owner"),new XAttribute("creation","creation"),new XElement("fetched","3"),new XElement("beforeGrid",string.Join(",",Enumerable.Repeat("0",12))),new XElement("afterGrid",string.Join(",",Enumerable.Repeat("0",12))),new XElement("beforeActor",Actor(2)),new XElement("afterActor",Actor(5)));
  switch(variant){case 1:image.SetAttributeValue("sourceDeaths",2);break;case 2:image.SetAttributeValue("deaths",1);break;case 3:image.SetAttributeValue("owner","other");break;case 4:image.SetAttributeValue("creation","other");break;case 5:image.SetAttributeValue("world","other");break;case 6:image.Element("fetched").Value="4";break;case 7:image.Element("afterActor").Value=Actor(6);break;case 8:image.Element("afterGrid").Value="1";break;case 9:root.Add(new XElement("ownerAction",new XAttribute("domain","other")));break;case 10:RebirthRemoteResourceNetworkScope.Valid=false;break;case 11:RebirthRemoteResourceDurableJournal.Fail=true;break;case 12:RebirthRemoteResourceDurableJournal.ChangeScope=true;break;case 13:image.SetAttributeValue("fetchId",6);break;case 14:root.Add(new XElement("ownerRefund"));break;case 15:image.SetAttributeValue("parentAdmission",Guid.NewGuid());break;}
  string body=image.ToString(SaveOptions.DisableFormatting),before=root.ToString();bool accepted=RebirthRemoteResourceDurableOutcome.WitnessHistoricalCookingRefund(actor,child,1,image.Element("beforeActor").Value,body);Need(accepted==(variant==0),"historical source/current recipient Before qualification "+sourcePhase+"/"+variant);
  if(!accepted){Need(root.ToString()==before,"refused historical handoff preserves original");continue;}
  Need(record.Phase==sourcePhase&&root.Element("ownerAction")!=null&&root.Element("removed").Value=="3","Before reserves only original ownerAction; source stage/outcome remain frozen");
  Need(!RebirthRemoteResourceDurableOutcome.WitnessHistoricalCookingRefund(actor,Guid.NewGuid(),2,image.Element("afterActor").Value,body),"foreign child cannot complete original");
  Need(RebirthRemoteResourceDurableOutcome.WitnessHistoricalCookingRefund(actor,child,2,image.Element("afterActor").Value,body)&&record.Phase==4&&(string)root.Attribute("disposition")=="refunded","saved After completes exact paid/refused subset without new source debit");
  int writes=RebirthRemoteResourceDurableJournal.Advances;Need(RebirthRemoteResourceDurableOutcome.WitnessHistoricalCookingRefund(actor,child,2,image.Element("afterActor").Value,body)&&writes==RebirthRemoteResourceDurableJournal.Advances,"terminal duplicate is metadata-only");
 }
 Console.WriteLine("MP010 historical cooking refund witness: actual source cohort authored; native saved-owner qualification remains caller-owned.");}
}
