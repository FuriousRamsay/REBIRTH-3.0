#requires -Version 7.0
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$production=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/Survivor/Progression/RebirthStationObservationDispatcher.cs'))
$start=$production.IndexOf('    private static bool PersistPublication(');$end=$production.IndexOf('    private static void Clear()', $start)
if($start-lt0-or$end-lt0){throw 'Receipt persistence extraction failed'}
$method=$production.Substring($start,$end-$start).Replace('private static bool PersistPublication','public static bool PersistPublication')
$source=@"
using System;using System.Collections.Generic;using System.Xml.Linq;
public class EntityPlayer{public int entityId=42;}
public class GameManager{public static GameManager Instance=new GameManager();public object World=new object();}
public static class GameIO{public static string GetSaveGameDir()=>"fixture";}
public class RebirthStationGridAdmission{public string JobId="job";public string Image="one";public RebirthStationGridAdmission Clone()=>this;public XElement Write()=>new XElement("admission",new XAttribute("image",Image));}
public class RebirthStationPublicationRecord{public static bool Create=true;public static bool TryCreate(string root,RebirthStationGridAdmission a,int owner,object proof,out RebirthStationPublicationRecord r){r=new RebirthStationPublicationRecord();return Create;}}
public class Progression{public Dictionary<string,RebirthStationGridAdmission> StationPreparations=new Dictionary<string,RebirthStationGridAdmission>();public Dictionary<string,RebirthStationPublicationRecord> StationPublications=new Dictionary<string,RebirthStationPublicationRecord>();}
public class Record{public bool Dirty;public Progression Progression=new Progression();}
public static class RebirthStationSnapshotEvidence{public static bool Published=true;public static bool TryGetPublished(object watch,out object proof){proof=new object();return Published;}}
public static class RebirthWorldCharacterService{public static Record Current;public static bool Save=true,Identity=true;public static int Saves;public static bool TryGet(EntityPlayer p,out Record r){r=Current;return r!=null;}public static bool TryGetIdentity(EntityPlayer p,out object id){id=new object();return Identity;}public static void MarkDirty(Record r,string reason){r.Dirty=true;}public static bool FlushPlayer(EntityPlayer p,string why){Saves++;if(Save)Current.Dirty=false;return Save;}}
public static class RebirthWorldCharacterRepository{public static bool Witness=true;public static bool HasSavedStationPublication(object id,RebirthStationGridAdmission a,RebirthStationPublicationRecord r)=>Witness;}
public static class ReceiptHost{public static Dictionary<string,RebirthStationGridAdmission> verifiedQueuedAdmissions=new Dictionary<string,RebirthStationGridAdmission>();public static Dictionary<string,Tuple<object,int>> verifiedQueuedStations=new Dictionary<string,Tuple<object,int>>();public static Dictionary<string,Record> verifiedQueuedOwners=new Dictionary<string,Record>();public static HashSet<string> verifiedQueuedJobs=new HashSet<string>();public class Entry{public object Tile=new object();public object World,Watch;public EntityPlayer Owner;public RebirthStationGridAdmission Admission;}
"@
$source+=$method+'}'
$source+=@"
public static class ReceiptFixture{
 static int checks;static void Check(bool b,string n){if(!b)throw new Exception(n);checks++;}
 static ReceiptHost.Entry Reset(){ReceiptHost.verifiedQueuedJobs.Clear();ReceiptHost.verifiedQueuedStations.Clear();RebirthStationSnapshotEvidence.Published=true;RebirthStationPublicationRecord.Create=true;RebirthWorldCharacterService.Save=true;RebirthWorldCharacterService.Identity=true;RebirthWorldCharacterService.Saves=0;RebirthWorldCharacterRepository.Witness=true;var e=new ReceiptHost.Entry{World=GameManager.Instance.World,Watch=new object(),Owner=new EntityPlayer(),Admission=new RebirthStationGridAdmission()};RebirthWorldCharacterService.Current=new Record();RebirthWorldCharacterService.Current.Progression.StationPreparations.Add("job",e.Admission);return e;}
 public static string Run(){var e=Reset();Check(ReceiptHost.PersistPublication(e)&&RebirthWorldCharacterService.Saves==1&&!RebirthWorldCharacterService.Current.Dirty,"new publication saved confirmed");Check(ReceiptHost.verifiedQueuedJobs.Contains("job"),"confirmed receipt enables queue processing");
 e=Reset();RebirthWorldCharacterService.Save=false;Check(!ReceiptHost.PersistPublication(e)&&RebirthWorldCharacterService.Current.Dirty&&RebirthWorldCharacterService.Current.Progression.StationPublications.Count==1,"failed save retains receipt");Check(!ReceiptHost.verifiedQueuedJobs.Contains("job"),"failed save cannot enable processing");RebirthWorldCharacterService.Save=true;Check(ReceiptHost.PersistPublication(e)&&RebirthWorldCharacterService.Current.Progression.StationPublications.Count==1,"same receipt retry");Check(ReceiptHost.verifiedQueuedJobs.Contains("job"),"same receipt confirmed retry enables processing");
 e=Reset();RebirthWorldCharacterRepository.Witness=false;Check(!ReceiptHost.PersistPublication(e)&&RebirthWorldCharacterService.Current.Dirty,"failed readback marks retry dirty");Check(!ReceiptHost.verifiedQueuedJobs.Contains("job"),"failed readback cannot enable processing");RebirthWorldCharacterRepository.Witness=true;Check(ReceiptHost.PersistPublication(e)&&RebirthWorldCharacterService.Saves==2,"readback retry resaves same receipt");Check(ReceiptHost.verifiedQueuedJobs.Contains("job"),"readback confirmed retry enables processing");
 e=Reset();RebirthStationSnapshotEvidence.Published=false;Check(!ReceiptHost.PersistPublication(e)&&RebirthWorldCharacterService.Saves==0,"no native proof");Check(ReceiptHost.verifiedQueuedJobs.Count==0,"no native proof cannot enable processing");
 e=Reset();RebirthWorldCharacterService.Current.Progression.StationPreparations["job"]=new RebirthStationGridAdmission{Image="different"};Check(!ReceiptHost.PersistPublication(e)&&RebirthWorldCharacterService.Saves==0,"changed admission");
 return "PASS "+checks+" extracted production publication receipt persistence checks with explicit world/native-proof/repository doubles; no disk/native save qualification";
 }
}
"@
Add-Type -TypeDefinition $source
[ReceiptFixture]::Run()