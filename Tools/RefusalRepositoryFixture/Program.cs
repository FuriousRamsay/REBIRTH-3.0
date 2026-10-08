using System;using System.Linq;using System.Xml.Linq;
public class RebirthBackpackLibraryReceipt {public string CreationId,TransactionId;public long ExpectedGearRevision;public XElement ToXml()=>throw new Exception("Nonempty library outside boundary");public static bool TryRead(XElement x,out RebirthBackpackLibraryReceipt v){v=null;return false;}}
internal static class RemoteResourceRefundSupportPersistence {internal static XElement Write(XElement x,string o,string c){if(x!=null)throw new Exception("Nonempty refund outside boundary");return null;} internal static bool TryRead(XElement x,string o,string c,out XElement v){v=null;return x!=null&&!x.Elements("remoteResourceRefunds").Any();}}
public static partial class Program {
static int checks;static void Check(bool b,string n){if(!b)throw new Exception(n);checks++;}
public static void Main(){
foreach(var creation in new[]{Guid.NewGuid().ToString("N"),"legacy-"+new string('a',64)}){
var bag=new RebirthGearInventoryPlan.Stack[52];var belt=new RebirthGearInventoryPlan.Stack[4];
for(int i=0;i<52;i++)bag[i]=new();for(int i=0;i<4;i++)belt[i]=new();
Check(RebirthGearInventorySnapshot.TryCaptureEncoded(bag,belt,4,out var snapshot),"snapshot");Check(RebirthGearPreparationIntent.TryCreateUnequip(creation,Guid.NewGuid(),3,"backpack",snapshot,out var intent),"intent");
Check(RebirthGearPreparationMarker.TryEncode(Guid.NewGuid(),4,intent,out var marker),"marker");
Check(RebirthGearPreparationRefusal.TryCreateStale(marker,5,out var refusal),"refusal");
var state=new RebirthWorldSupportState{GearRevision=5,PendingGearPreparationRefusal=refusal,MusicRevision=8,AudiobookRevision=2,MusicShuffle=false};
state.EquippedGearBySlot["belt"]="belt";state.EquippedGearItemDataBySlot["belt"]="AQ==";
state.MusicCassettes.Add(new(){ItemId="music",ItemData="Ag=="});
state.AudiobookCassettes.Add(new(){SlotId=Guid.NewGuid().ToString("N"),ItemId="audio",ItemData="Aw=="});
state.Entries["a"]=new(){SupportProfileId="a",Stacks=3,GraceRemainingActiveSeconds=4.5f};
ConfirmTests(state,creation,refusal,snapshot);RetirementTests(state,creation,refusal,snapshot);RetiredTests(refusal,snapshot);ServerTests(state,creation,marker,refusal,intent,snapshot);WireTests(refusal);WitnessTests(state,creation,marker,refusal);var xml=SerializeSupport(state,"owner",creation);
Check(TryDeserializeSupport(XElement.Parse(xml.ToString()),"owner",creation,out var restored,out var error),"roundtrip "+error);
Check(XNode.DeepEquals(xml,SerializeSupport(restored,"owner",creation)),"all serialized siblings");
var clone=state.Clone();Check(XNode.DeepEquals(xml,SerializeSupport(clone,"owner",creation)),"clone");
clone.MusicCassettes[0].ItemData="BA==";clone.AudiobookCassettes[0].ItemData="BQ==";clone.Entries["a"].Stacks=99;clone.EquippedGearBySlot["belt"]="other";
Check(state.MusicCassettes[0].ItemData=="Ag=="&&state.AudiobookCassettes[0].ItemData=="Aw=="&&state.Entries["a"].Stacks==3&&state.EquippedGearBySlot["belt"]=="belt","clone detached siblings");
Check(ReferenceEquals(clone.PendingGearPreparationRefusal,refusal),"immutable refusal shared");
var absent=new XElement(xml);absent.Element("gearPreparationRefusal").Remove();Check(TryDeserializeSupport(absent,"owner",creation,out restored,out error)&&restored.PendingGearPreparationRefusal==null,"absent backward");
var duplicate=new XElement(xml);duplicate.Add(new XElement(duplicate.Element("gearPreparationRefusal")));Check(!TryDeserializeSupport(duplicate,"owner",creation,out restored,out error),"duplicate refuses");
Check(!TryDeserializeSupport(xml,"owner",Guid.NewGuid().ToString("N"),out restored,out error),"foreign refuses");
var future=new XElement(xml);future.Element("gearPreparationRefusal").SetAttributeValue("observedRevision",6);Check(!TryDeserializeSupport(future,"owner",creation,out restored,out error),"future refuses");
var plan=new RebirthGearInventoryPlan{BagSlotsBefore=52,BagSlotsAfter=52,BeltSlotsBefore=4,BeltSlotsAfter=4};
plan.GearAfter=new(){ItemData="AQ==",Count=1};plan.Changes.Add(new(){IsBag=true,Index=0,Before=new(){ItemData="AQ==",Count=1}});Check(RebirthGearTransferState.TryCreate(Guid.NewGuid().ToString("N"),creation,"backpack",5,plan,out var pending),"actual gear pending factory");
int saves=0;var journalState=new RebirthWorldSupportState{GearRevision=5,PendingGearPreparationRefusal=refusal};Check(!RebirthGearTransferJournal.Prepare(journalState,pending,creation,()=>{saves++;return true;})&&saves==0&&journalState.PendingGearTransfer==null,"actual journal refusal no stage or save");journalState.PendingGearPreparationRefusal=null;Check(RebirthGearTransferJournal.Prepare(journalState,pending,creation,()=>{saves++;return true;})&&saves==1&&ReferenceEquals(journalState.PendingGearTransfer,pending),"actual journal ordinary prepares");Check(RebirthGearTransferJournal.Prepare(journalState,pending,creation,()=>{saves++;return true;})&&saves==2,"actual journal exact retry");var gearConflict=new XElement(xml);gearConflict.Element("gearTransfers").Add(pending.ToXml());Check(!TryDeserializeSupport(gearConflict,"owner",creation,out restored,out error),"actual pending gear conflict");var musicConflict=new XElement(xml);musicConflict.Element("music").Add(new RebirthMusicTransferState{TransactionId=Guid.NewGuid().ToString("N"),CreationId=creation,Operation=1,ExpectedRevision=8,LibraryIndex=0,SourceIsBag=true,SourceIndex=0,ItemId="music",ItemData="AQ=="}.ToXml());
Check(!TryDeserializeSupport(musicConflict,"owner",creation,out restored,out error),"pending music conflict");
var terminal=new XElement(xml);terminal.Add(new XElement("gearSettlement",new XAttribute("version",1),new XAttribute("creation",creation),new XAttribute("transaction",intent.TransactionId.ToString("N")),new XAttribute("revision",5),new XAttribute("applied",false)));
Check(!TryDeserializeSupport(terminal,"owner",creation,out restored,out error),"same tx terminal conflict");
state.PendingMusicTransfer=new RebirthMusicTransferState();bool threw=false;try{SerializeSupport(state,"owner",creation);}catch(InvalidOperationException){threw=true;}finally{state.PendingMusicTransfer=null;}Check(threw,"writer music conflict");
var folder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"rebirth-refusal-fixture-"+Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(folder);var path=System.IO.Path.Combine(folder,"support.xml");
try{xml.Save(path);Check(TryDeserializeSupport(XElement.Load(path),"owner",creation,out restored,out error),"real file support reload");Check(XNode.DeepEquals(xml,SerializeSupport(restored,"owner",creation)),"real file all support siblings");System.IO.File.WriteAllText(path,"<support");bool corrupt=false;try{XElement.Load(path);}catch(System.Xml.XmlException){corrupt=true;}Check(corrupt,"corrupt disk XML rejected by XML loader");System.IO.File.Delete(path);Check(!System.IO.File.Exists(path),"missing temp file");}finally{System.IO.Directory.Delete(folder,true);}foreach(var attr in new[]{"version","reason","marker","observedRevision"}){var bad=new XElement(xml);bad.Element("gearPreparationRefusal").SetAttributeValue(attr,"bad");Check(!TryDeserializeSupport(bad,"owner",creation,out restored,out error),"malformed "+attr);}
}
Console.WriteLine("CONFIRM PASS "+confirmChecks);Console.WriteLine("RETIREMENT PASS "+retirementChecks);Console.WriteLine("RETIRED PASS "+retiredChecks);Console.WriteLine("SERVER PASS "+serverChecks);Console.WriteLine("WIRE PASS "+wireChecks);Console.WriteLine("PASS "+checks+" actual extracted support codec/clone checks; unrelated refund/library nonempty adapters refuse; no final-file witness.");
}}
