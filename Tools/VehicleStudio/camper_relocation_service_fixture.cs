// Explicit adapters: native item/recipe images, applied transfer journal observations,
// vehicle save evidence, portable carrier decoding, live lease and owner save callbacks.
// Admission, address, correlation, publication and relocation service are actual sources.
public static class MobileStationAdmissionFixture {public static bool Material=false,PerBatch=false;public static string Job=Guid.NewGuid().ToString("N");}
public static class ThreadManager {public static bool Main=true;public static bool IsMainThread()=>Main;}
public static class GameIO {public static string Root=Path.Combine(Path.GetTempPath(),"camper_relocation_"+Guid.NewGuid().ToString("N"));public static string GetSaveGameDir()=>Root;}
public class EntityPlayer {}
public class RecipeQueueItem {public Recipe Recipe=new Recipe();}
public class RebirthCamperStationState {public Guid InstallationId;public RecipeQueueItem[] Queue=new[]{new RecipeQueueItem()};}
public static class RebirthCamperCraftingStation {public const int ModuleSlots=10;}
public class Installation {public RebirthCamperStationState State;}
public class RebirthCamperStationInstallations {public Installation Entry;public Installation GetSlot(int i)=>i==0?Entry:null;public Installation Find(Guid id)=>Entry?.State.InstallationId==id?Entry:null;}
public class RebirthCamperTransferJournal {
 public enum Phase {Prepared,Applied}public class Record {public Guid Operation,Vehicle;public string OwnerCreation;public Phase State;public byte[] Before,After;}
 public static Dictionary<Guid,Record> Shared;public Dictionary<Guid,Record> Records=new Dictionary<Guid,Record>();public RebirthCamperTransferJournal(){Shared=Records;}public RebirthCamperTransferJournal(string path){Records=Shared;}public IEnumerable<Record> FindApplied(Guid vehicle,string creation)=>Records.Values.Where(r=>r.State==Phase.Applied&&r.Vehicle==vehicle&&r.OwnerCreation==creation);public Record Read(Guid g)=>Records.TryGetValue(g,out var r)?r:null;
}
public class TransferImage {public RebirthCamperStationInstallations Stations;public ItemStack Cursor;}
public static class RebirthCamperTransferSnapshot {public static Dictionary<byte,TransferImage> Images=new Dictionary<byte,TransferImage>();public static TransferImage Restore(Guid g,byte[] bytes)=>Images[bytes[0]];}
public static class RebirthCamperInventorySnapshot {public static byte[] Capture(ItemStack[] cells)=>BitConverter.GetBytes(cells[0].count);}
public static class RebirthCamperPortableStation {public static RebirthCamperStationAddress Address;public static RebirthCamperStationState State;public static bool TryGetSourceAddress(ItemStack c,out RebirthCamperStationAddress a){a=Address;return c.count==1;}public static bool TryRestore(ItemStack c,out RebirthCamperStationState s){s=State;return c.count==1;}}
public static class RebirthCamperStationSaveEvidence {public static Publication RecoveryProof;public static bool Observable=true,ThrowBegin;public static int Begins;public class Request:IDisposable {public bool Closed;public Func<bool> Current;public void Dispose(){Closed=true;}}public static Request BeginRecovery(EntityPlayer p,RebirthCamperStationState s,RebirthCamperStationAddress a,Func<bool> current){Begins++;if(ThrowBegin)throw new InvalidOperationException("nativebegin");return new Request{Current=current};}public static bool TryObserve(Request r,out Publication p){p=RecoveryProof;return Observable&&r!=null&&!r.Closed&&r.Current();}public class Publication {
 public RebirthCamperStationAddress Address;public string SaveDirectory=GameIO.GetSaveGameDir();public string StationDigest=new string('A',64),VehicleEntityDigest=new string('B',64),VehicleBagDigest=new string('C',64);public int VehicleEntityId=11,VehicleClass=5;public RebirthCamperStationState State;
 public RebirthCamperStationState RestoreStationSnapshot()=>State;
}}
public static class RebirthCamperQueuedPublicationMatch {
 public static bool Valid=true;public static bool Matches(RebirthCamperStationSaveEvidence.Publication p,RebirthStationGridAdmission a,int owner)=>Valid&&owner==7&&a.TryGetMobileAddress(out var x)&&x.Equals(p.Address);
 public static bool MatchesRestored(RebirthCamperStationSaveEvidence.Publication p,RebirthStationGridAdmission a,int owner)=>Matches(p,a,owner);
 public static bool MatchesRestoredStation(RebirthCamperStationState s,RebirthCamperStationAddress x,RebirthStationGridAdmission a,int owner)=>Valid&&s!=null&&s.InstallationId==x.Installation&&owner==7;
}
public class Progression {
 public Dictionary<string,RebirthStationGridAdmission> StationPreparations=new Dictionary<string,RebirthStationGridAdmission>();
 public Dictionary<string,RebirthCamperStationPublicationRecord> CamperStationPublications=new Dictionary<string,RebirthCamperStationPublicationRecord>();
 public Dictionary<string,object> StationPublications=new Dictionary<string,object>(),StationTerminalIntents=new Dictionary<string,object>(),StationCompletionPublications=new Dictionary<string,object>(),StationCancellationAttempts=new Dictionary<string,object>(),StationCancellationPublications=new Dictionary<string,object>(),CamperStationCompletions=new Dictionary<string,object>(),CamperStationTrainingPlans=new Dictionary<string,object>(),CamperStationCancellations=new Dictionary<string,object>(),CamperStationRefundDeliveries=new Dictionary<string,object>(),StationCancellationRefunds=new Dictionary<string,object>(),StationRefundDeliveries=new Dictionary<string,object>(),StationRefundArchives=new Dictionary<string,object>();
}
public class Owner {public Progression Progression=new Progression();}
public class Identity {public string StorageKey="owner",CanonicalId="canonical";}
public static class RebirthStationLiveAccess {
 public static object Lease=new object();
 public static RebirthCamperStationAddress CurrentAddress;
 public static bool Valid=true;public static RebirthCamperStationState Station;public static Owner Owner=new Owner();
 public static bool TryResolveMobile(EntityPlayer p,RebirthCamperStationAddress a,string creation,out object station,out Owner owner){station=Station;owner=Owner;return Valid&&(!CurrentAddress.IsValid||CurrentAddress.Equals(a));}
 public static bool TryGetMobileLease(EntityPlayer p,RebirthCamperStationAddress a,string creation,out RebirthCamperStationState station,out object lease){station=Station;lease=Lease;return Valid&&(!CurrentAddress.IsValid||CurrentAddress.Equals(a));}
}
public static class RebirthWorldCharacterService {
 public static Identity Identity=new Identity();public static bool Flush=true;public static int Flushes;public static Action BeforeFlush,AfterFlush,Dirty;
 public static bool TryGetIdentity(EntityPlayer p,out Identity i){i=Identity;return i!=null;}
 public static bool TryGet(EntityPlayer p,out Owner owner){owner=RebirthStationLiveAccess.Owner;return owner!=null;}
 public static void MarkDirty(Owner o,string s){Dirty?.Invoke();}
 public static bool FlushPlayer(EntityPlayer p,string why){Flushes++;BeforeFlush?.Invoke();if(!Flush)return false;var q=RebirthStationLiveAccess.Owner.Progression;foreach(var pair in q.StationPreparations)if(q.CamperStationPublications.TryGetValue(pair.Key,out var pub))RebirthWorldCharacterRepository.Save(pair.Value,pub);AfterFlush?.Invoke();return true;}
}
public static class RebirthCamperStationWindowSession {public static bool Enabled=true;public static bool TryGetCurrent(EntityPlayer p,out RebirthCamperStationState s,out RebirthCamperStationAddress a){s=RebirthStationLiveAccess.Station;a=RebirthStationLiveAccess.CurrentAddress;return Enabled&&a.IsValid;}}
public static class RebirthCamperRemoteStationTransport {public static bool Enabled;public static bool TryGetCurrent(EntityPlayer p,out RebirthCamperStationState s,out RebirthCamperStationAddress a,out string creation,out object identity){s=RebirthStationLiveAccess.Station;a=RebirthStationLiveAccess.CurrentAddress;creation="creation";identity=RebirthStationLiveAccess.Lease;return Enabled&&a.IsValid;}}
public static class RebirthCamperStationCompletionCallsite {public static bool IsHeld(RebirthCamperStationState s)=>false;}
internal static class RebirthCamperStationQueuedAuthority {
 internal class Binding {internal EntityPlayer Player;internal Owner Owner;internal Identity Identity;internal RebirthCamperStationState Station;internal RebirthCamperStationAddress Address;internal RebirthStationGridAdmission Admission;internal RebirthCamperStationPublicationRecord Publication;internal Progression Progression=>Owner.Progression;internal int QueuedOwner=>7;internal Binding(EntityPlayer p,Owner o,Identity i,RebirthCamperStationState s,RebirthCamperStationAddress a,RebirthStationGridAdmission d,RebirthCamperStationPublicationRecord r){Player=p;Owner=o;Identity=i;Station=s;Address=a;Admission=d;Publication=r;}internal bool IsCurrent()=>RebirthStationLiveAccess.Valid;}
 internal static int Registers;internal static bool TryGetCurrent(RebirthCamperStationState s,RecipeQueueItem r,out Binding b){b=null;return false;}
 internal static bool TryRegisterRecovered(EntityPlayer p,RebirthStationGridAdmission a,RebirthCamperStationPublicationRecord r,RebirthCamperStationSaveEvidence.Publication proof){if(!RebirthWorldCharacterRepository.HasSavedCamperStationPublication(RebirthWorldCharacterService.Identity,a,r))throw new Exception("registered without exact saved pair");Registers++;return true;}
}
public static class RebirthWorldCharacterRepository {
 public static List<Tuple<XElement,XElement>> Saved=new List<Tuple<XElement,XElement>>();
 public static void Save(RebirthStationGridAdmission a,RebirthCamperStationPublicationRecord p){Saved.Add(Tuple.Create(a.Write(),p.Write()));}
 public static bool HasSavedCamperStationPublication(Identity i,RebirthStationGridAdmission a,RebirthCamperStationPublicationRecord p)=>i!=null&&Saved.Any(s=>XNode.DeepEquals(s.Item1,a.Write())&&XNode.DeepEquals(s.Item2,p.Write()));
}
public static class RelocationServiceChecks {
 static void Check(bool v,string why){if(!v)throw new Exception(why);}
 static RebirthStationGridAdmission original;static RebirthCamperStationPublicationRecord parent;static RebirthCamperStationRelocationCorrelation correlation;static EntityPlayer player=new EntityPlayer();
 static XElement Admission(RebirthCamperStationAddress a)=>new XElement("stationAdmission",new XAttribute("version",4),new XAttribute("phase","publicationAttempted"),new XAttribute("job",MobileStationAdmissionFixture.Job),new XAttribute("creation",Guid.NewGuid().ToString("N")),new XAttribute("definition",new string('a',64)),new XAttribute("x",1),new XAttribute("y",2),new XAttribute("z",3),new XAttribute("block","forge"),new XAttribute("stationAddress",a.ToString()),new XElement("before",new XElement("grid",new XAttribute("count",2))),new XElement("after",new XElement("grid",new XAttribute("count",1))),new XElement("recipe",new XAttribute("type",1),new XAttribute("count",1),new XAttribute("xp",0),new XAttribute("seconds",2),new XAttribute("area","forge"),new XElement("grid",new XAttribute("count",1))));
 static void Reset(bool save=true){RebirthStationLiveAccess.Valid=true;RebirthStationLiveAccess.Lease=new object();RebirthCamperQueuedPublicationMatch.Valid=true;ThreadManager.Main=true;RebirthStationLiveAccess.Owner=new Owner();var q=RebirthStationLiveAccess.Owner.Progression;q.StationPreparations[original.JobId]=original.Clone();q.CamperStationPublications[original.JobId]=parent.Clone();RebirthWorldCharacterRepository.Saved.Clear();if(save)RebirthWorldCharacterRepository.Save(original,parent);RebirthWorldCharacterService.Identity=new Identity();RebirthWorldCharacterService.Flush=true;RebirthWorldCharacterService.Flushes=0;RebirthWorldCharacterService.BeforeFlush=RebirthWorldCharacterService.AfterFlush=RebirthWorldCharacterService.Dirty=null;}
 public static void Run(){
  var source=new RebirthCamperStationAddress(Guid.NewGuid(),Guid.NewGuid());var destination=new RebirthCamperStationAddress(Guid.NewGuid(),source.Installation);var station=new RebirthCamperStationState{InstallationId=source.Installation};RebirthStationLiveAccess.Station=station;
  Check(RebirthStationGridAdmission.TryReadStored(Admission(source),out original),"original admission");var sourceProof=new RebirthCamperStationSaveEvidence.Publication{Address=source,State=station};Check(RebirthCamperStationPublicationRecord.TryCreate(original,7,sourceProof,out parent),"legacy original publication");Check((string)parent.Write().Attribute("version")=="1"&&RebirthCamperStationPublicationRecord.TryRead(parent.Write(),original,out var same)&&XNode.DeepEquals(same.Write(),parent.Write()),"legacy v1 changed");
  var proof=new RebirthCamperStationSaveEvidence.Publication{Address=destination,State=station};var carrier=new ItemStack(1);RebirthCamperPortableStation.Address=source;RebirthCamperPortableStation.State=station;
  var filled=new RebirthCamperStationInstallations{Entry=new Installation{State=station}};var empty=new RebirthCamperStationInstallations();
  RebirthCamperTransferSnapshot.Images[1]=new TransferImage{Stations=filled,Cursor=new ItemStack(0)};RebirthCamperTransferSnapshot.Images[2]=new TransferImage{Stations=empty,Cursor=carrier};RebirthCamperTransferSnapshot.Images[3]=new TransferImage{Stations=empty,Cursor=carrier};RebirthCamperTransferSnapshot.Images[4]=new TransferImage{Stations=filled,Cursor=new ItemStack(0)};
  var journal=new RebirthCamperTransferJournal();var removal=new RebirthCamperTransferJournal.Record{Operation=Guid.NewGuid(),Vehicle=source.Vehicle,OwnerCreation=original.CreationId,State=RebirthCamperTransferJournal.Phase.Applied,Before=new byte[]{1},After=new byte[]{2}};var install=new RebirthCamperTransferJournal.Record{Operation=Guid.NewGuid(),Vehicle=destination.Vehicle,OwnerCreation=original.CreationId,State=RebirthCamperTransferJournal.Phase.Applied,Before=new byte[]{3},After=new byte[]{4}};journal.Records[removal.Operation]=removal;journal.Records[install.Operation]=install;
  Check(RebirthCamperStationRelocationCorrelation.TryCreate(journal,removal.Operation,install.Operation,carrier,original,parent,proof,out correlation),"actual applied correlation");
  Check(!RebirthCamperStationRelocationCorrelation.TryCreate(journal,Guid.NewGuid(),install.Operation,carrier,original,parent,proof,out _),"wrong operation admitted");install.State=RebirthCamperTransferJournal.Phase.Prepared;Check(!RebirthCamperStationRelocationCorrelation.TryCreate(journal,removal.Operation,install.Operation,carrier,original,parent,proof,out _),"unapplied operation admitted");install.State=RebirthCamperTransferJournal.Phase.Applied;
  RebirthCamperStationPublicationRecord receipt=null;Check(original.TryRelocate(correlation,out var moved)&&RebirthCamperStationPublicationRecord.TryCreateRelocated(correlation,moved,out receipt)&&RebirthCamperStationPublicationRecord.TryRead(receipt.Write(),moved,out _),"v2 relocation publication");Check((string)receipt.Write().Attribute("version")=="2"&&moved.TryGetMobileAddress(out var address)&&address.Equals(destination),"destination proof");
  var bad=receipt.Write();bad.SetAttributeValue("removal",Guid.NewGuid().ToString("N"));Check(!RebirthCamperStationPublicationRecord.TryRead(bad,moved,out _),"wrong receipt operation");bad=receipt.Write();bad.SetAttributeValue("parentAdmission",new string('F',64));Check(!RebirthCamperStationPublicationRecord.TryRead(bad,moved,out _),"wrong parent hash");bad=receipt.Write();bad.SetAttributeValue("stationSnapshot","bad");Check(!RebirthCamperStationPublicationRecord.TryRead(bad,moved,out _),"malformed native digest");
  bad=receipt.Write();bad.SetAttributeValue("version",1);foreach(var k in new[]{"parentAdmission","parentPublication","removal","installation"})bad.Attribute(k).Remove();Check(!RebirthCamperStationPublicationRecord.TryRead(bad,moved,out _),"v1 cannot authorize schema7");
  var altered=moved.Write();altered.SetAttributeValue("removalAfter",new string('F',64));
  Check(RebirthStationGridAdmission.TryReadStored(altered,out var alteredMoved)&&!RebirthCamperStationPublicationRecord.TryCreateRelocated(correlation,alteredMoved,out _),"different syntactically valid journal hash authorized");
  Reset();Check(RebirthCamperStationRelocationService.TryRecord(player,correlation)&&RebirthWorldCharacterRepository.HasSavedCamperStationPublication(RebirthWorldCharacterService.Identity,moved,receipt),"destination exact pair persisted");Check(RebirthWorldCharacterService.Flushes==1&&XNode.DeepEquals(RebirthStationLiveAccess.Owner.Progression.StationPreparations[original.JobId].Write(),moved.Write()),"adopted pair");
  Reset(false);Check(!RebirthCamperStationRelocationService.TryRecord(player,correlation)&&RebirthWorldCharacterService.Flushes==0&&XNode.DeepEquals(RebirthStationLiveAccess.Owner.Progression.StationPreparations[original.JobId].Write(),original.Write()),"unsaved parent mutated");
  Reset();RebirthWorldCharacterService.Flush=false;Check(!RebirthCamperStationRelocationService.TryRecord(player,correlation)&&XNode.DeepEquals(RebirthStationLiveAccess.Owner.Progression.StationPreparations[original.JobId].Write(),moved.Write())&&!RebirthWorldCharacterRepository.HasSavedCamperStationPublication(RebirthWorldCharacterService.Identity,moved,receipt),"failed flush did not retain exact pending pair");RebirthWorldCharacterService.Flush=true;Check(RebirthCamperStationRelocationService.TryRecord(player,correlation)&&RebirthWorldCharacterService.Flushes==2,"retained exact pair retry");
  foreach(var terminal in new[]{"ordinary","intent","complete","cancelAttempt","cancelPublished","camperComplete"}){Reset();var q=RebirthStationLiveAccess.Owner.Progression;var map=terminal=="ordinary"?q.StationPublications:terminal=="intent"?q.StationTerminalIntents:terminal=="complete"?q.StationCompletionPublications:terminal=="cancelAttempt"?q.StationCancellationAttempts:terminal=="cancelPublished"?q.StationCancellationPublications:q.CamperStationCompletions;map[original.JobId]=new object();Check(!RebirthCamperStationRelocationService.TryRecord(player,correlation)&&RebirthWorldCharacterService.Flushes==0,"terminal record not fenced "+terminal);}
  for(int index=0;index<6;index++){Reset();var q=RebirthStationLiveAccess.Owner.Progression;var maps=new[]{q.CamperStationTrainingPlans,q.CamperStationCancellations,q.CamperStationRefundDeliveries,q.StationCancellationRefunds,q.StationRefundDeliveries,q.StationRefundArchives};maps[index][original.JobId]=new object();Check(!RebirthCamperStationRelocationService.TryRecord(player,correlation)&&RebirthWorldCharacterService.Flushes==0,"additional terminal/refund guard "+index);}
  Reset();RebirthStationLiveAccess.Owner.Progression.CamperStationPublications[original.JobId]=receipt;Check(!RebirthCamperStationRelocationService.TryRecord(player,correlation)&&RebirthWorldCharacterService.Flushes==0,"mixed cached parent/destination pair accepted");
  Reset();RebirthWorldCharacterService.BeforeFlush=()=>RebirthStationLiveAccess.Owner.Progression.StationPreparations[original.JobId]=original.Clone();Check(!RebirthCamperStationRelocationService.TryRecord(player,correlation),"changed pair during flush acknowledged");
  Reset();RebirthWorldCharacterService.AfterFlush=()=>RebirthStationLiveAccess.Owner.Progression.StationTerminalIntents[original.JobId]=new object();Check(!RebirthCamperStationRelocationService.TryRecord(player,correlation),"terminal race after save acknowledged");
  Reset();RebirthStationLiveAccess.Valid=false;Check(!RebirthCamperStationRelocationService.TryRecord(player,correlation),"missing native lease");Reset();RebirthWorldCharacterService.Dirty=()=>RebirthStationLiveAccess.Owner=new Owner();Check(!RebirthCamperStationRelocationService.TryRecord(player,correlation)&&RebirthWorldCharacterService.Flushes==0,"changed owner before flush");
  Reset();RebirthWorldCharacterService.AfterFlush=()=>RebirthStationLiveAccess.Valid=false;Check(!RebirthCamperStationRelocationService.TryRecord(player,correlation),"changed lease after save acknowledged");Reset();RebirthWorldCharacterService.AfterFlush=()=>RebirthWorldCharacterService.Identity=new Identity{StorageKey="other"};Check(!RebirthCamperStationRelocationService.TryRecord(player,correlation),"changed identity after save acknowledged");
  Console.WriteLine("PASS actual admission/correlation/publication v1-v2/relocation service core: exact parent/destination pair, absent saved-parent refusal, failedflush retained retry, changed access/owner/identity and all terminal guards, wrong operation/hash/digest and schema7-v1 refusal. Native transfer/save/access adapters explicitly doubled; no gameplay proof.");
  Reset();RebirthWorldCharacterService.AfterFlush=()=>RebirthStationLiveAccess.Lease=new object();
  Check(!RebirthCamperStationRelocationService.TryRecord(player,correlation),"KNOWN GAP: opaque lease replacement to same player/station acknowledged; root patch reserved until build freeze released");
  Console.WriteLine("PASS exact opaque lease replacement refused across save callback.");
  Reset();RebirthWorldCharacterService.Flush=false;Check(!RebirthCamperStationRelocationService.TryRecord(player,correlation),"prepare reconnect failedflush");
  var exactRetained=RebirthStationLiveAccess.Owner.Progression.CamperStationPublications[original.JobId].Clone();
  RebirthStationLiveAccess.Lease=new object();RebirthWorldCharacterService.Flush=true;
  var reconnectProof=new RebirthCamperStationSaveEvidence.Publication{Address=destination,State=station,StationDigest=new string('D',64),VehicleEntityDigest=new string('E',64)};
  Check(RebirthCamperStationRelocationService.TryFlushRecorded(player,moved,exactRetained,reconnectProof)&&
    XNode.DeepEquals(RebirthStationLiveAccess.Owner.Progression.CamperStationPublications[original.JobId].Write(),exactRetained.Write())&&
    RebirthWorldCharacterRepository.HasSavedCamperStationPublication(RebirthWorldCharacterService.Identity,moved,exactRetained),"new lease/proof reconnect rewrote or lost retained pair");
  Check(!RebirthCamperStationRelocationService.TryFlushRecorded(player,moved,exactRetained,sourceProof),"wrong destination proof resumed");
  var changedReceipt=exactRetained.Write();changedReceipt.SetAttributeValue("removal",Guid.NewGuid().ToString("N"));
  Check(!RebirthCamperStationPublicationRecord.TryRead(changedReceipt,moved,out _),"reconnect altered receipt operation");
  RebirthWorldCharacterService.AfterFlush=()=>RebirthStationLiveAccess.Lease=new object();
  Check(!RebirthCamperStationRelocationService.TryFlushRecorded(player,moved,exactRetained,reconnectProof),"reconnect replacement lease acknowledged");
  Console.WriteLine("PASS exact retained destination-pair reconnect with different fresh proof; wrong address/operation and replacement lease refusal.");
  Directory.CreateDirectory(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CamperTransfers"));
  Reset();RebirthStationLiveAccess.CurrentAddress=destination;RebirthCamperStationSaveEvidence.RecoveryProof=proof;RebirthCamperStationSaveEvidence.Observable=false;
  RebirthCamperStationQueuedAuthority.Registers=0;RebirthCamperStationSaveEvidence.Begins=0;
  Check(RebirthCamperStationQueueRecovery.TryBegin(player,original.JobId,out var recover)&&recover!=null&&recover.CanContinue&&RebirthCamperStationSaveEvidence.Begins==1,"destination relocation recovery not invoked");
  Check(!recover.TryRecover()&&RebirthCamperStationQueuedAuthority.Registers==0,"unobserved native save rebound queue");
  RebirthCamperStationSaveEvidence.Observable=true;RebirthWorldCharacterService.Flush=false;
  Check(!recover.TryRecover()&&recover.CanContinue&&RebirthCamperStationQueuedAuthority.Registers==0&&
    (string)RebirthStationLiveAccess.Owner.Progression.StationPreparations[original.JobId].Write().Attribute("version")=="7","failedflush recovery lost pending pair or rebound");
  RebirthWorldCharacterService.Flush=true;Check(recover.TryRecover()&&RebirthCamperStationQueuedAuthority.Registers==1&&RebirthCamperStationSaveEvidence.Begins==1,"same native request retry not saved before binding");recover.Dispose();
  Reset();RebirthStationLiveAccess.CurrentAddress=destination;RebirthCamperStationSaveEvidence.RecoveryProof=proof;RebirthWorldCharacterService.Flush=false;
  Check(RebirthCamperStationQueueRecovery.TryBegin(player,original.JobId,out var firstLease)&&!firstLease.TryRecover(),"reconnect staged recovery");
  var stagedReceipt=RebirthStationLiveAccess.Owner.Progression.CamperStationPublications[original.JobId].Write();
  RebirthStationLiveAccess.Lease=new object();Check(!firstLease.CanContinue,"old recovery survived replacement lease");firstLease.Dispose();
  RebirthWorldCharacterService.Flush=true;RebirthCamperStationSaveEvidence.RecoveryProof=reconnectProof;
  Check(RebirthCamperStationQueueRecovery.TryBegin(player,original.JobId,out var reopened)&&reopened.TryRecover()&&
    XNode.DeepEquals(stagedReceipt,RebirthStationLiveAccess.Owner.Progression.CamperStationPublications[original.JobId].Write()),"reopened recovery changed proof receipt or failed exact pair retry");reopened.Dispose();
  Reset();RebirthStationLiveAccess.CurrentAddress=destination;install.State=RebirthCamperTransferJournal.Phase.Prepared;
  int begins=RebirthCamperStationSaveEvidence.Begins;Check(!RebirthCamperStationQueueRecovery.TryBegin(player,original.JobId,out var missing)&&missing==null&&RebirthCamperStationSaveEvidence.Begins==begins,"unapplied journal opened recovery");install.State=RebirthCamperTransferJournal.Phase.Applied;
  var duplicate=new RebirthCamperTransferJournal.Record{Operation=Guid.NewGuid(),Vehicle=source.Vehicle,OwnerCreation=original.CreationId,State=RebirthCamperTransferJournal.Phase.Applied,Before=removal.Before,After=removal.After};journal.Records[duplicate.Operation]=duplicate;
  Check(!RebirthCamperStationQueueRecovery.TryBegin(player,original.JobId,out _)&&RebirthCamperStationSaveEvidence.Begins==begins,"ambiguous source journal guessed");journal.Records.Remove(duplicate.Operation);
  Reset();RebirthStationLiveAccess.CurrentAddress=destination;RebirthStationLiveAccess.Owner.Progression.StationTerminalIntents[original.JobId]=new object();
  Check(!RebirthCamperStationQueueRecovery.TryBegin(player,original.JobId,out _)&&RebirthCamperStationSaveEvidence.Begins==begins,"terminal recovery began native save");
  Reset();RebirthStationLiveAccess.CurrentAddress=destination;RebirthCamperStationSaveEvidence.ThrowBegin=true;
  Check(!RebirthCamperStationQueueRecovery.TryBegin(player,original.JobId,out var retainedOperation)&&retainedOperation!=null,"native request failure lost operation cleanup ownership");retainedOperation.Dispose();RebirthCamperStationSaveEvidence.ThrowBegin=false;
  Reset();RebirthStationLiveAccess.CurrentAddress=destination;RebirthCamperStationWindowSession.Enabled=false;RebirthCamperRemoteStationTransport.Enabled=true;RebirthCamperStationSaveEvidence.RecoveryProof=proof;
  RebirthCamperStationQueueRecoveryWatch.Calls=0;
  RemoteQueueObserverChecks.Observe(player,RebirthStationLiveAccess.Owner,RebirthStationLiveAccess.Station,destination);
  Check(RebirthCamperStationQueueRecoveryWatch.Calls==1,"remote callback excluded original source admission at destination");
  RemoteQueueObserverChecks.Observe(player,RebirthStationLiveAccess.Owner,new RebirthCamperStationState(),destination);
  Check(RebirthCamperStationQueueRecoveryWatch.Calls==1,"remote callback accepted unrelated station");
  RebirthStationLiveAccess.Valid=false;
  RemoteQueueObserverChecks.Observe(player,RebirthStationLiveAccess.Owner,RebirthStationLiveAccess.Station,destination);
  Check(RebirthCamperStationQueueRecoveryWatch.Calls==1,"remote callback accepted unauthenticated lease");RebirthStationLiveAccess.Valid=true;
  Check(RebirthCamperStationQueueRecovery.TryBegin(player,original.JobId,out var remoteRecovery)&&remoteRecovery.TryRecover(),"authenticated remote destination did not invoke relocation branch");remoteRecovery.Dispose();RebirthCamperRemoteStationTransport.Enabled=false;RebirthCamperStationWindowSession.Enabled=true;
  Console.WriteLine("PASS actual local/remote shared queue recovery branch: unique applied journal/carrier discovery, native proof then exact saved pair before binding, same-request failedflush retry, reopened lease/newproof retained-receipt retry, oldlease invalidation, unapplied/ambiguous/terminal refusal and failedbegin cleanup retention. Native request/access/binding adapters explicit.");
 }
}
