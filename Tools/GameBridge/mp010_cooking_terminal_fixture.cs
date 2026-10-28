using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
// AUTHORED_NOT_RUN. Journal/identity/native codec are explicit adapters.
internal sealed class World {internal bool Remote;internal EntityPlayer Registered;internal bool IsRemote()=>Remote;internal object GetEntity(int id)=>Registered;}
internal sealed class EntityPlayer {internal World world;internal int entityId=1,Died=2;internal bool Dead;internal string Owner="owner",Creation="creation";internal bool IsDead()=>Dead;}
internal static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
internal sealed class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
internal static class RebirthOriginalActionScope {internal static bool Matches(EntityPlayer a,string owner,string creation)=>a.Owner==owner&&a.Creation==creation;}
internal sealed class RebirthStablePlayerIdentity {internal string StorageKey;internal static bool TryResolveServerEntity(EntityPlayer a,out RebirthStablePlayerIdentity i){i=new RebirthStablePlayerIdentity{StorageKey=a?.Owner};return a?.Owner!=null;}}
internal static class RemoteResourceWorldIdentity {internal static bool TryGet(World w,out string id){id="world";return w!=null;}}
internal static class RebirthCamperCharacterAccess {internal static string CurrentCreation(EntityPlayer a)=>a.Creation;}
internal enum RemoteResourceClientOperation {CookingPull=7}
internal sealed class RebirthRemoteResourceAdmission:IDisposable {public void Dispose(){}}
internal sealed class RebirthRemoteResourceSourceCoordinator:IDisposable {public void Dispose(){}}
// Transport currentness is independently exercised by the network-scope cohort.
// This cohort varies that input explicitly; it does not certify a native channel.
internal sealed class RebirthRemoteResourceNetworkScope {internal bool Current;}
internal sealed class ItemStack {internal string Rich="native-rich-A";internal int count=2;internal bool IsEmpty()=>count==0;}
internal static class RebirthCamperInventorySnapshot {internal static byte[] Capture(ItemStack[] items)=>Encoding.UTF8.GetBytes(string.Join("|",items.Select(i=>i.Rich+":"+i.count)));internal static ItemStack[] Restore(byte[] image)=>image.Length==0?new ItemStack[0]:new[]{new ItemStack()};}
internal static class RebirthRemoteResourceDurableJournal {
 internal sealed class Record {internal Guid Operation;internal int Phase=4;internal string Binding="binding",World="world",Owner="owner",Creation="creation";internal XElement Root;internal XElement Copy()=>new XElement(Root);}
 internal static Record Saved;internal static bool Unknown,FailAdvance;internal static int Reads,Advances;
 internal static Guid Operation(string world,string owner,string creation,ulong epoch,ulong request,RemoteResourceClientOperation kind){using(var h=MD5.Create())return new Guid(h.ComputeHash(Encoding.UTF8.GetBytes(world+"|"+owner+"|"+creation+"|"+epoch+"|"+request+"|"+kind)));}
 internal static bool TryRead(string owner,Guid id,out Record r,out bool unknown){Reads++;r=Saved;unknown=Unknown;return true;}
 internal static string Digest(string value){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}
 internal static bool TryAdvance(Record original,Action<XElement> change,Func<bool> current,out Record advanced){Advances++;advanced=null;if(FailAdvance||!current())return false;change(original.Root);original.Phase=(int)original.Root.Attribute("phase");advanced=original;return true;}
}
internal static class RebirthRemoteResourceSourceCustody {
 internal sealed class Lease {internal bool TryObserveSaved(int phase,out string proof){proof=null;return false;}}
 internal static int Reservations;
 internal static bool TryReserveRemoteResource(EntityPlayer actor,Guid op,string id,Func<bool> scope,out Lease lease){Reservations++;lease=null;return false;}
}
internal static partial class RebirthRemoteResourceDurableDispatch {
// ACTUAL_SCOPE
}
internal static partial class RebirthRemoteResourceDurableOutcome {
 // Saved native handoff qualification is composed in its own complete-source cohort.
 internal static bool TryCompleteSavedCookingHandoff(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record original,out RebirthRemoteResourceDurableJournal.Record terminal){terminal=null;return false;}
 internal static bool TryReconcileCookingSources(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record original,out RebirthRemoteResourceDurableJournal.Record next){next=null;return false;}
 internal static bool IsRestoredCookingRefusal(RebirthRemoteResourceDurableJournal.Record original)=>false;
}
internal static class Program {
 static void Need(bool b,string text){if(!b)throw new Exception(text);}
 public static void Main(){
  for(int variant=0;variant<24;variant++){
   ThreadManager.Main=true;var w=new World();var a=new EntityPlayer{world=w};w.Registered=a;GameManager.Instance.World=w;
   var needs=new List<ItemStack>{new ItemStack()};string hash;
   using(var h=SHA256.Create())hash=BitConverter.ToString(h.ComputeHash(RebirthCamperInventorySnapshot.Capture(needs.ToArray()))).Replace("-","").ToLowerInvariant();
   var r=new RebirthRemoteResourceDurableJournal.Record{Operation=RebirthRemoteResourceDurableJournal.Operation("world","owner","creation",1,5,RemoteResourceClientOperation.CookingPull),Root=new XElement("original",new XAttribute("deaths",1),new XAttribute("kind",7),new XAttribute("disposition","accepted"),new XAttribute("ownerProof","saved-proof"),new XAttribute("requirementsHash",hash))};
   RebirthRemoteResourceDurableJournal.Saved=r;RebirthRemoteResourceDurableJournal.Unknown=false;
   Need(RebirthRemoteResourceDurableDispatch.TryReadCookingTerminal(a,5,needs,out var accepted)&&ReferenceEquals(accepted,r),"ended-life terminal unavailable");
   string before=r.Root.ToString();long request=5;
   switch(variant){
    case 0:r.Phase=3;break;case 1:r.Phase=2;break;case 2:r.Root.SetAttributeValue("deaths",3);break;case 3:r.Root.SetAttributeValue("deaths",-1);break;
    case 4:r.Owner="foreign";break;case 5:r.World="foreign";break;case 6:r.Creation="foreign";break;case 7:r.Operation=Guid.NewGuid();break;
    case 8:needs[0].Rich="mutated-rich";break;case 9:r.Root.SetAttributeValue("ownerProof","");break;case 10:r.Root.SetAttributeValue("disposition","");break;case 11:r.Root.SetAttributeValue("kind",8);break;
    case 12:a.Dead=true;break;case 13:w.Remote=true;break;case 14:w.Registered=new EntityPlayer();break;case 15:GameManager.Instance.World=new World();break;
    case 16:ThreadManager.Main=false;break;case 17:RebirthRemoteResourceDurableJournal.Unknown=true;break;case 18:RebirthRemoteResourceDurableJournal.Saved=null;break;
    case 19:request=0;break;case 20:needs.Clear();break;case 21:needs[0].count=-1;break;case 22:r.Root.Attribute("deaths").Remove();break;case 23:a.Owner="foreign";break;
   }
   before=r.Root.ToString();Need(!RebirthRemoteResourceDurableDispatch.TryReadCookingTerminal(a,request,needs,out var rejected)&&rejected==null,"invalid terminal accepted "+variant);
   Need(r.Root.ToString()==before,"query mutated saved original");
  }
  RefusalCases();
  PendingCases();
  UnattemptedAdmissionCases();
  MetadataOnlyCases();
  Console.WriteLine("PASS actual cooking terminal query/scope; adapters do not establish native journal or remote topology evidence");
 }
 static void UnattemptedAdmissionCases(){
  foreach(int phase in new[]{0,1})for(int variant=0;variant<16;variant++){
   ThreadManager.Main=true;var w=new World();var a=new EntityPlayer{world=w};w.Registered=a;GameManager.Instance.World=w;
   var needs=new List<ItemStack>{new ItemStack()};string hash;
   using(var h=SHA256.Create())hash=BitConverter.ToString(h.ComputeHash(RebirthCamperInventorySnapshot.Capture(needs.ToArray()))).Replace("-","").ToLowerInvariant();
   var root=new XElement("original",new XAttribute("deaths",1),new XAttribute("kind",7),new XAttribute("outcome",0),new XAttribute("phase",phase),new XAttribute("requirementsHash",hash),new XElement("sources",new XElement("source",new XAttribute("attempted",0),new XAttribute("published",0))),new XElement("removed",""),new XElement("reason",""));
   var r=new RebirthRemoteResourceDurableJournal.Record{Phase=phase,Operation=RebirthRemoteResourceDurableJournal.Operation("world","owner","creation",1,5,RemoteResourceClientOperation.CookingPull),Root=root};
   RebirthRemoteResourceDurableJournal.Saved=r;RebirthRemoteResourceDurableJournal.Unknown=false;RebirthRemoteResourceDurableJournal.FailAdvance=false;RebirthRemoteResourceDurableJournal.Advances=0;
   switch(variant){
    case 1:root.Element("sources").Element("source").SetAttributeValue("attempted",1);break;
    case 2:root.Element("sources").Element("source").SetAttributeValue("published",1);break;
    case 3:root.Add(new XElement("ownerAction"));break;case 4:root.Add(new XElement("ownerRefund"));break;
    case 5:root.Element("removed").Value=Convert.ToBase64String(new byte[]{1});break;
    case 6:root.SetAttributeValue("deaths",2);break;case 7:root.SetAttributeValue("deaths",3);break;case 8:needs[0].Rich="changed";break;
    case 9:r.Owner="foreign";break;case 10:RebirthRemoteResourceDurableJournal.Unknown=true;break;
    case 11:RebirthRemoteResourceDurableJournal.FailAdvance=true;break;case 12:r.World="foreign";break;
    case 13:r.Creation="foreign";break;case 14:a.Dead=true;break;case 15:w.Registered=new EntityPlayer();break;
   }
   string before=root.ToString();bool result=RebirthRemoteResourceDurableDispatch.TryReadCookingReplay(a,5,needs,out var found);
   if(variant==0){
    Need(result&&ReferenceEquals(found,r)&&r.Phase==3&&(int)root.Attribute("outcome")==2,"untouched ended-life admission not refused");
    Need(RebirthRemoteResourceDurableJournal.Advances==1,"recovery republished original admission");
    RebirthRemoteResourceSourceCustody.Reservations=0;
    Need(RebirthRemoteResourceDurableOutcome.AcknowledgeNoPayment(a,r.Operation,r.Binding)&&r.Phase==4&&RebirthRemoteResourceSourceCustody.Reservations==0,"recovered refusal did not settle without sources");
   }else{
    Need(!result&&found==null&&root.ToString()==before&&r.Phase==phase,"unsafe admission recovery changed original "+phase+"/"+variant);
    Need(RebirthRemoteResourceDurableJournal.Advances==(variant==11?1:0),"invalid recovery attempted journal publication");
   }
  }
 }
 static void MetadataOnlyCases(){
  foreach(int stage in new[]{0,1})foreach(bool attempted in new[]{false,true}){
   ThreadManager.Main=true;var world=new World();var actor=new EntityPlayer{world=world};world.Registered=actor;GameManager.Instance.World=world;
   var needs=new List<ItemStack>{new ItemStack()};string hash;
   using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(RebirthCamperInventorySnapshot.Capture(needs.ToArray()))).Replace("-","").ToLowerInvariant();
   var root=new XElement("original",new XAttribute("deaths",actor.Died),new XAttribute("kind",7),new XAttribute("outcome",0),new XAttribute("phase",stage),new XAttribute("requirementsHash",hash),
    new XElement("sources",new XElement("source",new XAttribute("attempted",attempted?1:0),new XAttribute("published",0))),new XElement("removed",""),new XElement("reason",""));
   var record=new RebirthRemoteResourceDurableJournal.Record{Phase=stage,Operation=RebirthRemoteResourceDurableJournal.Operation("world","owner","creation",1,5,RemoteResourceClientOperation.CookingPull),Root=root};
   RebirthRemoteResourceDurableJournal.Saved=record;RebirthRemoteResourceDurableJournal.Unknown=false;RebirthRemoteResourceDurableJournal.FailAdvance=false;RebirthRemoteResourceDurableJournal.Advances=0;
   Need(!RebirthRemoteResourceDurableDispatch.TryReadCookingReplay(actor,5,needs,out _)&&record.Phase==stage,"ordinary current-life replay must not prematurely refuse active source work");
   string before=root.ToString();bool found=RebirthRemoteResourceDurableDispatch.TryReadCookingMetadata(actor,5,needs,out var refusal);
   if(attempted)Need(!found&&root.ToString()==before&&RebirthRemoteResourceDurableJournal.Advances==0,"metadata-only recovery cannot retire attempted native source custody");
   else{
    Need(found&&ReferenceEquals(refusal,record)&&record.Phase==3,"positively unattempted metadata-only original must reach no-payment refusal");
    RebirthRemoteResourceSourceCustody.Reservations=0;
    Need(RebirthRemoteResourceDurableOutcome.AcknowledgeNoPayment(actor,record.Operation,record.Binding)&&record.Phase==4&&RebirthRemoteResourceSourceCustody.Reservations==0,"metadata-only refusal must reach terminal without source reservation or owner grant");
   }
  }
 }
 static void PendingCases(){
  ThreadManager.Main=true;var w=new World();var a=new EntityPlayer{world=w};w.Registered=a;GameManager.Instance.World=w;
  Need(RebirthRemoteResourceDurableDispatch.CheckPending(a,2),"current life pending runner refused");
  Need(!RebirthRemoteResourceDurableDispatch.CheckPending(a,2,false),"ended transport pending runner retained");
  Need(!RebirthRemoteResourceDurableDispatch.CheckPending(a,1),"earlier-life pending runner retained");
  Need(!RebirthRemoteResourceDurableDispatch.CheckPending(a,3),"future-life pending runner retained");
  a.Dead=true;Need(!RebirthRemoteResourceDurableDispatch.CheckPending(a,2),"dead pending runner retained");
  a.Dead=false;w.Registered=new EntityPlayer();Need(!RebirthRemoteResourceDurableDispatch.CheckPending(a,2),"replaced roster pending runner retained");
 }
 static void RefusalCases(){
  for(int variant=0;variant<12;variant++){
   ThreadManager.Main=true;var w=new World();var a=new EntityPlayer{world=w};w.Registered=a;GameManager.Instance.World=w;
   var needs=new List<ItemStack>{new ItemStack()};string hash;
   using(var h=SHA256.Create())hash=BitConverter.ToString(h.ComputeHash(RebirthCamperInventorySnapshot.Capture(needs.ToArray()))).Replace("-","").ToLowerInvariant();
   var root=new XElement("original",new XAttribute("deaths",1),new XAttribute("kind",7),new XAttribute("outcome",2),new XAttribute("requirementsHash",hash),new XElement("sources",new XElement("source",new XAttribute("attempted",0),new XAttribute("published",0))),new XElement("removed",""));
   var r=new RebirthRemoteResourceDurableJournal.Record{Phase=3,Operation=RebirthRemoteResourceDurableJournal.Operation("world","owner","creation",1,5,RemoteResourceClientOperation.CookingPull),Root=root};
   RebirthRemoteResourceDurableJournal.Saved=r;RebirthRemoteResourceDurableJournal.Unknown=false;
   Need(RebirthRemoteResourceDurableDispatch.TryReadCookingReplay(a,5,needs,out var found)&&ReferenceEquals(found,r),"ended-life untouched refusal unavailable");
   Need(RebirthRemoteResourceDurableOutcome.CheckScope(a,r),"ended-life untouched refusal acknowledgement unavailable");
   Need(!RebirthRemoteResourceDurableDispatch.TryReadCookingTerminal(a,5,needs,out _),"refusal incorrectly treated as terminal");
   RebirthRemoteResourceSourceCustody.Reservations=0;
   Need(RebirthRemoteResourceDurableOutcome.AcknowledgeNoPayment(a,r.Operation,r.Binding)&&r.Phase==4&&(string)root.Attribute("disposition")=="unused-no-payment","untouched refusal acknowledgement did not terminalize");
   Need(RebirthRemoteResourceSourceCustody.Reservations==0,"untouched refusal reserved a source");
   r.Phase=3;root.SetAttributeValue("phase",3);root.Attribute("disposition").Remove();root.Attribute("ownerProof").Remove();
   switch(variant){
    case 0:root.Element("sources").Element("source").SetAttributeValue("attempted",1);break;
    case 1:root.Element("sources").Element("source").SetAttributeValue("published",1);break;
    case 2:root.Add(new XElement("ownerAction"));break;case 3:root.Add(new XElement("ownerRefund"));break;
    case 4:root.Element("removed").Value=Convert.ToBase64String(new byte[]{1});break;
    case 5:root.SetAttributeValue("outcome",1);break;case 6:root.SetAttributeValue("kind",8);break;
    case 7:r.Phase=2;break;case 8:root.SetAttributeValue("deaths",3);break;
    case 9:r.Owner="foreign";break;case 10:a.Dead=true;break;case 11:w.Registered=new EntityPlayer();break;
   }
   // A retained paid payload is owed, not an untouched no-payment refusal.
   // Querying it exposes custody without granting items or authorizing settlement.
   string before=root.ToString();bool replay=RebirthRemoteResourceDurableDispatch.TryReadCookingReplay(a,5,needs,out var replayed);
   Need(variant==4?replay&&ReferenceEquals(replayed,r):!replay,"unsafe ended-life refusal replayed "+variant);
   Need(!RebirthRemoteResourceDurableOutcome.CheckScope(a,r),"unsafe ended-life no-payment scope "+variant);
   Need(root.ToString()==before,"refusal query changed journal");
  }
 }
}
