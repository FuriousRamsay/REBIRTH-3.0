using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using UnityEngine;

public static partial class RebirthNpcLifecycleGameplayService
{
    private static World lifecycleWorld;
    private static string lifecyclePath;
    private static object lifecycleEpoch=new object();
    private static RebirthNpcOriginalSaveBinding lifecycleBinding;
    private static bool LifecycleBoundCurrent=>lifecycleWorld==null||(ReferenceEquals(lifecycleWorld,GameManager.Instance?.World)&&lifecyclePath==PathName());
    private static void RequireLifecycleCurrent()
    {
        if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||!LifecycleBoundCurrent||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)throw new InvalidOperationException("Lifecycle gameplay requires its exact authoritative native world and available original domain");
    }
    private static void BindLifecycle()
    {lifecycleWorld=GameManager.Instance.World;lifecyclePath=PathName();lifecycleBinding=RebirthNpcOriginalSaveBinding.Capture(lifecycleWorld,lifecyclePath,lifecycleEpoch);}
    private static void MarkLifecycleMutationNoLock()
    {dirty=true;lifecycleEpoch=new object();if(!ThreadManager.IsMainThread())lifecycleBinding?.InvalidateMutation();else lifecycleBinding?.ObserveMutation(lifecycleEpoch);}
    private static void SaveLifecycleBound()
    {
        RequireLifecycleCurrent();LoadLifecycleBound();lock(Sync)
        {
            if(!dirty&&!RebirthNpcPersistenceCoordinator.IsCheckpointWrite)return;
            if(!persistenceLoaded||lifecycleWorld==null)throw new InvalidOperationException("Lifecycle gameplay backing unresolved");
            using(var writer=RebirthNpcPersistenceFile.BeginWrite(lifecyclePath))
            {var epoch=lifecycleEpoch;var doc=LifecycleDocument();ReadLifecycleDocument(doc);RebirthNpcPersistenceFile.RequireCurrentWriter(lifecyclePath);if(!ReferenceEquals(epoch,lifecycleEpoch))throw new InvalidOperationException("Lifecycle snapshot epoch changed");RebirthNpcPersistenceFile.SaveAtomic(lifecyclePath,doc);dirty=false;lifecycleBinding=RebirthNpcOriginalSaveBinding.Capture(lifecycleWorld,lifecyclePath,epoch);}
        }
    }
    private static void LoadLifecycleBound()
    {
        RequireLifecycleCurrent();lock(Sync)
        {
            if(persistenceLoaded)return;
            if(Records.Count!=0||Replay.Count!=0||PendingReplay.Count!=0)throw new InvalidOperationException("Retained lifecycle originals cannot be overwritten by ordinary load");
            string path=PathName();
            if(!RebirthNpcPersistenceFile.TryLoad(path,doc=>{ReadLifecycleDocument(doc);return true;},out var document,out _,out var error))
            {if(!RebirthNpcPersistenceFile.CanInitializeEmpty(path))throw new InvalidDataException("Lifecycle original load retained: "+error);RebirthNpcPersistenceFile.AssertWritable(path);BindLifecycle();persistenceLoaded=true;return;}
            var read=ReadLifecycleDocument(document);Records=read.Records;Replay=read.Replay;PendingReplay=read.Pending;lifecycleEpoch=new object();dirty=false;BindLifecycle();persistenceLoaded=true;
        }
    }
    private sealed class LifecycleRead
    {internal readonly Dictionary<RebirthNpcStableId,RebirthNpcLifecycleGameplayRecord> Records=new Dictionary<RebirthNpcStableId,RebirthNpcLifecycleGameplayRecord>();internal readonly HashSet<string> Replay=new HashSet<string>(StringComparer.Ordinal);internal readonly HashSet<string> Pending=new HashSet<string>(StringComparer.Ordinal);}
    private static readonly string[] LifecycleFields={"id","owner","category","priorOrder","revision","recallCooldown","missionOutcome","lastDetail","vehicle","seat","suspended","mission","missionStart","missionEnd","contract","contractEnd","nextWage","wage","expiry","awaitingRespawn","respawnAt","lastX","lastY","lastZ","returnX","returnY","returnZ"};
    private static LifecycleRead ReadLifecycleDocument(XmlDocument doc)
    {
        var root=doc?.DocumentElement;if(root==null||root.Name!="RebirthNpcLifecycleGameplay"||!int.TryParse(root.GetAttribute("schema"),NumberStyles.None,CultureInfo.InvariantCulture,out int version)||version<1||version>SchemaVersion)throw new InvalidDataException("Lifecycle schema invalid");var read=new LifecycleRead();
        foreach(XmlNode node in root.ChildNodes)
        {
            if(node is not XmlElement row){if(node is XmlWhitespace||node is XmlComment)continue;throw new InvalidDataException("Lifecycle record content invalid");}
            if(row.Name=="Replay"||row.Name=="PendingReplay")
            {string key=row.GetAttribute("key");if(row.ChildNodes.Count!=0||row.Attributes.Count!=1||string.IsNullOrWhiteSpace(key)||!(row.Name=="Replay"?read.Replay:read.Pending).Add(key))throw new InvalidDataException("Lifecycle original replay invalid");continue;}
            if(row.Name!="Npc"||row.ChildNodes.Count!=0||!RebirthNpcStableId.TryParse(row.GetAttribute("id"),out var id)||id.IsEmpty||read.Records.ContainsKey(id)||read.Records.Count>=RebirthNpcSaveQuarantineIds.MaximumIds)throw new InvalidDataException("Lifecycle original identity invalid");
            foreach(XmlAttribute attribute in row.Attributes)if(Array.IndexOf(LifecycleFields,attribute.Name)<0)throw new InvalidDataException("Unknown lifecycle record field retained");if(version>=2)foreach(string field in LifecycleFields)if(!row.HasAttribute(field))throw new InvalidDataException("Lifecycle record field missing");
            var r=new RebirthNpcLifecycleGameplayRecord{StableId=id,OwnerId=row.GetAttribute("owner"),Category=row.GetAttribute("category"),PriorOrder=row.GetAttribute("priorOrder"),Revision=LifecycleLong(row,"revision"),RecallCooldownUntilUtcTicks=LifecycleLong(row,"recallCooldown"),MissionOutcome=row.GetAttribute("missionOutcome"),LastDetail=row.GetAttribute("lastDetail"),VehicleId=row.GetAttribute("vehicle"),VehicleSeat=LifecycleInt(row,"seat",-1),TravelSuspended=LifecycleBool(row,"suspended"),MissionId=row.GetAttribute("mission"),MissionStartedUtcTicks=LifecycleLong(row,"missionStart"),MissionEndsUtcTicks=LifecycleLong(row,"missionEnd"),ContractId=row.GetAttribute("contract"),ContractEndsUtcTicks=LifecycleLong(row,"contractEnd"),NextWageUtcTicks=LifecycleLong(row,"nextWage"),WageAmount=LifecycleInt(row,"wage"),ExpiryDisposition=(RebirthNpcContractExpiryDisposition)LifecycleInt(row,"expiry"),AwaitingRespawn=LifecycleBool(row,"awaitingRespawn"),RespawnEligibleUtcTicks=LifecycleLong(row,"respawnAt"),LastPosition=new Vector3(LifecycleFloat(row,"lastX"),LifecycleFloat(row,"lastY"),LifecycleFloat(row,"lastZ")),ReturnPosition=new Vector3(LifecycleFloat(row,"returnX"),LifecycleFloat(row,"returnY"),LifecycleFloat(row,"returnZ"))};
            if(r.Revision<0||r.VehicleSeat< -1||r.WageAmount<0||!Enum.IsDefined(typeof(RebirthNpcContractExpiryDisposition),r.ExpiryDisposition))throw new InvalidDataException("Lifecycle semantic values invalid");read.Records.Add(id,r);
        }
        foreach(string key in read.Pending)if(read.Replay.Contains(key))throw new InvalidDataException("Lifecycle original is both pending and completed");return read;
    }
    private static long LifecycleLong(XmlElement e,string field,long fallback=0)
    {if(!e.HasAttribute(field))return fallback;if(!long.TryParse(e.GetAttribute(field),NumberStyles.Integer,CultureInfo.InvariantCulture,out var value))throw new InvalidDataException("Lifecycle integer invalid");return value;}
    private static int LifecycleInt(XmlElement e,string field,int fallback=0)
    {long value=LifecycleLong(e,field,fallback);if(value<int.MinValue||value>int.MaxValue)throw new InvalidDataException("Lifecycle integer overflow");return (int)value;}
    private static float LifecycleFloat(XmlElement e,string field)
    {if(!e.HasAttribute(field))return 0;if(!float.TryParse(e.GetAttribute(field),NumberStyles.Float,CultureInfo.InvariantCulture,out var value)||float.IsNaN(value)||float.IsInfinity(value))throw new InvalidDataException("Lifecycle position nonfinite");return value;}
    private static bool LifecycleBool(XmlElement e,string field)
    {if(!e.HasAttribute(field))return false;string value=e.GetAttribute(field);if(value!="0"&&value!="1")throw new InvalidDataException("Lifecycle boolean invalid");return value=="1";}
    private static XmlDocument LifecycleDocument()=>LifecycleDocument(Records,Replay,PendingReplay);
    private static XmlDocument LifecycleDocument(Dictionary<RebirthNpcStableId,RebirthNpcLifecycleGameplayRecord> records,HashSet<string> replay,HashSet<string> pending)
    {
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("RebirthNpcLifecycleGameplay");root.SetAttribute("schema",SchemaVersion.ToString(CultureInfo.InvariantCulture));doc.AppendChild(root);var ids=new List<RebirthNpcStableId>(records.Keys);ids.Sort((a,b)=>string.CompareOrdinal(a.ToString(),b.ToString()));foreach(var id in ids)
        {
            var r=records[id];if(r==null||r.StableId!=id)throw new InvalidOperationException("Lifecycle original record missing");var row=doc.CreateElement("Npc");root.AppendChild(row);row.SetAttribute("id",id.ToString());row.SetAttribute("owner",r.OwnerId??string.Empty);row.SetAttribute("category",r.Category??string.Empty);row.SetAttribute("priorOrder",r.PriorOrder??string.Empty);row.SetAttribute("revision",r.Revision.ToString(CultureInfo.InvariantCulture));row.SetAttribute("recallCooldown",r.RecallCooldownUntilUtcTicks.ToString(CultureInfo.InvariantCulture));row.SetAttribute("missionOutcome",r.MissionOutcome??string.Empty);row.SetAttribute("lastDetail",r.LastDetail??string.Empty);row.SetAttribute("vehicle",r.VehicleId??string.Empty);row.SetAttribute("seat",r.VehicleSeat.ToString(CultureInfo.InvariantCulture));row.SetAttribute("suspended",r.TravelSuspended?"1":"0");row.SetAttribute("mission",r.MissionId??string.Empty);row.SetAttribute("missionStart",r.MissionStartedUtcTicks.ToString(CultureInfo.InvariantCulture));row.SetAttribute("missionEnd",r.MissionEndsUtcTicks.ToString(CultureInfo.InvariantCulture));row.SetAttribute("contract",r.ContractId??string.Empty);row.SetAttribute("contractEnd",r.ContractEndsUtcTicks.ToString(CultureInfo.InvariantCulture));row.SetAttribute("nextWage",r.NextWageUtcTicks.ToString(CultureInfo.InvariantCulture));row.SetAttribute("wage",r.WageAmount.ToString(CultureInfo.InvariantCulture));row.SetAttribute("expiry",((byte)r.ExpiryDisposition).ToString(CultureInfo.InvariantCulture));row.SetAttribute("awaitingRespawn",r.AwaitingRespawn?"1":"0");row.SetAttribute("respawnAt",r.RespawnEligibleUtcTicks.ToString(CultureInfo.InvariantCulture));LifecycleVector(row,"last",r.LastPosition);LifecycleVector(row,"return",r.ReturnPosition);
        }
        LifecycleKeys(doc,root,"Replay",replay);LifecycleKeys(doc,root,"PendingReplay",pending);return doc;
    }
    private static void LifecycleVector(XmlElement row,string prefix,Vector3 value)
    {row.SetAttribute(prefix+"X",value.x.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute(prefix+"Y",value.y.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute(prefix+"Z",value.z.ToString("R",CultureInfo.InvariantCulture));}
    private static void LifecycleKeys(XmlDocument doc,XmlElement root,string name,HashSet<string> values)
    {var sorted=new List<string>(values);sorted.Sort(StringComparer.Ordinal);foreach(string key in sorted){var row=doc.CreateElement(name);row.SetAttribute("key",key);root.AppendChild(row);}}
    private static bool LifecycleOpaque=>RebirthNpcLifecycleGameplayAdapters.Placement!=null||RebirthNpcLifecycleGameplayAdapters.Economy!=null||RebirthNpcLifecycleGameplayAdapters.World!=null||PendingReplay.Count!=0;
    internal static bool CanRetireLifecycleBacking
    {
        get{lock(Sync)try
        {
            if(dirty||LifecycleOpaque)return false;if(lifecycleWorld==null)return !persistenceLoaded&&Records.Count==0&&Replay.Count==0;
            if(!persistenceLoaded||lifecycleBinding?.MatchesRetirement(lifecycleEpoch)!=true)return false;var current=LifecycleDocument();ReadLifecycleDocument(current);
            if(!File.Exists(lifecyclePath))return Records.Count==0&&Replay.Count==0&&RebirthNpcPersistenceFile.CanInitializeEmpty(lifecyclePath);
            var saved=RebirthNpcRecoveryCurrentBacking.Read(lifecyclePath);ReadLifecycleDocument(saved);return LifecycleCanonical(saved)==current.OuterXml;
        }catch{return false;}}
    }
    private static string LifecycleCanonical(XmlDocument saved)
    {var read=ReadLifecycleDocument(saved);return LifecycleDocumentFor(read).OuterXml;}
    private static XmlDocument LifecycleDocumentFor(LifecycleRead read)=>LifecycleDocument(read.Records,read.Replay,read.Pending);
    internal static bool TryFlushOriginalLifecycle()
    {
        lock(Sync)try
        {
            if(lifecycleWorld==null)return CanRetireLifecycleBacking;
            if(LifecycleBoundCurrent){SaveLifecycleBound();return CanRetireLifecycleBacking;}
            if(!dirty)return CanRetireLifecycleBacking;
            if(!persistenceLoaded||lifecycleBinding==null||!lifecycleBinding.TryPublish(LifecycleDocument,doc=>{ReadLifecycleDocument(doc);return true;},()=>lifecycleEpoch,out _))return false;dirty=false;return CanRetireLifecycleBacking;
        }catch{return false;}
    }
    internal static bool TryResetLifecycle(bool save)
    {if(save&&!TryFlushOriginalLifecycle())return false;lock(Sync){if(!CanRetireLifecycleBacking)return false;Records=new Dictionary<RebirthNpcStableId,RebirthNpcLifecycleGameplayRecord>();Replay=new HashSet<string>(StringComparer.Ordinal);PendingReplay=new HashSet<string>(StringComparer.Ordinal);initialized=false;dirty=false;persistenceLoaded=false;lifecycleWorld=null;lifecyclePath=null;lifecycleBinding=null;lifecycleEpoch=new object();return true;}}
    internal static bool TryPrepareQuarantine(RebirthNpcSaveQuarantineScope scope,List<RebirthNpcSaveQuarantineDomain> domains)
    {
        lock(Sync)
        {
            if(LifecycleOpaque||!scope.MatchesBinding(lifecycleWorld,lifecyclePath,persistenceLoaded||dirty||Records.Count!=0||Replay.Count!=0||PendingReplay.Count!=0))return false;
            var records=Records;var replay=Replay;var pending=PendingReplay;var world=lifecycleWorld;string path=lifecyclePath;var binding=lifecycleBinding;var epoch=lifecycleEpoch;bool wasInitialized=initialized,wasDirty=dirty,wasLoaded=persistenceLoaded;
            var nextRecords=new Dictionary<RebirthNpcStableId,RebirthNpcLifecycleGameplayRecord>();var nextReplay=new HashSet<string>(StringComparer.Ordinal);var nextPending=new HashSet<string>(StringComparer.Ordinal);var nextEpoch=new object();var doc=LifecycleDocument();ReadLifecycleDocument(doc);
            var evidence=new XmlDocument{XmlResolver=null};var root=evidence.CreateElement("lifecycleGameplayQuarantine");evidence.AppendChild(root);root.SetAttribute("path",path??string.Empty);root.SetAttribute("initialized",initialized?"1":"0");root.SetAttribute("loaded",persistenceLoaded?"1":"0");root.SetAttribute("dirty",dirty?"1":"0");root.AppendChild(evidence.ImportNode(doc.DocumentElement,true));
            domains.Add(new RebirthNpcSaveQuarantineDomain("lifecycle-gameplay",evidence,
                ()=>{lock(Sync)return !LifecycleOpaque&&ReferenceEquals(Records,records)&&ReferenceEquals(Replay,replay)&&ReferenceEquals(PendingReplay,pending)&&ReferenceEquals(lifecycleWorld,world)&&lifecyclePath==path&&ReferenceEquals(lifecycleBinding,binding)&&ReferenceEquals(lifecycleEpoch,epoch)&&initialized==wasInitialized&&dirty==wasDirty&&persistenceLoaded==wasLoaded&&LifecycleDocument().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){Records=nextRecords;Replay=nextReplay;PendingReplay=nextPending;lifecycleWorld=null;lifecyclePath=null;lifecycleBinding=null;lifecycleEpoch=nextEpoch;initialized=false;dirty=false;persistenceLoaded=false;}},
                ()=>{lock(Sync){Records=records;Replay=replay;PendingReplay=pending;lifecycleWorld=world;lifecyclePath=path;lifecycleBinding=binding;lifecycleEpoch=epoch;initialized=wasInitialized;dirty=wasDirty;persistenceLoaded=wasLoaded;}}));
            return true;
        }
    }
    internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids)
    {lock(Sync){if(LifecycleOpaque)throw new InvalidOperationException("Lifecycle original external adapter custody unknown");foreach(var id in Records.Keys)RebirthNpcSaveQuarantineIds.Add(ids,id);}}
}
