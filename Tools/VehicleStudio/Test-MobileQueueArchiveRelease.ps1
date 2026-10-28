#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperStationQueuedAuthority.cs'))
$camperStart=$camperSource.IndexOf('    internal static bool TryReleaseArchived(')
$camperEnd=$camperSource.IndexOf('    // Mobile queues have no terrain tile address.',$camperStart)
if($camperStart -lt 0 -or $camperEnd -le $camperStart){throw 'Actual archive-release method missing.'}
$camperFixture=@'
using System;using System.Collections.Generic;using System.Runtime.CompilerServices;using System.Xml.Linq;
public static class ThreadManager {public static bool Main=true;public static bool IsMainThread()=>Main;}
public class Recipe {public bool Marked=true,Readable=true;public string Job="other";}
public class Row {public Recipe Recipe=new Recipe();}
public class RebirthCamperStationState {public object BoundVehicle=new object();public Row[] Queue=new[]{new Row()};public object CraftCompleteList=new object();}
public class RebirthStablePlayerIdentity {public string StorageKey="owner";}
public class Progression {public Dictionary<string,object> StationPreparations=new Dictionary<string,object>();}
public class RebirthWorldCharacterRecord {public Progression Progression=new Progression();}
public class Address {public string Vehicle="vehicle",Installation="install";}
public class RebirthStationGridAdmission {public string JobId="job";public bool Mobile=true;public XElement Write()=>new XElement("stationAdmission",new XAttribute("job",JobId));public bool TryGetMobileAddress(out Address a){a=new Address();return Mobile;}public static bool TryReadStored(XElement n,out RebirthStationGridAdmission a){a=new RebirthStationGridAdmission{JobId=(string)n?.Attribute("job")};return n!=null&&a.JobId!=null;}}
public class Publication {public XElement Image=new XElement("camperStationPublication",new XAttribute("binding","original"));public XElement Write()=>new XElement(Image);}
public class RebirthStationRefundArchive {public bool IsMobile=true;public XElement Image=new XElement("archive",new XAttribute("owner","owner"),new RebirthStationGridAdmission().Write(),new Publication().Write());public XElement Write()=>new XElement(Image);public static bool TryRead(XElement n,string owner,out RebirthStationRefundArchive a){a=new RebirthStationRefundArchive{Image=new XElement(n)};return (string)n.Attribute("owner")==owner;}}
public static class RebirthWorldCharacterRepository {public static bool Cached=true,Saved=true;public static bool IsCurrentCachedRecord(RebirthWorldCharacterRecord o)=>Cached;public static bool HasSavedStationRefundArchive(RebirthStablePlayerIdentity i,RebirthStationRefundArchive a)=>Saved;}
public static class RebirthCamperVehicleIdentity {public static bool Unique=true;public static object Expected;public static bool IsUniqueCurrent(object v,string id)=>Unique&&ReferenceEquals(v,Expected)&&id=="vehicle";}
public class Installed {public RebirthCamperStationState State;}
public class Stations {public Installed Installed=new Installed();public bool Valid=true;public Installed Find(string id)=>Valid&&id=="install"?Installed:null;}
public class RebirthCamperModuleRuntime {public Stations Stations=new Stations();public static RebirthCamperModuleRuntime Current=new RebirthCamperModuleRuntime();public static RebirthCamperModuleRuntime For(object vehicle)=>Current;}
public static class RebirthStationCompletionReceipt {public static bool Absent=true;public static bool HasNoJobReceipt(object receipts,string job)=>Absent;}
public static class RebirthStationGridQueue {public static bool IsMarked(Recipe r)=>r!=null&&r.Marked;public static bool TryGetJobId(Recipe r,out string job){job=r.Job;return r.Readable&&job!=null;}}
public static class QueueArchiveReleaseFixture {
 internal class Binding {public RebirthWorldCharacterRecord Owner;public RebirthStablePlayerIdentity Identity=new RebirthStablePlayerIdentity();public RebirthStationGridAdmission Admission=new RebirthStationGridAdmission();public Publication Publication=new Publication();}
 private static readonly ConditionalWeakTable<RebirthCamperStationState,Dictionary<string,Binding>> bindings=new ConditionalWeakTable<RebirthCamperStationState,Dictionary<string,Binding>>();
METHOD_BODY
 static RebirthCamperStationState station;static RebirthWorldCharacterRecord owner;static RebirthStablePlayerIdentity identity;static RebirthStationRefundArchive archive;static Dictionary<string,Binding> records;static bool Ack;static int callbacks;
 static bool Saved(){callbacks++;return Ack;}
 static void Check(bool v,string n){if(!v)throw new Exception(n);}
 static void Setup(){ThreadManager.Main=true;RebirthWorldCharacterRepository.Cached=true;RebirthWorldCharacterRepository.Saved=true;RebirthCamperVehicleIdentity.Unique=true;RebirthStationCompletionReceipt.Absent=true;station=new RebirthCamperStationState();RebirthCamperVehicleIdentity.Expected=station.BoundVehicle;RebirthCamperModuleRuntime.Current=new RebirthCamperModuleRuntime();RebirthCamperModuleRuntime.Current.Stations.Installed.State=station;owner=new RebirthWorldCharacterRecord();identity=new RebirthStablePlayerIdentity();archive=new RebirthStationRefundArchive();records=new Dictionary<string,Binding>{{"job",new Binding{Owner=owner}},{"other",new Binding{Owner=owner}}};bindings.Add(station,records);Ack=true;callbacks=0;}
 static bool Release()=>TryReleaseArchived(station,owner,identity,archive,Saved);
 static void Denied(string n){Check(!Release()&&records.ContainsKey("job")&&records.ContainsKey("other"),n);}
 public static void Run(){
  Setup();RebirthWorldCharacterRepository.Saved=false;Denied("absent primary archive");Setup();Ack=false;Denied("unacknowledged callback");Setup();records["job"].Owner=new RebirthWorldCharacterRecord();Denied("wrong original owner");Setup();identity.StorageKey="foreign";Denied("archive owner mismatch");Setup();records["job"].Identity.StorageKey="foreign";Denied("original identity mismatch");Setup();records["job"].Admission.JobId="different";Denied("original admission mismatch");Setup();records["job"].Publication.Image.SetAttributeValue("binding","changed");Denied("original full publication mismatch");Setup();station.BoundVehicle=new object();Denied("different vehicle");Setup();RebirthCamperModuleRuntime.Current.Stations.Valid=false;Denied("missing install");Setup();RebirthCamperModuleRuntime.Current.Stations.Installed.State=new RebirthCamperStationState();Denied("different station install");Setup();owner.Progression.StationPreparations.Add("job",new object());Denied("active admission cannot release");
  Setup();station.Queue[0].Recipe.Job="job";Denied("target still in live queue");Setup();station.Queue[0].Recipe.Readable=false;Denied("unreadable marked row");Setup();station.Queue=new Row[]{null};Denied("null row");Setup();station.Queue=null;Denied("missing queue");Setup();RebirthStationCompletionReceipt.Absent=false;Denied("target native completion receipt remains");Setup();ThreadManager.Main=false;Denied("off-thread");
  Setup();Check(!TryReleaseArchived(station,owner,identity,archive,()=>{throw new Exception("callback failed");})&&records.Count==2,"callback exception retains bindings");Setup();int acknowledgements=0;Check(!TryReleaseArchived(station,owner,identity,archive,()=>++acknowledgements==1)&&records.Count==2,"second callback false retains original binding");
  Setup();var other=records["other"];Check(Release()&&records.Count==1&&!records.ContainsKey("job")&&ReferenceEquals(records["other"],other),"remove only exact job preserve other binding");Check(Release()&&records.Count==1,"repeat idempotent with positive archive proof");RebirthWorldCharacterRepository.Saved=false;Check(!Release()&&records.Count==1,"already removed still needs primary proof");
  Setup();bindings.Remove(station);RebirthWorldCharacterRepository.Saved=false;Check(!Release(),"no bindings needs primary archive");RebirthWorldCharacterRepository.Saved=true;Ack=false;Check(!Release(),"no bindings needs acknowledgement");Ack=true;Check(Release(),"no bindings positive terminal archive accepted");
  Console.WriteLine("PASS extracted actual TryReleaseArchived: primary proof/callback/owner/admission/publication/vehicle/install/queue/receipt/thread refusal, exact one-job removal and proof-dependent idempotence. Native/context/archive/repository adapters doubled; whole-class and primary-file durability not claimed.");
 }
}
'@
Add-Type -TypeDefinition $camperFixture.Replace('METHOD_BODY',$camperSource.Substring($camperStart,$camperEnd-$camperStart))
[QueueArchiveReleaseFixture]::Run()
