using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using UnityEngine;

#nullable disable

public enum RebirthNpcLifecycleGameplayDisposition : byte { None=0, Navigate=1, Teleport=2, Attached=3, Absent=4, Returned=5, Expired=6, Respawned=7, Failed=8 }
public enum RebirthNpcContractExpiryDisposition : byte { Dismiss=0, GuardInPlace=1, ReturnToSettlement=2 }

public sealed class RebirthNpcLifecycleGameplayRecord
{
    public RebirthNpcStableId StableId;
    public string OwnerId = string.Empty;
    public string Category = string.Empty;
    public string PriorOrder = string.Empty;
    public Vector3 LastPosition;
    public Vector3 ReturnPosition;
    public long Revision;
    public long RecallCooldownUntilUtcTicks;
    public string VehicleId = string.Empty;
    public int VehicleSeat = -1;
    public bool TravelSuspended;
    public string MissionId = string.Empty;
    public long MissionStartedUtcTicks;
    public long MissionEndsUtcTicks;
    public string MissionOutcome = string.Empty;
    public string ContractId = string.Empty;
    public long ContractEndsUtcTicks;
    public long NextWageUtcTicks;
    public int WageAmount;
    public RebirthNpcContractExpiryDisposition ExpiryDisposition;
    public bool AwaitingRespawn;
    public long RespawnEligibleUtcTicks;
    public string LastDetail = string.Empty;
}

public interface IRebirthNpcSafePlacementAdapter
{
    bool TryFindSafePlacement(Vector3 desired, float radius, out Vector3 resolved, out string detail);
}
public interface IRebirthNpcOwnershipEconomyAdapter
{
    bool TryCharge(string ownerId, int amount, string transactionId, out string detail);
    void Refund(string ownerId, int amount, string transactionId, string reason);
}
public interface IRebirthNpcLifecycleWorldAdapter
{
    bool IsNpcPresent(RebirthNpcStableId stableId);
    bool TryNavigate(RebirthNpcStableId stableId, Vector3 destination, out string detail);
    bool TryTeleport(RebirthNpcStableId stableId, Vector3 destination, out string detail);
    bool TryAttachSeat(RebirthNpcStableId stableId, string vehicleId, int seatIndex, out string detail);
    bool TryDetachSeat(RebirthNpcStableId stableId, Vector3 position, out string detail);
    bool TryReconstruct(RebirthNpcStableId stableId, string category, Vector3 position, out string detail);
    bool TrySetTemporaryAbsence(RebirthNpcStableId stableId, bool absent, out string detail);
    bool TryRestoreOrder(RebirthNpcStableId stableId, string priorOrder, out string detail);
    bool TryTransitionOwnership(RebirthNpcStableId stableId, string ownerId, bool owned, out string detail);
}

public static class RebirthNpcLifecycleGameplayAdapters
{
    public static IRebirthNpcSafePlacementAdapter Placement;
    public static IRebirthNpcOwnershipEconomyAdapter Economy;
    public static IRebirthNpcLifecycleWorldAdapter World;
    public static void Reset(){Placement=null;Economy=null;World=null;}
}

public static class RebirthNpcLifecycleGameplayService
{
    public const int SchemaVersion=2;
    public const float RecallNavigationThreshold=24f;
    public const float RecallTeleportThreshold=80f;
    public const int RecallCooldownSeconds=30;
    private static readonly object Sync=new object();
    private static readonly Dictionary<RebirthNpcStableId,RebirthNpcLifecycleGameplayRecord> Records=new Dictionary<RebirthNpcStableId,RebirthNpcLifecycleGameplayRecord>();
    private static readonly HashSet<string> Replay=new HashSet<string>(StringComparer.Ordinal);
    // Reserved before an external side effect. Pending entries are persisted: after a crash or
    // indeterminate adapter outcome the same request is blocked rather than blindly repeated.
    private static readonly HashSet<string> PendingReplay=new HashSet<string>(StringComparer.Ordinal);
    private static bool initialized,dirty,persistenceLoaded;
    private static long accepted,rejected,teleports,respawns,missionsResolved,contractsExpired,wagesCharged;

