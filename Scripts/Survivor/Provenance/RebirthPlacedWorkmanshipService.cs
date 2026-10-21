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
    public string SourceIncarnation=string.Empty;
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
    private const ushort Version = 2;
    private const int MaxRecords = 250000;
    private const float SaveInterval = 20f;
    private static readonly Dictionary<string,RebirthPlacedWorkmanshipRecord> records = new Dictionary<string,RebirthPlacedWorkmanshipRecord>(StringComparer.Ordinal);
    private static bool installed, loaded, dirty;
    private static bool loadFailed, awaitingStart;
    private static bool startDoneRegistered,startingRegistered,updateRegistered,worldShutdownRegistered,shutdownRegistered;
    private static string loadedPath;
    private static WorldBase loadedWorld;
    private static float nextSave;
    private static object mutation=new object(),pendingMutation;
    private static string pendingTemporary,pendingDigest;
    private static void MarkDirty(){mutation=new object();dirty=true;}
    private static bool SafePath(string path)
    {
        try
        {
            if(string.IsNullOrEmpty(path))return false;
            for(var directory=new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(path)));directory!=null;directory=directory.Parent)
                if(directory.Exists&&(directory.Attributes&FileAttributes.ReparsePoint)!=0)return false;
            return !File.Exists(path)||(File.GetAttributes(path)&FileAttributes.ReparsePoint)==0;
        }catch{return false;}
    }

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
        if (installed){RebirthPlacedOriginal.Install(harmony);return "[REBIRTH Provenance World] already installed";}
        if(harmony==null)throw new ArgumentNullException(nameof(harmony));
        RebirthPlacedWorkmanshipIdentity.Install(harmony);
        int placementPatches = RebirthPlacedWorkmanshipPatchInstaller.Install(harmony);
        RebirthPlacedOriginal.Install(harmony);
        if(!startDoneRegistered){ModEvents.GameStartDone.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartDoneData>(OnGameStartDone));startDoneRegistered=true;}
        if(!startingRegistered){ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnGameStarting));startingRegistered=true;}
        if(!updateRegistered){ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));updateRegistered=true;}
        if(!worldShutdownRegistered){ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));worldShutdownRegistered=true;}
        if(!shutdownRegistered){ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));shutdownRegistered=true;}
        installed=true;
        return "[REBIRTH Provenance World] installed placementPatches="+placementPatches;
    }

    private static bool IsServer(){ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;return c!=null&&c.IsServer;}
    private static string Key(int clrIdx,Vector3i p){return clrIdx+"|"+p.x+","+p.y+","+p.z;}
    private static string CurrentPersistencePath { get { string root=GameIO.GetSaveGameDir();return string.IsNullOrWhiteSpace(root)?string.Empty:Path.Combine(Path.GetFullPath(root),"RebirthData","Survivor","PlacedWorkmanship.dat"); } }
    private static string PersistencePath { get { return loadedPath ?? CurrentPersistencePath; } }
    private static bool HasCurrentStore { get { return loaded && !loadFailed && ReferenceEquals(loadedWorld,GameManager.Instance!=null?GameManager.Instance.World:null) && string.Equals(loadedPath,CurrentPersistencePath,StringComparison.Ordinal); } }
    private static void OnGameStarting(ref ModEvents.SGameStartingData data)
    {
        // Native GameStarting precedes construction of the next World. Never
        // bind its sidecar to null/the previous world or a provisional save path.
        awaitingStart=false;
        if(loadFailed){Log.Warning("[REBIRTH Provenance World] unresolved original store retained; new world provenance unavailable.");return;}
        if(dirty){Save();if(dirty){Log.Warning("[REBIRTH Provenance World] prior save unresolved; new world provenance unavailable.");return;}}
        Reset(false);awaitingStart=true;
    }
    private static void OnGameStartDone(ref ModEvents.SGameStartDoneData data)
    {
        if(!awaitingStart)return;awaitingStart=false;
        var world=GameManager.Instance!=null?GameManager.Instance.World:null;
        if(!IsServer()||world==null||world.IsRemote()||dirty||loadFailed)return;
        loadedPath=CurrentPersistencePath;loadedWorld=world;
        Load();loaded=true;nextSave=Time.realtimeSinceStartup+SaveInterval;
    }
    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data){if(!loaded||!IsServer()||!dirty||Time.realtimeSinceStartup<nextSave)return;Save();nextSave=Time.realtimeSinceStartup+SaveInterval;}
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data){Reset(true);}
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data){Reset(true);}
    private static void Reset(bool save){if(save&&loaded&&dirty)Save();if(dirty||loadFailed)return;records.Clear();loaded=false;loadFailed=false;loadedPath=null;loadedWorld=null;nextSave=0f;awaitingStart=false;pendingTemporary=pendingDigest=null;pendingMutation=null;}
    internal static bool CanCaptureOriginal(WorldBase world)=>HasCurrentStore&&ReferenceEquals(loadedWorld,world);
    public static bool CanReset {get{return !dirty&&!loadFailed;}}
    public static bool TryFlushForShutdown(){if(dirty)Save();return CanReset;}
    public static bool TryResetAfterCoordinatedSave(){if(dirty||loadFailed)return false;Reset(false);return true;}

    public static void CapturePlacement(WorldBase world, BlockPlacement.Result result, EntityPlayer player, ItemValue heldBeforePlacement)
    {
        if(!HasCurrentStore||world==null||!ReferenceEquals(world,loadedWorld)||world.IsRemote()||!IsServer()||player==null||!ReferenceEquals(player.world,world)||!ReferenceEquals(GameManager.Instance.World.GetEntity(player.entityId),player)||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        BlockValue current=world.GetBlock(result.blockPos);if(current.Block==null||current.isair||current.type!=result.blockValue.type||!RebirthPlacedWorkmanshipIdentity.AcceptedPlacement(world,result.blockPos))return;
        if(current.Block is BlockPlantGrowingRebirth)return;
        string blockName=current.Block.GetBlockName()??string.Empty;
        bool technical=TechnicalTrapNames.Contains(blockName);
        // Electrical configuration is a separate component and may coexist with Engineer trap provenance.
        bool restoredElectrical=RebirthInfrastructureWorkService.RestorePickedUpConfiguration(world,result.blockPos,heldBeforePlacement);
        if(RebirthInfrastructureWorkService.IsElectricalBlock(current.Block) && !restoredElectrical)RebirthInfrastructureWorkService.CaptureBuiltConfiguration(world,result.blockPos,player);
        if(RebirthInfrastructureWorkService.IsElectricalBlock(current.Block) && !technical)return;

        RebirthPlacedWorkmanshipRecord carried;
        if(TryReadCarried(heldBeforePlacement,result.blockPos,current,out carried))
        { Admit(world,carried);return; }

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
        Admit(world,r);
    }

    internal static string FreezeRemotePlacement(WorldBase world,Vector3i position,EntityPlayer player,ItemValue input,BlockValue target)
    {
        if(!HasCurrentStore||world==null||!ReferenceEquals(world,loadedWorld)||world.IsRemote()||player==null||!ReferenceEquals(player.world,world)||!ReferenceEquals(GameManager.Instance.World.GetEntity(player.entityId),player)||target.Block==null||target.isair||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return null;
        RebirthPlacedWorkmanshipRecord r;
        if(!TryReadCarried(input,position,target,out r))
        {
            RebirthProvenanceAuthorSnapshot author;if(!RebirthProvenanceIdentity.TryCapture(player,out author))return null;
            string name=target.Block.GetBlockName();bool technical=TechnicalTrapNames.Contains(name);string skillId=technical?GetTechnicalTrapSkillId(name):"skill.construction";float skill=0;RebirthServiceCraftSkillService.TryGetSkillValue(player,skillId,out skill);
            r=new RebirthPlacedWorkmanshipRecord{Position=position,BlockName=name,Kind=technical?RebirthPlacedProvenanceKind.TechnicalTrapPlacement:RebirthPlacedProvenanceKind.ConstructionPlacement,CreatorStableId=author.StablePlayerId,BackgroundId=author.BackgroundId,BonusId=author.BonusId,SkillId=skillId,SkillValue=skill,Workmanship=Mathf.Lerp(.45f,1f,Mathf.Clamp01((Mathf.Clamp(skill,-50f,100f)+50f)/150f)),OriginalBaseMaxDurability=Math.Max(0,target.Block.MaxDamage),CreatedWorldTime=GameManager.Instance.World.worldTime};
        }
        string image=RebirthPlacedWorkmanshipIdentity.Freeze(r,checked((ushort)target.type));return RebirthPlacedWorkmanshipIdentity.ReadFrozen(image,position,target)==null?null:image;
    }
    internal static bool ApplyRemotePlacement(WorldBase world,Vector3i position,BlockValue target,string frozen)
    {
        if(!HasCurrentStore||!ReferenceEquals(world,loadedWorld)||!RebirthPlacedWorkmanshipIdentity.AcceptedPlacement(world,position)||world.GetBlock(position).rawData!=target.rawData)return false;
        var record=RebirthPlacedWorkmanshipIdentity.ReadFrozen(frozen,position,target);if(record==null)return false;if(RebirthInfrastructureWorkService.IsElectricalBlock(target.Block)&&!TechnicalTrapNames.Contains(target.Block.GetBlockName())){record.SourceIncarnation=RebirthPlacedWorkmanshipIdentity.Read(world,position);return RebirthPlacedWorkmanshipIdentity.Store(world,record);}Admit(world,record);return RebirthPlacedWorkmanshipIdentity.Matches(world,record);
    }
    private static void Admit(WorldBase world,RebirthPlacedWorkmanshipRecord record)
    {
        string id=RebirthPlacedWorkmanshipIdentity.Read(world,record.Position);if(id==null)return;record.SourceIncarnation=id;
        // Exact native source retains carried/new provenance even at sidecar cap.
        if(!RebirthPlacedWorkmanshipIdentity.Store(world,record))return;
        string key=Key(record.ClrIdx,record.Position);if(records.ContainsKey(key)||records.Count<MaxRecords){records[key]=record;MarkDirty();}
    }
    internal static void NativeReplaced(WorldBase world,Vector3i pos,BlockValue before,BlockValue after,bool upgrade,string source)
    {
        if(!HasCurrentStore||!ReferenceEquals(world,loadedWorld))return;string key=Key(0,pos);
        if(!records.TryGetValue(key,out var r))return;
        if(upgrade&&r.SourceIncarnation==source){r.BlockName=after.Block.GetBlockName();r.OriginalBaseMaxDurability=Math.Max(0,after.Block.MaxDamage);MarkDirty();}
        else{records.Remove(key);MarkDirty();}
    }
    public static void HandleUpgrade(WorldBase world,Vector3i pos,EntityPlayer player)
    {
        if(!HasCurrentStore||world==null||!ReferenceEquals(world,loadedWorld)||world.IsRemote()||!IsServer()||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        if(!RebirthPlacedWorkmanshipIdentity.AcceptedUpgrade(world,pos))return;
        BlockValue current=world.GetBlock(pos);if(current.Block==null||current.isair)return;
        string blockName=current.Block.GetBlockName()??string.Empty;bool technical=TechnicalTrapNames.Contains(blockName);
        if(current.Block is BlockPlantGrowingRebirth){Remove(pos);return;}
        if(RebirthInfrastructureWorkService.IsElectricalBlock(current.Block)&&!technical){Remove(pos);return;}
        string key=Key(0,pos);RebirthPlacedWorkmanshipRecord r;
        if(TryGet(world,pos,out r)&&r!=null)
        {
            r.Position=pos;r.BlockName=blockName;r.Kind=technical?RebirthPlacedProvenanceKind.TechnicalTrapPlacement:RebirthPlacedProvenanceKind.ConstructionPlacement;
            r.OriginalBaseMaxDurability=Math.Max(0,current.Block.MaxDamage);Admit(world,r);return;
        }
        RebirthProvenanceAuthorSnapshot author;if(player==null||!RebirthProvenanceIdentity.TryCapture(player,out author))return;
        string skillId=technical?GetTechnicalTrapSkillId(blockName):"skill.construction";float skill=0f;RebirthServiceCraftSkillService.TryGetSkillValue(player,skillId,out skill);
        float normalized=Mathf.Clamp01((Mathf.Clamp(skill,-50f,100f)+50f)/150f);var upgraded=new RebirthPlacedWorkmanshipRecord{ClrIdx=0,Position=pos,BlockName=blockName,Kind=technical?RebirthPlacedProvenanceKind.TechnicalTrapPlacement:RebirthPlacedProvenanceKind.ConstructionPlacement,CreatorStableId=author.StablePlayerId,BackgroundId=author.BackgroundId,BonusId=author.BonusId,SkillId=skillId,SkillValue=skill,Workmanship=Mathf.Lerp(0.45f,1f,normalized),OriginalBaseMaxDurability=Math.Max(0,current.Block.MaxDamage),CreatedWorldTime=GameManager.Instance!=null&&GameManager.Instance.World!=null?GameManager.Instance.World.worldTime:0UL};Admit(world,upgraded);
    }

    public static bool TryGet(WorldBase world,Vector3i pos,out RebirthPlacedWorkmanshipRecord record)
    {
        if(world!=null&&RebirthInfrastructureWorkService.IsElectricalBlock(world.GetBlock(pos).Block)&&!TechnicalTrapNames.Contains(world.GetBlock(pos).Block.GetBlockName())){record=null;return false;}
        record=null;if(!HasCurrentStore||(world!=null&&!ReferenceEquals(world,loadedWorld)))return false;RebirthPlacedWorkmanshipRecord r;if(!records.TryGetValue(Key(0,pos),out r)||r==null)r=RebirthPlacedWorkmanshipIdentity.ReadRecord(world??loadedWorld,pos);if(r==null)return false;
        if(world!=null){BlockValue current=world.GetBlock(pos);string name=current.Block!=null?current.Block.GetBlockName():string.Empty;if(current.isair||!string.Equals(name,r.BlockName,StringComparison.OrdinalIgnoreCase)){records.Remove(Key(0,pos));MarkDirty();return false;}}
        if(!RebirthPlacedWorkmanshipIdentity.Matches(world??loadedWorld,r)){r=RebirthPlacedWorkmanshipIdentity.ReadRecord(world??loadedWorld,pos);if(r==null)return false;}record=Clone(r);return true;
    }

    public static void Remove(Vector3i pos){if(!HasCurrentStore)return;RebirthPlacedWorkmanshipIdentity.ClearRecord(loadedWorld,pos);if(records.Remove(Key(0,pos)))MarkDirty();}

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
        StringBuilder b=new StringBuilder();int retainedLegacy=0;foreach(var entry in records.Values)if(string.IsNullOrEmpty(entry.SourceIncarnation))retainedLegacy++;b.AppendLine("[REBIRTH Provenance World] records="+records.Count+" loaded="+loaded+" unresolved="+loadFailed+" legacyUnproven="+retainedLegacy+" persistence="+PersistencePath);
        if(pos.HasValue){RebirthPlacedWorkmanshipRecord r;if(TryGet(world,pos.Value,out r))b.Append("pos=").Append(pos.Value).Append(" block=").Append(r.BlockName).Append(" kind=").Append(r.Kind).Append(" creator=").Append(r.CreatorStableId).Append(" background=").Append(r.BackgroundId).Append(" bonus=").Append(r.BonusId).Append(" skill=").Append(r.SkillId).Append("@").Append(r.SkillValue.ToString("0.###")).Append(" workmanship=").Append(r.Workmanship.ToString("0.###")).Append(" originalMax=").Append(r.OriginalBaseMaxDurability).Append(" effectiveMax=").Append(RebirthWorkmanshipSignatureService.CalculateEffectivePlacedMax(r)).Append(" durabilityMultiplier=").Append(RebirthWorkmanshipSignatureService.CalculatePlacedDurabilityMultiplier(r).ToString("0.###"));else b.Append("pos=").Append(pos.Value).Append(" provenance=none");}
        return b.ToString();
    }

    private static RebirthPlacedWorkmanshipRecord Clone(RebirthPlacedWorkmanshipRecord r){return r==null?null:new RebirthPlacedWorkmanshipRecord{SourceIncarnation=r.SourceIncarnation,ClrIdx=r.ClrIdx,Position=r.Position,BlockName=r.BlockName,Kind=r.Kind,CreatorStableId=r.CreatorStableId,BackgroundId=r.BackgroundId,BonusId=r.BonusId,SkillId=r.SkillId,SkillValue=r.SkillValue,Workmanship=r.Workmanship,OriginalBaseMaxDurability=r.OriginalBaseMaxDurability,CreatedWorldTime=r.CreatedWorldTime};}
    private static void WriteString(BinaryWriter bw,string value)
    {value=value??string.Empty;if(value.Length>512)throw new InvalidDataException("oversized provenance string");bw.Write(value);}
    private static string ReadString(BinaryReader br)
    {
        // BinaryWriter-compatible 7-bit UTF8 length, bounded BEFORE allocation.
        int length=0,shift=0,used=0;
        while(true)
        {
            byte next=br.ReadByte();used++;
            if(used>5||used==5&&(next&0xf8)!=0)throw new InvalidDataException("invalid string length");
            length|=(next&0x7f)<<shift;
            if((next&0x80)==0)break;shift+=7;
        }
        if(length<0||length>2048||used>1&&length<(1<<(7*(used-1)))||length>br.BaseStream.Length-br.BaseStream.Position)throw new InvalidDataException("oversized or noncanonical string length");
        byte[] bytes=br.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();
        string value=new UTF8Encoding(false,true).GetString(bytes);if(value.Length>512)throw new InvalidDataException("oversized provenance string");return value;
    }
    private static void Load()
    {
        records.Clear();
        string path=PersistencePath;
        if(string.IsNullOrEmpty(path)||!SafePath(path)||!SafePath(path+".bak")||!SafePath(path+".tmp")){loadFailed=true;return;}
        try{string directory=Path.GetDirectoryName(path);if(Directory.Exists(directory))using(var entries=Directory.EnumerateFileSystemEntries(directory).GetEnumerator())entries.MoveNext(); }
        catch{loadFailed=true;return;} // Inaccessible is unknown, not an empty store.
        Dictionary<string,RebirthPlacedWorkmanshipRecord> staged;
        string error;
        // An interrupted temporary is retained evidence, not disposable scratch.
        if(File.Exists(path+".tmp")||Directory.Exists(path+".tmp")){loadFailed=true;Log.Warning("[REBIRTH Provenance World] retained temporary requires reconciliation; provenance mutation disabled.");return;}
        if(TryLoadFile(path,out staged,out error))
        {
            foreach(var pair in staged)records.Add(pair.Key,pair.Value);return;
        }
        if(TryLoadFile(path+".bak",out staged,out error))
        {
            // Preserve BOTH exact originals before a later File.Replace could
            // rotate corrupt primary over the last known-readable backup.
            if(!PreserveRecoveryFile(path)||!PreserveRecoveryFile(path+".bak")){loadFailed=true;return;}
            foreach(var pair in staged)records.Add(pair.Key,pair.Value);return;
        }
        if(File.Exists(path)||File.Exists(path+".bak")||Directory.Exists(path)||Directory.Exists(path+".bak")){loadFailed=true;Log.Warning("[REBIRTH Provenance World] recovery unresolved; writes and provenance mutation disabled: "+error);}
    }
    private static string FileDigest(string path)
    {
        using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();
    }
    private static bool PreserveRecoveryFile(string path)
    {
        try
        {
            if(!File.Exists(path))return !Directory.Exists(path);
            if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)return false;
            // Above the maximum representable bounded record envelope, retain
            // the original IN PLACE rather than copying arbitrary large input.
            if(new FileInfo(path).Length>10L+MaxRecords*12350L)return false;
            string digest=FileDigest(path),retained=path+".retained."+digest;
            if(File.Exists(retained))return (File.GetAttributes(retained)&FileAttributes.ReparsePoint)==0&&FileDigest(retained)==digest&&FileDigest(path)==digest;
            using(var source=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var output=new FileStream(retained,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {source.CopyTo(output,65536);output.Flush(true);}
            return FileDigest(retained)==digest&&FileDigest(path)==digest;
        }catch(Exception error){Log.Warning("[REBIRTH Provenance World] original recovery bytes retained in place; backup activation refused: "+error.Message);return false;}
    }

    private static bool TryLoadFile(string path,out Dictionary<string,RebirthPlacedWorkmanshipRecord> staged,out string error)
    {
        staged=null;error="missing";
        if(string.IsNullOrEmpty(path)||!SafePath(path)||!File.Exists(path))return false;
        try
        {
            Dictionary<string,RebirthPlacedWorkmanshipRecord> candidate=new Dictionary<string,RebirthPlacedWorkmanshipRecord>(StringComparer.Ordinal);
            using(FileStream fs=File.OpenRead(path))using(BinaryReader br=new BinaryReader(fs))
            {
                if(fs.Length<10||fs.Length>10L+MaxRecords*12350L)throw new InvalidDataException("invalid bounded file length");
                if(br.ReadUInt32()!=Magic)throw new InvalidDataException("bad magic");
                ushort version=br.ReadUInt16();if(version<1||version>Version)throw new InvalidDataException("unsupported version");
                int count=br.ReadInt32();if(count<0||count>MaxRecords)throw new InvalidDataException("record count "+count);
                for(int i=0;i<count;i++)
                {
                    RebirthPlacedWorkmanshipRecord r=new RebirthPlacedWorkmanshipRecord();r.ClrIdx=br.ReadInt32();r.Position=new Vector3i(br.ReadInt32(),br.ReadInt32(),br.ReadInt32());r.BlockName=ReadString(br);r.Kind=(RebirthPlacedProvenanceKind)br.ReadByte();r.CreatorStableId=ReadString(br);r.BackgroundId=ReadString(br);r.BonusId=ReadString(br);r.SkillId=ReadString(br);r.SkillValue=br.ReadSingle();r.Workmanship=br.ReadSingle();r.OriginalBaseMaxDurability=br.ReadInt32();r.CreatedWorldTime=br.ReadUInt64();if(version>=2)r.SourceIncarnation=ReadString(br);
                    if(version>=2&&!Guid.TryParseExact(r.SourceIncarnation,"N",out _)&&r.SourceIncarnation.Length!=0)throw new InvalidDataException("Invalid source identity");
                    if(string.IsNullOrEmpty(r.BlockName)||string.IsNullOrEmpty(r.CreatorStableId)||
                        (r.Kind!=RebirthPlacedProvenanceKind.ConstructionPlacement&&r.Kind!=RebirthPlacedProvenanceKind.TechnicalTrapPlacement)||
                        float.IsNaN(r.SkillValue)||float.IsInfinity(r.SkillValue)||float.IsNaN(r.Workmanship)||float.IsInfinity(r.Workmanship)||
                        r.Workmanship<0f||r.Workmanship>1f||r.OriginalBaseMaxDurability<0||candidate.ContainsKey(Key(r.ClrIdx,r.Position)))
                        throw new InvalidDataException("Invalid or duplicate placed provenance record.");
                    candidate.Add(Key(r.ClrIdx,r.Position),r);
                }
                if(fs.Position!=fs.Length)throw new InvalidDataException("trailing bytes after record payload");
            }
            staged=candidate;error=string.Empty;return true;
        }
        catch(Exception ex){error=ex.GetType().Name+": "+ex.Message;return false;}
    }

    private static bool SameRecords(Dictionary<string,RebirthPlacedWorkmanshipRecord> candidate)
    {
        if(candidate==null||candidate.Count!=records.Count)return false;
        foreach(var pair in records)
        {
            if(!candidate.TryGetValue(pair.Key,out var b))return false;var a=pair.Value;
            if(a.SourceIncarnation!=b.SourceIncarnation||a.ClrIdx!=b.ClrIdx||a.Position!=b.Position||a.BlockName!=b.BlockName||a.Kind!=b.Kind||a.CreatorStableId!=b.CreatorStableId||a.BackgroundId!=b.BackgroundId||a.BonusId!=b.BonusId||a.SkillId!=b.SkillId||a.SkillValue!=b.SkillValue||a.Workmanship!=b.Workmanship||a.OriginalBaseMaxDurability!=b.OriginalBaseMaxDurability||a.CreatedWorldTime!=b.CreatedWorldTime)return false;
        }
        return true;
    }

    public static void Save()
    {
        if(!ThreadManager.IsMainThread()||!IsServer()||!loaded||loadFailed||!dirty)return;string path=PersistencePath;if(string.IsNullOrEmpty(path))return;
        try
        {
            if(records.Count>MaxRecords)throw new InvalidDataException("record count exceeds durable cap: "+records.Count);
            if(!SafePath(path)||!SafePath(path+".tmp")||!SafePath(path+".bak"))throw new IOException("Unsafe sidecar path retained.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));string tmp=path+".tmp";
            bool resume=File.Exists(tmp);
            if(Directory.Exists(tmp)||resume&&(pendingTemporary!=tmp||pendingDigest==null||FileDigest(tmp)!=pendingDigest))throw new IOException("Retained temporary requires reconciliation; no original bytes deleted.");
            if(resume&&!ReferenceEquals(pendingMutation,mutation))
            {
                // Only this process's exact verified old snapshot may move to
                // retained history when new native placements made it obsolete.
                File.Move(tmp,tmp+".retained."+pendingDigest+"."+Guid.NewGuid().ToString("N"));
                pendingTemporary=pendingDigest=null;pendingMutation=null;resume=false;
            }
            if(!resume)
            {
                using(FileStream fs=new FileStream(tmp,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(BinaryWriter bw=new BinaryWriter(fs))
                {
                    bw.Write(Magic);bw.Write(Version);bw.Write(records.Count);
                    foreach(RebirthPlacedWorkmanshipRecord r in records.Values){bw.Write(r.ClrIdx);bw.Write(r.Position.x);bw.Write(r.Position.y);bw.Write(r.Position.z);WriteString(bw,r.BlockName);bw.Write((byte)r.Kind);WriteString(bw,r.CreatorStableId);WriteString(bw,r.BackgroundId);WriteString(bw,r.BonusId);WriteString(bw,r.SkillId);bw.Write(r.SkillValue);bw.Write(r.Workmanship);bw.Write(r.OriginalBaseMaxDurability);bw.Write(r.CreatedWorldTime);WriteString(bw,r.SourceIncarnation);}
                    bw.Flush();fs.Flush(true);
                }
            }
            Dictionary<string,RebirthPlacedWorkmanshipRecord> verified;string verifyError;
            if(!TryLoadFile(tmp,out verified,out verifyError)||!SameRecords(verified))throw new InvalidDataException("temporary verification failed: "+verifyError);
            pendingTemporary=tmp;pendingDigest=FileDigest(tmp);pendingMutation=mutation;
            string publishError;if(!RebirthDurableFileCommit.TryPublish(tmp,path,out publishError))throw new IOException(publishError);
            if(!TryLoadFile(path,out verified,out verifyError)||!SameRecords(verified)||FileDigest(path)!=pendingDigest){loadFailed=true;throw new InvalidDataException("Final sidecar readback failed; original dirty state retained.");}
            dirty=false;pendingTemporary=pendingDigest=null;pendingMutation=null;
        }
        catch(Exception ex){Log.Warning("[REBIRTH Provenance World] save failed; dirty state retained: "+ex.GetType().Name+": "+ex.Message);}
    }
}

public static class RebirthPlacedWorkmanshipPatchInstaller
{
    private static bool installed;
    private static readonly HashSet<MethodBase> patched=new HashSet<MethodBase>();
    public static int Install(Harmony harmony)
    {
        if(installed||harmony==null)return 0;int count=0;
        MethodInfo prefix=AccessTools.Method(typeof(RebirthPlacedWorkmanshipPlacementPatch),nameof(RebirthPlacedWorkmanshipPlacementPatch.Prefix));
        MethodInfo postfix=AccessTools.Method(typeof(RebirthPlacedWorkmanshipPlacementPatch),nameof(RebirthPlacedWorkmanshipPlacementPatch.Postfix));
        MethodInfo finalizer=AccessTools.Method(typeof(RebirthPlacedWorkmanshipPlacementPatch),nameof(RebirthPlacedWorkmanshipPlacementPatch.Finalizer));
        HashSet<MethodBase> seen=new HashSet<MethodBase>();
        foreach(Assembly assembly in new[]{typeof(Block).Assembly,typeof(RebirthPlacedWorkmanshipService).Assembly})
        {
            Type[] types;try{types=assembly.GetTypes();}catch(ReflectionTypeLoadException ex){types=ex.Types;}
            if(types==null)continue;for(int i=0;i<types.Length;i++){Type t=types[i];if(t==null||!typeof(Block).IsAssignableFrom(t))continue;MethodInfo m=t.GetMethod(nameof(Block.PlaceBlock),BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly,null,new[]{typeof(WorldBase),typeof(BlockPlacement.Result),typeof(EntityAlive)},null);if(m==null||!seen.Add(m)||patched.Contains(m))continue;PatchProcessor processor=harmony.CreateProcessor(m);processor.AddPrefix(new HarmonyMethod(prefix));processor.AddPostfix(new HarmonyMethod(postfix));processor.AddFinalizer(new HarmonyMethod(finalizer));processor.Patch();patched.Add(m);count++;}
        }
        installed=true;return count;
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
        public object OriginalScope;
    }
    // Positional Harmony arguments are intentional. V3.2 renamed the placement result
    // parameter to _bpResult; using __0/__1/__2 prevents harmless upstream parameter-name
    // changes from aborting the entire Survivor progression installer.
    public static void Prefix(WorldBase __0,BlockPlacement.Result __1,EntityAlive __2,out PlacementScopeState __state)
    {
        __state=new PlacementScopeState{PriorDepth=depth,PriorHeld=held,IsOutermost=depth==0};
        depth=__state.PriorDepth+1;
        if(!__state.IsOutermost)return;
        __state.OriginalScope=RebirthPlacedWorkmanshipIdentity.BeginPlacement(__0,__1.blockPos);
        EntityPlayer p=__2 as EntityPlayer;
        held=p!=null&&p.inventory!=null&&p.inventory.holdingItemItemValue!=null?p.inventory.holdingItemItemValue.Clone():null;
    }
    public static void Postfix(WorldBase __0,BlockPlacement.Result __1,EntityAlive __2,PlacementScopeState __state)
    {
        if(__state.IsOutermost)RebirthPlacedWorkmanshipService.CapturePlacement(__0,__1,__2 as EntityPlayer,held);
    }
    public static Exception Finalizer(Exception __exception,PlacementScopeState __state)
    {
        RebirthPlacedWorkmanshipIdentity.EndPlacement(__state.OriginalScope);
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
