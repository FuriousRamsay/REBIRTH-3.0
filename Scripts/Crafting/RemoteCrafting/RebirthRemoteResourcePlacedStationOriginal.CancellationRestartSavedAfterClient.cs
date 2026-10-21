using System;
using System.Linq;
using System.Text;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    // Positive native owner2 hydration only. This type can establish a metadata
    // hold/version; it has no owner-slot or station setter permit conversion.
    internal sealed class CancellationOwnerSavedAfterHydrated
    {
        internal readonly CancellationClient Original;
        private readonly CancellationJoining joining;private readonly EntityPlayerLocal actor;private readonly World world;
        private readonly ItemStackGrid bag,belt;private readonly string permission,owner,creation;private readonly int entity,deaths;
        private bool accepted;
        private CancellationOwnerSavedAfterHydrated(CancellationClient original,CancellationJoining source)
        {
            Original=original;joining=source;actor=source.Actor;world=source.World;bag=actor.bag.ItemGrid;belt=actor.inventory.ItemGrid;
            permission=original.Permission;owner=original.Original.Owner;creation=original.Original.Creation;entity=actor.entityId;deaths=original.Original.Deaths;
        }
        internal static CancellationOwnerSavedAfterHydrated FromActualHydration(CancellationClient original,CancellationJoining source)
        {
            if(!ReferenceEquals(cancellationJoining,source)||!CancellationJoinConnectionCurrent(source)||source.Reads!=1||original.Stage!=2||
                !ReferenceEquals(source.Actor,original.Actor)||source.File?.bLoaded!=true||source.File.bDead||source.File.deaths!=original.Original.Deaths||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(source.File,CancellationReceipt(original.Operation),out float phase,out bool present)||!present||phase!=2f||
                original.Actor.Buffs.GetCustomVar(CancellationReceipt(original.Operation))!=2f||!PermissionCurrent(original))return null;
            var value=new CancellationOwnerSavedAfterHydrated(original,source);return value.Current?value:null;
        }
        private static bool PermissionCurrent(CancellationClient original)
        {
            try
            {
            if(!original.PermissionSaved||original.Permission==null||original.After==null)return false;
            var record=CancellationJournal(original.Operation,"Permission").Read(original.Operation);byte[] full=Encoding.UTF8.GetBytes(original.Permission);
            return record?.State==RebirthCamperTransferJournal.Phase.Uncertain&&record.Vehicle==original.Source&&record.OwnerCreation==original.Original.Creation&&record.Revision==0&&
                record.Before.SequenceEqual(full)&&record.After.SequenceEqual(full)&&original.After.Matches(RebirthCamperEnergyActorImage.Capture(original.Actor));
            }
            catch{return false;}
        }
        internal void NativeJoinCompleted(CancellationJoining source)
        {if(ReferenceEquals(joining,source)&&ReferenceEquals(cancellationJoining,source)&&CancellationJoinConnectionCurrent(source)&&source.Reads==1&&ReferenceEquals(source.Actor,actor))accepted=true;}
        internal bool Accepted=>accepted&&Current;
        internal bool Current=>ThreadManager.IsMainThread()&&(accepted||ReferenceEquals(cancellationJoining,joining))&&CancellationJoinConnectionCurrent(joining)&&
            Original.Memory?.Current==true&&ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(actor.world,world)&&ReferenceEquals(Original.Actor,actor)&&
            actor.entityId==entity&&!actor.IsDead()&&actor.Died==deaths&&ReferenceEquals(actor.bag?.ItemGrid,bag)&&ReferenceEquals(actor.inventory?.ItemGrid,belt)&&
            RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&identity.CanonicalId==owner&&RebirthCamperCharacterAccess.CurrentCreation(actor)==creation&&
            Original.Permission==permission&&(actor.Buffs.GetCustomVar(CancellationReceipt(Original.Operation))==2f||actor.Buffs.GetCustomVar(CancellationReceipt(Original.Operation))==3f)&&
            PermissionCurrent(Original);
    }
    private static void HydrateRestartSavedAfter(CancellationClient original,CancellationJoining joining)
    {
        var proof=CancellationOwnerSavedAfterHydrated.FromActualHydration(original,joining);if(proof==null||cancellationClients.ContainsKey(original.Operation))return;
        original.RestartSavedAfterOwner=proof;cancellationClients.Add(original.Operation,original);
        if(!RebirthRemoteResourceOwnerAction.HoldRestartSavedAfterCancellation(proof))return;
        original.OwnerSavedAfterVersion=RebirthRemoteResourceOwnerAction.PlacedCancellationSavedAfterVersion.CaptureRestart(proof);
    }
}
