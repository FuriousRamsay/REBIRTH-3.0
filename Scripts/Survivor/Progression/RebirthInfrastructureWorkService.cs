using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public enum RebirthElectricalServiceKind : byte
{
    Control = 0,
    Generator = 1,
    BatteryBank = 2
}

public sealed class RebirthElectricalServiceRecord
{
    public int ClrIdx;
    public Vector3i Position;
    public RebirthElectricalServiceKind Kind;
    public float Condition = 100f;
    public float Workmanship = 0.5f;
    public ulong LastServiceWorldTime;
    public int ServiceCount;
    // PC017 configuration provenance. These fields are descriptive only until the later
    // Electrician effect chunk consumes them; existing Electrical Skill behavior is unchanged.
    public string ConfiguredByStableId = string.Empty;
    public string ConfiguredByBackgroundId = string.Empty;
    public string ConfiguredByBonusId = string.Empty;
    public float ConfiguredSkillValue;
}

/// <summary>
/// Pass 3E-B2 infrastructure runtime.
/// Construction uses the native authoritative block repair/upgrade commit itself as the
/// completed Work Action. Electrical service uses a server-authoritative service request,
/// transactional local/Remote Resources consumption and a per-world persisted service state.
/// </summary>
public static class RebirthInfrastructureWorkService
{
    private const uint PersistenceMagic = 0x57494252U; // RBIW
    private const ushort PersistenceVersion = 2;
    private const int MaxRecords = 8192;
    private const float TickSeconds = 1f;
    private const float SaveIntervalSeconds = 20f;

    private static readonly Dictionary<string, RebirthElectricalServiceRecord> records =
        new Dictionary<string, RebirthElectricalServiceRecord>(StringComparer.Ordinal);

    private static bool installed;
    private static bool serverLoaded;
    private static bool dirty;
    private static float nextTick;
    private static float nextSave;
    private static float lastTickRealtime;

