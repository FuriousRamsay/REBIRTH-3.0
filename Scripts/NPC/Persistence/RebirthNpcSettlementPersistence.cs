using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public static class RebirthNpcSettlementPersistenceStore
{
    private const int FormatVersion=3;
    private const string FileName="rebirth_npc_settlement.xml";
    private static readonly object Sync=new object();
    private static bool loaded;
    private static volatile bool dirty;
    private static long nextSaveUtcTicks,saves,restored,rejected;
    // Reserved telemetry counter; no settlement reconciliation pass is active yet.
    private static long reconciled = 0;

    public static void Tick()
    {
        if (!IsServer() || GameManager.Instance?.World == null) return;
        long now = DateTime.UtcNow.Ticks;
        if (now < nextSaveUtcTicks) return;
        if (string.IsNullOrEmpty(GetPath())) return;
        // Save ensures loading before its dirty check. Keep automatic failed-load
        // retries on the save cadence; explicit operations still load immediately.
        nextSaveUtcTicks = now + TimeSpan.FromSeconds(30).Ticks;
        Save();
    }
    public static void EnsureLoaded()
    {
        if (GameManager.Instance?.World == null) return;
        if(!IsServer())return;
        lock(Sync)
        {
            if(loaded)return; string path=GetPath(); if(string.IsNullOrEmpty(path))return;
            try
            {
                XmlDocument document;string source,error;
                if(!RebirthNpcPersistenceFile.TryLoad(path,ValidateDocument,out document,out source,out error))
                {
                    if(!RebirthNpcPersistenceFile.CanInitializeEmpty(path))
                        Log.Warning("[REBIRTH NPC] Failed to load settlement state: "+error);
                    else loaded=true;
                    return;
                }
                RebirthNpcSettlementPersistentState state=Read(document.DocumentElement);
                if(!RebirthNpcSettlementSimulation.TryRestorePersistentState(state,out error))throw new InvalidDataException(error);
                restored=(state.Needs?.Length??0)+(state.Professions?.Length??0)+(state.WorkOrders?.Length??0)+(state.Resources?.Length??0)+(state.ResourceReservations?.Length??0);
                int loadedFormat=int.Parse(document.DocumentElement.GetAttribute("format"),CultureInfo.InvariantCulture);
                RebirthNpcPersistenceSchemaTelemetry.RecordLoad("settlement",path,loadedFormat,FormatVersion,source);
                dirty=loadedFormat<FormatVersion;
                loaded=true;
            }
            catch(Exception ex)
            {
                RebirthNpcSettlementSimulation.ClearPersistentRuntimeState(); rejected++;
                RebirthNpcPersistenceSchemaTelemetry.RecordRejected("settlement",path,FormatVersion,ex.GetType().Name+": "+ex.Message);
                Log.Warning("[REBIRTH NPC] Failed to load settlement state: "+ex.GetType().Name+": "+ex.Message);
            }
        }
    }
    public static void MarkDirty(){if(IsServer())dirty=true;}
    public static void Save()
    {
        if(!IsServer())return;EnsureLoaded();lock(Sync){if(!dirty && !RebirthNpcPersistenceCoordinator.IsCheckpointWrite)return;string path=GetPath();RebirthNpcPersistenceFile.RequireCheckpointLoaded(loaded,path);if(string.IsNullOrEmpty(path))return;
        try{XmlDocument d=Write(RebirthNpcSettlementSimulation.CapturePersistentState());RebirthNpcPersistenceSchemaTelemetry.PrepareUpgradeSnapshot("settlement",path,FormatVersion);RebirthNpcPersistenceFile.SaveAtomic(path,d);dirty=false;saves++;}
        catch(Exception ex){Log.Warning("[REBIRTH NPC] Failed to save settlement state: "+ex.GetType().Name+": "+ex.Message);throw;}}
    }
    public static void Reset(bool save){if(save)Save();lock(Sync){RebirthNpcSettlementSimulation.ClearPersistentRuntimeState();loaded=false;dirty=false;nextSaveUtcTicks=0;}}
    public static string GetReport(){EnsureLoaded();return "[REBIRTH NPC Settlement Persistence] loaded="+loaded+" dirty="+dirty+" restored="+restored+" reconciled="+reconciled+" rejected="+rejected+" saves="+saves+" file="+GetPath();}

    private static XmlDocument Write(RebirthNpcSettlementPersistentState state)
    {
        XmlDocument d=new XmlDocument();XmlElement root=d.CreateElement("rebirthNpcSettlementState");root.SetAttribute("format",FormatVersion.ToString(CultureInfo.InvariantCulture));d.AppendChild(root);
        foreach(RebirthNpcNeedPersistentRecord r in state.Needs){XmlElement e=d.CreateElement("needs");e.SetAttribute("npc",r.NpcId.ToString());e.SetAttribute("last",r.LastUpdateUtcTicks.ToString(CultureInfo.InvariantCulture));foreach(KeyValuePair<RebirthNpcNeedKind,float> v in r.Values)e.SetAttribute(v.Key.ToString(),v.Value.ToString("R",CultureInfo.InvariantCulture));root.AppendChild(e);}
        foreach(RebirthNpcProfessionPersistentRecord r in state.Professions){XmlElement e=d.CreateElement("profession");e.SetAttribute("npc",r.NpcId.ToString());e.SetAttribute("id",r.ProfessionId);e.SetAttribute("settlement",r.SettlementId);e.SetAttribute("workplace",r.WorkplaceId);e.SetAttribute("revision",r.Revision.ToString(CultureInfo.InvariantCulture));foreach(KeyValuePair<string,float> v in r.Skills){XmlElement s=d.CreateElement("skill");s.SetAttribute("id",v.Key);s.SetAttribute("xp",v.Value.ToString("R",CultureInfo.InvariantCulture));e.AppendChild(s);}root.AppendChild(e);}
        foreach(RebirthNpcSettlementResourcePersistentRecord r in state.Resources??new RebirthNpcSettlementResourcePersistentRecord[0]){XmlElement e=d.CreateElement("resource");e.SetAttribute("settlement",r.SettlementId);e.SetAttribute("key",r.ResourceKey);e.SetAttribute("quantity",r.Quantity.ToString(CultureInfo.InvariantCulture));e.SetAttribute("revision",r.Revision.ToString(CultureInfo.InvariantCulture));root.AppendChild(e);}
        foreach(RebirthNpcResourceReservationPersistentRecord r in state.ResourceReservations??new RebirthNpcResourceReservationPersistentRecord[0]){XmlElement e=d.CreateElement("reservation");e.SetAttribute("work",r.WorkId.ToString("N"));e.SetAttribute("settlement",r.SettlementId);e.SetAttribute("key",r.ResourceKey);e.SetAttribute("quantity",r.Quantity.ToString(CultureInfo.InvariantCulture));e.SetAttribute("state",((int)r.State).ToString(CultureInfo.InvariantCulture));e.SetAttribute("created",r.CreatedUtcTicks.ToString(CultureInfo.InvariantCulture));root.AppendChild(e);}
        foreach(RebirthNpcWorkOrder w in state.WorkOrders){XmlElement e=d.CreateElement("work");e.SetAttribute("id",w.WorkId.ToString("N"));e.SetAttribute("settlement",w.SettlementId);e.SetAttribute("type",w.WorkType);e.SetAttribute("profession",w.RequiredProfession);e.SetAttribute("priority",w.Priority.ToString(CultureInfo.InvariantCulture));e.SetAttribute("state",((int)w.State).ToString(CultureInfo.InvariantCulture));e.SetAttribute("claimedBy",w.ClaimedBy.ToString());e.SetAttribute("claimExpires",w.ClaimExpiresUtcTicks.ToString(CultureInfo.InvariantCulture));e.SetAttribute("created",w.CreatedUtcTicks.ToString(CultureInfo.InvariantCulture));e.SetAttribute("resource",w.ResourceKey);e.SetAttribute("quantity",w.ResourceQuantity.ToString(CultureInfo.InvariantCulture));e.SetAttribute("x",w.TargetPosition.x.ToString("R",CultureInfo.InvariantCulture));e.SetAttribute("y",w.TargetPosition.y.ToString("R",CultureInfo.InvariantCulture));e.SetAttribute("z",w.TargetPosition.z.ToString("R",CultureInfo.InvariantCulture));e.SetAttribute("duration",w.DurationSeconds.ToString(CultureInfo.InvariantCulture));e.SetAttribute("executionStarted",w.ExecutionStartedUtcTicks.ToString(CultureInfo.InvariantCulture));e.SetAttribute("executionElapsed",w.ExecutionElapsedTicks.ToString(CultureInfo.InvariantCulture));e.SetAttribute("executionHeartbeat",w.LastExecutionHeartbeatUtcTicks.ToString(CultureInfo.InvariantCulture));e.SetAttribute("executionAttempts",w.ExecutionAttemptCount.ToString(CultureInfo.InvariantCulture));e.SetAttribute("executionDetail",w.LastExecutionDetail??string.Empty);root.AppendChild(e);}return d;
    }
    private static RebirthNpcSettlementPersistentState Read(XmlElement root)
    {
        List<RebirthNpcNeedPersistentRecord> needs=new List<RebirthNpcNeedPersistentRecord>();List<RebirthNpcProfessionPersistentRecord> prof=new List<RebirthNpcProfessionPersistentRecord>();List<RebirthNpcWorkOrder> work=new List<RebirthNpcWorkOrder>();List<RebirthNpcSettlementResourcePersistentRecord> resources=new List<RebirthNpcSettlementResourcePersistentRecord>();List<RebirthNpcResourceReservationPersistentRecord> reservations=new List<RebirthNpcResourceReservationPersistentRecord>();
        foreach(XmlNode node in root.ChildNodes){XmlElement e=node as XmlElement;if(e==null)continue;
            if(e.Name=="needs"){RebirthNpcStableId id;long last;if(!RebirthNpcStableId.TryParse(e.GetAttribute("npc"),out id)||!long.TryParse(e.GetAttribute("last"),out last))throw new InvalidDataException("Invalid needs record.");Dictionary<RebirthNpcNeedKind,float> values=new Dictionary<RebirthNpcNeedKind,float>();foreach(RebirthNpcNeedKind k in Enum.GetValues(typeof(RebirthNpcNeedKind))){float v=0;float.TryParse(e.GetAttribute(k.ToString()),NumberStyles.Float,CultureInfo.InvariantCulture,out v);values[k]=v;}needs.Add(new RebirthNpcNeedPersistentRecord{NpcId=id,LastUpdateUtcTicks=last,Values=values});}
            else if(e.Name=="profession"){RebirthNpcStableId id;uint rev;if(!RebirthNpcStableId.TryParse(e.GetAttribute("npc"),out id)||!uint.TryParse(e.GetAttribute("revision"),out rev))throw new InvalidDataException("Invalid profession record.");Dictionary<string,float> skills=new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase);foreach(XmlNode sn in e.ChildNodes){XmlElement se=sn as XmlElement;float xp;if(se==null||se.Name!="skill"||!float.TryParse(se.GetAttribute("xp"),NumberStyles.Float,CultureInfo.InvariantCulture,out xp))throw new InvalidDataException("Invalid skill record.");skills.Add(se.GetAttribute("id"),xp);}prof.Add(new RebirthNpcProfessionPersistentRecord{NpcId=id,ProfessionId=e.GetAttribute("id"),SettlementId=e.GetAttribute("settlement"),WorkplaceId=e.GetAttribute("workplace"),Revision=rev,Skills=skills});}
            else if(e.Name=="resource"){long quantity;uint revision;if(!long.TryParse(e.GetAttribute("quantity"),NumberStyles.Integer,CultureInfo.InvariantCulture,out quantity)||!uint.TryParse(e.GetAttribute("revision"),NumberStyles.Integer,CultureInfo.InvariantCulture,out revision)||quantity<0)throw new InvalidDataException("Invalid resource record.");resources.Add(new RebirthNpcSettlementResourcePersistentRecord{SettlementId=e.GetAttribute("settlement"),ResourceKey=e.GetAttribute("key"),Quantity=quantity,Revision=revision});}
            else if(e.Name=="reservation"){Guid workId;int quantity,state;long created;if(!Guid.TryParseExact(e.GetAttribute("work"),"N",out workId)||!int.TryParse(e.GetAttribute("quantity"),NumberStyles.Integer,CultureInfo.InvariantCulture,out quantity)||!int.TryParse(e.GetAttribute("state"),NumberStyles.Integer,CultureInfo.InvariantCulture,out state)||!long.TryParse(e.GetAttribute("created"),NumberStyles.Integer,CultureInfo.InvariantCulture,out created)||quantity<=0||state<0||state>1)throw new InvalidDataException("Invalid resource reservation.");reservations.Add(new RebirthNpcResourceReservationPersistentRecord{WorkId=workId,SettlementId=e.GetAttribute("settlement"),ResourceKey=e.GetAttribute("key"),Quantity=quantity,State=(RebirthNpcResourceReservationState)state,CreatedUtcTicks=created});}
            else if(e.Name=="work"){Guid id;int priority,state,qty,duration;long expires,created;float x,y,z;RebirthNpcStableId claimed=default(RebirthNpcStableId);string claimedText=e.GetAttribute("claimedBy");if(!string.IsNullOrEmpty(claimedText))RebirthNpcStableId.TryParse(claimedText,out claimed);if(!Guid.TryParseExact(e.GetAttribute("id"),"N",out id)||!int.TryParse(e.GetAttribute("priority"),out priority)||!int.TryParse(e.GetAttribute("state"),out state)||!long.TryParse(e.GetAttribute("claimExpires"),out expires)||!long.TryParse(e.GetAttribute("created"),out created)||!int.TryParse(e.GetAttribute("quantity"),out qty)||!float.TryParse(e.GetAttribute("x"),NumberStyles.Float,CultureInfo.InvariantCulture,out x)||!float.TryParse(e.GetAttribute("y"),NumberStyles.Float,CultureInfo.InvariantCulture,out y)||!float.TryParse(e.GetAttribute("z"),NumberStyles.Float,CultureInfo.InvariantCulture,out z)||!int.TryParse(e.GetAttribute("duration"),out duration))throw new InvalidDataException("Invalid work record.");long executionStarted=0,executionElapsed=0,executionHeartbeat=0;int executionAttempts=0;long.TryParse(e.GetAttribute("executionStarted"),NumberStyles.Integer,CultureInfo.InvariantCulture,out executionStarted);long.TryParse(e.GetAttribute("executionElapsed"),NumberStyles.Integer,CultureInfo.InvariantCulture,out executionElapsed);long.TryParse(e.GetAttribute("executionHeartbeat"),NumberStyles.Integer,CultureInfo.InvariantCulture,out executionHeartbeat);int.TryParse(e.GetAttribute("executionAttempts"),NumberStyles.Integer,CultureInfo.InvariantCulture,out executionAttempts);work.Add(new RebirthNpcWorkOrder{WorkId=id,SettlementId=e.GetAttribute("settlement"),WorkType=e.GetAttribute("type"),RequiredProfession=e.GetAttribute("profession"),Priority=priority,State=(RebirthNpcWorkState)state,ClaimedBy=claimed,ClaimExpiresUtcTicks=expires,CreatedUtcTicks=created,ResourceKey=e.GetAttribute("resource"),ResourceQuantity=qty,TargetPosition=new Vector3(x,y,z),DurationSeconds=duration,ExecutionStartedUtcTicks=Math.Max(0,executionStarted),ExecutionElapsedTicks=Math.Max(0,executionElapsed),LastExecutionHeartbeatUtcTicks=Math.Max(0,executionHeartbeat),ExecutionAttemptCount=Math.Max(0,executionAttempts),LastExecutionDetail=e.GetAttribute("executionDetail")});}
            else throw new InvalidDataException("Unknown settlement element: "+e.Name);
        }
        return new RebirthNpcSettlementPersistentState{Needs=needs.ToArray(),Professions=prof.ToArray(),WorkOrders=work.ToArray(),Resources=resources.ToArray(),ResourceReservations=reservations.ToArray()};
    }
    private static bool ValidateDocument(XmlDocument d)
    {
        XmlElement root=d?.DocumentElement;int format;
        if(root==null||root.Name!="rebirthNpcSettlementState"||!int.TryParse(root.GetAttribute("format"),out format)||format<1||format>FormatVersion)return false;
        string error;
        return RebirthNpcSettlementSimulation.ValidatePersistentState(Read(root),out error);
    }
    private static string GetPath(){string dir=GameIO.GetSaveGameDir();return string.IsNullOrEmpty(dir)?string.Empty:Path.Combine(dir,FileName);}
    private static bool IsServer(){return SingletonMonoBehaviour<ConnectionManager>.Instance!=null&&SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer;}
}

[Preserve]
public sealed class RebirthNpcSettlementPersistenceModApi:IModApi
{
    public void InitMod(Mod mod){ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));}
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)=>RebirthNpcSettlementPersistenceStore.Reset(true);
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data)=>RebirthNpcSettlementPersistenceStore.Reset(true);
}
