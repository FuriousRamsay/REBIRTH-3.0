using System;using System.Xml.Linq;
public static partial class Program{
static int retirementChecks;
static void RetirementTests(RebirthWorldSupportState original,string creation,RebirthGearPreparationRefusal refusal,RebirthGearInventorySnapshot image){
void R(bool b,string n){Check(b,n);retirementChecks++;}
bool Read(XElement x,out RebirthWorldSupportState state)=>TryDeserializeSupport(x,"owner",creation,out state,out _);
var state=original.Clone();state.PendingGearPreparationRefusal=null;state.LastGearPreparationRefusal=refusal;
var xml=SerializeSupport(state,"owner",creation);R(Read(xml,out var loaded)&&XNode.DeepEquals(xml,SerializeSupport(loaded,"owner",creation)),"retirement roundtrip siblings");
R(ReferenceEquals(state.Clone().LastGearPreparationRefusal,refusal),"retirement immutable clone");
var absent=new XElement(xml);absent.Element("gearPreparationRefusalRetirement").Remove();R(Read(absent,out loaded)&&loaded.LastGearPreparationRefusal==null,"retirement old absent");
var duplicate=new XElement(xml);duplicate.Add(new XElement(duplicate.Element("gearPreparationRefusalRetirement")));R(!Read(duplicate,out _),"retirement duplicate");
foreach(var fault in new[]{"version","extra","nested","missing","samepending","future","settlement"}){
var bad=new XElement(xml);var wrapper=bad.Element("gearPreparationRefusalRetirement");
switch(fault){case "version":wrapper.SetAttributeValue("version",2);break;case "extra":wrapper.SetAttributeValue("extra",1);break;case "nested":wrapper.Element("gearPreparationRefusal").SetAttributeValue("reason","wrong");break;case "missing":wrapper.RemoveNodes();break;case "samepending":bad.Add(refusal.Write());break;case "future":wrapper.Element("gearPreparationRefusal").SetAttributeValue("observedRevision",6);break;case "settlement":bad.Add(new XElement("gearSettlement",new XAttribute("version",1),new XAttribute("creation",creation),new XAttribute("transaction",refusal.TransactionId.ToString("N")),new XAttribute("revision",5),new XAttribute("applied",false)));break;}
R(!Read(bad,out _),"retirement refuses "+fault);}
R(!TryDeserializeSupport(xml,"owner",Guid.NewGuid().ToString("N"),out _,out _),"retirement foreign");
RebirthGearPreparationIntent.TryCreateUnequip(creation,Guid.NewGuid(),3,"backpack",image,out var newIntent);RebirthGearPreparationMarker.TryEncode(refusal.SavedWorld,4,newIntent,out var newMarker);RebirthGearPreparationRefusal.TryCreateStale(newMarker,5,out var newer);
var different=new XElement(xml);different.Add(newer.Write());R(Read(different,out loaded)&&loaded.PendingGearPreparationRefusal.TransactionId!=loaded.LastGearPreparationRefusal.TransactionId,"retirement different pending");
var music=new XElement(xml);music.Element("music").Add(new RebirthMusicTransferState{TransactionId=Guid.NewGuid().ToString("N"),CreationId=creation,Operation=1,ExpectedRevision=8,LibraryIndex=0,SourceIsBag=true,SourceIndex=0,ItemId="music",ItemData="AQ=="}.ToXml());R(Read(music,out _),"retirement history permits new music custody");
var identity=new RebirthStablePlayerIdentity();
void Reset(){serverAuthority=true;fixturePath="path";cacheOk=loaderOk=ownerMatches=worldMatches=true;migrated=loaderThrows=false;cached=new(){Support=state.Clone(),Origin=new(){CreationId=creation}};saved=new(){Support=state.Clone(),Origin=new(){CreationId=creation}};}
bool Witness()=>HasSavedGearPreparationRefusalRetirement(identity,refusal);
Reset();R(Witness(),"retirement witness exact");Reset();cached.Support.PendingGearPreparationRefusal=refusal;R(!Witness(),"retirement witness same pending");Reset();saved.Support.LastGearPreparationRefusal=null;R(!Witness(),"retirement witness missing");
Reset();RebirthGearPreparationRefusal.TryRead(new XElement("support",refusal.Write()),out var detached);cached.Support.LastGearPreparationRefusal=detached;R(!Witness(),"retirement witness exact cache ref");
Reset();saved.Support.MusicRevision++;R(!Witness(),"retirement witness sibling difference");
Reset();cached.Support.GearRevision=saved.Support.GearRevision=6;R(Witness(),"retirement witness history later revision");
Reset();cached.Support.GearRevision=saved.Support.GearRevision=4;R(!Witness(),"retirement witness revision below observed");
Reset();migrated=true;R(!Witness(),"retirement witness migration");Reset();loaderThrows=true;R(!Witness(),"retirement witness exception");Reset();saved.Origin.CreationId=Guid.NewGuid().ToString("N");R(!Witness(),"retirement witness foreign creation");
Reset();cached.Support.PendingGearPreparationRefusal=refusal;cached.Support.LastGearPreparationRefusal=null;R(HasSavedGearRefusalRetirementBase(identity,cached,refusal),"retirement base cached pending saved last");
Reset();saved.Support.PendingGearPreparationRefusal=refusal;saved.Support.LastGearPreparationRefusal=null;R(HasSavedGearRefusalRetirementBase(identity,cached,refusal),"retirement base saved pending cached last");
Reset();saved.Support.PendingGearPreparationRefusal=refusal;saved.Support.LastGearPreparationRefusal=newer;R(HasSavedGearRefusalRetirementBase(identity,cached,refusal),"retirement base old history overwritten known transition");
foreach(var fault in new[]{"sibling","revision","music","foreign","missing","migrated","cache","exception"}){
Reset();switch(fault){case "sibling":saved.Support.EquippedGearItemDataBySlot["belt"]="BA==";break;case "revision":saved.Support.GearRevision++;break;case "music":saved.Support.PendingMusicTransfer=new();break;case "foreign":saved.Support.LastGearPreparationRefusal=newer;break;case "missing":saved.Support.LastGearPreparationRefusal=null;break;case "migrated":migrated=true;break;case "cache":cacheOk=false;break;case "exception":loaderThrows=true;break;}R(!HasSavedGearRefusalRetirementBase(identity,cached,refusal),"retirement base rejects "+fault);}Reset();cached.Support.PendingGearPreparationRefusal=saved.Support.PendingGearPreparationRefusal=newer;R(Witness(),"retirement witness different pending allowed");
}
}