    public static string Install(Harmony harmony)
    {
        if (installed) return "[REBIRTH Survivor Infrastructure] already installed";
        installed = true;
        if (harmony != null)
        {
            try
            {
                RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthConstructionDamageBlockCommitPatch));
            }
            catch(Exception ex)
            {
                // Construction repair/upgrade LBD is a supporting hook. A game-build signature
                // change must not abort the entire Survivor progression installer or make the
                // Progression Explorer/Survivor Profile UI unusable. The failure remains explicit.
                Log.Error("[REBIRTH Survivor Construction] damage-commit hook unavailable; continuing progression installation: "+ex.GetType().Name+": "+ex.Message);
            }
            RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthElectricalPowerUsedPatch));
        }
        ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnGameStarting));
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        return "[REBIRTH Survivor Infrastructure] installed construction=native-commit-LBD electrical=service-lifecycle";
    }

    private static bool IsServer()
    {
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        return c!=null&&c.IsServer;
    }

    private static void OnGameStarting(ref ModEvents.SGameStartingData data)
    {
        Reset(false);
        if(!IsServer())return;
        Load();
        serverLoaded=true;
        lastTickRealtime=Time.realtimeSinceStartup;
        nextTick=lastTickRealtime+TickSeconds;
        nextSave=lastTickRealtime+SaveIntervalSeconds;
    }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        if(!serverLoaded||!IsServer())return;
        float now=Time.realtimeSinceStartup;
        if(now<nextTick)return;
        float dt=Mathf.Clamp(now-lastTickRealtime,0f,5f);
        lastTickRealtime=now;
        nextTick=now+TickSeconds;
        TickLoadedPoweredObjects(dt);
        if(dirty&&now>=nextSave)
        {
            Save();
            nextSave=now+SaveIntervalSeconds;
        }
    }

    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data){Reset(true);}
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data){Reset(true);}

    public static void Reset(bool save)
    {
        if(save&&serverLoaded&&dirty)Save();
        records.Clear(); serverLoaded=false; dirty=false; nextTick=0f; nextSave=0f; lastTickRealtime=0f; lastClientSnapshot=null;
    }

    private static string PersistencePath
    {
        get
        {
            string root=GameIO.GetSaveGameDir();
            return string.IsNullOrEmpty(root)?string.Empty:Path.Combine(root,"RebirthData","Survivor","InfrastructureElectrical.dat");
        }
    }

    private static RebirthElectricalServiceRecord FindRecordAt(Vector3i pos)
    {
        foreach(RebirthElectricalServiceRecord record in records.Values)
            if(record!=null&&record.Position==pos)
                return record;
        return null;
    }

    private static string Key(int clrIdx,Vector3i p)
    { return clrIdx.ToString()+"|"+p.x+","+p.y+","+p.z; }

    private static void Load()
    {
        records.Clear();
        string path=PersistencePath;
        if(string.IsNullOrEmpty(path))return;
        Dictionary<string,RebirthElectricalServiceRecord> staged;string error;
        if(TryLoadFile(path,out staged,out error)||TryLoadFile(path+".bak",out staged,out error))
        {
            foreach(KeyValuePair<string,RebirthElectricalServiceRecord> kv in staged)records[kv.Key]=kv.Value;
            { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH Survivor Infrastructure] loaded electrical service records="+records.Count); }
            return;
        }
        if(File.Exists(path)||File.Exists(path+".bak"))
            Log.Warning("[REBIRTH Survivor Infrastructure] electrical persistence load failed; no state published: "+error);
    }

    private static bool TryLoadFile(string path,out Dictionary<string,RebirthElectricalServiceRecord> staged,out string error)
    {
        staged=null;error="missing";if(string.IsNullOrEmpty(path)||!File.Exists(path))return false;
        try
        {
            Dictionary<string,RebirthElectricalServiceRecord> candidate=new Dictionary<string,RebirthElectricalServiceRecord>(StringComparer.Ordinal);
            using(FileStream fs=File.OpenRead(path))using(BinaryReader br=new BinaryReader(fs))
            {
                if(br.ReadUInt32()!=PersistenceMagic)throw new InvalidDataException("bad magic");
                ushort version=br.ReadUInt16();if(version<1||version>PersistenceVersion)throw new InvalidDataException("unsupported version "+version);
                int count=br.ReadInt32();if(count<0||count>MaxRecords)throw new InvalidDataException("record count "+count);
                for(int i=0;i<count;i++)
                {
                    RebirthElectricalServiceRecord r=new RebirthElectricalServiceRecord();
                    r.ClrIdx=br.ReadInt32();r.Position=new Vector3i(br.ReadInt32(),br.ReadInt32(),br.ReadInt32());
                    int rawKind=br.ReadByte();if(rawKind<0||rawKind>2)throw new InvalidDataException("invalid kind "+rawKind);r.Kind=(RebirthElectricalServiceKind)rawKind;
                    float condition=br.ReadSingle(),workmanship=br.ReadSingle();if(float.IsNaN(condition)||float.IsInfinity(condition)||float.IsNaN(workmanship)||float.IsInfinity(workmanship))throw new InvalidDataException("non-finite electrical state");
                    r.Condition=Mathf.Clamp(condition,0f,100f);r.Workmanship=Mathf.Clamp01(workmanship);r.LastServiceWorldTime=br.ReadUInt64();r.ServiceCount=Math.Max(0,br.ReadInt32());
                    if(version>=2){r.ConfiguredByStableId=RebirthSurvivorNetworkCodec.ReadString(br,512);r.ConfiguredByBackgroundId=RebirthSurvivorNetworkCodec.ReadString(br,128);r.ConfiguredByBonusId=RebirthSurvivorNetworkCodec.ReadString(br,128);r.ConfiguredSkillValue=br.ReadSingle();if(float.IsNaN(r.ConfiguredSkillValue)||float.IsInfinity(r.ConfiguredSkillValue))throw new InvalidDataException("non-finite configured skill");}
                    string key=Key(r.ClrIdx,r.Position);if(candidate.ContainsKey(key))throw new InvalidDataException("duplicate electrical record "+key);candidate[key]=r;
                }
                if(fs.Position!=fs.Length)throw new InvalidDataException("trailing bytes after electrical payload");
            }
            staged=candidate;error=string.Empty;return true;
        }
        catch(Exception ex){error=ex.GetType().Name+": "+ex.Message;return false;}
    }

    public static void Save()
    {
        if(!IsServer())return;
        string path=PersistencePath;
        if(string.IsNullOrEmpty(path))return;
        try
        {
            if(records.Count>MaxRecords)throw new InvalidDataException("record count exceeds durable cap: "+records.Count);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp=path+".tmp";
            if(File.Exists(tmp))File.Delete(tmp);
            using(FileStream fs=File.Create(tmp))
            using(BinaryWriter bw=new BinaryWriter(fs))
            {
                bw.Write(PersistenceMagic); bw.Write(PersistenceVersion);
                bw.Write(records.Count);
                foreach(RebirthElectricalServiceRecord r in records.Values)
                {
                    bw.Write(r.ClrIdx); bw.Write(r.Position.x); bw.Write(r.Position.y); bw.Write(r.Position.z);
                    bw.Write((byte)r.Kind); bw.Write(r.Condition); bw.Write(r.Workmanship);
                    bw.Write(r.LastServiceWorldTime); bw.Write(r.ServiceCount);
                    RebirthSurvivorNetworkCodec.WriteString(bw,r.ConfiguredByStableId??string.Empty,512);
                    RebirthSurvivorNetworkCodec.WriteString(bw,r.ConfiguredByBackgroundId??string.Empty,128);
                    RebirthSurvivorNetworkCodec.WriteString(bw,r.ConfiguredByBonusId??string.Empty,128);
                    bw.Write(r.ConfiguredSkillValue);
                }
                bw.Flush();fs.Flush();
            }
            Dictionary<string,RebirthElectricalServiceRecord> verified;string verifyError;
            if(!TryLoadFile(tmp,out verified,out verifyError)||verified.Count!=records.Count)throw new InvalidDataException("temporary verification failed: "+verifyError);
            string publishError;if(!RebirthDurableFileCommit.TryPublish(tmp,path,out publishError))throw new IOException("durable publish failed: "+publishError);
            dirty=false;
        }
        catch(Exception ex){Log.Warning("[REBIRTH Survivor Infrastructure] electrical persistence save failed: "+ex.GetType().Name+": "+ex.Message);}
    }

    private static void TickLoadedPoweredObjects(float dt)
    {
        if(dt<=0f||records.Count==0)return;
        World world=GameManager.Instance!=null?GameManager.Instance.World:null;
        if(world==null)return;
        float decayPerSecond=Math.Max(0f,RebirthProgressionRuntimeConfig.ElectricalConditionDecayPerPoweredMinute)/60f;
        if(decayPerSecond<=0f)return;
        foreach(RebirthElectricalServiceRecord r in records.Values)
        {
            TileEntityPowered te=world.GetTileEntity(r.Position) as TileEntityPowered;
            if(te==null)continue; // unloaded chunk/state is retained without offline decay
            bool active=te.IsPowered;
            TileEntityPowerSource source=te as TileEntityPowerSource;
            if(source!=null)active=source.IsOn;
            if(!active)continue;
            float before=r.Condition;
            r.Condition=Mathf.Clamp(r.Condition-decayPerSecond*dt,0f,100f);
            if(Math.Abs(r.Condition-before)>=0.0001f)dirty=true;
        }
    }

    public static int AdjustPowerUsed(TileEntityPowered te,int nativePowerUsed)
    {
        if(nativePowerUsed<=0||te==null||!IsServer())return nativePowerUsed;
        RebirthElectricalServiceRecord r=FindRecordAt(te.ToWorldPos());
        if(r==null)return nativePowerUsed;

        float condition=Mathf.Clamp01(r.Condition/100f);
        float workmanship=Mathf.Clamp01(r.Workmanship);
        // Normal Electrical Skill workmanship remains available to every serviced/built Rebirth system.
        float savings=Mathf.Clamp(RebirthProgressionRuntimeConfig.ElectricalMaxPowerSavings,0f,0.50f)*workmanship*condition;
        // Power Saver is additional persisted configuration provenance, never an Electrician aura.
        // Re-service by another survivor replaces ConfiguredBy* and therefore removes this extra saving;
        // pickup/redeploy preserves it through RebirthElectricalItemProvenance.
        if(string.Equals(r.ConfiguredByBonusId,"background_bonus.power_saver",StringComparison.OrdinalIgnoreCase) ||
           string.Equals(r.ConfiguredByBackgroundId,"background.electrician",StringComparison.OrdinalIgnoreCase))
            savings+=Mathf.Clamp(RebirthWorkmanshipSignatureService.GetBonusTuning("background_bonus.power_saver","additional_power_savings",0.15f),0f,0.35f)*condition;
        savings=Mathf.Clamp(savings,0f,0.50f);
        float penalty=0f;
        if(condition<0.25f)
            penalty=Mathf.Clamp(RebirthProgressionRuntimeConfig.ElectricalNeglectPowerPenalty,0f,0.50f)*(1f-condition/0.25f);
        float multiplier=Mathf.Clamp(1f-savings+penalty,0.50f,1.50f);
        return Math.Max(nativePowerUsed>0?1:0,Mathf.RoundToInt(nativePowerUsed*multiplier));
    }

    public static bool TryService(EntityPlayer player,int clrIdx,Vector3i pos,out string message,out RebirthElectricalServiceRecord snapshot)
    {
        message=string.Empty;snapshot=null;
        if(!IsServer()){message="Electrical service requires server authority.";return false;}
        if(player==null||GameManager.Instance==null||GameManager.Instance.World==null){message="Electrical service is unavailable.";return false;}
        World world=GameManager.Instance.World;
        clrIdx=0;
        if((player.position-pos.ToVector3()).sqrMagnitude>Constants.cDigAndBuildDistance*Constants.cDigAndBuildDistance)
        {message="Move closer to the electrical device.";return false;}

        BlockValue block=world.GetBlock(pos);
        if(block.Block==null||!RebirthInfrastructureWorkService.IsElectricalBlock(block.Block)){message="Target a powered electrical device.";return false;}
        if(block.ischild){message="Target the main block of the electrical device.";return false;}
        TileEntityPowered te=world.GetTileEntity(pos) as TileEntityPowered;
        if(te==null){message="The powered device is not ready for service.";return false;}
        if(!te.IsPlayerPlaced){message="Only player-placed electrical devices can be serviced.";return false;}

        PersistentPlayerData persistent=GameManager.Instance.GetPersistentPlayerList().GetPlayerDataFromEntityID(player.entityId);
        if(persistent==null||!world.CanPlaceBlockAt(pos,persistent))
        {message="You do not have permission to service this device.";return false;}
        TileEntityPowerSource source=te as TileEntityPowerSource;
        if(source!=null&&source.GetOwner()!=null&&persistent.PrimaryId!=null&&!source.GetOwner().Equals(persistent.PrimaryId))
        {message="You are not the owner of this power source.";return false;}

        string blockName=block.Block.GetBlockName()??string.Empty;
        RebirthElectricalServiceKind kind=Classify(blockName,te);
        string requiredKnowledge=kind==RebirthElectricalServiceKind.Generator?"procedure.electrical.generator_service":
            kind==RebirthElectricalServiceKind.BatteryBank?"procedure.electrical.battery_service":"procedure.electrical.control_devices";
        if(!RebirthKnowledgeService.HasKnowledge(player,requiredKnowledge))
        {message="Missing required electrical procedure: "+FriendlyProcedure(kind)+".";return false;}

        // Stage every fallible eligibility/calculation step before the resource commit. The
        // consumption call is the commit boundary; after it succeeds, only deterministic
        // publication into already-resolved runtime objects remains.
        float skill;
        if(!RebirthServiceCraftSkillService.TryGetSkillValue(player,"skill.electrical",out skill))skill=0f;
        if(float.IsNaN(skill)||float.IsInfinity(skill)){message="Electrical skill state is invalid.";return false;}
        float normalized=Mathf.Clamp01((Mathf.Clamp(skill,-50f,100f)+50f)/150f);
        float workmanship=Mathf.Lerp(0.45f,1f,normalized);
        string key=Key(clrIdx,pos);
        RebirthElectricalServiceRecord prior;records.TryGetValue(key,out prior);
        RebirthElectricalServiceRecord r=prior!=null?Clone(prior):new RebirthElectricalServiceRecord{ClrIdx=clrIdx,Position=pos,Kind=kind};
        r.ClrIdx=clrIdx;r.Position=pos;r.Kind=kind;r.Condition=100f;r.Workmanship=workmanship;
        r.LastServiceWorldTime=world.worldTime;r.ServiceCount=Math.Max(0,r.ServiceCount)+1;
        RebirthProvenanceAuthorSnapshot author;
        if(RebirthProvenanceIdentity.TryCapture(player,out author))
        {
            r.ConfiguredByStableId=author.StablePlayerId;
            r.ConfiguredByBackgroundId=author.BackgroundId;
            r.ConfiguredByBonusId=author.BonusId;
            r.ConfiguredSkillValue=skill;
        }

        // Phase 9 evidence must represent a committed state change, not a paid repeat button.
        // A fully serviced device may still be legitimately reconfigured by a different survivor,
        // a different background bonus, or meaningfully changed workmanship. Repeating the exact
        // already-published state is rejected before resources are consumed and therefore cannot
        // farm Electrical training.
        if(prior!=null&&!HasMeaningfulServiceStateChange(prior,r))
        {
            message="This electrical device is already fully serviced with the same configuration.";
            snapshot=Clone(prior);
            return false;
        }

        List<ItemStack> requirements=BuildRequirements(kind);
        RemoteResourceTransactions.ConsumptionResult consumed=RemoteResourceTransactions.TryConsume(player,requirements);
        if(consumed==null||!consumed.Success)
        {message="Electrical service requires "+RequirementText(kind)+".";return false;}

        records[key]=r;dirty=true;te.SetModified();

        RebirthPhase9TechnicalTrainingService.AwardElectrical(player,kind,clrIdx+":"+pos.x+":"+pos.y+":"+pos.z);

        int nativePower=te.PowerUsed;
        int adjusted=AdjustPowerUsed(te,nativePower);
        float pct=nativePower>0?(1f-(float)adjusted/nativePower)*100f:0f;
        message=FriendlyProcedure(kind)+" completed. Condition 100%, workmanship "+Mathf.RoundToInt(workmanship*100f)+"%"+
            (nativePower>0?", current power efficiency "+pct.ToString("0.#")+"%.":".");
        snapshot=Clone(r);
        return true;
    }

    public static bool IsElectricalBlock(Block block) { return block is BlockPowered || block is BlockPowerSource; }

    private static bool HasMeaningfulServiceStateChange(RebirthElectricalServiceRecord prior,RebirthElectricalServiceRecord candidate)
    {
        if(prior==null||candidate==null)return true;
        if(prior.Kind!=candidate.Kind)return true;
        if(prior.Condition<=99f)return true;
        if(candidate.Workmanship-prior.Workmanship>=0.003f)return true;
        if(candidate.ConfiguredSkillValue-prior.ConfiguredSkillValue>=1f)return true;
        if(!string.Equals(prior.ConfiguredByStableId??string.Empty,candidate.ConfiguredByStableId??string.Empty,StringComparison.Ordinal))return true;
        if(!string.Equals(prior.ConfiguredByBackgroundId??string.Empty,candidate.ConfiguredByBackgroundId??string.Empty,StringComparison.OrdinalIgnoreCase))return true;
        if(!string.Equals(prior.ConfiguredByBonusId??string.Empty,candidate.ConfiguredByBonusId??string.Empty,StringComparison.OrdinalIgnoreCase))return true;
        return false;
    }

    private static RebirthElectricalServiceKind Classify(string blockName,TileEntityPowered te)
    {
        string n=(blockName??string.Empty).ToLowerInvariant();
        if(n.Contains("generator"))return RebirthElectricalServiceKind.Generator;
        if(n.Contains("batterybank")||n.Contains("battery_bank")||te.PowerItemType==PowerItem.PowerItemTypes.BatteryBank)return RebirthElectricalServiceKind.BatteryBank;
        return RebirthElectricalServiceKind.Control;
    }

    private static List<ItemStack> BuildRequirements(RebirthElectricalServiceKind kind)
    {
        List<ItemStack> r=new List<ItemStack>();
        if(kind==RebirthElectricalServiceKind.Generator)
        {
            r.Add(new ItemStack(ItemClass.GetItem("rebirthElectricalFuse",false),1));
            r.Add(new ItemStack(ItemClass.GetItem("rebirthElectricalWiringHarness",false),1));
        }
        else if(kind==RebirthElectricalServiceKind.BatteryBank)
        {
            r.Add(new ItemStack(ItemClass.GetItem("rebirthElectricalFuse",false),1));
            r.Add(new ItemStack(ItemClass.GetItem("rebirthElectricalTerminalBlock",false),1));
        }
        else r.Add(new ItemStack(ItemClass.GetItem("rebirthElectricalTerminalBlock",false),1));
        return r;
    }

    private static string RequirementText(RebirthElectricalServiceKind kind)
    {
        if(kind==RebirthElectricalServiceKind.Generator)return "1 Electrical Fuse and 1 Wiring Harness";
        if(kind==RebirthElectricalServiceKind.BatteryBank)return "1 Electrical Fuse and 1 Terminal Block";
        return "1 Terminal Block";
    }

    private static string FriendlyProcedure(RebirthElectricalServiceKind kind)
    {
        if(kind==RebirthElectricalServiceKind.Generator)return "Generator Inspection & Service";
        if(kind==RebirthElectricalServiceKind.BatteryBank)return "Battery Bank Inspection & Service";
        return "Control Devices & Sensors";
    }

    private static RebirthElectricalServiceRecord Clone(RebirthElectricalServiceRecord r)
    {
        if(r==null)return null;
        return new RebirthElectricalServiceRecord{ClrIdx=r.ClrIdx,Position=r.Position,Kind=r.Kind,Condition=r.Condition,
            Workmanship=r.Workmanship,LastServiceWorldTime=r.LastServiceWorldTime,ServiceCount=r.ServiceCount,
            ConfiguredByStableId=r.ConfiguredByStableId,ConfiguredByBackgroundId=r.ConfiguredByBackgroundId,
            ConfiguredByBonusId=r.ConfiguredByBonusId,ConfiguredSkillValue=r.ConfiguredSkillValue};
    }

    public static bool TryGetRecord(Vector3i pos,out RebirthElectricalServiceRecord snapshot)
    {
        snapshot=Clone(FindRecordAt(pos));return snapshot!=null;
    }

    private static RebirthElectricalServiceRecord lastClientSnapshot;
    public static RebirthElectricalServiceRecord GetLastClientSnapshot(){return Clone(lastClientSnapshot);}
    public static void ReceiveClientSnapshot(RebirthElectricalServiceRecord snapshot){lastClientSnapshot=Clone(snapshot);}

    public static string BuildDebugAt(Vector3i pos)
    {
        RebirthElectricalServiceRecord r;if(!TryGetRecord(pos,out r))return "[REBIRTH Provenance Electrical] pos="+pos+" record=none";
        return "[REBIRTH Provenance Electrical] pos="+pos+" kind="+r.Kind+" condition="+r.Condition.ToString("0.###")+" workmanship="+r.Workmanship.ToString("0.###")+" configuredBy="+r.ConfiguredByStableId+" background="+r.ConfiguredByBackgroundId+" bonus="+r.ConfiguredByBonusId+" electricalSkill="+r.ConfiguredSkillValue.ToString("0.###")+" serviceCount="+r.ServiceCount;
    }

    public static bool CaptureBuiltConfiguration(WorldBase world,Vector3i pos,EntityPlayer player)
    {
        if(world==null||world.IsRemote()||!IsServer()||player==null||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return false;
        BlockValue block=world.GetBlock(pos);if(block.Block==null||!RebirthInfrastructureWorkService.IsElectricalBlock(block.Block))return false;
        TileEntityPowered te=world.GetTileEntity(pos) as TileEntityPowered;if(te==null||!te.IsPlayerPlaced)return false;
        if(FindRecordAt(pos)!=null)return true; // carried configuration wins; placement never stacks it.
        float skill=0f;RebirthServiceCraftSkillService.TryGetSkillValue(player,"skill.electrical",out skill);
        float normalized=Mathf.Clamp01((Mathf.Clamp(skill,-50f,100f)+50f)/150f);
        RebirthProvenanceAuthorSnapshot author;if(!RebirthProvenanceIdentity.TryCapture(player,out author))return false;
        RebirthElectricalServiceRecord r=new RebirthElectricalServiceRecord{ClrIdx=0,Position=pos,Kind=Classify(block.Block.GetBlockName()??string.Empty,te),Condition=100f,Workmanship=Mathf.Lerp(0.45f,1f,normalized),LastServiceWorldTime=GameManager.Instance!=null&&GameManager.Instance.World!=null?GameManager.Instance.World.worldTime:0UL,ServiceCount=0,ConfiguredByStableId=author.StablePlayerId,ConfiguredByBackgroundId=author.BackgroundId,ConfiguredByBonusId=author.BonusId,ConfiguredSkillValue=skill};
        records[Key(0,pos)]=r;dirty=true;te.SetModified();return true;
    }

    public static bool RestorePickedUpConfiguration(WorldBase world,Vector3i pos,ItemValue carriedValue)
    {
        if(world==null||world.IsRemote()||!IsServer()||carriedValue==null||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return false;
        RebirthElectricalServiceRecord carried;
        if(!RebirthElectricalItemProvenance.TryRead(carriedValue,out carried)||carried==null)return false;
        BlockValue block=world.GetBlock(pos);
        if(block.Block==null||!RebirthInfrastructureWorkService.IsElectricalBlock(block.Block))return false;
        TileEntityPowered te=world.GetTileEntity(pos) as TileEntityPowered;
        RebirthElectricalServiceRecord r=new RebirthElectricalServiceRecord();
        r.ClrIdx=0;r.Position=pos;r.Kind=te!=null?Classify(block.Block.GetBlockName()??string.Empty,te):carried.Kind;
        r.Condition=Mathf.Clamp(carried.Condition,0f,100f);r.Workmanship=Mathf.Clamp01(carried.Workmanship);
        r.LastServiceWorldTime=GameManager.Instance!=null&&GameManager.Instance.World!=null?GameManager.Instance.World.worldTime:0UL;
        r.ServiceCount=Math.Max(0,carried.ServiceCount);r.ConfiguredByStableId=carried.ConfiguredByStableId??string.Empty;
        r.ConfiguredByBackgroundId=carried.ConfiguredByBackgroundId??string.Empty;r.ConfiguredByBonusId=carried.ConfiguredByBonusId??string.Empty;
        r.ConfiguredSkillValue=carried.ConfiguredSkillValue;records[Key(0,pos)]=r;dirty=true;if(te!=null)te.SetModified();return true;
    }

    public static bool TryBuildPickupRecord(Vector3i pos,out RebirthElectricalServiceRecord snapshot)
    {
        snapshot=Clone(FindRecordAt(pos));return snapshot!=null;
    }

    public static void RemoveRecord(Vector3i pos)
    {
        RebirthElectricalServiceRecord r=FindRecordAt(pos);if(r==null)return;if(records.Remove(Key(r.ClrIdx,r.Position)))dirty=true;
    }

    public static string BuildDebugReport(EntityPlayer player)
    {
        System.Text.StringBuilder b=new System.Text.StringBuilder();
        b.AppendLine("[REBIRTH Survivor Infrastructure]");
        b.AppendLine("constructionCommitHook=True electricalServiceLifecycle="+serverLoaded+" records="+records.Count);
        b.AppendLine("constructionSource=native Block.DamageBlock negative repair/upgrade commit");
        b.AppendLine("electricalPersistence="+PersistencePath);
        b.AppendLine("powerSaver=additional persisted configured-author saving; normal Electrical Skill saving remains independent");
        if(player!=null)
        {
            float v; RebirthServiceCraftSkillService.TryGetSkillValue(player,"skill.construction",out v);
            b.AppendLine("skill.construction="+v.ToString("0.###"));
            RebirthServiceCraftSkillService.TryGetSkillValue(player,"skill.electrical",out v);
            b.AppendLine("skill.electrical="+v.ToString("0.###"));
        }
        return b.ToString();
    }
}

/// <summary>
/// Item-carried electrical configuration component. This is intentionally separate from the
/// generic crafted/placed provenance component so a powered technical trap can carry both its
/// Engineer placement workmanship and its Electrical configuration across pickup/redeploy.
/// </summary>
public static class RebirthElectricalItemProvenance
{
    private const int CurrentVersion=1;
    private const int FloatScale=100000;
    private const string KVersion="RebirthElectricalProvVersion";
    private const string KKind="RebirthElectricalProvKind";
    private const string KCondition="RebirthElectricalProvCondition";
    private const string KWorkmanship="RebirthElectricalProvWorkmanship";
    private const string KServiceCount="RebirthElectricalProvServiceCount";
    private const string KCreator="RebirthElectricalProvCreator";
    private const string KBackground="RebirthElectricalProvBackground";
    private const string KBonus="RebirthElectricalProvBonus";
    private const string KSkill="RebirthElectricalProvSkillValue";

    public static void Stamp(ItemValue value,RebirthElectricalServiceRecord r)
    {
        if(value==null||r==null)return;value.SetMetadata(KVersion,CurrentVersion);value.SetMetadata(KKind,(int)r.Kind);
        value.SetMetadata(KCondition,Encode(r.Condition));value.SetMetadata(KWorkmanship,Encode(r.Workmanship));value.SetMetadata(KServiceCount,Math.Max(0,r.ServiceCount));
        value.SetMetadata(KCreator,r.ConfiguredByStableId??string.Empty);value.SetMetadata(KBackground,r.ConfiguredByBackgroundId??string.Empty);value.SetMetadata(KBonus,r.ConfiguredByBonusId??string.Empty);value.SetMetadata(KSkill,Encode(r.ConfiguredSkillValue));
    }

    public static bool TryRead(ItemValue value,out RebirthElectricalServiceRecord r)
    {
        r=null;if(value==null)return false;int version;if(!value.TryGetMetadata(KVersion,out version)||version!=CurrentVersion)return false;
        int kind,condition,workmanship,serviceCount,skill;string creator,background,bonus;
        value.TryGetMetadata(KKind,out kind);value.TryGetMetadata(KCondition,out condition);value.TryGetMetadata(KWorkmanship,out workmanship);value.TryGetMetadata(KServiceCount,out serviceCount);
        value.TryGetMetadata(KCreator,out creator);value.TryGetMetadata(KBackground,out background);value.TryGetMetadata(KBonus,out bonus);value.TryGetMetadata(KSkill,out skill);
        if(kind<0||kind>2)return false;float decodedCondition=Decode(condition),decodedWorkmanship=Decode(workmanship),decodedSkill=Decode(skill);if(float.IsNaN(decodedCondition)||float.IsInfinity(decodedCondition)||float.IsNaN(decodedWorkmanship)||float.IsInfinity(decodedWorkmanship)||float.IsNaN(decodedSkill)||float.IsInfinity(decodedSkill))return false;
        r=new RebirthElectricalServiceRecord{Kind=(RebirthElectricalServiceKind)kind,Condition=Mathf.Clamp(decodedCondition,0f,100f),Workmanship=Mathf.Clamp01(decodedWorkmanship),ServiceCount=Math.Max(0,serviceCount),ConfiguredByStableId=creator??string.Empty,ConfiguredByBackgroundId=background??string.Empty,ConfiguredByBonusId=bonus??string.Empty,ConfiguredSkillValue=decodedSkill};return true;
    }

    public static bool AreStackCompatible(ItemValue a,ItemValue b)
    {
        RebirthElectricalServiceRecord ra,rb;bool ha=TryRead(a,out ra),hb=TryRead(b,out rb);if(!ha&&!hb)return true;if(ha!=hb)return false;
        return ra.Kind==rb.Kind&&Math.Abs(ra.Condition-rb.Condition)<0.0001f&&Math.Abs(ra.Workmanship-rb.Workmanship)<0.0001f&&ra.ServiceCount==rb.ServiceCount
            &&string.Equals(ra.ConfiguredByStableId??string.Empty,rb.ConfiguredByStableId??string.Empty,StringComparison.Ordinal)
            &&string.Equals(ra.ConfiguredByBackgroundId??string.Empty,rb.ConfiguredByBackgroundId??string.Empty,StringComparison.Ordinal)
            &&string.Equals(ra.ConfiguredByBonusId??string.Empty,rb.ConfiguredByBonusId??string.Empty,StringComparison.Ordinal)
            &&Math.Abs(ra.ConfiguredSkillValue-rb.ConfiguredSkillValue)<0.0001f;
    }

    public static string BuildDebugSummary(ItemValue value)
    {
        RebirthElectricalServiceRecord r;if(!TryRead(value,out r))return "[REBIRTH Provenance Electrical Item] none";
        return "[REBIRTH Provenance Electrical Item] kind="+r.Kind+" condition="+r.Condition.ToString("0.###")+" workmanship="+r.Workmanship.ToString("0.###")+" serviceCount="+r.ServiceCount+" configuredBy="+r.ConfiguredByStableId+" background="+r.ConfiguredByBackgroundId+" bonus="+r.ConfiguredByBonusId+" skill="+r.ConfiguredSkillValue.ToString("0.###");
    }

    private static int Encode(float value){return (int)Math.Round(Math.Max(-20000f,Math.Min(20000f,value))*FloatScale);}
    private static float Decode(int value){return (float)value/FloatScale;}
}

public static class RebirthElectricalPickupBridge
{
    private sealed class PendingPickup{public long OperationId;public RebirthElectricalServiceRecord Record;public DateTime ExpiresUtc;}
    private static readonly Dictionary<string,Queue<PendingPickup>> pending=new Dictionary<string,Queue<PendingPickup>>(StringComparer.Ordinal);
    private static string scope=string.Empty;private static long nextOperationId;private static readonly TimeSpan PendingLifetime=TimeSpan.FromSeconds(30);
    private static string Key(Vector3i p){return p.x+","+p.y+","+p.z;}
    private static void EnsureScope(WorldBase world){string save=string.Empty;try{save=GameIO.GetSaveGameDir()??string.Empty;}catch{}string next=save+"|"+(world!=null?System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(world).ToString():"0");if(string.Equals(scope,next,StringComparison.Ordinal))return;scope=next;pending.Clear();}
    private static void CleanupExpired(){DateTime now=DateTime.UtcNow;List<string> empty=new List<string>();foreach(KeyValuePair<string,Queue<PendingPickup>> kv in pending){Queue<PendingPickup> q=kv.Value;while(q!=null&&q.Count>0&&(q.Peek()==null||q.Peek().ExpiresUtc<=now))q.Dequeue();if(q==null||q.Count==0)empty.Add(kv.Key);}for(int i=0;i<empty.Count;i++)pending.Remove(empty[i]);}
    private static RebirthElectricalServiceRecord Copy(RebirthElectricalServiceRecord r){return r==null?null:new RebirthElectricalServiceRecord{ClrIdx=r.ClrIdx,Position=r.Position,Kind=r.Kind,Condition=r.Condition,Workmanship=r.Workmanship,LastServiceWorldTime=r.LastServiceWorldTime,ServiceCount=r.ServiceCount,ConfiguredByStableId=r.ConfiguredByStableId??string.Empty,ConfiguredByBackgroundId=r.ConfiguredByBackgroundId??string.Empty,ConfiguredByBonusId=r.ConfiguredByBonusId??string.Empty,ConfiguredSkillValue=r.ConfiguredSkillValue};}
    public static void SendBeforePickup(World world,Vector3i pos,int playerId)
    {
        if(world==null||world.IsRemote())return;RebirthElectricalServiceRecord r;if(!RebirthInfrastructureWorkService.TryBuildPickupRecord(pos,out r)||r==null)return;
        long operationId=System.Threading.Interlocked.Increment(ref nextOperationId);ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;if(c==null)return;if(c.IsServer&&world.GetEntity(playerId) is EntityPlayerLocal)Store(world,pos,r,operationId);
        c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthElectricalProvenancePickup>().Setup(pos,r,operationId),_attachedToEntityId:playerId);
    }
    public static void ApplyPending(Vector3i pos,ItemValue value){if(value==null)return;WorldBase world=GameManager.Instance!=null?GameManager.Instance.World:null;EnsureScope(world);CleanupExpired();string key=Key(pos);Queue<PendingPickup> q;if(!pending.TryGetValue(key,out q)||q==null||q.Count==0)return;PendingPickup entry=q.Dequeue();if(q.Count==0)pending.Remove(key);if(entry==null||entry.Record==null||entry.ExpiresUtc<=DateTime.UtcNow)return;RebirthElectricalItemProvenance.Stamp(value,entry.Record);}
    public static void Store(WorldBase world,Vector3i pos,RebirthElectricalServiceRecord r,long operationId){if(r==null||operationId<=0)return;EnsureScope(world);CleanupExpired();string key=Key(pos);Queue<PendingPickup> q;if(!pending.TryGetValue(key,out q)||q==null){q=new Queue<PendingPickup>();pending[key]=q;}q.Enqueue(new PendingPickup{OperationId=operationId,Record=Copy(r),ExpiresUtc=DateTime.UtcNow+PendingLifetime});}
}

