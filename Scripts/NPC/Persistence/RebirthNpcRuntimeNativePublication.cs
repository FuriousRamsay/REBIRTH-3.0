using System;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEngine;

public static partial class RebirthNpcAggregatePersistenceStore
{
    // Independently saved CURRENT primary only. No backup, old captured DTO,
    // mutable pending identity guess or retained runtime object is installed.
    internal static bool TryReadNativeAdoptionRuntime(RebirthNpcRuntimeNativeAdoption.NativeReentry source,
        RebirthNpcRuntimeNativeAdoption.Outgoing original,out RebirthNpcRuntimeState state)
    {
        state=null;
        try
        {
            if(source?.Current()!=true||original?.IsDetached!=true||!source.Matches(original))return false;
            lock(Sync)
            {
                if(!loaded||dirty||QuarantinedRecords.Count!=0||!ReferenceEquals(loadedNativeWorld,source.World)||
                    !RebirthNpcSaveQuarantineScope.SameDirectory(loadedSaveDirectory,source.Directory))return false;
                string path=Path.Combine(loadedSaveDirectory,FileName);
                if(File.Exists(path+".tmp")||RebirthNpcPersistenceFile.IsRecoveryFenced(path))return false;
                using(var pin=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    var primary=RebirthNpcRecoveryCurrentBacking.Read(path);
                    if(!ValidateRecoveryDocument(primary,loadedSaveScope)||SavedImage(primary)!=RetirementImage(Records.Values))return false;
                    var rows=primary.DocumentElement.SelectNodes("record").Cast<XmlElement>().Select(ReadRecord)
                        .Where(value=>value.Identity.StableNpcId==source.StableId).ToArray();
                    if(rows.Length!=1)return false;var record=rows[0];
                    if(record.Profile.ProfileId!=source.Actor.RebirthProfileId||
                        !Enum.TryParse(record.Presence.PresenceState,false,out RebirthNpcPresenceState presence)||
                        !Enum.IsDefined(typeof(RebirthNpcPresenceState),presence))return false;
                    RebirthNpcOwnershipKind ownership=RebirthNpcOwnershipKind.None;RebirthNpcOrderState order=RebirthNpcOrderState.None;
                    if(record.Ownership!=null&&(!Enum.TryParse(record.Ownership.OwnershipState,false,out ownership)||!Enum.IsDefined(typeof(RebirthNpcOwnershipKind),ownership)))return false;
                    if(record.Order!=null&&(!Enum.TryParse(record.Order.OrderState,false,out order)||!Enum.IsDefined(typeof(RebirthNpcOrderState),order)))return false;
                    var next=new RebirthNpcRuntimeState(source.StableId,record.Profile.ProfileId){Presence=presence,OwnershipKind=ownership,
                        OwnerId=record.Ownership?.OwnerPlatformIdOrPersistentPlayerId??string.Empty,Order=order,Travel=RebirthNpcTravelState.None,
                        HasGuardPosition=record.Transform.AnchorPosition.HasValue,GuardPosition=record.Transform.AnchorPosition??Vector3.zero,
                        Revision=record.Profile.LastSuccessfulResolutionRevision};
                    if(record.HumanAppearance.HasValue){next.HasHumanAppearance=true;next.HumanAppearance=record.HumanAppearance.Value;}
                    next.MarkPersisted();
                    if(!original.MatchesSavedRuntime(next)||source.HasDogOwner&&source.NativeDogOwner!=next.OwnerId||!source.Current())return false;
                    state=next;return true;
                }
            }
        }
        catch{return false;}
    }
}

public static partial class RebirthNpcRuntimeRegistry
{
    internal static bool StageIncomingNativeRuntime(RebirthNpcRuntimeNativeAdoption.NativeReentry source,RebirthNpcRuntimeNativeAdoption.Outgoing original)
    {
        if(source?.Current()!=true||!RebirthNpcRuntimeNativeAdoption.IsIncomingHeld(source.Actor)||original?.IsDetached!=true||
            !source.Matches(original)||!RebirthNpcSaveQuarantineBundle.CanStageServerRuntimeNativeReentry(source,original)||
            !RebirthNpcAggregatePersistenceStore.TryReadNativeAdoptionRuntime(source,original,out var saved))return false;
        lock(Sync)
        {
            if(registryWorld!=null&&!ReferenceEquals(registryWorld,source.World)||
                registryDirectory!=null&&!RebirthNpcSaveQuarantineScope.SameDirectory(registryDirectory,source.Directory)||
                !source.Current()||!source.Matches(original))return false;
            if(EntityByStableId.TryGetValue(source.StableId,out int other)&&other!=source.Actor.entityId)return false;
            if(ByEntityId.TryGetValue(source.Actor.entityId,out var prior)&&!ReferenceEquals(source.Actor.RebirthRuntimeState,prior))return false;
            if(prior!=null&&original.MatchesSavedRuntime(prior))return source.Actor.InstallStagedNativeRuntime(source,prior);
            // Only the actual incoming actor's unactivated temporary registration
            // can be replaced. Other identities or an independently active state
            // cannot be overwritten by this staging operation.
            if(!source.Actor.CanStageNativeRuntime(source,prior))return false;
            if(prior!=null)EntityByStableId.Remove(prior.StableId);
            ByEntityId[source.Actor.entityId]=saved;EntityByStableId[source.StableId]=source.Actor.entityId;
            registryWorld=source.World;registryDirectory=source.Directory;
            return source.Actor.InstallStagedNativeRuntime(source,saved)&&original.MatchesSavedRuntime(source.Actor.RebirthRuntimeState);
        }
    }
}