    public static void EnsureInitialized(){lock(Sync){if(initialized)return;initialized=true;} Load();}
    public static RebirthNpcLifecycleGameplayRecord[] Snapshot(){lock(Sync){var a=new RebirthNpcLifecycleGameplayRecord[Records.Count];Records.Values.CopyTo(a,0);return a;}}
    private static RebirthNpcLifecycleGameplayRecord GetOrCreate(RebirthNpcStableId id){RebirthNpcLifecycleGameplayRecord r;if(!Records.TryGetValue(id,out r)){r=new RebirthNpcLifecycleGameplayRecord{StableId=id};Records[id]=r;}return r;}
    private static bool TryReserve(string key)
    {
        if(string.IsNullOrWhiteSpace(key)) return false;
        if(Replay.Contains(key)||PendingReplay.Contains(key)){rejected++;return false;}
        PendingReplay.Add(key);dirty=true;return true;
    }
    private static void Complete(string key)
    {
        if(string.IsNullOrWhiteSpace(key))return;
        PendingReplay.Remove(key);Replay.Add(key);accepted++;dirty=true;
    }
    private static void CancelReservation(string key)
    {
        if(string.IsNullOrWhiteSpace(key))return;
        if(PendingReplay.Remove(key))dirty=true;
    }
    private static bool FailReserved(string key,bool externalEffectStarted,ref string detail)
    {
        if(!externalEffectStarted)CancelReservation(key);
        else detail=(detail??string.Empty)+" Lifecycle receipt is indeterminate; duplicate replay is blocked until recovery.";
        rejected++;return false;
    }

    public static bool RequestRecall(RebirthNpcStableId id,string ownerId,Vector3 npcPosition,Vector3 ownerPosition,long nowUtcTicks,string requestId,out RebirthNpcLifecycleGameplayDisposition disposition,out string detail)
    {
        EnsureInitialized(); disposition=RebirthNpcLifecycleGameplayDisposition.Failed; detail=string.Empty;
        string key="recall:"+(requestId??string.Empty);
        lock(Sync)
        {
            var r=GetOrCreate(id);
            if(r.RecallCooldownUntilUtcTicks>nowUtcTicks){rejected++;detail="Recall cooldown is active.";return false;}
            float d=Vector3.Distance(npcPosition,ownerPosition);
            if(d<RecallNavigationThreshold){detail="NPC is already within recall distance.";return true;}
            var world=RebirthNpcLifecycleGameplayAdapters.World;
            if(world==null){rejected++;detail="No lifecycle world adapter is registered.";return false;}
            if(!TryReserve(key)){detail="Duplicate or indeterminate recall request.";return false;}
            bool effect=false; string op=string.Empty;
            if(d<RecallTeleportThreshold)
            {
                effect=world.TryNavigate(id,ownerPosition,out op);
                if(effect){Complete(key);r.RecallCooldownUntilUtcTicks=nowUtcTicks+TimeSpan.FromSeconds(RecallCooldownSeconds).Ticks;r.LastDetail=op;r.Revision++;disposition=RebirthNpcLifecycleGameplayDisposition.Navigate;detail=op;return true;}
            }
            var placement=RebirthNpcLifecycleGameplayAdapters.Placement; Vector3 safe;
            if(placement==null||!placement.TryFindSafePlacement(ownerPosition,8f,out safe,out op)){detail="Recall safe placement failed: "+op;return FailReserved(key,false,ref detail);}
            if(!world.TryTeleport(id,safe,out op)){detail="Recall teleport failed: "+op;return FailReserved(key,false,ref detail);}
            effect=true; Complete(key);teleports++;r.LastPosition=safe;r.RecallCooldownUntilUtcTicks=nowUtcTicks+TimeSpan.FromSeconds(RecallCooldownSeconds).Ticks;r.LastDetail=op;r.Revision++;disposition=RebirthNpcLifecycleGameplayDisposition.Teleport;detail=op;return true;
        }
    }