[Preserve]
public sealed class NetPackageRebirthElectricalProvenancePickup : NetPackage
{
    private Vector3i pos;private long operationId;private RebirthElectricalServiceRecord r=new RebirthElectricalServiceRecord();
    public override NetPackageDirection PackageDirection{get{return NetPackageDirection.ToClient;}}
    public NetPackageRebirthElectricalProvenancePickup Setup(Vector3i p,RebirthElectricalServiceRecord record,long id){pos=p;r=record??new RebirthElectricalServiceRecord();operationId=id;return this;}
    public override void read(PooledBinaryReader reader){BinaryReader b=(BinaryReader)reader;pos=new Vector3i(b.ReadInt32(),b.ReadInt32(),b.ReadInt32());operationId=b.ReadInt64();r=new RebirthElectricalServiceRecord();int rawKind=b.ReadByte();r.Kind=(RebirthElectricalServiceKind)Mathf.Clamp(rawKind,0,2);r.Condition=b.ReadSingle();r.Workmanship=b.ReadSingle();r.ServiceCount=b.ReadInt32();r.ConfiguredByStableId=RebirthSurvivorNetworkCodec.ReadString(b,512);r.ConfiguredByBackgroundId=RebirthSurvivorNetworkCodec.ReadString(b,128);r.ConfiguredByBonusId=RebirthSurvivorNetworkCodec.ReadString(b,128);r.ConfiguredSkillValue=b.ReadSingle();}
    public override void write(PooledBinaryWriter writer){base.write(writer);BinaryWriter b=(BinaryWriter)writer;b.Write(pos.x);b.Write(pos.y);b.Write(pos.z);b.Write(operationId);b.Write((byte)r.Kind);b.Write(r.Condition);b.Write(r.Workmanship);b.Write(r.ServiceCount);RebirthSurvivorNetworkCodec.WriteString(b,r.ConfiguredByStableId??string.Empty,512);RebirthSurvivorNetworkCodec.WriteString(b,r.ConfiguredByBackgroundId??string.Empty,128);RebirthSurvivorNetworkCodec.WriteString(b,r.ConfiguredByBonusId??string.Empty,128);b.Write(r.ConfiguredSkillValue);}
    public override void ProcessPackage(World world,GameManager callbacks){if(operationId>0)RebirthElectricalPickupBridge.Store(world,pos,r,operationId);}
    public int GetLength(){return 72+RebirthSurvivorNetworkCodec.EstimateString(r.ConfiguredByStableId,512)+RebirthSurvivorNetworkCodec.EstimateString(r.ConfiguredByBackgroundId,128)+RebirthSurvivorNetworkCodec.EstimateString(r.ConfiguredByBonusId,128);}
}

