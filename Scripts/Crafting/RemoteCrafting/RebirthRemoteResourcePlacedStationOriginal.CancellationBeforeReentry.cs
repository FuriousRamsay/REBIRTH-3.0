using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using HarmonyLib;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    // Native owner2 acknowledges the immutable cancellation permission in
    // BOTH modes. StationInput keeps the complete owner image unchanged.
    private sealed class CancellationBeforeContinuation
    {
        internal readonly CancellationServer Original;internal readonly string Before,Permission;internal readonly RebirthRemoteResourcePlacedStationScope.PaymentSource Payment;
        internal readonly CancellationNativeEvidence Saved;internal volatile bool Externalized;internal long ReadFloor;
        internal CancellationBeforeContinuation(CancellationServer original,CancellationNativeEvidence saved,RebirthRemoteResourcePlacedStationScope.PaymentSource payment)
        {Original=original;Before=saved.Canonical;Permission=original.Permission;Saved=saved;Payment=payment;ReadFloor=original.BeforeReentryReadFloor;}
        internal bool FinancialCurrent()
        {
            var original=Original;
            if(Externalized||original.CustodyEscaped||!CancellationHostCustodyCurrent(original)||!RebirthRemoteResourcePlacedStationGuards.IsInstalled||original.Memory?.Current!=true||original.Origin?.Current!=true||
                !ServerScope(original.Actor,original.Original)||original.Permission!=Permission||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(original.Actor.entityId),original.Peer)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(original.Actor,out var owner)||!RebirthCamperEnergySavedOwner.TryRead(owner,out var image,out var native)||
                native.id!=original.Actor.entityId||native.deaths!=original.Original.Deaths||!original.AfterActor.Matches(image)||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(native,CancellationReceipt(original.Operation),out float phase,out bool present)||!present||phase!=2f)return false;
            foreach(string purpose in new[]{"Before","Permission"})
            {
                var record=CancellationJournal(original.Operation,purpose,MaximumResultBytes,original.Memory,ReadFloor).Read(original.Operation);
                byte[] bytes=Encoding.UTF8.GetBytes(purpose=="Before"?Before:Permission);
                if(record==null||record.State!=(purpose=="Before"?RebirthCamperTransferJournal.Phase.Applied:RebirthCamperTransferJournal.Phase.Uncertain)||
                    record.Vehicle!=original.Source||record.OwnerCreation!=original.Original.Creation||record.Revision!=0||!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
            }
            return ReadCancellationAccepted(original,original.Accepted)&&(Payment!=RebirthRemoteResourcePlacedStationScope.PaymentSource.StationInput||ValidateCancellationInputReentry(original,Permission));
        }
        internal bool BeforeCurrent(CancellationLoaded loaded)
            =>BeforeCurrentCore(loaded,null);
        internal bool BeforeCurrentAfterWrite(CancellationLoaded loaded,byte[] command)
            =>command!=null&&BeforeCurrentCore(loaded,command);
        private bool BeforeCurrentCore(CancellationLoaded loaded,byte[] command)
        {
            var original=Original;
            if(!FinancialCurrent()||!original.QueueAttempted||original.QueueReentryAttempted||original.AfterSaved||original.AfterEvidence!=null||
                loaded==null||!loaded.Installed()||loaded.SaveDrain?.Current!=true||!ReferenceEquals(loaded.Original.Before,this)||!loaded.Original.OldRetired||
                CancellationFilesExist(original.Operation,"After")||CancellationFilesExist(original.Operation,"Terminal"))return false;
            if(command==null){if(CancellationFilesExist(original.Operation,"QueueReentry"))return false;}
            else
            {
                var reentry=CancellationJournal(original.Operation,"QueueReentry",MaximumResultBytes,original.Memory,ReadFloor).Read(original.Operation);
                if(reentry?.State!=RebirthCamperTransferJournal.Phase.Uncertain||reentry.Vehicle!=original.Source||reentry.OwnerCreation!=original.Original.Creation||reentry.Revision!=0||
                    !reentry.Before.SequenceEqual(command)||!reentry.After.SequenceEqual(command))return false;
            }
            var mutation=CancellationJournal(original.Operation,"Mutation",MaximumApplicationCommand).Read(original.Operation);byte[] bytes=CancellationMutation(original);
            return mutation?.State==RebirthCamperTransferJournal.Phase.Uncertain&&mutation.Vehicle==original.Source&&mutation.OwnerCreation==original.Original.Creation&&mutation.Revision==0&&
                mutation.Before.SequenceEqual(bytes)&&mutation.After.SequenceEqual(bytes)&&CaptureUploadNative(loaded.Tile,out var current)&&current.SequenceEqual(Saved.Tile);
        }
    }
    private static bool ArmCancellationBeforeAttempt(CancellationServer original)
    {
        if(original.LoadOriginal!=null)
        {
            if(original.LoadOriginal.Before!=null)return true;
            if(!original.LoadOriginal.Receipt1Before||original.QueueAttempted||!original.Current()||!SavedCancellationOwner(original,2f,true,out _)||
                !ReadCancellationSnapshot(original,false,out var prior)||!original.FullTile(original.BeforeTile)||
                !ValidateInput(original.Original,out _,out _,out _,out _,out _,out _,out _,out _,out var priorPayment,out _,out _))return false;
            if(original.BeforeReentryReadFloor==0)original.BeforeReentryReadFloor=original.Memory.ReservedBytes;
            var continuation=new CancellationBeforeContinuation(original,prior,priorPayment);
            if(!continuation.FinancialCurrent())return false;original.LoadOriginal.Before=continuation;return true;
        }
        if(!original.Current()||original.QueueAttempted||!original.BeforeSaved||!original.RefundQualified||!original.NativeLoadReserved||
            !ValidateInput(original.Original,out _,out _,out _,out _,out _,out _,out _,out _,out var payment,out _,out _)||
            payment!=RebirthRemoteResourcePlacedStationScope.PaymentSource.OwnerInventory&&payment!=RebirthRemoteResourcePlacedStationScope.PaymentSource.StationInput||
            !original.FullTile(original.BeforeTile)||!SavedCancellationOwner(original,2f,true,out _)||!ReadCancellationSnapshot(original,false,out var saved)||
            original.Actor.world.ChunkCache?.ChunkProvider is not ChunkProviderGenerateWorld provider||provider.m_RegionFileManager is not RegionFileManager manager||
            manager.snapshotUtil is not ChunkSnapshotUtil util||!ReferenceEquals(util.regionFileAccess,manager.regionFileAccess)||!ReferenceEquals(util.chunkReader.regionFileAccess,manager.regionFileAccess)||
            checked(4L*(saved.Snapshot.Length+saved.Tile.Length)+65536)>original.NativeLoadWorkspace)return false;
        if(original.BeforeReentryReadFloor==0)original.BeforeReentryReadFloor=original.Memory.ReservedBytes;
        var before=new CancellationBeforeContinuation(original,saved,payment);
        if(!before.FinancialCurrent())return false;
        var token=new CancellationLoadOriginal{Original=original,Before=before,World=original.Actor.world,WorldId=original.Actor.world.worldState.Guid,OldChunk=original.Chunk,OldTile=original.Station,
            Intent=original.Original,Memory=original.Memory,Operation=original.Operation,Source=original.Source,Binding=original.Binding,Birth=original.Birth,Recovery=Guid.NewGuid(),
            Provider=provider,Manager=manager,Util=util,Access=manager.regionFileAccess,Directory=System.IO.Path.GetFullPath(manager.saveDirectory),Header=original.Header,Permission=original.Permission,
            Evidence=saved.Canonical,Snapshot=saved.Snapshot,Tile=saved.Tile,Offset=saved.Offset,X=original.Chunk.X,Z=original.Chunk.Z,Retirement=new CancellationLoadRetirement()};
        if(!original.SnapshotBudget.AttachLoadRetirement(token.Retirement))return false;
        lock(cancellationLoadGate)cancellationLoads.Add(token);original.LoadOriginal=token;return true;
    }
    private static bool ApplyCancellationReconstructedQueue(CancellationServer original)
    {
        var loaded=original.Loaded;var proof=original.LoadOriginal?.Before;
        if(loaded?.SaveDrain==null||!loaded.SaveDrain.TryAcquire(out var drain))return false;
        using(drain)
        {
        if(proof==null||!proof.BeforeCurrent(loaded)||!original.Current()||!original.FullTile(original.BeforeTile)||
            !SavedCancellationOwner(original,2f,true,out var owner)||!RebirthRemoteResourcePlacedStationNativeReader.TryRead(original.AfterTile,out var after)||
            owner.buffData==null||owner.buffData.Length>1024*1024||proof.Payment==RebirthRemoteResourcePlacedStationScope.PaymentSource.OwnerInventory&&!SameGrid(original.Station.Input,after.Input)||
            !CancellationQueuePlan(original.BeforeQueue,original.Slot,original.Actor.entityId,out _,out var mapping)||
            !RebirthRemoteResourcePlacedStationQueue.TryDecode(RebirthCookingRemoteAdmission.ReadProtocolXml(original.Permission,MaximumResultBytes).Element("afterQueue").Value,out var afterQueue))return false;
        var command=new XElement("placedCancellationQueueReentry",new XAttribute("version",2),new XAttribute("payment",(byte)proof.Payment),new XAttribute("cancel",original.Operation.ToString("N")),new XAttribute("source",original.Source.ToString("N")),
            new XElement("permission",original.Permission),new XElement("nativeBeforeLoad",loaded.Encode()),new XElement("ownerAfter",RebirthRemoteResourceClientOriginal.ActorImage(original.AfterActor)),
            new XElement("ownerBuffs",Convert.ToBase64String(owner.buffData.ToArray())));
        if(!FitsReply(command))return false;
        string text=command.ToString(SaveOptions.DisableFormatting);byte[] bytes=Encoding.UTF8.GetBytes(text);
        if(!SaveCancellation(original.Operation,original.Source,original.Original.Creation,"QueueReentry",bytes,false)||!proof.BeforeCurrentAfterWrite(loaded,bytes)||!drain.Current)return false;
        // Sticky separate write-ahead attempt. The first financial/native
        // attempt is never cleared and no existing uncertain attempt replays.
        original.QueueReentryAttempted=true;original.QueueReentryCommand=text;original.QueueCompleted=false;original.Watch?.Dispose();original.Watch=null;
        var station=original.Station;var queue=new RecipeQueueItem[station.Queue.Length];
        if(proof.Payment==RebirthRemoteResourcePlacedStationScope.PaymentSource.StationInput&&!SameGrid(station.Input,after.Input))
        {
            var input=ItemStack.Clone(after.Input);
            if(!drain.Current||!CancellationNativeSet(original,"set_Input",input,()=>station.Input=input))return false;
        }
        for(int slot=0;slot<queue.Length;slot++)queue[slot]=mapping[slot]<0?afterQueue[slot]:station.Queue[mapping[slot]];
        if(!drain.Current||RebirthCookingRemoteAdmissionBackend.Queue(queue)!=RebirthCookingRemoteAdmissionBackend.Queue(afterQueue)||
            !CancellationNativeSet(original,"set_Queue",queue,()=>station.Queue=queue)||!drain.Current||!CancellationNativeSet(original,nameof(TileEntityWorkstation.ResetTickTime),null,station.ResetTickTime)||!original.FullTile(original.AfterTile))return false;
        string key=CancellationMarkerKey(original);if(original.Chunk.ChunkCustomData.dict.ContainsKey(key))return false;
        original.Chunk.ChunkCustomData.Set(key,new ChunkCustomData(key,ulong.MaxValue,false){data=(byte[])original.Marker.Clone()});original.Chunk.isModified=true;
        original.QueueReentryCompleted=drain.Current&&CancellationMarkerCurrent(original)&&original.FullTile(original.AfterTile);original.QueueCompleted=original.QueueReentryCompleted;return original.QueueCompleted;
        }
    }
    private sealed class CancellationQueueRecoveryPublication
    {
        private readonly CancellationServer original;private TileEntityWorkstation tile;private Chunk chunk;private readonly Guid recovery;private readonly long generation;
        private readonly string evidence;private readonly long offset;private readonly int length;private readonly CancellationNativeEvidence saved;private CancellationLoaded beforeLoad,currentAfter;private CancellationLoadOriginal afterLoad;
        internal CancellationQueueRecoveryPublication(CancellationServer value,CancellationLoaded before,CancellationNativeEvidence after)
        {original=value;tile=value.Station;chunk=value.Chunk;beforeLoad=before;recovery=before.Original.Recovery;generation=before.Generation;evidence=after.Canonical;offset=after.Offset;length=after.Tile.Length;saved=after;}
        internal void BindAfter(CancellationLoadOriginal token)
        {
            if(token.Before!=null||!ReferenceEquals(token.Original,original)||!ReferenceEquals(token.OldTile,tile)||!ReferenceEquals(token.OldChunk,chunk)||token.Evidence!=evidence)return;
            afterLoad=token;beforeLoad=null;
        }
        internal bool AdoptAfter(CancellationLoaded loaded)
        {
            if(afterLoad==null||!ReferenceEquals(loaded.Original,afterLoad)||!loaded.Installed()||afterLoad.Evidence!=evidence||afterLoad.Offset!=offset||afterLoad.Tile.Length!=length)return false;
            // Only the same private actual full After read chain may move the
            // sealed publication's current handles after another native load.
            tile=loaded.Tile;chunk=loaded.Chunk;currentAfter=loaded;return true;
        }
        internal bool Current=>original.Current()&&original.QueueReentryAttempted&&original.QueueReentryCompleted&&original.AfterSaved&&original.AfterEvidence==evidence&&
            ReferenceEquals(original.Station,tile)&&ReferenceEquals(original.Chunk,chunk)&&original.FullTile(original.AfterTile)&&CancellationMarkerCurrent(original)&&CurrentCancellationQueueAfterPayload(original,saved)&&
            (afterLoad!=null?ReferenceEquals(original.LoadOriginal,afterLoad)&&afterLoad.Active&&(currentAfter!=null?currentAfter.Installed():!afterLoad.OldRetired&&afterLoad.NativeEpoch==0):
                // The native read plus positively saved After were sealed
                // before retiring the old load buffers. Strong current TE/
                // chunk handles and native epoch remain consistency checks;
                // closing a loader scope is not loss of that positive event.
                beforeLoad!=null&&ReferenceEquals(beforeLoad.Tile,tile)&&ReferenceEquals(beforeLoad.Chunk,chunk)&&beforeLoad.NativeEpoch==beforeLoad.Original.NativeEpoch);
        internal string Encode()=>new XElement("placedCancellationReconstruction",new XAttribute("version",3),new XAttribute("cause","queue-reentry-saved-after"),
            new XAttribute("cancel",original.Operation.ToString("N")),new XAttribute("source",original.Source.ToString("N")),new XAttribute("binding",original.Binding),new XAttribute("birth",original.Birth),
            new XAttribute("generation",generation),new XAttribute("recovery",recovery.ToString("N")),new XAttribute("world",original.Original.World),new XAttribute("chunkX",chunk.X),new XAttribute("chunkZ",chunk.Z),
            new XAttribute("tileOffset",offset),new XAttribute("tileLength",length)).ToString(SaveOptions.DisableFormatting);
    }
    private static bool CompleteCancellationQueueRecovery(CancellationServer original)
    {
        if(original.QueueRecoveryPublication!=null)return original.QueueRecoveryPublication.Current;
        var loaded=original.Loaded;var proof=original.LoadOriginal?.Before;
        if(!original.QueueReentryAttempted||!original.QueueReentryCompleted||original.QueueReentryCommand==null||proof==null||!proof.FinancialCurrent()||
            loaded==null||!loaded.Installed()||!ReferenceEquals(loaded.Original.Before,proof)||!loaded.Original.OldRetired||!original.Current()||
            !original.FullTile(original.AfterTile)||!CancellationMarkerCurrent(original)||!SavedCancellationOwner(original,2f,true,out _)||!ReadCancellationSnapshot(original,true,out var after))return false;
        byte[] bytes=Encoding.UTF8.GetBytes(original.QueueReentryCommand);var record=CancellationJournal(original.Operation,"QueueReentry",MaximumResultBytes).Read(original.Operation);
        if(record==null||record.State!=RebirthCamperTransferJournal.Phase.Uncertain&&record.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=original.Source||record.OwnerCreation!=original.Original.Creation||record.Revision!=0||
            !record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes)||!SaveCancellation(original.Operation,original.Source,original.Original.Creation,"QueueReentry",bytes,true))return false;
        var publication=new CancellationQueueRecoveryPublication(original,loaded,after);if(!publication.Current)return false;original.QueueRecoveryPublication=publication;return true;
    }
    // Native loss/externalization is a causal invalidation, not an inferred
    // inventory mismatch. Clean native Chunk.Reset is observed separately.
    [HarmonyPatch]
    private static class CancellationBeforeExternalization
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(TileEntity),nameof(TileEntity.OnRemove));
            yield return AccessTools.Method(typeof(TileEntity),nameof(TileEntity.OnDestroy));
            foreach(string name in new[]{nameof(TileEntityWorkstation.ReplacedBy),nameof(TileEntityWorkstation.CheckForCraftComplete),nameof(TileEntityWorkstation.AddCraftComplete)})yield return AccessTools.Method(typeof(TileEntityWorkstation),name);
        }
        [HarmonyPriority(Priority.Last)]private static bool Prefix(TileEntity __instance,MethodBase __originalMethod,bool __runOriginal,out object __state)
        {
            __state=null;if(!__runOriginal||__instance is not TileEntityWorkstation station)return true;
            if(!EnterCancellationHostEscape(station,__originalMethod,out __state))return false;
            lock(cancellationLoadGate)
            {
                var loading=cancellationLoading;
                if(loading?.Original.Before!=null&&ReferenceEquals(loading.Tile,station))loading.Original.Before.Externalized=true;
                foreach(var token in cancellationLoads)
                    if(token.Active&&token.Before!=null&&(ReferenceEquals(token.OldTile,station)||ReferenceEquals(token.Loaded?.Tile,station)))token.Before.Externalized=true;
            }
            if(ThreadManager.IsMainThread())foreach(var original in cancellationServers.Values)
                if(original.Accepted!=null&&(ReferenceEquals(original.Station,station)||ReferenceEquals(original.LoadOriginal?.Loaded?.Tile,station)))original.CustodyEscaped=true;
            return true;
        }
        private static void Postfix(bool __runOriginal,object __state)=>FinishCancellationHostEscape(__state,__runOriginal);
        private static Exception Finalizer(Exception __exception,object __state){FinishCancellationHostEscape(__state,false);return __exception;}
    }
}
