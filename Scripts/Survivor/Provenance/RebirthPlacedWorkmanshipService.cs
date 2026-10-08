using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

#nullable disable

public enum RebirthPlacedProvenanceKind : byte
{
    ConstructionPlacement = 1,
    TechnicalTrapPlacement = 2
}

public sealed class RebirthPlacedWorkmanshipRecord
{
    public int ClrIdx;
    public Vector3i Position;
    public string BlockName = string.Empty;
    public RebirthPlacedProvenanceKind Kind;
    public string CreatorStableId = string.Empty;
    public string BackgroundId = string.Empty;
    public string BonusId = string.Empty;
    public string SkillId = string.Empty;
    public float SkillValue;
    public float Workmanship;
    public int OriginalBaseMaxDurability;
    public ulong CreatedWorldTime;
}

/// <summary>
/// Server-owned world sidecar for ordinary placed blocks that have no native authored metadata.
/// Electrical configured-system state deliberately stays in RebirthInfrastructureWorkService and
/// crop provenance deliberately stays on TileEntityPlantGrowingRebirth.
/// </summary>
public static class RebirthPlacedWorkmanshipService
{
    private const uint Magic = 0x50574252U; // RBWP
    private const ushort Version = 1;
    private const int MaxRecords = 250000;
    private const float SaveInterval = 20f;
    private static readonly Dictionary<string,RebirthPlacedWorkmanshipRecord> records = new Dictionary<string,RebirthPlacedWorkmanshipRecord>(StringComparer.Ordinal);
    private static bool installed, loaded, dirty;
    private static float nextSave;

