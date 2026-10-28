using System;using System.Linq;using System.Xml.Linq;
// Native effect publication, actor identity and journal I/O are adapters.
class EntityPlayer{internal int Died=1;internal object world=new object();internal bool Dead;internal bool IsDead()=>Dead;}
class Identity{internal string StorageKey="owner";}
static class RebirthStablePlayerIdentity{internal static bool TryResolveServerEntity(EntityPlayer p,out Identity identity){identity=new Identity();return p!=null;}}
enum RemoteResourceClientOperation{Craft=1,CookingPull=5,BlockRepair=3,BlockUpgrade=4}
static class RebirthRemoteResourceClientOriginal{internal class Intent{internal RemoteResourceClientOperation Kind=RemoteResourceClientOperation.Craft;}}
static class RebirthRemoteResourceBlockAdoption{internal static bool HasSavedAdmission(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent intent,RebirthRemoteResourceDurableJournal.Record root)=>false;}
static class RebirthRemoteResourceCraftProtocol{internal static bool Admitted=true;internal static bool HasSavedPersonalAdmission(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent intent,RebirthRemoteResourceDurableJournal.Record root)=>Admitted&&intent!=null;}
static class RebirthRemoteResourcePlacedStationOriginal{internal static bool IsMappedRoot(RebirthRemoteResourceDurableJournal.Record root)=>false;}
static class RebirthRemoteResourceFetchedStationOriginal{internal static bool IsMappedRoot(RebirthRemoteResourceDurableJournal.Record root)=>false;}
class RebirthRemoteResourceNetworkScope{internal static bool Live=true;internal bool Current=>Live;internal static RebirthRemoteResourceNetworkScope Capture(EntityPlayer p)=>p==null?null:new RebirthRemoteResourceNetworkScope();}
static class RebirthOriginalActionScope{internal static bool Valid=true;internal static bool Matches(EntityPlayer p,string owner,string creation)=>Valid;}
static class RemoteResourceWorldIdentity{internal static bool TryGet(object w,out string world){world="world";return w!=null;}}
class ItemStack{}
static class RebirthCamperInventorySnapshot{internal static byte[] Restore(byte[] bytes)=>bytes;internal static byte[] Capture(byte[] bytes)=>bytes;internal static byte[] Capture(ItemStack[] items)=>new byte[0];}
static class RebirthRemoteResourceSourceCustody{internal static bool Saved=true;internal static int Reads;internal static bool TryObserveEnteredCookingDebit(RebirthRemoteResourceDurableJournal.Record r,string id,out string proof,EntityPlayer actor=null,RebirthRemoteResourceClientOriginal.Intent craft=null){Reads++;proof=Saved?"native-positive":null;return Saved;}internal static bool TryRestoreHistoricalCookingDebit(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record r,string id,Func<bool> current,out string proof,RebirthRemoteResourceClientOriginal.Intent craft=null){Reads++;proof=Saved&&current()?"saved-restoration":null;return proof!=null;}}
static class RebirthRemoteResourceDurableJournal
{
 internal sealed class Record{internal Guid Operation=Guid.NewGuid();internal string Owner="owner",Creation="creation",World="world",Binding="binding";internal long Revision;internal XElement Image;internal int Phase=>(int)Image.Attribute("phase");internal bool HasOwnerAction=>Image.Element("ownerAction")!=null;internal XElement Copy()=>new XElement(Image);}
 internal static Record Current;internal static int Advances;
 internal static string Digest(string text)=>"full-original-digest";
 internal static bool TryRead(string owner,Guid op,out Record record,out bool unknown){record=Current;unknown=false;return true;}
 internal static bool TryAdvance(Record before,Action<XElement> change,Func<bool> scope,out Record next)
 {next=null;if(!scope()||before!=Current)return false;var image=before.Copy();change(image);next=Current=new Record{Operation=before.Operation,Revision=before.Revision+1,Image=image};Advances++;return scope();}
}
internal static partial class RebirthRemoteResourceDurableOutcome
{
 static bool AcknowledgeNoPayment(EntityPlayer actor,Guid operation,string binding)=>false;
 class Plan{}
 static Plan ReadNpc(RebirthRemoteResourceDurableJournal.Record r,XElement source,Func<bool> current)=>current()?new Plan():null;
 static bool TryObserveSavedNpc(Plan plan,bool after,out string proof){proof=RebirthRemoteResourceSourceCustody.Saved?"npc-positive":null;return after&&proof!=null;}
}
// ACTUAL_SOURCE
static class Program
{
 static EntityPlayer actor;static int assertions;
 static void Reset(int kind=5)
 {
  actor=new EntityPlayer();RebirthRemoteResourceNetworkScope.Live=true;RebirthOriginalActionScope.Valid=true;RebirthRemoteResourceSourceCustody.Saved=true;RebirthRemoteResourceSourceCustody.Reads=0;RebirthRemoteResourceDurableJournal.Advances=0;RebirthRemoteResourceCraftProtocol.Admitted=true;
  RebirthRemoteResourceDurableJournal.Current=new RebirthRemoteResourceDurableJournal.Record{Image=new XElement("root",new XAttribute("phase",1),new XAttribute("kind",5),new XAttribute("deaths",0),new XAttribute("outcome",0),new XElement("removed",""),new XElement("reason",""),new XElement("sources",new XElement("source",new XAttribute("id","source"),new XAttribute("kind",kind),new XAttribute("attempted",1),new XAttribute("published",0),new XAttribute("proof",""),new XElement("paid",Convert.ToBase64String(new byte[]{1,2})))))};
 }
 static bool Step()=>RebirthRemoteResourceDurableOutcome.TryReconcileCookingSources(actor,RebirthRemoteResourceDurableJournal.Current,out _);
 static XElement Source=>RebirthRemoteResourceDurableJournal.Current.Image.Element("sources").Element("source");
 static void Require(bool value,string message){assertions++;if(!value)throw new Exception(message);}
 static void Main()
 {
  Reset();Require(Step()&&(int)Source.Attribute("published")==1,"Positive historical effect advances only source metadata");Require(Step()&&RebirthRemoteResourceDurableJournal.Current.Phase==2,"Complete positive source proofs publish exact paid outcome");Require(!Step()&&RebirthRemoteResourceDurableJournal.Advances==2,"Repeated query cannot reenter terminal source runner");
  Reset(5);Require(Step()&&(string)Source.Attribute("proof")=="npc-positive","Existing saved typed NPC effect can complete metadata");
  Reset();RebirthRemoteResourceSourceCustody.Saved=false;Require(!Step()&&RebirthRemoteResourceDurableJournal.Advances==0,"Unknown native publication stays uncertain");
  Reset(1);Source.SetAttributeValue("attempted",0);Require(Step()&&Step()&&RebirthRemoteResourceDurableJournal.Current.Phase==3&&RebirthRemoteResourceSourceCustody.Reads==0,"Historical unattempted source refuses without withdrawal");
  Reset();Source.SetAttributeValue("restored",1);Require(!Step(),"Restored source cannot be paid twice");
  Reset();actor.Died=0;Require(!Step(),"Same life uses its existing normal coordinator");
  Reset();RebirthRemoteResourceNetworkScope.Live=false;Require(!Step(),"Stale native connection cannot reconcile");
  Reset();RebirthOriginalActionScope.Valid=false;Require(!Step(),"Foreign creation cannot reconcile");
  Reset();RebirthRemoteResourceDurableJournal.Current.Image.Add(new XElement("ownerAction"));Require(!Step(),"Existing owner disposition uses owning custody protocol");
  Reset();RebirthRemoteResourceDurableJournal.Current.Image.SetAttributeValue("aborting",1);Require(!Step(),"Aborting original cannot become paid");
  Reset(1);Require(Step()&&RebirthRemoteResourceSourceCustody.Reads==0,"Restitution fences original before any native restoration");Require(Step()&&(int)Source.Attribute("restored")==1,"Saved exact original restitution records proof");Require(Step()&&RebirthRemoteResourceDurableJournal.Current.Phase==3&&RebirthRemoteResourceDurableJournal.Current.Image.Element("removed").Value=="","Fully restored native original refuses with no owner grant");
  Reset(1);Require(Step(),"Restoration fence precedes unknown proof");RebirthRemoteResourceSourceCustody.Saved=false;Require(!Step()&&RebirthRemoteResourceDurableJournal.Current.Phase==1,"Unknown native restoration remains retained");
  Reset(1);Step();Step();Step();Require(RebirthRemoteResourceDurableOutcome.IsRestoredCookingRefusal(RebirthRemoteResourceDurableJournal.Current),"Complete saved restitution is no-payment refusal");Require(RebirthRemoteResourceDurableOutcome.AcknowledgeCookingNoPayment(actor,RebirthRemoteResourceDurableJournal.Current.Operation,"binding")&&RebirthRemoteResourceDurableJournal.Current.Phase==4,"Authenticated same owner acknowledges only metadata after saved restitution");
  Reset(1);Step();Require(!RebirthRemoteResourceDurableOutcome.AcknowledgeCookingNoPayment(actor,RebirthRemoteResourceDurableJournal.Current.Operation,"binding"),"Missing saved restoration cannot release custody");
  Reset();var untouched=new XElement(Source);untouched.SetAttributeValue("id","untouched");untouched.SetAttributeValue("attempted",0);untouched.SetAttributeValue("kind",1);Source.Parent.Add(untouched);Require(Step()&&(int)untouched.Attribute("attempted")==0,"Partial historical original observes only entered source");Require(Step()&&RebirthRemoteResourceDurableJournal.Current.Phase==3&&RebirthRemoteResourceDurableJournal.Current.Image.Element("removed").Value==Convert.ToBase64String(new byte[]{1,2}),"Saved partial payment becomes exact owed failure without new source debit");
  Reset();var intent=new RebirthRemoteResourceClientOriginal.Intent();RebirthRemoteResourceDurableJournal.Current.Image.SetAttributeValue("kind",1);Require(RebirthRemoteResourceDurableOutcome.TryReconcilePersonalCraftSources(actor,intent,RebirthRemoteResourceDurableJournal.Current,out _),"Explicit saved personal admission owns its historical source metadata");
  Reset();RebirthRemoteResourceDurableJournal.Current.Image.SetAttributeValue("kind",1);RebirthRemoteResourceCraftProtocol.Admitted=false;Require(!RebirthRemoteResourceDurableOutcome.TryReconcilePersonalCraftSources(actor,intent,RebirthRemoteResourceDurableJournal.Current,out _)&&RebirthRemoteResourceDurableJournal.Advances==0,"Missing personal native10 admission cannot reuse shared reconciliation");
  Console.WriteLine("Historical entered cooking source metadata: "+assertions+" assertions passed; native saved events remain separate evidence.");
 }
}
