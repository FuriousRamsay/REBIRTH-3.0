using System;
using UnityEngine;

// B13 approved tier effect: the directing player's Management multiplies the growing duration of
// a crop the NPC just harvested and auto-replanted (0/20/40/60/80 -> x1.0/0.95/0.90/0.85/0.80).
// Attribution reuses the saved crop provenance fields on TileEntityPlantGrowingRebirth
// (grower id, bonus id, planting skill value, planted world time): no save-format change.
// The Management level is frozen at prepare time. The intent (mgmtOwner, mgmtLevel) is journaled in the
// farmWorkOriginal row before the block commit; RebirthNpcFarmOriginal applies and verifies it with
// bounded retries and does not complete the row until it is applied, skipped, or given up.
// A crop that already carries another provenance bonus (Rapid Cultivation) is never overwritten.
internal static class RebirthNpcManagementGrowth
{
    internal const string BonusId="npc.management.growth";
    internal static readonly bool Qualified=true;
    internal const int MaxAttempts=12;

    // Neutral (1) unless the crop was stamped. Pure read for the growth-duration boundary.
    internal static float GetMultiplier(WorldBase world,Vector3i pos)
    {
        if(!Qualified||world==null)return 1f;
        var te=world.GetTileEntity(pos) as TileEntityPlantGrowingRebirth;
        if(te==null||!string.Equals(te.RebirthGrowerBonusId,BonusId,StringComparison.Ordinal))return 1f;
        return Mathf.Clamp(1f-RebirthCompanionLeadership.ManagementTimeReduction(te.RebirthPlantingSkillValue),0.5f,1f);
    }

    // Intent captured at prepare time; false when there is no tier to apply.
    internal static bool TryCaptureIntent(RebirthNpcStableId npc,out string ownerId,out float level)
    {
        ownerId=null;level=0f;
        if(!Qualified||npc.IsEmpty)return false;
        var owner=RebirthCompanionLeadership.DirectingPlayer(npc);
        level=RebirthCompanionLeadership.ManagementLevel(owner);
        if(owner==null||RebirthCompanionLeadership.ManagementTimeReduction(level)<=0f)return false;
        if(!RebirthNpcRuntimeRegistry.TryGetEntityId(npc,out int entity)||
           !RebirthNpcRuntimeRegistry.TryGet(entity,out var state)||state==null||string.IsNullOrEmpty(state.OwnerId))return false;
        ownerId=state.OwnerId;return true;
    }

    // Idempotent. True = settled (applied and read back, or deliberately skipped because another bonus owns the crop).
    // False = tile entity not available / not verified yet; retry later.
    internal static bool Stamp(World world,Vector3i pos,string ownerId,float level)
    {
        try
        {
            if(!Qualified||world==null||world.IsRemote()||string.IsNullOrEmpty(ownerId))return false;
            var existing=RebirthCropProvenanceAdapter.CaptureSnapshot(world,pos);
            if(existing!=null&&!string.IsNullOrEmpty(existing.BonusId)&&
               !string.Equals(existing.BonusId,BonusId,StringComparison.Ordinal))return true;
            if(existing==null||!string.Equals(existing.BonusId,BonusId,StringComparison.Ordinal)||
               !string.Equals(existing.GrowerStableId,ownerId,StringComparison.Ordinal)||
               Math.Abs(existing.PlantingSkillValue-level)>0.001f)
                RebirthCropProvenanceAdapter.RestoreSnapshot(world,pos,new RebirthCropProvenanceSnapshot{
                    GrowerStableId=ownerId,BackgroundId=string.Empty,BonusId=BonusId,
                    PlantingSkillValue=level,PlantedWorldTime=world.worldTime});
            var read=RebirthCropProvenanceAdapter.CaptureSnapshot(world,pos);
            return read!=null&&string.Equals(read.BonusId,BonusId,StringComparison.Ordinal)&&
                   string.Equals(read.GrowerStableId,ownerId,StringComparison.Ordinal)&&
                   Math.Abs(read.PlantingSkillValue-level)<=0.001f;
        }
        catch{return false;}
    }
}