    private static readonly HashSet<string> TechnicalTrapNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "autoTurret", "bladeTrap", "dartTrap", "electricfencepost", "m60Turret", "shotgunTurret" };

    public static bool IsTechnicalTrapName(string blockName){return TechnicalTrapNames.Contains(blockName??string.Empty);}
    public static string GetTechnicalTrapSkillId(string blockName)
    {
        string n=(blockName??string.Empty).ToLowerInvariant();
        if(n=="electricfencepost")return "skill.electrical";
        if(n=="bladetrap")return "skill.metalworking";
        if(n=="darttrap")return "skill.mechanics";
        return "skill.deployable_turrets";
    }

    public static string Install(Harmony harmony)
    {
        if (installed) return "[REBIRTH Provenance World] already installed";
        installed = true;
        int placementPatches = RebirthPlacedWorkmanshipPatchInstaller.Install(harmony);
        ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnGameStarting));
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        return "[REBIRTH Provenance World] installed placementPatches="+placementPatches;
    }

    private static bool IsServer(){ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;return c!=null&&c.IsServer;}
    private static string Key(int clrIdx,Vector3i p){return clrIdx+"|"+p.x+","+p.y+","+p.z;}
    private static string PersistencePath { get { string root=GameIO.GetSaveGameDir();return string.IsNullOrEmpty(root)?string.Empty:Path.Combine(root,"RebirthData","Survivor","PlacedWorkmanship.dat"); } }
    private static void OnGameStarting(ref ModEvents.SGameStartingData data){records.Clear();loaded=false;dirty=false;if(!IsServer())return;Load();loaded=true;nextSave=Time.realtimeSinceStartup+SaveInterval;}
    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data){if(!loaded||!IsServer()||!dirty||Time.realtimeSinceStartup<nextSave)return;Save();nextSave=Time.realtimeSinceStartup+SaveInterval;}
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data){Reset(true);}
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data){Reset(true);}
    private static void Reset(bool save){if(save&&loaded&&dirty)Save();records.Clear();loaded=false;dirty=false;nextSave=0f;}

    public static void CapturePlacement(WorldBase world, BlockPlacement.Result result, EntityPlayer player, ItemValue heldBeforePlacement)
    {
        if(world==null||world.IsRemote()||!IsServer()||player==null||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        BlockValue current=world.GetBlock(result.blockPos);if(current.Block==null||current.isair)return;
        if(current.Block is BlockPlantGrowingRebirth)return;
        string blockName=current.Block.GetBlockName()??string.Empty;
        bool technical=TechnicalTrapNames.Contains(blockName);
        // Electrical configuration is a separate component and may coexist with Engineer trap provenance.
        bool restoredElectrical=RebirthInfrastructureWorkService.RestorePickedUpConfiguration(world,result.blockPos,heldBeforePlacement);
        if(RebirthInfrastructureWorkService.IsElectricalBlock(current.Block) && !restoredElectrical)RebirthInfrastructureWorkService.CaptureBuiltConfiguration(world,result.blockPos,player);
        if(RebirthInfrastructureWorkService.IsElectricalBlock(current.Block) && !technical)return;

        RebirthPlacedWorkmanshipRecord carried;
        if(TryReadCarried(heldBeforePlacement,result.blockPos,current,out carried))
        { records[Key(0,result.blockPos)]=carried;dirty=true;return; }

        RebirthProvenanceAuthorSnapshot author;if(!RebirthProvenanceIdentity.TryCapture(player,out author))return;
        string skillId=technical?GetTechnicalTrapSkillId(blockName):"skill.construction";
        float skill=0f;RebirthServiceCraftSkillService.TryGetSkillValue(player,skillId,out skill);
        float normalized=Mathf.Clamp01((Mathf.Clamp(skill,-50f,100f)+50f)/150f);
        RebirthPlacedWorkmanshipRecord r=new RebirthPlacedWorkmanshipRecord
        {
            ClrIdx=0,Position=result.blockPos,BlockName=blockName,
            Kind=technical?RebirthPlacedProvenanceKind.TechnicalTrapPlacement:RebirthPlacedProvenanceKind.ConstructionPlacement,
            CreatorStableId=author.StablePlayerId,BackgroundId=author.BackgroundId,BonusId=author.BonusId,
            SkillId=skillId,SkillValue=skill,Workmanship=Mathf.Lerp(0.45f,1f,normalized),
            OriginalBaseMaxDurability=Math.Max(0,current.Block.MaxDamage),CreatedWorldTime=GameManager.Instance!=null&&GameManager.Instance.World!=null?GameManager.Instance.World.worldTime:0UL
        };
        string placementKey=Key(0,result.blockPos);if(!records.ContainsKey(placementKey)&&records.Count>=MaxRecords){Log.Warning("[REBIRTH Provenance World] durable record cap reached; placement provenance not admitted at "+result.blockPos);return;}records[placementKey]=r;dirty=true;
    }

    public static void HandleUpgrade(WorldBase world,Vector3i pos,EntityPlayer player)
    {
        if(world==null||world.IsRemote()||!IsServer()||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        BlockValue current=world.GetBlock(pos);if(current.Block==null||current.isair)return;
        string blockName=current.Block.GetBlockName()??string.Empty;bool technical=TechnicalTrapNames.Contains(blockName);
        if(current.Block is BlockPlantGrowingRebirth){Remove(pos);return;}
        if(RebirthInfrastructureWorkService.IsElectricalBlock(current.Block)&&!technical){Remove(pos);return;}
        string key=Key(0,pos);RebirthPlacedWorkmanshipRecord r;
        if(records.TryGetValue(key,out r)&&r!=null)
        {
            r.Position=pos;r.BlockName=blockName;r.Kind=technical?RebirthPlacedProvenanceKind.TechnicalTrapPlacement:RebirthPlacedProvenanceKind.ConstructionPlacement;
            r.OriginalBaseMaxDurability=Math.Max(0,current.Block.MaxDamage);dirty=true;return;
        }
        RebirthProvenanceAuthorSnapshot author;if(player==null||!RebirthProvenanceIdentity.TryCapture(player,out author))return;
        string skillId=technical?GetTechnicalTrapSkillId(blockName):"skill.construction";float skill=0f;RebirthServiceCraftSkillService.TryGetSkillValue(player,skillId,out skill);
        float normalized=Mathf.Clamp01((Mathf.Clamp(skill,-50f,100f)+50f)/150f);if(!records.ContainsKey(key)&&records.Count>=MaxRecords){Log.Warning("[REBIRTH Provenance World] durable record cap reached; upgrade provenance not admitted at "+pos);return;}records[key]=new RebirthPlacedWorkmanshipRecord{ClrIdx=0,Position=pos,BlockName=blockName,Kind=technical?RebirthPlacedProvenanceKind.TechnicalTrapPlacement:RebirthPlacedProvenanceKind.ConstructionPlacement,CreatorStableId=author.StablePlayerId,BackgroundId=author.BackgroundId,BonusId=author.BonusId,SkillId=skillId,SkillValue=skill,Workmanship=Mathf.Lerp(0.45f,1f,normalized),OriginalBaseMaxDurability=Math.Max(0,current.Block.MaxDamage),CreatedWorldTime=GameManager.Instance!=null&&GameManager.Instance.World!=null?GameManager.Instance.World.worldTime:0UL};dirty=true;
    }

    public static bool TryGet(WorldBase world,Vector3i pos,out RebirthPlacedWorkmanshipRecord record)
    {
        record=null;RebirthPlacedWorkmanshipRecord r;if(!records.TryGetValue(Key(0,pos),out r)||r==null)return false;
        if(world!=null){BlockValue current=world.GetBlock(pos);string name=current.Block!=null?current.Block.GetBlockName():string.Empty;if(current.isair||!string.Equals(name,r.BlockName,StringComparison.OrdinalIgnoreCase)){records.Remove(Key(0,pos));dirty=true;return false;}}
        record=Clone(r);return true;
    }

    public static void Remove(Vector3i pos){if(records.Remove(Key(0,pos)))dirty=true;}

    public static void ApplyToPickedUpItem(WorldBase world,Vector3i pos,ItemValue value)
    {
        if(value==null)return;RebirthPlacedWorkmanshipRecord r;if(!TryGet(world,pos,out r))return;
        RebirthItemProvenanceAdapter.Stamp(value,new RebirthItemProvenanceSnapshot
        { Version=RebirthItemProvenanceAdapter.CurrentVersion,Kind="placed_block",SourceId=r.BlockName,CreatorStableId=r.CreatorStableId,
          BackgroundId=r.BackgroundId,BonusId=r.BonusId,SkillId=r.SkillId,SkillValue=r.SkillValue,Workmanship=r.Workmanship,
          OriginalMaxUseTimes=r.OriginalBaseMaxDurability,BatchToken="placed:"+r.CreatorStableId+":"+r.BlockName });
    }

    private static bool TryReadCarried(ItemValue value,Vector3i pos,BlockValue current,out RebirthPlacedWorkmanshipRecord record)
    {
        record=null;RebirthItemProvenanceSnapshot p;if(!RebirthItemProvenanceAdapter.TryRead(value,out p)||!string.Equals(p.Kind,"placed_block",StringComparison.Ordinal))return false;
        string blockName=current.Block!=null?(current.Block.GetBlockName()??string.Empty):string.Empty;
        record=new RebirthPlacedWorkmanshipRecord{ClrIdx=0,Position=pos,BlockName=blockName,
            Kind=TechnicalTrapNames.Contains(blockName)?RebirthPlacedProvenanceKind.TechnicalTrapPlacement:RebirthPlacedProvenanceKind.ConstructionPlacement,
            CreatorStableId=p.CreatorStableId,BackgroundId=p.BackgroundId,BonusId=p.BonusId,SkillId=p.SkillId,SkillValue=p.SkillValue,Workmanship=p.Workmanship,
            OriginalBaseMaxDurability=Math.Max(0,Mathf.RoundToInt(p.OriginalMaxUseTimes)),CreatedWorldTime=GameManager.Instance!=null&&GameManager.Instance.World!=null?GameManager.Instance.World.worldTime:0UL};
        return true;
    }

    public static string BuildDebugSummary(WorldBase world,Vector3i? pos)
    {
        StringBuilder b=new StringBuilder();b.AppendLine("[REBIRTH Provenance World] records="+records.Count+" loaded="+loaded+" persistence="+PersistencePath);
        if(pos.HasValue){RebirthPlacedWorkmanshipRecord r;if(TryGet(world,pos.Value,out r))b.Append("pos=").Append(pos.Value).Append(" block=").Append(r.BlockName).Append(" kind=").Append(r.Kind).Append(" creator=").Append(r.CreatorStableId).Append(" background=").Append(r.BackgroundId).Append(" bonus=").Append(r.BonusId).Append(" skill=").Append(r.SkillId).Append("@").Append(r.SkillValue.ToString("0.###")).Append(" workmanship=").Append(r.Workmanship.ToString("0.###")).Append(" originalMax=").Append(r.OriginalBaseMaxDurability).Append(" effectiveMax=").Append(RebirthWorkmanshipSignatureService.CalculateEffectivePlacedMax(r)).Append(" durabilityMultiplier=").Append(RebirthWorkmanshipSignatureService.CalculatePlacedDurabilityMultiplier(r).ToString("0.###"));else b.Append("pos=").Append(pos.Value).Append(" provenance=none");}
        return b.ToString();
    }

    private static RebirthPlacedWorkmanshipRecord Clone(RebirthPlacedWorkmanshipRecord r){return r==null?null:new RebirthPlacedWorkmanshipRecord{ClrIdx=r.ClrIdx,Position=r.Position,BlockName=r.BlockName,Kind=r.Kind,CreatorStableId=r.CreatorStableId,BackgroundId=r.BackgroundId,BonusId=r.BonusId,SkillId=r.SkillId,SkillValue=r.SkillValue,Workmanship=r.Workmanship,OriginalBaseMaxDurability=r.OriginalBaseMaxDurability,CreatedWorldTime=r.CreatedWorldTime};}
    private static void WriteString(BinaryWriter bw,string value){RebirthSurvivorNetworkCodec.WriteString(bw,value??string.Empty,512);}
    private static string ReadString(BinaryReader br){return RebirthSurvivorNetworkCodec.ReadString(br,512);}
    private static void Load()
    {
        records.Clear();
        string path=PersistencePath;
        if(string.IsNullOrEmpty(path))return;
        Dictionary<string,RebirthPlacedWorkmanshipRecord> staged;
        string error;
        if(TryLoadFile(path,out staged,out error)||TryLoadFile(path+".bak",out staged,out error))
        {
            foreach(KeyValuePair<string,RebirthPlacedWorkmanshipRecord> pair in staged)records[pair.Key]=pair.Value;
            return;
        }
        if(File.Exists(path)||File.Exists(path+".bak"))Log.Warning("[REBIRTH Provenance World] load failed; retained empty runtime pending explicit recovery: "+error);
    }

    private static bool TryLoadFile(string path,out Dictionary<string,RebirthPlacedWorkmanshipRecord> staged,out string error)
    {
        staged=null;error="missing";
        if(string.IsNullOrEmpty(path)||!File.Exists(path))return false;
        try
        {
            Dictionary<string,RebirthPlacedWorkmanshipRecord> candidate=new Dictionary<string,RebirthPlacedWorkmanshipRecord>(StringComparer.Ordinal);
            using(FileStream fs=File.OpenRead(path))using(BinaryReader br=new BinaryReader(fs))
            {
                if(br.ReadUInt32()!=Magic)throw new InvalidDataException("bad magic");
                if(br.ReadUInt16()!=Version)throw new InvalidDataException("unsupported version");
                int count=br.ReadInt32();if(count<0||count>MaxRecords)throw new InvalidDataException("record count "+count);
                for(int i=0;i<count;i++)
                {
                    RebirthPlacedWorkmanshipRecord r=new RebirthPlacedWorkmanshipRecord();r.ClrIdx=br.ReadInt32();r.Position=new Vector3i(br.ReadInt32(),br.ReadInt32(),br.ReadInt32());r.BlockName=ReadString(br);r.Kind=(RebirthPlacedProvenanceKind)br.ReadByte();r.CreatorStableId=ReadString(br);r.BackgroundId=ReadString(br);r.BonusId=ReadString(br);r.SkillId=ReadString(br);r.SkillValue=br.ReadSingle();r.Workmanship=br.ReadSingle();r.OriginalBaseMaxDurability=br.ReadInt32();r.CreatedWorldTime=br.ReadUInt64();candidate[Key(r.ClrIdx,r.Position)]=r;
                }
                if(fs.Position!=fs.Length)throw new InvalidDataException("trailing bytes after record payload");
            }
            staged=candidate;error=string.Empty;return true;
        }
        catch(Exception ex){error=ex.GetType().Name+": "+ex.Message;return false;}
    }

    public static void Save()
    {
        if(!IsServer())return;string path=PersistencePath;if(string.IsNullOrEmpty(path))return;
        try
        {
            if(records.Count>MaxRecords)throw new InvalidDataException("record count exceeds durable cap: "+records.Count);
            Directory.CreateDirectory(Path.GetDirectoryName(path));string tmp=path+".tmp";string bak=path+".bak";
            if(File.Exists(tmp))File.Delete(tmp);
            using(FileStream fs=File.Create(tmp))using(BinaryWriter bw=new BinaryWriter(fs))
            {
                bw.Write(Magic);bw.Write(Version);bw.Write(records.Count);
                foreach(RebirthPlacedWorkmanshipRecord r in records.Values){bw.Write(r.ClrIdx);bw.Write(r.Position.x);bw.Write(r.Position.y);bw.Write(r.Position.z);WriteString(bw,r.BlockName);bw.Write((byte)r.Kind);WriteString(bw,r.CreatorStableId);WriteString(bw,r.BackgroundId);WriteString(bw,r.BonusId);WriteString(bw,r.SkillId);bw.Write(r.SkillValue);bw.Write(r.Workmanship);bw.Write(r.OriginalBaseMaxDurability);bw.Write(r.CreatedWorldTime);}
                bw.Flush();fs.Flush();
            }
            Dictionary<string,RebirthPlacedWorkmanshipRecord> verified;string verifyError;
            if(!TryLoadFile(tmp,out verified,out verifyError)||verified.Count!=records.Count)throw new InvalidDataException("temporary verification failed: "+verifyError);
            if(File.Exists(path))File.Replace(tmp,path,bak,true);else File.Move(tmp,path);
            dirty=false;
        }
        catch(Exception ex){Log.Warning("[REBIRTH Provenance World] save failed; dirty state retained: "+ex.GetType().Name+": "+ex.Message);}
    }
}

public static class RebirthPlacedWorkmanshipPatchInstaller
{
    private static bool installed;
    public static int Install(Harmony harmony)
    {
        if(installed||harmony==null)return 0;installed=true;int count=0;
        MethodInfo prefix=AccessTools.Method(typeof(RebirthPlacedWorkmanshipPlacementPatch),nameof(RebirthPlacedWorkmanshipPlacementPatch.Prefix));
        MethodInfo postfix=AccessTools.Method(typeof(RebirthPlacedWorkmanshipPlacementPatch),nameof(RebirthPlacedWorkmanshipPlacementPatch.Postfix));
        MethodInfo finalizer=AccessTools.Method(typeof(RebirthPlacedWorkmanshipPlacementPatch),nameof(RebirthPlacedWorkmanshipPlacementPatch.Finalizer));
        HashSet<MethodBase> seen=new HashSet<MethodBase>();
        foreach(Assembly assembly in new[]{typeof(Block).Assembly,typeof(RebirthPlacedWorkmanshipService).Assembly})
        {
            Type[] types;try{types=assembly.GetTypes();}catch(ReflectionTypeLoadException ex){types=ex.Types;}
            if(types==null)continue;for(int i=0;i<types.Length;i++){Type t=types[i];if(t==null||!typeof(Block).IsAssignableFrom(t))continue;MethodInfo m=t.GetMethod(nameof(Block.PlaceBlock),BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly,null,new[]{typeof(WorldBase),typeof(BlockPlacement.Result),typeof(EntityAlive)},null);if(m==null||!seen.Add(m))continue;PatchProcessor processor=harmony.CreateProcessor(m);processor.AddPrefix(new HarmonyMethod(prefix));processor.AddPostfix(new HarmonyMethod(postfix));processor.AddFinalizer(new HarmonyMethod(finalizer));processor.Patch();count++;}
        }
        return count;
    }
}

public static class RebirthPlacedWorkmanshipPlacementPatch
{
    [ThreadStatic] private static int depth;
    [ThreadStatic] private static ItemValue held;
    public struct PlacementScopeState
    {
        public int PriorDepth;
        public ItemValue PriorHeld;
        public bool IsOutermost;
    }
    // Positional Harmony arguments are intentional. V3.2 renamed the placement result
    // parameter to _bpResult; using __0/__1/__2 prevents harmless upstream parameter-name
    // changes from aborting the entire Survivor progression installer.
    public static void Prefix(WorldBase __0,BlockPlacement.Result __1,EntityAlive __2,out PlacementScopeState __state)
    {
        __state=new PlacementScopeState{PriorDepth=depth,PriorHeld=held,IsOutermost=depth==0};
        depth=__state.PriorDepth+1;
        if(!__state.IsOutermost)return;
        EntityPlayer p=__2 as EntityPlayer;
        held=p!=null&&p.inventory!=null&&p.inventory.holdingItemItemValue!=null?p.inventory.holdingItemItemValue.Clone():null;
    }
    public static void Postfix(WorldBase __0,BlockPlacement.Result __1,EntityAlive __2,PlacementScopeState __state)
    {
        if(__state.IsOutermost)RebirthPlacedWorkmanshipService.CapturePlacement(__0,__1,__2 as EntityPlayer,held);
    }
    public static Exception Finalizer(Exception __exception,PlacementScopeState __state)
    {
        depth=__state.PriorDepth;
        held=__state.PriorHeld;
        return __exception;
    }
}

/// <summary>
/// Pickup transfer bridge. The authoritative server snapshots placed provenance before the base
/// pickup packet removes the block; the client then stamps that snapshot onto the fresh ItemValue
/// created by REBIRTH's existing pickup replacement path. Network ordering on the same connection
/// keeps the provenance packet ahead of the pickup grant packet.
/// </summary>
public static class RebirthPlacedWorkmanshipPickupBridge
{
    private sealed class PendingPickup
    {
        public long OperationId;
        public RebirthItemProvenanceSnapshot Snapshot;
        public DateTime ExpiresUtc;
    }
    private static readonly Dictionary<string,Queue<PendingPickup>> pending = new Dictionary<string,Queue<PendingPickup>>(StringComparer.Ordinal);
    private static string scope=string.Empty;
    private static long nextOperationId;
    private static readonly TimeSpan PendingLifetime=TimeSpan.FromSeconds(30);
    private static string Key(Vector3i p){return p.x+","+p.y+","+p.z;}

    private static void EnsureScope(WorldBase world)
    {
        string save=string.Empty;try{save=GameIO.GetSaveGameDir()??string.Empty;}catch{}
        string next=save+"|"+(world!=null?System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(world).ToString():"0");
        if(string.Equals(scope,next,StringComparison.Ordinal))return;scope=next;pending.Clear();
    }

    private static void CleanupExpired()
    {
        DateTime now=DateTime.UtcNow;List<string> empty=new List<string>();
        foreach(KeyValuePair<string,Queue<PendingPickup>> kv in pending){Queue<PendingPickup> q=kv.Value;while(q!=null&&q.Count>0&&(q.Peek()==null||q.Peek().ExpiresUtc<=now))q.Dequeue();if(q==null||q.Count==0)empty.Add(kv.Key);}
        for(int i=0;i<empty.Count;i++)pending.Remove(empty[i]);
    }

    public static void SendBeforePickup(World world,Vector3i pos,int playerId)
    {
        if(world==null||world.IsRemote())return;
        RebirthPlacedWorkmanshipRecord r;if(!RebirthPlacedWorkmanshipService.TryGet(world,pos,out r))return;
        RebirthItemProvenanceSnapshot p=ToItemSnapshot(r);
        long operationId=System.Threading.Interlocked.Increment(ref nextOperationId);
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(c==null)return;
        if(c.IsServer && world.GetEntity(playerId) is EntityPlayerLocal) Store(world,pos,p,operationId);
        c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPlacedProvenancePickup>().Setup(pos,p,operationId),_attachedToEntityId:playerId);
    }

    public static void ApplyPending(Vector3i pos,ItemValue value)
    {
        if(value==null)return;WorldBase world=GameManager.Instance!=null?GameManager.Instance.World:null;EnsureScope(world);CleanupExpired();string key=Key(pos);Queue<PendingPickup> q;if(!pending.TryGetValue(key,out q)||q==null||q.Count==0)return;PendingPickup entry=q.Dequeue();if(q.Count==0)pending.Remove(key);if(entry==null||entry.Snapshot==null||entry.ExpiresUtc<=DateTime.UtcNow)return;RebirthItemProvenanceAdapter.Stamp(value,entry.Snapshot);
    }

    public static void Store(WorldBase world,Vector3i pos,RebirthItemProvenanceSnapshot p,long operationId){if(p==null||operationId<=0)return;EnsureScope(world);CleanupExpired();string key=Key(pos);Queue<PendingPickup> q;if(!pending.TryGetValue(key,out q)||q==null){q=new Queue<PendingPickup>();pending[key]=q;}q.Enqueue(new PendingPickup{OperationId=operationId,Snapshot=p,ExpiresUtc=DateTime.UtcNow+PendingLifetime});}

    private static RebirthItemProvenanceSnapshot ToItemSnapshot(RebirthPlacedWorkmanshipRecord r)
    {
        return new RebirthItemProvenanceSnapshot{Version=RebirthItemProvenanceAdapter.CurrentVersion,Kind="placed_block",SourceId=r.BlockName,
            CreatorStableId=r.CreatorStableId,BackgroundId=r.BackgroundId,BonusId=r.BonusId,SkillId=r.SkillId,SkillValue=r.SkillValue,
            Workmanship=r.Workmanship,OriginalMaxUseTimes=r.OriginalBaseMaxDurability,BatchToken="placed:"+r.CreatorStableId+":"+r.BlockName};
    }
}