public sealed class ItemActionElectricalServiceRebirth : ItemActionExchangeItem
{
    public override void ExecuteAction(ItemActionData actionData,bool released)
    {
        if(!released||actionData==null||actionData.invData==null||actionData.lastUseTime>0f)return;
        EntityPlayer player=actionData.invData.holdingEntity as EntityPlayer;
        if(player==null||actionData.invData.world==null)return;
        // Shared voxel results can belong to unrelated world queries; resolve the actual use ray.
        Ray ray=player.GetLookRay(); ray.origin+=ray.direction.normalized*0.5f;
        if(!Voxel.Raycast(actionData.invData.world,ray,Constants.cDigAndBuildDistance,-538480653,4095,0f))return;
        WorldRayHitInfo hitInfo=Voxel.voxelRayHitInfo;
        if(hitInfo==null||!hitInfo.bHitValid)return;
        Vector3i pos=hitInfo.hit.blockPos;
        int clrIdx=0;
        BlockValue block=actionData.invData.world.GetBlock(pos);
        if(block.Block==null||!RebirthInfrastructureWorkService.IsElectricalBlock(block.Block))
        {
            RebirthElectricalServiceUiFeedback.Receive(false,"Target a powered electrical device.");
            return;
        }

        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(player.world==null || (!player.world.IsRemote() && (c==null||!c.IsServer)))
        {RebirthElectricalServiceUiFeedback.Receive(false,Localization.Get("xuiRebirthSupportUnavailable"));return;}
        if(!player.world.IsRemote())
        {
            string msg; RebirthElectricalServiceRecord snap;
            bool ok=RebirthInfrastructureWorkService.TryService(player,clrIdx,pos,out msg,out snap);
            RebirthElectricalServiceUiFeedback.Receive(ok,msg);
        }
        else
        {
            var request=NetPackageManager.GetPackage<NetPackageRebirthElectricalServiceRequest>();
            if(!(player is EntityPlayerLocal)||!RebirthMusicLibraryClient.CanSend(c,request))
            {RebirthElectricalServiceUiFeedback.Receive(false,Localization.Get("xuiRebirthSupportUnavailable"));return;}
            c.SendToServer(request.Setup(player.entityId,clrIdx,pos));
        }
    }
}

