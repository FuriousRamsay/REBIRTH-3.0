using System;

#nullable disable

public sealed class RebirthCropProvenanceSnapshot
{
    public string GrowerStableId=string.Empty;
    public string BackgroundId=string.Empty;
    public string BonusId=string.Empty;
    public float PlantingSkillValue;
    public ulong PlantedWorldTime;
}

public static class RebirthCropProvenanceAdapter
{
    private sealed class PendingEntry
    {
        public RebirthCropProvenanceSnapshot Snapshot;
        public long Generation;
        public DateTime ExpiresUtc;
    }
    private static readonly System.Collections.Generic.Dictionary<string,PendingEntry> pending = new System.Collections.Generic.Dictionary<string,PendingEntry>(StringComparer.Ordinal);
    private static readonly System.Collections.Generic.Dictionary<string,long> generations = new System.Collections.Generic.Dictionary<string,long>(StringComparer.Ordinal);
    private static string scope=string.Empty;
    private static readonly TimeSpan PendingLifetime=TimeSpan.FromSeconds(30);
    private static string Key(Vector3i p){return p.x+","+p.y+","+p.z;}

    private static void EnsureScope(WorldBase world)
    {
        string save=string.Empty;try{save=GameIO.GetSaveGameDir()??string.Empty;}catch{}
        string next=save+"|"+(world!=null?System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(world).ToString():"0");
        if(string.Equals(scope,next,StringComparison.Ordinal))return;
        scope=next;pending.Clear();generations.Clear();
    }

    private static void CleanupExpired()
    {
        if(pending.Count==0)return;DateTime now=DateTime.UtcNow;System.Collections.Generic.List<string> expired=new System.Collections.Generic.List<string>();
        foreach(System.Collections.Generic.KeyValuePair<string,PendingEntry> kv in pending)if(kv.Value==null||kv.Value.ExpiresUtc<=now)expired.Add(kv.Key);
        for(int i=0;i<expired.Count;i++)pending.Remove(expired[i]);
    }

    public static void ApplyPending(WorldBase world,Vector3i pos)
    {
        EnsureScope(world);CleanupExpired();string key=Key(pos);PendingEntry entry;if(!pending.TryGetValue(key,out entry)||entry==null||entry.Snapshot==null)return;
        long current;generations.TryGetValue(key,out current);if(current!=entry.Generation){pending.Remove(key);return;}
        TileEntityPlantGrowingRebirth te=world!=null?world.GetTileEntity(pos) as TileEntityPlantGrowingRebirth:null;if(te==null)return;
        if(te.RebirthProvenanceWorldTime!=0UL&&te.RebirthProvenanceWorldTime!=entry.Snapshot.PlantedWorldTime){pending.Remove(key);return;}
        pending.Remove(key);Apply(te,entry.Snapshot);
    }

    public static void CapturePlanting(WorldBase world,Vector3i pos,PlatformUserIdentifierAbs addedByPlayer)
    {
        if(world==null||world.IsRemote()||addedByPlayer==null||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        EnsureScope(world);CleanupExpired();string key=Key(pos);long generation;generations.TryGetValue(key,out generation);generations[key]=generation+1L;pending.Remove(key);
        World liveWorld=GameManager.Instance!=null?GameManager.Instance.World:null;EntityPlayer player=FindPlayer(liveWorld,addedByPlayer);if(player==null||!RebirthBackgroundBonusService.HasBonus(player,"background_bonus.rapid_cultivation"))return;
        RebirthProvenanceAuthorSnapshot author;if(!RebirthProvenanceIdentity.TryCapture(player,out author))return;
        float skill=0f;RebirthServiceCraftSkillService.TryGetSkillValue(player,"skill.farming",out skill);
        TileEntityPlantGrowingRebirth te=world.GetTileEntity(pos) as TileEntityPlantGrowingRebirth;if(te==null)return;
        te.RebirthGrowerStableId=author.StablePlayerId;te.RebirthGrowerBackgroundId=author.BackgroundId;te.RebirthGrowerBonusId=author.BonusId;te.RebirthPlantingSkillValue=skill;te.RebirthProvenanceWorldTime=liveWorld!=null?liveWorld.worldTime:0UL;te.SetModified();
    }

    public static RebirthCropProvenanceSnapshot CaptureSnapshot(WorldBase world,Vector3i pos)
    {
        EnsureScope(world);
        TileEntityPlantGrowingRebirth te=world!=null?world.GetTileEntity(pos) as TileEntityPlantGrowingRebirth:null;if(te==null||string.IsNullOrEmpty(te.RebirthGrowerStableId))return null;
        return new RebirthCropProvenanceSnapshot{GrowerStableId=te.RebirthGrowerStableId,BackgroundId=te.RebirthGrowerBackgroundId,BonusId=te.RebirthGrowerBonusId,PlantingSkillValue=te.RebirthPlantingSkillValue,PlantedWorldTime=te.RebirthProvenanceWorldTime};
    }

    public static void RestoreSnapshot(WorldBase world,Vector3i pos,RebirthCropProvenanceSnapshot p)
    {
        if(world==null||p==null)return;EnsureScope(world);CleanupExpired();string key=Key(pos);TileEntityPlantGrowingRebirth te=world.GetTileEntity(pos) as TileEntityPlantGrowingRebirth;
        if(te==null){long generation;generations.TryGetValue(key,out generation);pending[key]=new PendingEntry{Snapshot=p,Generation=generation,ExpiresUtc=DateTime.UtcNow+PendingLifetime};return;}
        pending.Remove(key);Apply(te,p);
    }

    private static void Apply(TileEntityPlantGrowingRebirth te,RebirthCropProvenanceSnapshot p)
    {
        te.RebirthGrowerStableId=p.GrowerStableId??string.Empty;te.RebirthGrowerBackgroundId=p.BackgroundId??string.Empty;te.RebirthGrowerBonusId=p.BonusId??string.Empty;te.RebirthPlantingSkillValue=p.PlantingSkillValue;te.RebirthProvenanceWorldTime=p.PlantedWorldTime;te.SetModified();
    }

    public static string BuildDebugSummary(WorldBase world,Vector3i pos)
    {
        TileEntityPlantGrowingRebirth te=world!=null?world.GetTileEntity(pos) as TileEntityPlantGrowingRebirth:null;if(te==null)return "[REBIRTH Provenance Crop] pos="+pos+" tileEntity=none";
        return "[REBIRTH Provenance Crop] pos="+pos+" grower="+te.RebirthGrowerStableId+" background="+te.RebirthGrowerBackgroundId+" bonus="+te.RebirthGrowerBonusId+" farmingSkill="+te.RebirthPlantingSkillValue.ToString("0.###")+" plantedWorldTime="+te.RebirthProvenanceWorldTime;
    }

    private static EntityPlayer FindPlayer(World world,PlatformUserIdentifierAbs id)
    {
        if(world==null||id==null||world.Players==null||world.Players.list==null||GameManager.Instance==null)return null;
        for(int i=0;i<world.Players.list.Count;i++){EntityPlayer p=world.Players.list[i];if(p==null)continue;PersistentPlayerData pp=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(p.entityId);if(pp!=null&&pp.PrimaryId!=null&&pp.PrimaryId.Equals(id))return p;}return null;
    }
}