[UnityEngine.Scripting.Preserve]
public sealed class NetPackageRebirthPlacedProvenancePickup : NetPackage
{
    private Vector3i pos;
    private long operationId;
    private RebirthItemProvenanceSnapshot p = new RebirthItemProvenanceSnapshot();
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }
    public NetPackageRebirthPlacedProvenancePickup Setup(Vector3i position,RebirthItemProvenanceSnapshot snapshot,long id){pos=position;p=snapshot??new RebirthItemProvenanceSnapshot();operationId=id;return this;}
    public override void read(PooledBinaryReader reader){BinaryReader b=(BinaryReader)reader;pos=new Vector3i(b.ReadInt32(),b.ReadInt32(),b.ReadInt32());operationId=b.ReadInt64();p=new RebirthItemProvenanceSnapshot();p.Kind=RebirthSurvivorNetworkCodec.ReadString(b,64);p.SourceId=RebirthSurvivorNetworkCodec.ReadString(b,128);p.CreatorStableId=RebirthSurvivorNetworkCodec.ReadString(b,512);p.BackgroundId=RebirthSurvivorNetworkCodec.ReadString(b,128);p.BonusId=RebirthSurvivorNetworkCodec.ReadString(b,128);p.SkillId=RebirthSurvivorNetworkCodec.ReadString(b,128);p.SkillValue=b.ReadSingle();p.Workmanship=b.ReadSingle();p.OriginalMaxUseTimes=b.ReadSingle();p.BatchToken=RebirthSurvivorNetworkCodec.ReadString(b,512);}
    public override void write(PooledBinaryWriter writer){base.write(writer);BinaryWriter b=(BinaryWriter)writer;b.Write(pos.x);b.Write(pos.y);b.Write(pos.z);b.Write(operationId);RebirthSurvivorNetworkCodec.WriteString(b,p.Kind,64);RebirthSurvivorNetworkCodec.WriteString(b,p.SourceId,128);RebirthSurvivorNetworkCodec.WriteString(b,p.CreatorStableId,512);RebirthSurvivorNetworkCodec.WriteString(b,p.BackgroundId,128);RebirthSurvivorNetworkCodec.WriteString(b,p.BonusId,128);RebirthSurvivorNetworkCodec.WriteString(b,p.SkillId,128);b.Write(p.SkillValue);b.Write(p.Workmanship);b.Write(p.OriginalMaxUseTimes);RebirthSurvivorNetworkCodec.WriteString(b,p.BatchToken,512);}
    public override void ProcessPackage(World world,GameManager callbacks){if(operationId>0)RebirthPlacedWorkmanshipPickupBridge.Store(world,pos,p,operationId);}
    public int GetLength(){return 72+RebirthSurvivorNetworkCodec.EstimateString(p.Kind,64)+RebirthSurvivorNetworkCodec.EstimateString(p.SourceId,128)+RebirthSurvivorNetworkCodec.EstimateString(p.CreatorStableId,512)+RebirthSurvivorNetworkCodec.EstimateString(p.BackgroundId,128)+RebirthSurvivorNetworkCodec.EstimateString(p.BonusId,128)+RebirthSurvivorNetworkCodec.EstimateString(p.SkillId,128)+RebirthSurvivorNetworkCodec.EstimateString(p.BatchToken,512);}
}