[Preserve]
public sealed class NetPackageRebirthElectricalServiceRequest : NetPackage
{
    private int playerId,clrIdx;
    private Vector3i pos;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }
    public NetPackageRebirthElectricalServiceRequest Setup(int id,int clr,Vector3i p){playerId=id;clrIdx=clr;pos=p;return this;}
    public override void read(PooledBinaryReader reader){BinaryReader b=(BinaryReader)reader;playerId=b.ReadInt32();clrIdx=b.ReadInt32();pos=new Vector3i(b.ReadInt32(),b.ReadInt32(),b.ReadInt32());}
    public override void write(PooledBinaryWriter writer){base.write(writer);BinaryWriter b=(BinaryWriter)writer;b.Write(playerId);b.Write(clrIdx);b.Write(pos.x);b.Write(pos.y);b.Write(pos.z);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(world==null||world.IsRemote()||!ValidEntityIdForSender(playerId))return;
        EntityPlayer player=world.GetEntity(playerId) as EntityPlayer;if(player==null)return;
        string msg;RebirthElectricalServiceRecord snap;
        bool ok=RebirthInfrastructureWorkService.TryService(player,clrIdx,pos,out msg,out snap);
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(c!=null&&c.IsServer)c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthElectricalServiceResult>().Setup(ok,msg,snap),_attachedToEntityId:playerId);
    }
    public int GetLength(){return 32;}
}