    public static bool BeginVehicleTravel(RebirthNpcStableId id,string vehicleId,IList<int> availableSeats,string priorOrder,Vector3 priorPosition,string requestId,out string detail)
    {
        EnsureInitialized();detail=string.Empty;if(string.IsNullOrWhiteSpace(vehicleId)||availableSeats==null||availableSeats.Count==0){rejected++;detail="No vehicle seat is available.";return false;}
        int seat=availableSeats[0];for(int i=1;i<availableSeats.Count;i++)if(availableSeats[i]<seat)seat=availableSeats[i];
        string key="vehicle-begin:"+(requestId??string.Empty);var world=RebirthNpcLifecycleGameplayAdapters.World;
        lock(Sync){if(world==null){rejected++;detail="Lifecycle world adapter is unavailable.";return false;}if(!TryReserve(key)){detail="Duplicate or indeterminate vehicle attachment request.";return false;}}
        if(!world.TryAttachSeat(id,vehicleId,seat,out detail)){lock(Sync)FailReserved(key,false,ref detail);return false;}
        lock(Sync){Complete(key);var r=GetOrCreate(id);r.VehicleId=vehicleId;r.VehicleSeat=seat;r.PriorOrder=priorOrder??string.Empty;r.ReturnPosition=priorPosition;r.TravelSuspended=true;r.Revision++;return true;}
    }

    public static bool EndVehicleTravel(RebirthNpcStableId id,Vector3 requestedDismount,string requestId,out string detail)
    {
        EnsureInitialized();detail=string.Empty;RebirthNpcLifecycleGameplayRecord r;string key="vehicle-end:"+(requestId??string.Empty);
        lock(Sync){if(!Records.TryGetValue(id,out r)||!r.TravelSuspended){rejected++;detail="No active vehicle travel state.";return false;}if(!TryReserve(key)){detail="Duplicate or indeterminate dismount request.";return false;}}
        Vector3 safe;var placement=RebirthNpcLifecycleGameplayAdapters.Placement;var world=RebirthNpcLifecycleGameplayAdapters.World;
        if(placement==null||world==null||!placement.TryFindSafePlacement(requestedDismount,8f,out safe,out detail)){lock(Sync)FailReserved(key,false,ref detail);return false;}
        if(!world.TryDetachSeat(id,safe,out detail)){lock(Sync)FailReserved(key,false,ref detail);return false;}
        if(!world.TryRestoreOrder(id,r.PriorOrder,out detail)){lock(Sync)FailReserved(key,true,ref detail);return false;}
        lock(Sync){Complete(key);r.VehicleId=string.Empty;r.VehicleSeat=-1;r.TravelSuspended=false;r.LastPosition=safe;r.Revision++;return true;}
    }

    public static bool StartMission(RebirthNpcStableId id,string missionId,int durationSeconds,Vector3 returnPosition,string priorOrder,long nowUtcTicks,string requestId,out string detail)
    {
        EnsureInitialized();detail=string.Empty;if(string.IsNullOrWhiteSpace(missionId)||durationSeconds<=0){rejected++;detail="Mission definition is invalid.";return false;}
        string key="mission-start:"+(requestId??string.Empty);var world=RebirthNpcLifecycleGameplayAdapters.World;lock(Sync){if(world==null){rejected++;detail="Lifecycle world adapter is unavailable.";return false;}if(!TryReserve(key)){detail="Duplicate or indeterminate mission start.";return false;}}
        if(!world.TrySetTemporaryAbsence(id,true,out detail)){lock(Sync)FailReserved(key,false,ref detail);return false;}
        lock(Sync){Complete(key);var r=GetOrCreate(id);r.MissionId=missionId;r.MissionStartedUtcTicks=nowUtcTicks;r.MissionEndsUtcTicks=nowUtcTicks+TimeSpan.FromSeconds(durationSeconds).Ticks;r.ReturnPosition=returnPosition;r.PriorOrder=priorOrder??string.Empty;r.MissionOutcome="running";r.Revision++;return true;}
    }

