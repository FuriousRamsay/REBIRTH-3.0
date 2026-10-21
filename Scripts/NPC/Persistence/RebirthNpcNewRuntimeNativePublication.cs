using System;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEngine;

public static partial class RebirthNpcStableIdentityStore
{
    internal static bool QualifyNewNativeAdoptionIdentity(RebirthNpcRuntimeNativeAdoption.NativeReentry source)
    {
        try
        {
            if(source?.Current()!=true)return false;
            var scope=RebirthNpcSaveScope.ObserveCurrent();if(scope==null||!RebirthNpcSaveQuarantineScope.SameDirectory(scope.SaveDirectory,source.Directory))return false;
            string path=Path.Combine(source.Directory,FileName);
            if(File.Exists(path+".tmp")||RebirthNpcPersistenceFile.IsRecoveryFenced(path))return false;
            using(var pin=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                var primary=RebirthNpcRecoveryCurrentBacking.Read(path);
                if(!ValidateDocument(primary)||primary.DocumentElement.GetAttribute("saveScope")!=scope.Fingerprint)return false;
                var rows=primary.DocumentElement.SelectNodes("npc").Cast<XmlElement>()
                    .Where(row=>row.GetAttribute("stableId")==source.StableId.ToString()).ToArray();
                return rows.Length==1&&int.TryParse(rows[0].GetAttribute("class"),out int nativeClass)&&nativeClass==source.Actor.entityClass&&
                    rows[0].GetAttribute("profile")==source.Actor.RebirthProfileId&&source.Current();
            }
        }
        catch{return false;}
    }
}

public static partial class RebirthNpcAggregatePersistenceStore
{
    internal static bool TryReadNewNativeAdoptionRuntime(RebirthNpcRuntimeNativeAdoption.NativeReentry source,out RebirthNpcRuntimeState state)
    {
        state=null;
        try
        {
            if(source?.Current()!=true||!RebirthNpcStableIdentityStore.QualifyNewNativeAdoptionIdentity(source))return false;
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
                    if(record.Profile.ProfileId!=source.Actor.RebirthProfileId||record.Lifecycle.TombstoneState||
                        !Enum.TryParse(record.Presence.PresenceState,false,out RebirthNpcPresenceState presence)||
                        !Enum.IsDefined(typeof(RebirthNpcPresenceState),presence)||presence==RebirthNpcPresenceState.Removed)return false;
                    RebirthNpcOwnershipKind ownership=RebirthNpcOwnershipKind.None;RebirthNpcOrderState order=RebirthNpcOrderState.None;
                    if(record.Ownership!=null&&(!Enum.TryParse(record.Ownership.OwnershipState,false,out ownership)||!Enum.IsDefined(typeof(RebirthNpcOwnershipKind),ownership)))return false;
                    if(record.Order!=null&&(!Enum.TryParse(record.Order.OrderState,false,out order)||!Enum.IsDefined(typeof(RebirthNpcOrderState),order)))return false;
                    if(record.Vitals==null||record.Vitals.CurrentHealth!=source.Actor.Health||
                        !string.Equals(record.Vitals.DeathOrIncapacitationState,"alive",StringComparison.OrdinalIgnoreCase))return false;
                    if(record.NativeReconstruction!=null)
                    {
                        var native=record.NativeReconstruction.Write().Element("actor");
                        if(native==null||!source.MatchesSavedBodyFrame(Convert.FromBase64String(native.Value)))return false;
                    }
                    var next=new RebirthNpcRuntimeState(source.StableId,record.Profile.ProfileId){Presence=presence,OwnershipKind=ownership,
                        OwnerId=record.Ownership?.OwnerPlatformIdOrPersistentPlayerId??string.Empty,Order=order,Travel=RebirthNpcTravelState.None,
                        HasGuardPosition=record.Transform.AnchorPosition.HasValue,GuardPosition=record.Transform.AnchorPosition??Vector3.zero,
                        Revision=record.Profile.LastSuccessfulResolutionRevision};
                    if(record.Presence.PresenceRevision>next.Revision||(record.Ownership?.OwnerRevision??0)>next.Revision||(record.Order?.OrderRevision??0)>next.Revision)return false;
                    if(record.HumanAppearance.HasValue){next.HasHumanAppearance=true;next.HumanAppearance=record.HumanAppearance.Value;}
                    next.MarkPersisted();
                    if(source.HasDogOwner&&source.NativeDogOwner!=next.OwnerId||!source.Current())return false;
                    state=next;return true;
                }
            }
        }
        catch{return false;}
    }
}

public static partial class RebirthNpcRuntimeRegistry
{
    internal static bool StageNewIncomingNativeRuntime(RebirthNpcRuntimeNativeAdoption.NativeReentry source)
    {
        if(source?.Current()!=true||!RebirthNpcRuntimeNativeAdoption.IsIncomingHeld(source.Actor)||
            !RebirthNpcSaveQuarantineBundle.CanStageNewServerRuntimeNativeReentry(source)||
            !RebirthNpcAggregatePersistenceStore.TryReadNewNativeAdoptionRuntime(source,out var saved))return false;
        lock(Sync)
        {
            if(registryWorld!=null&&!ReferenceEquals(registryWorld,source.World)||
                registryDirectory!=null&&!RebirthNpcSaveQuarantineScope.SameDirectory(registryDirectory,source.Directory)||!source.Current()||
                !RebirthNpcSaveQuarantineBundle.CanStageNewServerRuntimeNativeReentry(source))return false;
            if(EntityByStableId.TryGetValue(source.StableId,out int other)&&other!=source.Actor.entityId)return false;
            if(ByEntityId.TryGetValue(source.Actor.entityId,out var prior)&&!ReferenceEquals(source.Actor.RebirthRuntimeState,prior))return false;
            if(!source.Actor.CanStageNativeRuntime(source,prior))return false;
            if(prior!=null)EntityByStableId.Remove(prior.StableId);
            ByEntityId[source.Actor.entityId]=saved;EntityByStableId[source.StableId]=source.Actor.entityId;
            registryWorld=source.World;registryDirectory=source.Directory;
            return source.Actor.InstallStagedNativeRuntime(source,saved);
        }
    }
}
