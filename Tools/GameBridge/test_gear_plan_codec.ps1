$ErrorActionPreference='Stop'
$plan=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Domain/RebirthGearInventoryPlan.cs')
$codec=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Domain/RebirthGearInventoryPlanCodec.cs')
$codec=$codec.Replace('using System;','').Replace('using System.Globalization;','').Replace('using System.Linq;','').Replace('using System.Xml.Linq;','')
$transfer=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Domain/RebirthGearTransferState.cs')
$transfer=$transfer.Replace('using System;','').Replace('using System.Globalization;','').Replace('using System.Linq;','').Replace('using System.Xml.Linq;','')
$persist=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Persistence/RebirthGearTransferPersistence.cs')
$persist=$persist.Replace('using System;','').Replace('using System.Globalization;','').Replace('using System.Linq;','').Replace('using System.Xml.Linq;','')
$migration=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Persistence/RebirthWorldCharacterMigration.cs')
$start=$migration.IndexOf('    private static string Migrate18To19(')
$end=$migration.IndexOf('    private static bool ValidateLegacyThreeAttributes',$start)
$migrationSource=$migration
$migrationFlow=$migrationSource.Substring($migrationSource.IndexOf('    public static bool TryMigrateToCurrent('))
$migrationFlow=$migrationFlow.Substring(0,$migrationFlow.LastIndexOf('}'))
$step17Start=$migrationSource.IndexOf('    private static string Migrate17To18(')
$step17End=$migrationSource.IndexOf('    private static string Migrate18To19(',$step17Start)
$step17=$migrationSource.Substring($step17Start,$step17End-$step17Start)
$runnerPrefix='public static class MigrationCheck { static readonly Dictionary<int,Func<XDocument,string>> Steps=new Dictionary<int,Func<XDocument,string>>{{17,Migrate17To18},{18,Migrate18To19}};'
$migration=$migration.Substring($start,$end-$start).Replace('private static string','public static string')
$models=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Domain/RebirthSurvivorRuntimeModels.cs')
if($models -notmatch 'CurrentSchemaVersion = 19;'){throw 'Update migration harness for current world schema'}
if($migrationSource -notmatch 'Register\(18, Migrate18To19\)'){throw 'Missing production migration registration'}
$start=$models.IndexOf('public sealed class RebirthTraitSupportRuntimeState')
$end=$models.IndexOf('public sealed class RebirthWorldCharacterRecord',$start)
$models=$models.Substring($start,$end-$start).Replace('PendingMusicTransfer?.Clone()','PendingMusicTransfer == null ? null : PendingMusicTransfer.Clone()')
$music=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Domain/RebirthMusicTransferState.cs')
$scope=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Network/RebirthSurvivorRequestScope.cs')
$music+=($scope.Replace('using System;',''))
$music=$music.Replace('using System;','').Replace('using System.Globalization;','').Replace('using System.Xml.Linq;','')
$repository=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Persistence/RebirthWorldCharacterRepository.cs')
$start=$repository.IndexOf('    private static XElement SerializeSupport(')
$end=$repository.IndexOf('    private static bool TryDeserialize(',$start)
$serialize=$repository.Substring($start,$end-$start)
$start=$repository.IndexOf('    private static bool TryDeserializeSupport(')
$end=$repository.IndexOf('    private static void AddIssue(',$start)
$deserialize=$repository.Substring($start,$end-$start)
# Only empty library state is exercised here. Actual library persistence/settlement,
# with a receipt adapter that rejects every nonempty payload, keeps that boundary explicit.
$library=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Persistence/RebirthBackpackLibraryPersistence.cs')
$library=$library.Substring($library.IndexOf('public enum RebirthBackpackLibraryPhase'))
$settlement=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Persistence/RebirthBackpackLibrarySettlement.cs')
$settlement=$settlement.Substring($settlement.IndexOf('public sealed class RebirthBackpackLibrarySettlement'))
$libraryAdapter='public class RebirthBackpackLibraryReceipt {public string CreationId,TransactionId;public long ExpectedGearRevision;public XElement ToXml(){throw new InvalidOperationException("Nonempty library outside gear fixture");}public static bool TryRead(XElement node,out RebirthBackpackLibraryReceipt value){value=null;return false;}}'
$supportHarness=@"
public static class SupportRoundtrip {
 static string A(XElement e,string n){return (string)e.Attribute(n)??"";}
 static bool TryFloat(XElement e,string n,out float v){return float.TryParse(A(e,n),NumberStyles.Float,CultureInfo.InvariantCulture,out v);}
 static string F(float v){return v.ToString("R",CultureInfo.InvariantCulture);}
 static void Check(bool v,string n){if(!v)throw new Exception(n);}
 public static void Run(RebirthGearTransferState pending){
  var state=new RebirthWorldSupportState{GearRevision=7,PendingGearTransfer=pending,MusicRevision=3,MusicShuffle=false};
  state.EquippedGearBySlot["walkman"]="test-gear";state.EquippedGearItemDataBySlot["walkman"]="AQID";
  state.MusicCassettes.Add(new RebirthMusicCassetteState{ItemId="cassette",ItemData="BAUG"});
  state.Entries["support"]=new RebirthTraitSupportRuntimeState{SupportProfileId="support",Stacks=2,GraceRemainingActiveSeconds=4.5f};
  var clone=state.Clone();Check(clone.GearRevision==7 && clone.PendingGearTransfer.TransactionId==pending.TransactionId,"support clone dropped pending");
  var original=SerializeSupport(state);RebirthWorldSupportState restored;string error;
  Check(TryDeserializeSupport(XElement.Parse(original.ToString()),out restored,out error),"repository support reload: "+error);
  Check(XNode.DeepEquals(original,SerializeSupport(restored)),"repository support roundtrip changed fields");
  var invalid=new XElement(original);invalid.Element("gearTransfers").Remove();Check(!TryDeserializeSupport(invalid,out restored,out error),"missing section accepted");
  invalid=new XElement(original);invalid.Add(new XElement(invalid.Element("gearTransfers")));Check(!TryDeserializeSupport(invalid,out restored,out error),"duplicate section accepted");
  invalid=new XElement(original);invalid.Element("gearTransfers").Element("pendingGearTransfer").SetAttributeValue("creationId","broken");Check(!TryDeserializeSupport(invalid,out restored,out error),"bad pending silently dropped");
 }
"@ + $serialize + $deserialize + "}"
$journal=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Support/RebirthGearTransferJournal.cs')
$journal=$journal.Replace('using System;','').Replace('using System.Xml.Linq;','')
Add-Type -TypeDefinition ('using System.Globalization;using System.Linq;using System.Xml.Linq;' + $plan + $codec + $transfer + $persist + $models + $music + $libraryAdapter + $library + $settlement + $journal + $supportHarness + "public class RebirthWorldCharacterRecord {public const int CurrentSchemaVersion=19;}" + $runnerPrefix + $migration + $step17 + $migrationFlow + "}" + @"
public static class GearCodecChecks {
 static void Check(bool value,string name){if(!value)throw new System.Exception(name);}
 static void Bad(XElement xml,string name){RebirthGearInventoryPlan p;Check(!RebirthGearInventoryPlanCodec.TryRead(xml,out p) && p==null,name);}
 public static void Run(){RunFor(System.Guid.NewGuid().ToString());RunFor("legacy-"+new string('a',64));}
 private static void RunFor(string creation){
  var p=new RebirthGearInventoryPlan{BagSlotsBefore=65,BagSlotsAfter=52,BeltSlotsBefore=20,BeltSlotsAfter=4};
  p.GearBefore=new RebirthGearInventoryPlan.Stack{ItemData="AQID",Count=1};
  p.Recovery.Add(new RebirthGearInventoryPlan.RecoveryEntry{Origin=RebirthGearInventoryPlan.RecoveryOrigin.DisplacedGear,Item=new RebirthGearInventoryPlan.Stack{ItemData="AQID",Count=1}});
  p.Changes.Add(new RebirthGearInventoryPlan.Change{IsBag=true,Index=64,Before=new RebirthGearInventoryPlan.Stack{ItemData="BAUG",Count=7}});
  p.Recovery.Add(new RebirthGearInventoryPlan.RecoveryEntry{Origin=RebirthGearInventoryPlan.RecoveryOrigin.Backpack,SourceIndex=64,Item=new RebirthGearInventoryPlan.Stack{ItemData="BAUG",Count=7}});
  var saved=RebirthGearInventoryPlanCodec.Write(p);RebirthGearInventoryPlan loaded;
  Check(RebirthGearInventoryPlanCodec.TryRead(XElement.Parse(saved.ToString()),out loaded),"roundtrip failed");
  Check(XNode.DeepEquals(saved,RebirthGearInventoryPlanCodec.Write(loaded)),"roundtrip lost fields");
  loaded.Recovery[0].Item.Count=2;Check(p.Recovery[0].Item.Count==1,"roundtrip aliased input");
  var x=new XElement(saved);x.SetAttributeValue("version",2);Bad(x,"future version accepted");
  x=new XElement(saved);x.Element("gearBefore").Remove();Bad(x,"missing gear accepted");
  x=new XElement(saved);x.Add(new XElement(x.Element("gearAfter")));Bad(x,"duplicate gear accepted");
  x=new XElement(saved);x.Element("recovery").Elements().First().SetAttributeValue("origin",99);Bad(x,"unknown origin accepted");
  x=new XElement(saved);x.Element("changes").Elements().First().Element("before").SetAttributeValue("count",8);Bad(x,"unbalanced items accepted");
  x=new XElement(saved);x.SetAttributeValue("bagAfter","oops");Bad(x,"bad integer accepted");
  x=new XElement(saved);x.Add(new XElement("ignored"));Bad(x,"unknown content ignored");
  x=new XElement(saved);x.Element("gearBefore").SetAttributeValue("data","bad");Bad(x,"invalid item data accepted");
  Bad(null,"missing plan accepted");
  string tx=System.Guid.NewGuid().ToString();
  RebirthGearTransferState state;
  Check(RebirthGearTransferState.TryCreate(tx,creation,"backpack",7,p,out state),"valid pending record rejected");
  SupportRoundtrip.Run(state);
  var support=new RebirthWorldSupportState{GearRevision=7};support.EquippedGearBySlot["backpack"]="gear";support.EquippedGearItemDataBySlot["backpack"]="AQID";
  int saves=0;Func<bool> success=()=>{saves++;return true;};
  Check(!RebirthGearTransferJournal.Prepare(support,state,creation,()=>false) && support.PendingGearTransfer==null,"failed prepare retained pending");
  try{RebirthGearTransferJournal.Prepare(support,state,creation,()=>{throw new Exception("disk");});}catch(Exception){}
  Check(support.PendingGearTransfer==null,"throwing prepare retained pending");
  Check(!RebirthGearTransferJournal.Prepare(support,state,System.Guid.NewGuid().ToString(),success) && saves==0,"replacement creation accepted");
  support.PendingMusicTransfer=new RebirthMusicTransferState();Check(!RebirthGearTransferJournal.Prepare(support,state,creation,success) && saves==0,"music conflict accepted");support.PendingMusicTransfer=null;
  Check(RebirthGearTransferJournal.Prepare(support,state,creation,success) && saves==1,"prepare did not save");
  Check(RebirthGearTransferJournal.Prepare(support,state,creation,success) && saves==2,"retry did not reverify save");
  Check(!RebirthGearTransferJournal.CancelRejected(support,state.TransactionId,creation,()=>false) && support.GearRevision==7 && support.PendingGearTransfer!=null,"failed cancel lost custody");
  try{RebirthGearTransferJournal.CancelRejected(support,state.TransactionId,creation,()=>{throw new Exception("disk");});}catch(Exception){}
  Check(support.GearRevision==7 && support.PendingGearTransfer!=null,"throwing cancel lost custody");
  Check(RebirthGearTransferJournal.CancelRejected(support,state.TransactionId,creation,success) && support.GearRevision==8 && support.PendingGearTransfer==null,"cancel did not persist revision");
  Check(!RebirthGearTransferJournal.Prepare(support,state,creation,success),"stale revision replay accepted");
  support.GearRevision=7;Check(RebirthGearTransferJournal.Prepare(support,state,creation,success),"phase test prepare");
  Check(!RebirthGearTransferJournal.CommitGear(support,state.TransactionId,creation,null,success),"commit before owner receipt");
  Check(!RebirthGearTransferJournal.MarkOwnerApplied(support,state.TransactionId,creation,()=>false) && support.GearTransferPhase==RebirthGearTransferPhase.Prepared,"failed owner-stage save advanced");
  Check(RebirthGearTransferJournal.MarkOwnerApplied(support,state.TransactionId,creation,success),"owner-stage save failed");
  Check(!RebirthGearTransferJournal.CancelRejected(support,state.TransactionId,creation,success),"applied inventory cancelled");
  Check(!RebirthGearTransferJournal.Prepare(support,state,creation,success),"applied inventory reoffered");
  Check(!RebirthGearTransferJournal.CommitGear(support,state.TransactionId,creation,null,()=>false) && support.GearRevision==7 && support.EquippedGearItemDataBySlot["backpack"]=="AQID" && support.GearTransferPhase==RebirthGearTransferPhase.OwnerApplied,"failed commit did not restore gear");
  try{RebirthGearTransferJournal.CommitGear(support,state.TransactionId,creation,null,()=>{throw new Exception("disk");});}catch(Exception){}
  Check(support.GearRevision==7 && support.EquippedGearBySlot.ContainsKey("backpack") && support.GearTransferPhase==RebirthGearTransferPhase.OwnerApplied,"throwing commit lost gear");
  Check(RebirthGearTransferJournal.CommitGear(support,state.TransactionId,creation,null,success) && support.GearRevision==8 && support.PendingGearTransfer!=null && !support.EquippedGearBySlot.ContainsKey("backpack"),"commit discarded recovery or kept gear");
  Check(RebirthGearTransferJournal.CommitGear(support,state.TransactionId,creation,null,success) && support.GearRevision==8,"retry duplicated revision");
  long stagedRevision;RebirthGearTransferState stagedPending;RebirthGearTransferPhase stagedPhase;
  Check(RebirthGearTransferPersistence.TryRead(RebirthGearTransferPersistence.Write(support.GearRevision,support.PendingGearTransfer,support.GearTransferPhase),out stagedRevision,out stagedPending,out stagedPhase) && stagedRevision==8 && stagedPhase==RebirthGearTransferPhase.GearCommitted,"committed custody cannot reload");
  Check(!RebirthGearTransferJournal.FinishRecovery(support,state.TransactionId,creation,()=>false) && support.PendingGearTransfer!=null && support.GearTransferPhase==RebirthGearTransferPhase.GearCommitted,"failed recovery save lost custody");
  Check(RebirthGearTransferJournal.FinishRecovery(support,state.TransactionId,creation,success) && support.PendingGearTransfer==null && support.GearRevision==8 && support.GearTransferPhase==RebirthGearTransferPhase.Prepared,"recovery finish failed");


  for(int occupied=0;occupied<2;occupied++){
   var exchange=new RebirthGearInventoryPlan{BagSlotsBefore=52,BagSlotsAfter=52,BeltSlotsBefore=20,BeltSlotsAfter=4};
   exchange.GearAfter=new RebirthGearInventoryPlan.Stack{ItemData="BAUG",Count=1};
   var change=new RebirthGearInventoryPlan.Change{IsBag=true,Index=0,Before=new RebirthGearInventoryPlan.Stack{ItemData="BAUG",Count=1}};
   var owner=new RebirthWorldSupportState();
   if(occupied==1){exchange.GearBefore=new RebirthGearInventoryPlan.Stack{ItemData="AQID",Count=1};change.After=new RebirthGearInventoryPlan.Stack{ItemData="AQID",Count=1};owner.EquippedGearBySlot["backpack"]="old-item";owner.EquippedGearItemDataBySlot["backpack"]="AQID";}
   exchange.Changes.Add(change);RebirthGearTransferState swap;
   Check(RebirthGearTransferState.TryCreate(System.Guid.NewGuid().ToString(),creation,"backpack",0,exchange,out swap),"swap fixture invalid");
   Check(RebirthGearTransferJournal.Prepare(owner,swap,creation,()=>true) && RebirthGearTransferJournal.MarkOwnerApplied(owner,swap.TransactionId,creation,()=>true),"swap preparation failed");
   Check(!RebirthGearTransferJournal.CommitGear(owner,swap.TransactionId,creation,data=>null,()=>{throw new Exception("invalid resolver attempted save");}),"invalid gear resolved");
   Func<string,string> resolver=data=>{Check(data=="BAUG","wrong bytes supplied to resolver");return "new-item";};
   Check(!RebirthGearTransferJournal.CommitGear(owner,swap.TransactionId,creation,resolver,()=>false),"failed swap save accepted");
   Check(owner.GearRevision==0 && owner.GearTransferPhase==RebirthGearTransferPhase.OwnerApplied && owner.PendingGearTransfer==swap && owner.EquippedGearBySlot.ContainsKey("backpack")== (occupied==1),"swap rollback presence lost");
   if(occupied==1)Check(owner.EquippedGearBySlot["backpack"]=="old-item" && owner.EquippedGearItemDataBySlot["backpack"]=="AQID","swap rollback changed original item");
   else Check(!owner.EquippedGearItemDataBySlot.ContainsKey("backpack"),"empty equip rollback left serialized data");
   Check(RebirthGearTransferJournal.CommitGear(owner,swap.TransactionId,creation,resolver,()=>true) && owner.EquippedGearBySlot["backpack"]=="new-item" && owner.EquippedGearItemDataBySlot["backpack"]=="BAUG" && owner.GearRevision==1,"exact swap commit failed");
   Check(RebirthGearTransferJournal.CommitGear(owner,swap.TransactionId,creation,data=>{throw new Exception("retry re-resolved item");},()=>true) && owner.GearRevision==1,"swap duplicate commit");
  }
  string pending=state.ToXml().ToString();
  p.GearBefore.ItemData="changed";Check(state.ToXml().ToString()==pending,"pending aliases input plan");
  Check(state.TryGetPlan(out loaded),"plan access failed");loaded.GearBefore.Count=55;
  Check(state.ToXml().ToString()==pending,"pending aliases returned plan");
  var exported=state.ToXml();exported.SetAttributeValue("slot","belt");Check(state.ToXml().ToString()==pending,"pending aliases exported xml");
  RebirthGearTransferState restored;
  Check(RebirthGearTransferState.TryRead(XElement.Parse(pending),out restored) && restored.ToXml().ToString()==pending,"pending roundtrip failed");
  exported=state.ToXml();exported.SetAttributeValue("transactionId",System.Guid.Empty);Check(!RebirthGearTransferState.TryRead(exported,out restored),"empty transaction accepted");
  exported=state.ToXml();exported.SetAttributeValue("creationId","bad");Check(!RebirthGearTransferState.TryRead(exported,out restored),"bad creation accepted");
  exported=state.ToXml();exported.SetAttributeValue("revision",long.MaxValue);Check(!RebirthGearTransferState.TryRead(exported,out restored),"overflow revision accepted");
  exported=state.ToXml();exported.SetAttributeValue("slot","armor");Check(!RebirthGearTransferState.TryRead(exported,out restored),"unsupported slot accepted");
  var custody=RebirthGearTransferPersistence.Write(7,state);long rev;
  Check(RebirthGearTransferPersistence.TryRead(custody,out rev,out restored) && rev==7 && restored.TransactionId==state.TransactionId,"custody persistence roundtrip");
  custody.SetAttributeValue("revision",8);Check(!RebirthGearTransferPersistence.TryRead(custody,out rev,out restored) && restored==null,"revision mismatch accepted");
  custody=RebirthGearTransferPersistence.Write(7,state);custody.Add(state.ToXml());Check(!RebirthGearTransferPersistence.TryRead(custody,out rev,out restored),"duplicate pending accepted");
  Check(RebirthGearTransferPersistence.TryRead(RebirthGearTransferPersistence.Write(0,null),out rev,out restored) && restored==null,"empty custody rejected");
  Check(!RebirthGearTransferPersistence.TryRead(null,out rev,out restored),"missing custody silently accepted");
  var doc=XDocument.Parse("<rebirthWorldCharacter schemaVersion='18'><support><gear><slot id='walkman' itemId='original'/></gear><music revision='3'/></support></rebirthWorldCharacter>");
  string gear=doc.Root.Element("support").Element("gear").ToString();
  Check(MigrationCheck.Migrate18To19(doc)=="" && (string)doc.Root.Attribute("schemaVersion")=="19","migration failed");
  Check(doc.Root.Element("support").Element("gear").ToString()==gear && (string)doc.Root.Element("support").Element("music").Attribute("revision")=="3","migration rewrote existing ownership");
  Check(RebirthGearTransferPersistence.TryRead(doc.Root.Element("support").Element("gearTransfers"),out rev,out restored) && rev==0 && restored==null,"migration fabricated custody");
  string unchanged=doc.ToString();Check(MigrationCheck.Migrate18To19(doc)!="" && doc.ToString()==unchanged,"duplicate custody overwritten");
  bool changed;string migrationError;
  var legacy=XDocument.Parse("<rebirthWorldCharacter schemaVersion='17'><progression><skills><skill id='example' value='22'/></skills></progression><support><gear/></support></rebirthWorldCharacter>");
  Check(MigrationCheck.TryMigrateToCurrent(legacy,out changed,out migrationError) && changed && (string)legacy.Root.Attribute("schemaVersion")=="19","17-to-19 runner failed: "+migrationError);
  Check((string)legacy.Root.Element("progression").Element("skills").Element("skill").Attribute("value")=="22","migration changed existing progression");
  Check(legacy.Root.Element("progression").Element("skillAwardCooldowns")!=null && legacy.Root.Element("support").Element("gearTransfers")!=null,"chain omitted step");
  var conflict=XDocument.Parse("<rebirthWorldCharacter schemaVersion='18'><support><gearTransfers revision='123'/></support></rebirthWorldCharacter>");
  unchanged=conflict.ToString();Check(!MigrationCheck.TryMigrateToCurrent(conflict,out changed,out migrationError) && !changed && conflict.ToString()==unchanged,"failed migration mutated source");
  conflict.Root.SetAttributeValue("schemaVersion",20);unchanged=conflict.ToString();Check(!MigrationCheck.TryMigrateToCurrent(conflict,out changed,out migrationError) && conflict.ToString()==unchanged,"future schema rewritten");



 }
}
"@)
[GearCodecChecks]::Run()
Write-Output 'PASS: production gear plan XML roundtrip, deep detachment, missing/duplicate/unknown nodes, future version, malformed values, conservation rejection, support clone and actual repository support serializer/deserializer roundtrip, 17-to-19 migration runner and failure rollback.'