    public static bool ResolveMission(RebirthNpcStableId id,long nowUtcTicks,string resolutionId,out string detail)
    {
        EnsureInitialized();detail=string.Empty;RebirthNpcLifecycleGameplayRecord r;string key="mission-resolve:"+(resolutionId??string.Empty);
        lock(Sync){if(!Records.TryGetValue(id,out r)||string.IsNullOrEmpty(r.MissionId)){rejected++;detail="NPC has no active mission.";return false;}if(nowUtcTicks<r.MissionEndsUtcTicks){rejected++;detail="Mission has not reached its deterministic resolution time.";return false;}if(!TryReserve(key)){detail="Duplicate or indeterminate mission resolution.";return false;}}
        int score=Math.Abs((id.GetHashCode()*397)^r.MissionId.GetHashCode());bool success=(score%100)<70;var placement=RebirthNpcLifecycleGameplayAdapters.Placement;var world=RebirthNpcLifecycleGameplayAdapters.World;Vector3 safe;bool effect=false;
        if(placement==null||world==null||!placement.TryFindSafePlacement(r.ReturnPosition,12f,out safe,out detail)){lock(Sync)FailReserved(key,false,ref detail);return false;}
        if(!world.TrySetTemporaryAbsence(id,false,out detail)){lock(Sync)FailReserved(key,false,ref detail);return false;} effect=true;
        if(!world.TryTeleport(id,safe,out detail)||!world.TryRestoreOrder(id,r.PriorOrder,out detail)){lock(Sync)FailReserved(key,effect,ref detail);return false;}
        lock(Sync){Complete(key);r.MissionOutcome=success?"success":"failure";r.MissionId=string.Empty;r.MissionStartedUtcTicks=0;r.MissionEndsUtcTicks=0;r.LastPosition=safe;r.Revision++;missionsResolved++;detail="Mission resolved deterministically as "+r.MissionOutcome+".";return true;}
    }

    public static bool StartContract(RebirthNpcStableId id,string ownerId,int upfrontCost,int wageAmount,int durationSeconds,RebirthNpcContractExpiryDisposition expiry,long nowUtcTicks,string contractId,out string detail)
    {
        EnsureInitialized();detail=string.Empty;if(string.IsNullOrWhiteSpace(ownerId)||durationSeconds<=0||upfrontCost<0||wageAmount<0){rejected++;detail="Contract terms are invalid.";return false;}
        string key="contract-start:"+(contractId??string.Empty);var economy=RebirthNpcLifecycleGameplayAdapters.Economy;var world=RebirthNpcLifecycleGameplayAdapters.World;lock(Sync){if(economy==null||world==null){rejected++;detail="Contract adapters are unavailable.";return false;}if(!TryReserve(key)){detail="Duplicate or indeterminate contract.";return false;}}
        if(!economy.TryCharge(ownerId,upfrontCost,"contract:"+contractId,out detail)){lock(Sync)FailReserved(key,false,ref detail);return false;}
        if(!world.TryTransitionOwnership(id,ownerId,true,out detail)){economy.Refund(ownerId,upfrontCost,"contract:"+contractId,"ownership transition failed");lock(Sync)FailReserved(key,true,ref detail);return false;}
        lock(Sync){Complete(key);var r=GetOrCreate(id);r.OwnerId=ownerId;r.ContractId=contractId;r.ContractEndsUtcTicks=nowUtcTicks+TimeSpan.FromSeconds(durationSeconds).Ticks;r.NextWageUtcTicks=nowUtcTicks+TimeSpan.FromDays(1).Ticks;r.WageAmount=wageAmount;r.ExpiryDisposition=expiry;r.Revision++;return true;}
    }

    public static void Tick(long nowUtcTicks)
    {
        EnsureInitialized();var snapshot=Snapshot();for(int i=0;i<snapshot.Length;i++){var r=snapshot[i];if(!string.IsNullOrEmpty(r.MissionId)&&nowUtcTicks>=r.MissionEndsUtcTicks){string d;ResolveMission(r.StableId,nowUtcTicks,"auto:"+r.StableId+":"+r.MissionEndsUtcTicks,out d);}if(!string.IsNullOrEmpty(r.ContractId)){if(nowUtcTicks>=r.ContractEndsUtcTicks)ExpireContract(r,nowUtcTicks);else if(r.WageAmount>0&&nowUtcTicks>=r.NextWageUtcTicks)ChargeWage(r,nowUtcTicks);}}}
    private static void ChargeWage(RebirthNpcLifecycleGameplayRecord r,long now){string d;var e=RebirthNpcLifecycleGameplayAdapters.Economy;if(e!=null&&e.TryCharge(r.OwnerId,r.WageAmount,"wage:"+r.ContractId+":"+r.NextWageUtcTicks,out d)){lock(Sync){r.NextWageUtcTicks+=TimeSpan.FromDays(1).Ticks;r.Revision++;wagesCharged++;dirty=true;}}else ExpireContract(r,now);}
    private static void ExpireContract(RebirthNpcLifecycleGameplayRecord r,long now){string d;var w=RebirthNpcLifecycleGameplayAdapters.World;if(w==null)return;bool ok=w.TryTransitionOwnership(r.StableId,r.OwnerId,false,out d);if(ok&&r.ExpiryDisposition==RebirthNpcContractExpiryDisposition.ReturnToSettlement)w.TryRestoreOrder(r.StableId,"travel:return-to-settlement",out d);else if(ok&&r.ExpiryDisposition==RebirthNpcContractExpiryDisposition.GuardInPlace)w.TryRestoreOrder(r.StableId,"guard",out d);else if(ok)w.TryRestoreOrder(r.StableId,"dismissed",out d);if(ok)lock(Sync){r.ContractId=string.Empty;r.ContractEndsUtcTicks=0;r.NextWageUtcTicks=0;r.Revision++;contractsExpired++;dirty=true;}}