[Preserve]
public sealed class NetPackageRebirthElectricalServiceResult : NetPackage
{
    private bool success;private string message=string.Empty;
    private int clrIdx,x,y,z;private byte kind;private float condition,workmanship,configuredSkillValue;
    private string configuredByStableId=string.Empty,configuredByBackgroundId=string.Empty,configuredByBonusId=string.Empty;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }
    public NetPackageRebirthElectricalServiceResult Setup(bool ok,string msg,RebirthElectricalServiceRecord r)
    {
        success=ok;message=msg??string.Empty;
        clrIdx=0;x=0;y=0;z=0;kind=0;condition=0f;workmanship=0f;configuredSkillValue=0f;configuredByStableId=string.Empty;configuredByBackgroundId=string.Empty;configuredByBonusId=string.Empty;
        if(r!=null){clrIdx=r.ClrIdx;x=r.Position.x;y=r.Position.y;z=r.Position.z;kind=(byte)r.Kind;condition=r.Condition;workmanship=r.Workmanship;configuredByStableId=r.ConfiguredByStableId??string.Empty;configuredByBackgroundId=r.ConfiguredByBackgroundId??string.Empty;configuredByBonusId=r.ConfiguredByBonusId??string.Empty;configuredSkillValue=r.ConfiguredSkillValue;}
        return this;
    }
    public override void read(PooledBinaryReader reader)
    {BinaryReader b=(BinaryReader)reader;success=b.ReadBoolean();message=RebirthSurvivorNetworkCodec.ReadString(b,256);clrIdx=b.ReadInt32();x=b.ReadInt32();y=b.ReadInt32();z=b.ReadInt32();kind=b.ReadByte();condition=b.ReadSingle();workmanship=b.ReadSingle();configuredByStableId=RebirthSurvivorNetworkCodec.ReadString(b,512);configuredByBackgroundId=RebirthSurvivorNetworkCodec.ReadString(b,128);configuredByBonusId=RebirthSurvivorNetworkCodec.ReadString(b,128);configuredSkillValue=b.ReadSingle();}
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);BinaryWriter b=(BinaryWriter)writer;b.Write(success);RebirthSurvivorNetworkCodec.WriteString(b,message,256);b.Write(clrIdx);b.Write(x);b.Write(y);b.Write(z);b.Write(kind);b.Write(condition);b.Write(workmanship);RebirthSurvivorNetworkCodec.WriteString(b,configuredByStableId,512);RebirthSurvivorNetworkCodec.WriteString(b,configuredByBackgroundId,128);RebirthSurvivorNetworkCodec.WriteString(b,configuredByBonusId,128);b.Write(configuredSkillValue);}
    public override void ProcessPackage(World world,GameManager callbacks){RebirthInfrastructureWorkService.ReceiveClientSnapshot(new RebirthElectricalServiceRecord{ClrIdx=clrIdx,Position=new Vector3i(x,y,z),Kind=(RebirthElectricalServiceKind)kind,Condition=condition,Workmanship=workmanship,ConfiguredByStableId=configuredByStableId,ConfiguredByBackgroundId=configuredByBackgroundId,ConfiguredByBonusId=configuredByBonusId,ConfiguredSkillValue=configuredSkillValue});RebirthElectricalServiceUiFeedback.Receive(success,message);}
    public int GetLength(){return 64+RebirthSurvivorNetworkCodec.EstimateString(message,256)+RebirthSurvivorNetworkCodec.EstimateString(configuredByStableId,512)+RebirthSurvivorNetworkCodec.EstimateString(configuredByBackgroundId,128)+RebirthSurvivorNetworkCodec.EstimateString(configuredByBonusId,128);}
}

