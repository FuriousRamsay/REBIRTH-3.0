using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

internal static class RebirthCompanionLeadership
{
    internal static int Capacity(float leadership) => leadership>=60f?4:leadership>=40f?3:leadership>=20f?2:1;
    internal static float ManagementTimeReduction(float level) => level>=80f?.20f:level>=60f?.15f:level>=40f?.10f:level>=20f?.05f:0f;
    internal static float ManagementBlockDamage(float level) => level>=80f?1.40f:level>=60f?1.30f:level>=40f?1.20f:level>=20f?1.10f:1f;
    // Pure read APIs (no side effects) for the directing player's Management tiers. Neutral (1 / no
    // reduction) when the owner cannot be resolved; callers apply each exactly once with their other effects.
    public static float ManagementLevel(EntityPlayer owner)=>owner==null?0f:Skill(owner,RebirthSurvivorIds.SkillManagement);
    public static EntityPlayer DirectingPlayer(RebirthNpcStableId npc)
    {
        if(npc.IsEmpty||!RebirthNpcRuntimeRegistry.TryGetEntityId(npc,out int entity)||!RebirthNpcRuntimeRegistry.TryGet(entity,out var state)||state==null||
           state.OwnershipKind!=RebirthNpcOwnershipKind.Player||string.IsNullOrEmpty(state.OwnerId))return null;
        return RebirthNpcInteractionMutationHandlers.FindActorPlayer(state.OwnerId);
    }
    public static float ManagementBlockDamageMultiplier(EntityPlayer owner)=>ManagementBlockDamage(ManagementLevel(owner));
    public static float ManagementBlockDamageMultiplier(RebirthNpcStableId npc)=>ManagementBlockDamageMultiplier(DirectingPlayer(npc));
    // Applies the cooking-time reduction to an already skill-adjusted duration (any unit).
    public static float ManagementAdjustedCookingDuration(float skillAdjustedDuration,EntityPlayer owner)=>
        skillAdjustedDuration*(1f-ManagementTimeReduction(ManagementLevel(owner)));
    public static float ManagementAdjustedCookingDuration(float skillAdjustedDuration,RebirthNpcStableId npc)=>
        ManagementAdjustedCookingDuration(skillAdjustedDuration,DirectingPlayer(npc));
    // B13 PROVISIONAL (unapproved percentages; same 0.95/0.90/0.85/0.80 tiers as the other Management effects).
    // Repair material: payable quantity of a whole repair. Rounds UP so a discount shows only once it saves a
    // whole item, never below 1 (one-item repairs keep their cost). Dormant until the P9 repair gates open.
    public static int ManagementAdjustedRepairMaterial(int nativeNeeded,EntityPlayer owner)
    {
        if(nativeNeeded<=1)return Math.Max(nativeNeeded,0);
        float scaled=nativeNeeded*(1f-ManagementTimeReduction(ManagementLevel(owner)));
        return Math.Max(1,Math.Min(nativeNeeded,(int)Math.Ceiling(scaled-0.0001f)));
    }
    public static int ManagementAdjustedRepairMaterial(int nativeNeeded,RebirthNpcStableId npc)=>
        ManagementAdjustedRepairMaterial(nativeNeeded,DirectingPlayer(npc));
    // Looting: container-search TIME only (requirements forbid loot stage/quantity/quality from Management).
    public static float ManagementAdjustedLootSearchSeconds(float skillAdjustedSeconds,EntityPlayer owner)=>
        skillAdjustedSeconds*(1f-ManagementTimeReduction(ManagementLevel(owner)));
    public static float ManagementAdjustedLootSearchSeconds(float skillAdjustedSeconds,RebirthNpcStableId npc)=>
        ManagementAdjustedLootSearchSeconds(skillAdjustedSeconds,DirectingPlayer(npc));
    internal static float Skill(EntityPlayer player,string id)
    { return RebirthServiceCraftSkillService.TryGetPracticalSkillValue(player,id,out var value)?value:0f; }
    // Group-role classes are never recruitable, even while formation is unfinished (bootstrap held,
    // followers not composed yet, or an interrupted formation reloaded without its journal row).
    private static bool IsGroupRoleEntity(RebirthNpcStableId id)
    {
        if(!RebirthNpcRuntimeRegistry.TryGetEntityId(id,out int entityId)||
           !(GameManager.Instance?.World?.GetEntity(entityId) is EntityRebirthHumanoidNPC npc))return false;
        if(npc.IsFreshGroupBootstrapPending)return true;
        if(EntityClass.list==null||!EntityClass.list.TryGetValue(npc.entityClass,out var definition))return false;
        string name=definition.entityClassName;
        return name=="npcRebirthGroupLeaderManSDCS"||name=="npcRebirthGroupLeaderWomanSDCS"||
               name=="npcRebirthGroupFollowerManSDCS"||name=="npcRebirthGroupFollowerWomanSDCS";
    }
    internal static bool IsEstablishedGroupMember(RebirthNpcStableId id)
    {
        if(IsGroupRoleEntity(id))return true;
        if(!RebirthNpcAggregatePersistenceStore.TryGet(id,out var person))return false;
        foreach(string raw in person.UnknownOptionalElements??new string[0])
        {
            // A damaged optional group row must never make its NPC recruitable.
            try
            {
                var node=RebirthCompanionDeskStore.Parse(raw);
                if(node.Name=="humanGroup")return true; // Forming and active groups are protected.
            }
            catch(System.Xml.XmlException){return true;}
            catch(System.IO.InvalidDataException){return true;}
        }
        return false;
    }
    internal static bool CanHire(EntityPlayer player,RebirthNpcStableId target,out string reason)=>CanHire(player,target,null,out reason);
    // Read API for UI: world-wide owned humans, Leadership limit, and whether the override lifts the limit.
    internal static bool TryGetCapacity(EntityPlayer player,out int used,out int limit,out bool unrestricted)
    {
        used=0;limit=0;unrestricted=false;
        var owner=player==null?null:GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId)?.PrimaryId;
        if(owner==null)return false;
        unrestricted=RebirthSandboxOptionManager.Current!=null&&RebirthSandboxOptionManager.Current.DisableHiringRestrictions;
        used=OwnedHumanCount(owner.ToString(),null);limit=Capacity(Skill(player,RebirthSurvivorIds.SkillLeadership));
        return true;
    }
    // reserved: other NPCs whose paid hire already started debiting for this owner. They hold a slot so
    // two prepared payments can never both pay for the one remaining slot.
    internal static bool CanHire(EntityPlayer player,RebirthNpcStableId target,ISet<RebirthNpcStableId> reserved,out string reason)
    {
        reason="";
        if(player==null||player.world==null||player.world.IsRemote()||player.IsDead()) { reason=RebirthCompanionDeskStore.L("Unavailable");return false; }
        if(IsEstablishedGroupMember(target)){reason=Localization.Get("xuiRebirthNpcEstablishedGroup");return false;}
        // The override removes capacity restrictions, never the world's existing group membership.
        if(RebirthSandboxOptionManager.Current!=null&&RebirthSandboxOptionManager.Current.DisableHiringRestrictions)return true;
        var owner=GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId)?.PrimaryId;
        if(owner==null){reason=RebirthCompanionDeskStore.L("Unavailable");return false;}
        // Management's roster is radius-filtered and capped for its window. Hiring
        // capacity is a world-wide ownership rule, including unloaded humans.
        int used=OwnedHumanCount(owner.ToString(),reserved);
        int limit=Capacity(Skill(player,RebirthSurvivorIds.SkillLeadership));
        if(used>=limit)
        {
            int owned=reserved==null||reserved.Count==0?used:OwnedHumanCount(owner.ToString(),null);
            reason=used>owned
                ?string.Format(Localization.Get("xuiRebirthNpcLeadershipReserved"),owned,limit,used-owned)
                :string.Format(Localization.Get("xuiRebirthNpcLeadershipCapacity"),used,limit);
            return false;
        }
        return true;
    }
    // Reservation-adjusted availability for UI (server/host only; a remote client must rely on the
    // server's rejection text). owned excludes pending hires, reserved counts other unsettled payments.
    internal static bool GetHireAvailability(EntityPlayer player,RebirthNpcStableId target,out int owned,out int reservedSlots,out int limit,out bool unrestricted,out string reason)
    {
        owned=0;reservedSlots=0;limit=0;reason="";unrestricted=false;
        // Runs in a UI refresh loop: never throws, and every missing input is a localized refusal.
        try
        {
            var data=player==null||target.IsEmpty?null:GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
            if(data?.PrimaryId==null||player.world==null){reason=RebirthCompanionDeskStore.L("Unavailable");return false;}
            string ownerKey=data.PrimaryId.ToString();
            unrestricted=RebirthSandboxOptionManager.Current!=null&&RebirthSandboxOptionManager.Current.DisableHiringRestrictions;
            owned=OwnedHumanCount(ownerKey,null);limit=Capacity(Skill(player,RebirthSurvivorIds.SkillLeadership));
            var held=RebirthSurvivorHirePayment.CachedReservations(player,target);
            reservedSlots=Math.Max(0,OwnedHumanCount(ownerKey,held)-owned);
            return CanHire(player,target,held,out reason);
        }
        catch(Exception ex)
        {
            Log.Warning("[REBIRTH Leadership] availability: "+ex.GetType().Name);
            reason=RebirthCompanionDeskStore.L("Unavailable");return false;
        }
    }
    private static int OwnedHumanCount(string owner,ISet<RebirthNpcStableId> reserved)
    {
        var seen=new HashSet<RebirthNpcStableId>();
        if(reserved!=null)foreach(var held in reserved)if(!held.IsEmpty)seen.Add(held);
        foreach(var state in RebirthNpcRuntimeRegistry.GetSnapshot())
            if(state!=null && !state.StableId.IsEmpty && state.Presence!=RebirthNpcPresenceState.Removed &&
               state.OwnershipKind==RebirthNpcOwnershipKind.Player &&
               string.Equals(state.OwnerId,owner,StringComparison.OrdinalIgnoreCase) && IsHumanProfile(state.ProfileId))
                seen.Add(state.StableId);
        foreach(var saved in RebirthNpcAggregatePersistenceStore.SnapshotViews())
            if(saved?.Identity!=null && !saved.Identity.StableNpcId.IsEmpty &&
               saved.Lifecycle?.TombstoneState!=true &&
               !string.Equals(saved.Presence?.PresenceState,RebirthNpcPresenceState.Removed.ToString(),StringComparison.OrdinalIgnoreCase) &&
               saved.Dog==null && saved.BoundUndead==null &&
               string.Equals(saved.Ownership?.OwnershipState,RebirthNpcOwnershipKind.Player.ToString(),StringComparison.OrdinalIgnoreCase) &&
               string.Equals(saved.Ownership?.OwnerPlatformIdOrPersistentPlayerId,owner,StringComparison.OrdinalIgnoreCase) &&
               IsHumanProfile(saved.Profile?.ProfileId))
                seen.Add(saved.Identity.StableNpcId);
        return seen.Count;
    }
    private static bool IsHumanProfile(string profileId)
    {
        if(!RebirthNpcProfileRegistry.TryResolve(profileId,out var profile))return true; // Unknown owned records fail closed.
        return profile.Category!=RebirthNpcCategory.DogCompanion && profile.Category!=RebirthNpcCategory.PantherCompanion;
    }
}