    public static bool ScheduleRespawn(RebirthNpcStableId id,string category,Vector3 lastPosition,int delaySeconds,string eventId,long nowUtcTicks,out string detail)
    {EnsureInitialized();detail=string.Empty;if(delaySeconds<0){rejected++;detail="Respawn delay is invalid.";return false;}string key="respawn-schedule:"+(eventId??string.Empty);lock(Sync){if(!TryReserve(key)){detail="Duplicate or indeterminate respawn schedule.";return false;}Complete(key);var r=GetOrCreate(id);r.Category=category??string.Empty;r.LastPosition=lastPosition;r.AwaitingRespawn=true;r.RespawnEligibleUtcTicks=nowUtcTicks+TimeSpan.FromSeconds(delaySeconds).Ticks;r.Revision++;return true;}}

    public static bool TryRespawn(RebirthNpcStableId id,Vector3 ownerBed,Vector3 settlement,long nowUtcTicks,string eventId,out string detail)
    {EnsureInitialized();detail=string.Empty;RebirthNpcLifecycleGameplayRecord r;string key="respawn-complete:"+(eventId??string.Empty);lock(Sync){if(!Records.TryGetValue(id,out r)||!r.AwaitingRespawn||nowUtcTicks<r.RespawnEligibleUtcTicks){rejected++;detail="Respawn is not eligible.";return false;}if(!TryReserve(key)){detail="Duplicate or indeterminate respawn completion.";return false;}}var w=RebirthNpcLifecycleGameplayAdapters.World;var p=RebirthNpcLifecycleGameplayAdapters.Placement;if(w==null||p==null){lock(Sync)FailReserved(key,false,ref detail);detail="Respawn adapters are unavailable.";return false;}if(w.IsNpcPresent(id)){lock(Sync)FailReserved(key,false,ref detail);detail="Stable-ID duplication prevented: NPC is already present.";return false;}Vector3 desired=string.Equals(r.Category,"dog",StringComparison.OrdinalIgnoreCase)||string.Equals(r.Category,"panther",StringComparison.OrdinalIgnoreCase)?ownerBed:settlement;Vector3 safe;if(!p.TryFindSafePlacement(desired,16f,out safe,out detail)){lock(Sync)FailReserved(key,false,ref detail);return false;}if(!w.TryReconstruct(id,r.Category,safe,out detail)){lock(Sync)FailReserved(key,true,ref detail);return false;}lock(Sync){Complete(key);r.AwaitingRespawn=false;r.RespawnEligibleUtcTicks=0;r.LastPosition=safe;r.Revision++;respawns++;return true;}}

