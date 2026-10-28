// AUTHORED_NOT_RUN. Actual HasSavedPersonalAdmission. Storage/identity/schema adapters only.
using System;using System.Text;using System.Linq;using System.Xml.Linq;
enum RemoteResourceClientOperation{Craft=1}
class EntityPlayer{internal object world=new object();}
static class RebirthRemoteResourceClientOriginal
{
 internal sealed class Intent{internal string Creation="creation",Digest="original-digest";internal Guid Operation=Guid.NewGuid();internal int Deaths=1;internal ulong Epoch=2,Request=3;internal RemoteResourceClientOperation Kind=RemoteResourceClientOperation.Craft;internal XElement Copy()=>new XElement("fetchClientOriginal",new XAttribute("digest",Digest),new XAttribute("request",Request));}
}
static class RebirthRemoteResourceCraftAdoption{internal static bool Historical;internal static bool HostScope(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original)=>Historical;}
static class RebirthRemoteResourceCraftOriginal{internal static bool Valid=true;internal static bool ValidateInput(RebirthRemoteResourceClientOriginal.Intent original,out object a,out object b,out object c,out object d,out object e){a=b=c=d=e=null;return Valid;}}
static class RebirthRemoteResourceFetchedStationOriginal{internal static bool Mapped;internal static bool IsCandidate(RebirthRemoteResourceClientOriginal.Intent original)=>Mapped;}
static class RebirthRemoteResourcePlacedStationOriginal{internal static bool Mapped;internal static bool IsCandidate(RebirthRemoteResourceClientOriginal.Intent original)=>Mapped;}
class RebirthStablePlayerIdentity{internal string StorageKey="owner";internal static bool TryResolveServerEntity(EntityPlayer actor,out RebirthStablePlayerIdentity owner){owner=new RebirthStablePlayerIdentity();return actor!=null;}}
static class RemoteResourceWorldIdentity{internal static bool TryGet(object world,out string key){key="world";return world!=null;}}
static class RebirthRemoteResourceDurableJournal
{
 internal static Guid Key=Guid.NewGuid();internal static Guid Operation(string world,string owner,string creation,ulong epoch,ulong request,RemoteResourceClientOperation kind)=>Key;
 internal sealed class Record{internal string World="world",Owner="owner",Creation="creation";internal Guid Operation=Key;internal XElement Root=new XElement("root",new XAttribute("kind",1),new XAttribute("deaths",1));internal XElement Copy()=>new XElement(Root);}
}
static class RebirthCamperTransferJournal{internal enum Phase{Prepared,Applied}}
class Admission{internal RebirthCamperTransferJournal.Phase State=RebirthCamperTransferJournal.Phase.Applied;internal Guid Operation,Vehicle;internal string OwnerCreation="creation";internal long Revision;internal byte[] Before,After;}
class Store{internal Admission Value;internal Admission Read(Guid operation)=>Value;}
static class RebirthRemoteResourceCraftProtocol
{
 internal static bool Live=true;internal static Store Journal=new Store();static Store Admissions()=>Journal;
 static bool Scope(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original)=>Live;
 // ACTUAL_ADMISSION_PREDICATE
}
static class Program
{
 static int count;static void Need(bool value,string message){count++;if(!value)throw new Exception(message);}
 static void Main()
 {
  var actor=new EntityPlayer();var intent=new RebirthRemoteResourceClientOriginal.Intent();var root=new RebirthRemoteResourceDurableJournal.Record();
  Func<Admission> fresh=()=>{var bytes=Encoding.UTF8.GetBytes(new XElement("craftSourceAdmission",new XAttribute("originalDigest",intent.Digest),intent.Copy()).ToString(SaveOptions.DisableFormatting));return new Admission{Operation=root.Operation,Vehicle=intent.Operation,Before=bytes,After=(byte[])bytes.Clone()};};
  Func<bool> read=()=>RebirthRemoteResourceCraftProtocol.HasSavedPersonalAdmission(actor,intent,root);
  RebirthRemoteResourceCraftProtocol.Journal.Value=fresh();Need(read(),"exact current-life personal admission");
  RebirthRemoteResourceCraftProtocol.Live=false;RebirthRemoteResourceCraftAdoption.Historical=true;Need(read(),"exact historical personal admission");RebirthRemoteResourceCraftAdoption.Historical=false;Need(!read(),"foreign native scope");RebirthRemoteResourceCraftProtocol.Live=true;
  RebirthRemoteResourceCraftProtocol.Journal.Value=null;Need(!read(),"missing saved admission");
  Action<Action<Admission>,string> refuse=(change,name)=>{var record=fresh();change(record);RebirthRemoteResourceCraftProtocol.Journal.Value=record;Need(!read(),name);};
  refuse(x=>x.State=RebirthCamperTransferJournal.Phase.Prepared,"prepared is not saved admission");
  refuse(x=>x.Operation=Guid.NewGuid(),"foreign source operation");refuse(x=>x.Vehicle=Guid.NewGuid(),"foreign client original");
  refuse(x=>x.OwnerCreation="other","foreign creation");refuse(x=>x.Revision=1,"noninitial admission");
  refuse(x=>x.Before=new byte[]{1},"changed before");refuse(x=>x.After=new byte[]{2},"changed after");
  RebirthRemoteResourceCraftProtocol.Journal.Value=fresh();root.World="other";Need(!read(),"foreign world");root.World="world";root.Owner="other";Need(!read(),"foreign owner");root.Owner="owner";root.Creation="other";Need(!read(),"foreign root creation");root.Creation="creation";
  root.Root.SetAttributeValue("deaths",2);Need(!read(),"foreign original life");root.Root.SetAttributeValue("deaths",1);root.Root.SetAttributeValue("kind",0);Need(!read(),"other source purpose");root.Root.SetAttributeValue("kind",1);
  RebirthRemoteResourceFetchedStationOriginal.Mapped=true;Need(!read(),"fetched mapping excluded");RebirthRemoteResourceFetchedStationOriginal.Mapped=false;RebirthRemoteResourcePlacedStationOriginal.Mapped=true;Need(!read(),"placed mapping excluded");RebirthRemoteResourcePlacedStationOriginal.Mapped=false;
  RebirthRemoteResourceCraftOriginal.Valid=false;Need(!read(),"nonpersonal input excluded");RebirthRemoteResourceCraftOriginal.Valid=true;
  intent.Digest="changed";Need(!read(),"changed full original digest");intent.Digest="original-digest";
  root.Operation=Guid.NewGuid();Need(!read(),"derived original operation mismatch");root.Operation=RebirthRemoteResourceDurableJournal.Key;Need(read(),"original remains eligible");
  Console.WriteLine("PASS actual personal admission assertions="+count);
 }
}
