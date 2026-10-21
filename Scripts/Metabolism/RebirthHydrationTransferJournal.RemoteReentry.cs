using System;

internal static partial class RebirthHydrationTransferJournal
{
    // Historical terminal completion. A positively saved native terminal-seen
    // marker and the original financial Applied WAL permit metadata ACK only.
    // Current inventory may legitimately differ after the native owner release.
    internal static bool TryPersistColdAcknowledgment(EntityPlayer player,Guid operation,string digest)
    {
        try
        {
            if(TryReadAcknowledged(player,operation,digest,out _))return true;
            if(!TryReadPending(player,out var original,out _)||original==null||original.Operation!=operation||original.Digest!=digest||!HasApplied(player,original))return false;
            var world=player.world;var game=GameManager.Instance;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            var peer=manager?.Clients?.ForEntityId(player.entityId);int deaths=player.Died;
            var transport=peer?.netConnection!=null&&peer.netConnection.Length>0?peer.netConnection[0]:null;
            bool Current()=>ThreadManager.IsMainThread()&&peer!=null&&peer.loginDone&&!peer.disconnecting&&
                transport!=null&&peer.netConnection!=null&&peer.netConnection.Length>0&&ReferenceEquals(peer.netConnection[0],transport)&&!peer.netConnection[0].IsDisconnected()&&
                ReferenceEquals(GameManager.Instance,game)&&ReferenceEquals(game.World,world)&&ReferenceEquals(player.world,world)&&
                ReferenceEquals(world.GetEntity(player.entityId),player)&&player.Died==deaths&&!player.IsDead()&&player.IsSpawned()&&
                ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,manager)&&manager.IsServer&&
                ReferenceEquals(manager.Clients?.ForEntityId(player.entityId),peer)&&RebirthHydrationTransferJournal.Current(player,original)&&HasApplied(player,original);
            bool NativeSeen()
            {
                if(!Current()||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)||owner.StorageKey!=original.Owner||
                    !RebirthGearNativePlayerFile.TryRead(owner,out var file)||file.deaths!=original.Deaths||file.buffData==null||file.buffData.Length<1||file.buffData.Length>1024*1024)return false;
                var bytes=file.buffData.ToArray();
                return RebirthGearReceiptReader.TryReadHydrationSeenStage(bytes,operation,digest,out var seen)&&seen==1f&&
                    RebirthGearReceiptReader.TryReadHydrationStage(bytes,operation,digest,out var hydration)&&hydration==1f&&
                    RebirthGearReceiptReader.Contains(bytes,operation.ToString("N"),RebirthGearOwnerReceipt.Applied)&&
                    RebirthGearReceiptReader.TryReadHydrationRefusedStage(bytes,operation,digest,out var refused)&&refused==0f&&Current();
            }
            if(!NativeSeen())return false;
            string path=AcknowledgmentPath(original.World,original.Owner,original.Creation);RequireSafe(path);
            var journal=new RebirthCamperTransferJournal(path);foreach(var ignored in journal.ReadForIndex()){}
            var image=Bytes(original);var record=journal.Prepare(operation,Group(original.World,original.Owner,original.Creation),original.Creation,original.Revision,image,image);
            if(!Matches(record,original)||!NativeSeen())return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
            if(!NativeSeen())return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            return record.State==RebirthCamperTransferJournal.Phase.Applied&&Matches(record,original)&&NativeSeen()&&TryReadAcknowledged(player,operation,digest,out _);
        }
        catch{return false;}
    }
}