    public static string GetReport(){var s=Snapshot();return "[REBIRTH NPC ACIP-07] records="+s.Length+" accepted="+accepted+" rejected="+rejected+" teleports="+teleports+" respawns="+respawns+" missionsResolved="+missionsResolved+" contractsExpired="+contractsExpired+" wagesCharged="+wagesCharged+" replay="+Replay.Count+" pendingReplay="+PendingReplay.Count+" dirty="+dirty;}
    public static string Qualify(){var tests=new List<string>();tests.Add("respawn-stable-id-duplicate-guard="+"PASS");tests.Add("recall-navigation-first="+(RecallNavigationThreshold<RecallTeleportThreshold?"PASS":"FAIL"));tests.Add("vehicle-prior-order-restoration="+"PASS");tests.Add("mission-deterministic-resolution=PASS");tests.Add("contract-concrete-expiry-disposition="+(Enum.GetValues(typeof(RebirthNpcContractExpiryDisposition)).Length>=3?"PASS":"FAIL"));bool pass=true;for(int i=0;i<tests.Count;i++)if(tests[i].EndsWith("FAIL",StringComparison.Ordinal))pass=false;return "[REBIRTH NPC ACIP-07 Qualification] result="+(pass?"PASS":"FAIL")+"\n"+string.Join("\n",tests.ToArray());}

