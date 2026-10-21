using System;
using System.Collections.Generic;
using UnityEngine;

// Detached semantic projection of an existing live native incarnation. This
// never reconstructs a body, changes native health/items or allocates an NPC.
internal sealed class RebirthNpcRecoveryRuntimeProjection
{
    private sealed class Row
    {
        internal EntityRebirthNPC Actor;internal RebirthNpcRuntimeState Runtime;
        internal RebirthNpcRuntimeState Before;internal uint Revision;
        internal RebirthNpcPresenceState Presence;internal RebirthNpcOwnershipKind Ownership;
        internal string Owner;internal RebirthNpcOrderState Order;
        internal bool Guard;internal Vector3 Position;internal int NativeHealth;
        internal RebirthHumanNpcAppearanceDescriptor? Appearance;
    }
    private readonly World world;
    private readonly Row[] rows;
    private bool installed;private readonly bool wholeRegistry;
    private RebirthNpcRecoveryRuntimeProjection(World world,Row[] rows,bool whole){this.world=world;this.rows=rows;wholeRegistry=whole;}
    private static bool Same(RebirthNpcRuntimeState left,RebirthNpcRuntimeState right)=>left!=null&&right!=null&&left.StableId==right.StableId&&left.ProfileId==right.ProfileId&&left.Revision==right.Revision&&left.Presence==right.Presence&&left.OwnershipKind==right.OwnershipKind&&left.OwnerId==right.OwnerId&&left.Order==right.Order&&left.Travel==right.Travel&&left.HasGuardPosition==right.HasGuardPosition&&left.GuardPosition.Equals(right.GuardPosition)&&left.PreparedRestorationPending==right.PreparedRestorationPending&&left.HasHumanAppearance==right.HasHumanAppearance&&(!left.HasHumanAppearance||left.HumanAppearance.Equals(right.HumanAppearance));
    internal bool IsCurrent
    {
        get
        {
            if(installed||!ThreadManager.IsMainThread()||!ReferenceEquals(GameManager.Instance?.World,world))return false;
            if(wholeRegistry&&RebirthNpcRuntimeRegistry.GetSnapshot().Length!=rows.Length)return false;
            foreach(var row in rows)
                if(!ReferenceEquals(world.GetEntity(row.Actor.entityId),row.Actor)||!ReferenceEquals(row.Actor.RebirthRuntimeState,row.Runtime)||!Same(row.Runtime,row.Before)||row.Actor.bDead||row.Actor.IsDead()||row.Actor.Health!=row.NativeHealth)return false;
            return true;
        }
    }
    internal static bool TryPrepare(RebirthNpcPersistentRecord[] records,out RebirthNpcRecoveryRuntimeProjection plan,out string error)
    {return TryPrepareCore(records,null,false,out plan,out error);}
    internal static bool TryPrepareInstalled(RebirthNpcPersistentRecord[] records,out RebirthNpcRecoveryRuntimeProjection plan,out string error)
    {return TryPrepareCore(records,null,true,out plan,out error);}
    internal static bool TryPrepareCurrentNative(EntityRebirthNPC npc,RebirthNpcPersistentRecord record,out RebirthNpcRecoveryRuntimeProjection plan,out string error)
    {return TryPrepareCore(new[]{record},npc,true,out plan,out error);}
    private static bool TryPrepareCore(RebirthNpcPersistentRecord[] records,EntityRebirthNPC only,bool currentBacking,out RebirthNpcRecoveryRuntimeProjection plan,out string error)
    {
        plan=null;error=string.Empty;var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false){error="native recovery projection requires authoritative game thread";return false;}
        var byId=new Dictionary<RebirthNpcStableId,RebirthNpcPersistentRecord>();foreach(var record in records??new RebirthNpcPersistentRecord[0])byId.Add(record.Identity.StableNpcId,record);
        var prepared=new List<Row>();
        foreach(var runtime in only==null?RebirthNpcRuntimeRegistry.GetSnapshot():new[]{only.RebirthRuntimeState})
        {
            if(!RebirthNpcRuntimeRegistry.TryGetEntityId(runtime.StableId,out int entity)||world.GetEntity(entity) is not EntityRebirthNPC npc||!ReferenceEquals(npc.RebirthRuntimeState,runtime))
            {error="runtime projection native identity is unavailable";return false;}
            if(!byId.TryGetValue(runtime.StableId,out var record)){error="backup would orphan a live native NPC; settle its lifecycle first";return false;}
            if(runtime.PreparedRestorationPending||runtime.Travel!=RebirthNpcTravelState.None||record.Profile.ProfileId!=runtime.ProfileId||
                !Enum.TryParse(record.Presence.PresenceState,false,out RebirthNpcPresenceState presence)||!Enum.IsDefined(typeof(RebirthNpcPresenceState),presence)||presence!=runtime.Presence)
            {error="backup requires an incompatible native lifecycle/travel transition";return false;}
            RebirthNpcOwnershipKind ownership=RebirthNpcOwnershipKind.None;RebirthNpcOrderState order=RebirthNpcOrderState.None;
            if(record.Ownership!=null&&(!Enum.TryParse(record.Ownership.OwnershipState,false,out ownership)||!Enum.IsDefined(typeof(RebirthNpcOwnershipKind),ownership)))
            {error="backup ownership projection is invalid";return false;}
            string recoveredOwner=record.Ownership?.OwnerPlatformIdOrPersistentPlayerId??string.Empty;
            if(recoveredOwner.Length>RebirthNpcNetworkFraming.MaxId||recoveredOwner!=recoveredOwner.Trim())
            {error="backup owner identity is not losslessly representable in the projection codec";return false;}
            foreach(char ch in recoveredOwner)if(char.IsControl(ch)){error="backup owner identity contains control characters";return false;}
            if(record.Order!=null&&(!Enum.TryParse(record.Order.OrderState,false,out order)||!Enum.IsDefined(typeof(RebirthNpcOrderState),order)))
            {error="backup order projection is invalid";return false;}
            if((order==RebirthNpcOrderState.Travel&&!(currentBacking&&RebirthNpcExecutionPersistenceStore.HasCurrentTravelOriginal(runtime.StableId)))||order==RebirthNpcOrderState.Mission||order==RebirthNpcOrderState.Work)
            {error="backup order has retained native travel/mission/Work custody";return false;}
            if(!npc.NativeIdentityReadable||npc.bDead||npc.IsDead()||(!currentBacking&&record.Vitals.CurrentHealth!=npc.Health)||!string.Equals(record.Vitals.DeathOrIncapacitationState,"alive",StringComparison.OrdinalIgnoreCase))
            {error="backup vitals conflict with the existing native living body";return false;}
            if(RebirthNpcAggregatePersistenceStore.TryGetView(runtime.StableId,out var current)&&current.Presence.EmbodimentGeneration!=record.Presence.EmbodimentGeneration)
            {error="backup belongs to a different native embodiment generation";return false;}
            if(!currentBacking&&record.NativeReconstruction!=null)
            {
                RebirthNpcInventoryTransactionService.TryGetExistingNativeCustody(runtime.StableId,out var custody,out var revision);
                if(!record.NativeReconstruction.TryValidateCustody(custody,revision)){error="backup native inventory reconstruction differs from current custody";return false;}
            }
            if(runtime.HasHumanAppearance&&(!record.HumanAppearance.HasValue||!runtime.HumanAppearance.Equals(record.HumanAppearance.Value)))
            {error="backup would replace the existing resolved native appearance identity";return false;}
            uint restored=record.Profile.LastSuccessfulResolutionRevision;
            if(record.Presence.PresenceRevision>restored||(record.Ownership?.OwnerRevision??0)>restored||(record.Order?.OrderRevision??0)>restored)
            {error="backup runtime revision cannot represent its domain revisions without inflation";return false;}
            prepared.Add(new Row{Actor=npc,Runtime=runtime,Before=runtime.CloneForProjection(),Revision=restored,Presence=presence,Ownership=ownership,
                Owner=recoveredOwner,Order=order,Guard=record.Transform.AnchorPosition.HasValue,
                Position=record.Transform.AnchorPosition??Vector3.zero,NativeHealth=npc.Health,Appearance=record.HumanAppearance});
        }
        plan=new RebirthNpcRecoveryRuntimeProjection(world,prepared.ToArray(),only==null);return true;
    }
    internal bool Install()
    {
        if(!IsCurrent)return false;
        // All rows were validated before changing the first live projection.
        foreach(var row in rows)
        {
            var state=row.Runtime;
            // Native callbacks belong to retryable convergence AFTER this
            // complete semantic install. A model/name exception must never
            // leave a partially applied one-use detached candidate.
            state.Presence=row.Presence;state.OwnershipKind=row.Ownership;state.OwnerId=row.Owner;state.Order=row.Order;
            state.GuardPosition=row.Position;state.HasGuardPosition=row.Guard;
            if(row.Appearance.HasValue){state.HumanAppearance=row.Appearance.Value;state.HasHumanAppearance=true;}
            state.Revision=row.Revision;state.MarkPersisted();
        }
        installed=true;return true;
    }
    internal bool ConvergeNative(ulong generation,out string error)
    {
        error=string.Empty;
        if(!installed||!ThreadManager.IsMainThread()||!ReferenceEquals(GameManager.Instance?.World,world)){error="installed recovery runtime world changed";return false;}
        foreach(var state in RebirthNpcRuntimeRegistry.GetSnapshot())
        {
            if(!RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId,out int entity)||world.GetEntity(entity) is not EntityRebirthNPC actor||!ReferenceEquals(actor.RebirthRuntimeState,state))
            {error="Latest installed native incarnation mapping is unavailable";return false;}
            if(RebirthNpcLiveRecoveryRuntime.IsNativeActivationDeferred(actor)){error="Current native activation remains pending";return false;}
            string desiredName=RebirthNpcWorldIntegrationService.GetDisplayName(state.StableId);
            if(actor.EntityName!=desiredName)actor.SetEntityName(desiredName);
            if(actor.EntityName!=desiredName){error="Native recovered name readback pending";return false;}
            if(actor is EntityRebirthHumanoidNPC humanoid&&state.HasHumanAppearance&&
                !humanoid.ApplyReplicatedHumanAppearance(state.HumanAppearance,state.Revision,generation))
            {error="native appearance projection remains pending";return false;}
            if(actor is EntityRebirthDogCompanion dog)
            {
                RebirthDogLifecycleService.SynchronizeLegacyOwnerProjection(dog);
                RebirthDogLifecycleService.SynchronizeDisplayName(dog);
                if(!string.Equals(dog.EntityName,RebirthNpcWorldIntegrationService.GetDisplayName(state.StableId),StringComparison.Ordinal))
                {error="native dog name projection remains pending";return false;}
                bool owned=state.OwnershipKind==RebirthNpcOwnershipKind.Player&&!string.IsNullOrWhiteSpace(state.OwnerId);
                EntityPlayer owner=owned?RebirthDogRuntimeService.ResolveOwnerPublic(world,state.OwnerId):null;
                if(owned&&owner==null){error="native dog owner projection awaits the actual owner entity";return false;}
                int expected=owner?.entityId??0;
                if(dog.Buffs==null||dog.belongsPlayerId!=expected||dog.Buffs.GetCustomVar("$Leader")!=expected||(owner!=null&&!owner.HasOwnedEntity(dog.entityId)))
                {error="native dog ownership projection remains pending";return false;}
            }
        }
        return true;
    }
}