public static class RebirthElectricalServiceUiFeedback
{
    public static void Receive(bool success,string message)
    {
        if(string.IsNullOrEmpty(message))return;
        Log.Out("[REBIRTH Survivor Electrical] result="+(success?"OK":"FAIL")+" "+message);
        try
        {
            EntityPlayerLocal p=GameManager.Instance!=null&&GameManager.Instance.World!=null?GameManager.Instance.World.GetPrimaryPlayer():null;
            if(p!=null)GameManager.ShowTooltip(p,message,true,false,3f);
        }catch{}
    }
}

public struct RebirthConstructionCommitState
{
    public bool Valid;
    public int EntityId;
    public int ClrIdx;
    public Vector3i Pos;
    public int BeforeType;
    public int BeforeDamage;
    public int BeforeMaxDamage;
    public ItemValue Tool;
}

[HarmonyPatch]
public static class RebirthConstructionDamageBlockCommitPatch
{
    private static int worldArg=-1;
    private static int clrArg=-1;
    private static int positionArg=-1;
    private static int blockRefArg=-1;
    private static int blockValueArg=-1;
    private static int damageArg=-1;
    private static int entityArg=-1;
    private static int attackHitArg=-1;

    // V3.2 changed the Block damage contract. Resolve the authoritative OnBlockDamaged
    // overload at runtime instead of freezing a patch attribute to one build-specific signature.
    private static MethodBase TargetMethod()
    {
        MethodInfo best=null;
        int bestScore=int.MinValue;
        MethodInfo[] methods=typeof(Block).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        for(int m=0;m<methods.Length;m++)
        {
            MethodInfo candidate=methods[m];
            if(candidate==null||candidate.Name!="OnBlockDamaged"||candidate.ReturnType!=typeof(int))continue;
            ParameterInfo[] ps=candidate.GetParameters();
            int w=-1,c=-1,pos=-1,bref=-1,bv=-1,dmg=-1,ent=-1,hit=-1;
            int score=0;
            for(int i=0;i<ps.Length;i++)
            {
                ParameterInfo pi=ps[i];
                Type pt=pi.ParameterType;
                string pn=pi.Name??string.Empty;
                if(typeof(WorldBase).IsAssignableFrom(pt)){w=i;score+=8;}
                if(pt==typeof(Vector3i)){pos=i;score+=5;}
                if(pt==typeof(BlockValue)){bv=i;score+=8;}
                if(pt==typeof(ItemActionAttack.AttackHitInfo)){hit=i;score+=5;}
                if(string.Equals(pt.Name,"BlockValueRef",StringComparison.Ordinal)){bref=i;score+=5;}
                if(pt==typeof(int)&&pn.IndexOf("clr",StringComparison.OrdinalIgnoreCase)>=0){c=i;score+=2;}
                if(pt==typeof(int)&&pn.IndexOf("damagePoints",StringComparison.OrdinalIgnoreCase)>=0){dmg=i;score+=6;}
                if(pt==typeof(int)&&pn.IndexOf("entityId",StringComparison.OrdinalIgnoreCase)>=0){ent=i;score+=6;}
            }
            if(w<0||bv<0)continue;
            // Metadata names are normally present; retain a conservative positional fallback for
            // stripped builds where damage/entity parameters follow BlockValue as in native contracts.
            if(dmg<0||ent<0)
            {
                for(int i=bv+1;i<ps.Length;i++)
                {
                    if(ps[i].ParameterType!=typeof(int))continue;
                    if(dmg<0){dmg=i;continue;}
                    if(ent<0){ent=i;break;}
                }
            }
            if(dmg<0||ent<0)continue;
            if(pos<0&&bref<0&&hit<0)continue;
            if(score<=bestScore)continue;
            bestScore=score;best=candidate;
            worldArg=w;clrArg=c;positionArg=pos;blockRefArg=bref;blockValueArg=bv;damageArg=dmg;entityArg=ent;attackHitArg=hit;
        }
        if(best!=null)
            { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[REBIRTH Survivor Construction] resolved Block.OnBlockDamaged target="+best); }
        else
            Log.Error("[REBIRTH Survivor Construction] no compatible Block.OnBlockDamaged target was found; Construction repair/upgrade LBD hook is unavailable on this build.");
        return best;
    }

    public static void Prefix(object[] __args,ref RebirthConstructionCommitState __state)
    {
        __state=new RebirthConstructionCommitState();
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(c==null||!c.IsServer||__args==null)return;
        WorldBase world=GetArg<WorldBase>(__args,worldArg);
        BlockValue blockValue=GetBlockValue(__args,blockValueArg);
        int damagePoints=GetInt(__args,damageArg,0);
        int entityId=GetInt(__args,entityArg,0);
        if(world==null||damagePoints>=0||entityId<=0||blockValue.Block==null)return;
        Vector3i blockPos;
        if(!TryGetBlockPosition(__args,out blockPos))return;
        EntityPlayer player=GameManager.Instance!=null&&GameManager.Instance.World!=null?GameManager.Instance.World.GetEntity(entityId) as EntityPlayer:null;
        if(player==null)return;
        __state.Valid=true;__state.EntityId=entityId;__state.ClrIdx=GetInt(__args,clrArg,-1);__state.Pos=blockPos;
        __state.BeforeType=blockValue.type;__state.BeforeDamage=blockValue.damage;__state.BeforeMaxDamage=Math.Max(1,blockValue.Block.MaxDamage);
        ItemValue held=player.inventory!=null?player.inventory.holdingItemItemValue:null;__state.Tool=held!=null&&!held.IsEmpty()?held.Clone():null;
    }

    public static void Postfix(ref RebirthConstructionCommitState __state)
    {
        if(!__state.Valid||GameManager.Instance==null||GameManager.Instance.World==null)return;
        World world=GameManager.Instance.World;
        EntityPlayer player=world.GetEntity(__state.EntityId) as EntityPlayer;if(player==null)return;
        BlockValue after=world.GetBlock(__state.Pos);if(after.Block==null)return;

        bool upgraded=after.type!=__state.BeforeType;
        int repaired=Math.Max(0,__state.BeforeDamage-after.damage);
        if(!upgraded&&repaired<=0)return;

        RebirthSkillTrainingEvidence evidence;
        if(upgraded)
        {
            RebirthPlacedWorkmanshipService.HandleUpgrade(world,__state.Pos,player);
            evidence=new RebirthSkillTrainingEvidence
            {
                SkillId="skill.construction",SourceKey="phase8:construction-upgrade:"+__state.ClrIdx+":"+__state.Pos.x+":"+__state.Pos.y+":"+__state.Pos.z,
                ReferenceDescription="committed structural upgrade",AuthoritativeSuccess=true,Mode=RebirthSkillTrainingEvidenceMode.DiscreteAward,
                CreditedWork=1f,DiscreteRawAward=Mathf.Min(.60f,Math.Max(0f,RebirthProgressionRuntimeConfig.ConstructionUpgradeAward))
            };
        }
        else
        {
            float rate;if(__state.Tool==null||!RebirthWeaponSustainedDpsService.TryGetWorldWorkRate(player,__state.Tool,repaired,out rate)||rate<=0f)return;
            evidence=new RebirthSkillTrainingEvidence
            {
                SkillId="skill.construction",SourceKey="phase8:construction-repair:"+__state.ClrIdx+":"+__state.Pos.x+":"+__state.Pos.y+":"+__state.Pos.z,
                ReferenceDescription="actual structural condition restored",AuthoritativeSuccess=true,Mode=RebirthSkillTrainingEvidenceMode.ContinuousWork,
                CreditedWork=repaired,LiveWorkRate=rate
            };
        }
        RebirthSkillTrainingComputation computation;float gained,attribute;
        RebirthSkillAwardService.TryAwardMigratedTrainingEvidence(player,evidence,out computation,out gained,out attribute);
    }

    private static T GetArg<T>(object[] args,int index) where T:class
    {
        return index>=0&&index<args.Length?args[index] as T:null;
    }

    private static int GetInt(object[] args,int index,int fallback)
    {
        if(index<0||index>=args.Length||!(args[index] is int))return fallback;
        return (int)args[index];
    }

    private static BlockValue GetBlockValue(object[] args,int index)
    {
        if(index<0||index>=args.Length||!(args[index] is BlockValue))return default(BlockValue);
        return (BlockValue)args[index];
    }

    private static bool TryGetBlockPosition(object[] args,out Vector3i pos)
    {
        pos=default(Vector3i);
        if(positionArg>=0&&positionArg<args.Length&&args[positionArg] is Vector3i)
        { pos=(Vector3i)args[positionArg]; return true; }

        if(blockRefArg>=0&&blockRefArg<args.Length&&TryReadVector3iMember(args[blockRefArg],out pos))return true;

        if(attackHitArg>=0&&attackHitArg<args.Length)
        {
            ItemActionAttack.AttackHitInfo hit=args[attackHitArg] as ItemActionAttack.AttackHitInfo;
            if(hit!=null){pos=hit.raycastHitPosition;return true;}
        }
        return false;
    }

    private static bool TryReadVector3iMember(object value,out Vector3i pos)
    {
        pos=default(Vector3i);
        if(value==null)return false;
        Type t=value.GetType();

        // V3.2 BlockValueRef exposes TryGetBlockPos(out Vector3i), which correctly rejects
        // prop references. Use it when available so prop damage cannot be mistaken for block 0,0,0.
        MethodInfo tryGetBlockPos=t.GetMethod("TryGetBlockPos",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,
            null,new Type[]{typeof(Vector3i).MakeByRefType()},null);
        if(tryGetBlockPos!=null&&tryGetBlockPos.ReturnType==typeof(bool))
        {
            object[] call=new object[]{default(Vector3i)};
            object ok=tryGetBlockPos.Invoke(value,call);
            if(ok is bool&&(bool)ok&&call[0] is Vector3i){pos=(Vector3i)call[0];return true;}
            return false;
        }

        string[] names=new[]{"BlockPosition","blockPosition","Position","position","Pos","pos","_blockPos"};
        for(int i=0;i<names.Length;i++)
        {
            PropertyInfo p=t.GetProperty(names[i],BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            if(p!=null&&p.PropertyType==typeof(Vector3i))
            { object v=p.GetValue(value,null); if(v is Vector3i){pos=(Vector3i)v;return true;} }
            FieldInfo f=t.GetField(names[i],BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            if(f!=null&&f.FieldType==typeof(Vector3i))
            { object v=f.GetValue(value); if(v is Vector3i){pos=(Vector3i)v;return true;} }
        }
        return false;
    }
}

[HarmonyPatch(typeof(TileEntityPowered),"get_PowerUsed")]
public static class RebirthElectricalPowerUsedPatch
{
    public static void Postfix(TileEntityPowered __instance,ref int __result)
    { __result=RebirthInfrastructureWorkService.AdjustPowerUsed(__instance,__result); }
}