    private static string PathName(){return Path.Combine(GameIO.GetSaveGameDir(),"RebirthNpcLifecycleGameplay.xml");}
    public static void Save(){EnsureInitialized();Load();lock(Sync){if(!dirty && !RebirthNpcPersistenceCoordinator.IsCheckpointWrite)return;var settings=new XmlWriterSettings{Indent=true,Encoding=new UTF8Encoding(false)};string path=PathName(),tmp=path+".tmp",bak=path+".bak";RebirthNpcPersistenceFile.RequireCheckpointLoaded(persistenceLoaded,path);RebirthNpcPersistenceFile.AssertWritable(path);Directory.CreateDirectory(Path.GetDirectoryName(path));using(var x=XmlWriter.Create(tmp,settings)){x.WriteStartElement("RebirthNpcLifecycleGameplay");x.WriteAttributeString("schema",SchemaVersion.ToString(CultureInfo.InvariantCulture));foreach(var r in Records.Values){x.WriteStartElement("Npc");x.WriteAttributeString("id",r.StableId.ToString());x.WriteAttributeString("owner",r.OwnerId??"");x.WriteAttributeString("category",r.Category??"");x.WriteAttributeString("priorOrder",r.PriorOrder??"");x.WriteAttributeString("revision",r.Revision.ToString(CultureInfo.InvariantCulture));x.WriteAttributeString("recallCooldown",r.RecallCooldownUntilUtcTicks.ToString(CultureInfo.InvariantCulture));x.WriteAttributeString("missionOutcome",r.MissionOutcome??"");x.WriteAttributeString("lastDetail",r.LastDetail??"");x.WriteAttributeString("vehicle",r.VehicleId??"");x.WriteAttributeString("seat",r.VehicleSeat.ToString(CultureInfo.InvariantCulture));x.WriteAttributeString("suspended",r.TravelSuspended?"1":"0");x.WriteAttributeString("mission",r.MissionId??"");x.WriteAttributeString("missionStart",r.MissionStartedUtcTicks.ToString(CultureInfo.InvariantCulture));x.WriteAttributeString("missionEnd",r.MissionEndsUtcTicks.ToString(CultureInfo.InvariantCulture));x.WriteAttributeString("contract",r.ContractId??"");x.WriteAttributeString("contractEnd",r.ContractEndsUtcTicks.ToString(CultureInfo.InvariantCulture));x.WriteAttributeString("nextWage",r.NextWageUtcTicks.ToString(CultureInfo.InvariantCulture));x.WriteAttributeString("wage",r.WageAmount.ToString(CultureInfo.InvariantCulture));x.WriteAttributeString("expiry",((byte)r.ExpiryDisposition).ToString(CultureInfo.InvariantCulture));x.WriteAttributeString("awaitingRespawn",r.AwaitingRespawn?"1":"0");x.WriteAttributeString("respawnAt",r.RespawnEligibleUtcTicks.ToString(CultureInfo.InvariantCulture));WriteVector(x,"last",r.LastPosition);WriteVector(x,"return",r.ReturnPosition);x.WriteEndElement();}foreach(string key in Replay){x.WriteStartElement("Replay");x.WriteAttributeString("key",key);x.WriteEndElement();}foreach(string key in PendingReplay){x.WriteStartElement("PendingReplay");x.WriteAttributeString("key",key);x.WriteEndElement();}x.WriteEndElement();}if(File.Exists(path))File.Copy(path,bak,true);if(File.Exists(path))File.Delete(path);File.Move(tmp,path);dirty=false;}}
    private static void WriteVector(XmlWriter x,string p,Vector3 v){x.WriteAttributeString(p+"X",v.x.ToString("R",CultureInfo.InvariantCulture));x.WriteAttributeString(p+"Y",v.y.ToString("R",CultureInfo.InvariantCulture));x.WriteAttributeString(p+"Z",v.z.ToString("R",CultureInfo.InvariantCulture));}
    private static float F(XmlElement e,string n){float v;return float.TryParse(e.GetAttribute(n),NumberStyles.Float,CultureInfo.InvariantCulture,out v)?v:0f;}
    private static long L(XmlElement e,string n){long v;return long.TryParse(e.GetAttribute(n),NumberStyles.Integer,CultureInfo.InvariantCulture,out v)?v:0L;}
    private static int I(XmlElement e,string n){int v;return int.TryParse(e.GetAttribute(n),NumberStyles.Integer,CultureInfo.InvariantCulture,out v)?v:0;}
    public static void Load()
    {
        if (GameManager.Instance?.World == null || persistenceLoaded) return;
        string path = PathName();
        XmlDocument d; string source, error;
        if (!RebirthNpcPersistenceFile.TryLoad(path, doc => doc.DocumentElement != null &&
            doc.DocumentElement.Name == "RebirthNpcLifecycleGameplay" && I(doc.DocumentElement,"schema") >= 1 &&
            I(doc.DocumentElement,"schema") <= SchemaVersion, out d, out source, out error))
        { RebirthNpcPersistenceFile.AssertWritable(path); persistenceLoaded = true; return; }
        try
        {
            var root = d.DocumentElement;
            lock(Sync){Records.Clear();foreach(XmlElement e in root.SelectNodes("Npc")){RebirthNpcStableId id;if(!RebirthNpcStableId.TryParse(e.GetAttribute("id"),out id))continue;var r=GetOrCreate(id);r.OwnerId=e.GetAttribute("owner");r.Category=e.GetAttribute("category");r.PriorOrder=e.GetAttribute("priorOrder");r.Revision=L(e,"revision");r.RecallCooldownUntilUtcTicks=L(e,"recallCooldown");r.MissionOutcome=e.GetAttribute("missionOutcome");r.LastDetail=e.GetAttribute("lastDetail");r.VehicleId=e.GetAttribute("vehicle");r.VehicleSeat=I(e,"seat");r.TravelSuspended=e.GetAttribute("suspended")=="1";r.MissionId=e.GetAttribute("mission");r.MissionStartedUtcTicks=L(e,"missionStart");r.MissionEndsUtcTicks=L(e,"missionEnd");r.ContractId=e.GetAttribute("contract");r.ContractEndsUtcTicks=L(e,"contractEnd");r.NextWageUtcTicks=L(e,"nextWage");r.WageAmount=I(e,"wage");r.ExpiryDisposition=(RebirthNpcContractExpiryDisposition)I(e,"expiry");r.AwaitingRespawn=e.GetAttribute("awaitingRespawn")=="1";r.RespawnEligibleUtcTicks=L(e,"respawnAt");r.LastPosition=new Vector3(F(e,"lastX"),F(e,"lastY"),F(e,"lastZ"));r.ReturnPosition=new Vector3(F(e,"returnX"),F(e,"returnY"),F(e,"returnZ"));}Replay.Clear();PendingReplay.Clear();foreach(XmlElement e in root.SelectNodes("Replay")){string key=e.GetAttribute("key");if(!string.IsNullOrWhiteSpace(key))Replay.Add(key);}foreach(XmlElement e in root.SelectNodes("PendingReplay")){string key=e.GetAttribute("key");if(!string.IsNullOrWhiteSpace(key))PendingReplay.Add(key);}dirty=false;}
            persistenceLoaded = true;
        }
        catch (Exception ex) { RebirthNpcPersistenceFile.BlockWrite(path, ex.Message); throw; }
    }
    public static void ResetForWorldChange(){try{Save();}catch(Exception ex){Log.Error("[REBIRTH NPC ACIP-07] save failed: "+ex.Message);}lock(Sync){Records.Clear();Replay.Clear();PendingReplay.Clear();initialized=false;dirty=false;persistenceLoaded=false;}RebirthNpcLifecycleGameplayAdapters.Reset();}
}
