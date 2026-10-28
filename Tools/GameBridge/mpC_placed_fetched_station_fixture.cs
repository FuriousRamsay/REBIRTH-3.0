using System;
using System.Linq;
using System.Collections.Generic;

// AUTHORED, NOT RUN. Actual production helper calls; caller supplies an
// installed-version native workstation and positively observed native span.
// These are not a transport/payment/publication test substitute.
internal static class MpCPlacedFetchedStationFixture
{
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    private static T NativeField<T>(object value,string name)
    {
        Require(value!=null,"actual retained production object: "+name);
        var field=value.GetType().GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
        Require(field!=null,"source-linked production field: "+name);return (T)field.GetValue(value);
    }
    internal static void ColdSavedAfterHostNativeRead(Guid actualOperation)
    {
        var field=typeof(RebirthRemoteResourcePlacedStationOriginal).GetField("restartCancellations",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        var retained=(System.Collections.IDictionary)field.GetValue(null);var archive=retained[actualOperation];
        Require(archive!=null&&NativeField<bool>(archive,"SavedAfterRestart"),"real startup discovered complete-effects owner2 original, not a fabricated fixture record");
        var token=NativeField<object>(archive,"Load");var loaded=NativeField<object>(token,"Loaded");
        Require(ReferenceEquals(NativeField<object>(loaded,"Original"),token)&&NativeField<long>(loaded,"Generation")>0,"actual private factory/read/full LoadChunk issued this generation");
        var chunk=NativeField<Chunk>(loaded,"Chunk");var station=NativeField<TileEntityWorkstation>(loaded,"Tile");
        Require(ReferenceEquals(chunk.GetTileEntity(station.localChunkPos),station)&&ReferenceEquals(station.GetChunk(),chunk)&&
            ReferenceEquals(GameManager.Instance.World.GetTileEntity(station.ToWorldPos()),station),"actual native result was installed; equal later lookup cannot create this source");
        byte[] snapshot=NativeField<byte[]>(token,"Snapshot"),tile=NativeField<byte[]>(token,"Tile");long offset=NativeField<long>(token,"Offset");
        Require(offset>=8&&offset+tile.Length<=snapshot.Length&&snapshot.Skip(checked((int)offset)).Take(tile.Length).SequenceEqual(tile),"entire saved native span, not count or hash, matches exact original snapshot");
        Require(RebirthRemoteResourcePlacedStationCandidate.TrySerialize(station,station.Input,station.Queue,station.IsBurning,out var native,out int clock),"actual installed native body readable");
        Buffer.BlockCopy(BitConverter.GetBytes(station.lastTickTime),0,native,clock,8);
        Require(native.SequenceEqual(tile),"complete actual native After matches the reconstructed full saved After");
        foreach(string purpose in new[]{"Accepted","Before","Permission","Mutation","After","Terminal"})
        {
            var journal=new RebirthRemoteResourceClientOriginalJournal(System.IO.Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativePlacedCancellation"+purpose));
            var record=journal.Read(actualOperation);
            Require(record!=null&&record.Vehicle==NativeField<Guid>(archive,"Source")&&record.Revision==0&&record.Before.SequenceEqual(record.After),"full same-original "+purpose+" record");
            Require(record.State==(purpose=="Permission"?RebirthCamperTransferJournal.Phase.Uncertain:RebirthCamperTransferJournal.Phase.Applied),"no attempted/Prepared "+purpose+" can pass as completed effects");
        }
    }
    internal sealed class ColdSavedAfterCheckpoint
    {internal EntityPlayerLocal Actor;internal ItemStackGrid Bag,Belt;internal RebirthCamperEnergyActorImage FullAfter;internal Guid Operation,Source;internal string Creation;}
    internal static ColdSavedAfterCheckpoint ColdSavedAfterClientReadOnly(RebirthRemoteResourcePlacedStationOriginal.CancellationClient actual)
    {
        Require(actual?.RestartSavedAfterOwner?.Accepted==true&&actual.OwnerSavedAfterVersion?.IsCurrent==true&&actual.Stage==2,"actual authenticated native owner2 hydration issued metadata-only version");
        Require(actual.Station==null&&actual.Window==null&&actual.Card==null&&!actual.NativeLease()&&!actual.OwnerAttempted,"restart neither imports a target nor marks a refund attempt");
        var image=RebirthCamperEnergyActorImage.Capture(actual.Actor);var bag=actual.Actor.bag.ItemGrid;var belt=actual.Actor.inventory.ItemGrid;
        Require(!RebirthRemoteResourcePlacedStationOriginal.CancellationOwnerPermission.TryCreate(actual,out _),"real production owner-write permit factory refuses this metadata source");
        Require(actual.After.Matches(image)&&image.Matches(RebirthCamperEnergyActorImage.Capture(actual.Actor))&&ReferenceEquals(bag,actual.Actor.bag.ItemGrid)&&ReferenceEquals(belt,actual.Actor.inventory.ItemGrid),"readonly checks preserve every full owner slot and native grid identity");
        Require(actual.Actor.Buffs.GetCustomVar("RebirthPlacedCancelV1_"+actual.Operation.ToString("N"))==2f,"actual native hydrated receipt remains2 until full host phase4");
        return new ColdSavedAfterCheckpoint{Actor=actual.Actor,Bag=bag,Belt=belt,FullAfter=image,Operation=actual.Operation,Source=actual.Source,Creation=actual.Original.Creation};
    }
    internal static void ColdSavedAfterFinalAckNoRefundReplay(ColdSavedAfterCheckpoint before)
    {
        Require(ReferenceEquals(before.Actor.bag.ItemGrid,before.Bag)&&ReferenceEquals(before.Actor.inventory.ItemGrid,before.Belt)&&
            before.FullAfter.Matches(RebirthCamperEnergyActorImage.Capture(before.Actor)),"phase4/owner3/FinalAck changed no owner items or grid objects");
        var terminal=new RebirthRemoteResourceClientOriginalJournal(System.IO.Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativePlacedCancellationTerminal")).Read(before.Operation);
        var ack=new RebirthRemoteResourceClientOriginalJournal(System.IO.Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativePlacedCancellationFinalAck")).Read(before.Operation);
        Require(terminal?.State==RebirthCamperTransferJournal.Phase.Applied&&ack?.State==RebirthCamperTransferJournal.Phase.Applied&&
            terminal.Vehicle==before.Source&&ack.Vehicle==before.Source&&terminal.OwnerCreation==before.Creation&&ack.OwnerCreation==before.Creation&&
            terminal.Before.SequenceEqual(terminal.After)&&ack.Before.SequenceEqual(ack.After),"exact full local terminal and FinalAck precede receipt retirement");
        Require(before.Actor.Buffs.GetCustomVar("RebirthPlacedCancelV1_"+before.Operation.ToString("N"))==0f,"only metadata receipt is retired after positive complete ACK");
    }
    internal static void EqualExistingTileCannotMintNativeLoad(TileEntityWorkstation actualCurrent,PooledBinaryReader exactSavedReader)
    {
        var owner=typeof(RebirthRemoteResourcePlacedStationOriginal);var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
        Require(owner.GetField("cancellationLoading",flags).GetValue(null)==null,"run outside an actual native LoadChunk/factory callback");
        var enter=owner.GetMethod("EnterCancellationPersistencyRead",flags);
        long position=exactSavedReader.BaseStream.Position;var body=RebirthCamperInventorySnapshot.Capture(actualCurrent.Input);
        var result=enter.Invoke(null,new object[]{actualCurrent,exactSavedReader,global::StreamModeRead.Persistency,true});
        Require(result==null&&exactSavedReader.BaseStream.Position==position&&body.SequenceEqual(RebirthCamperInventorySnapshot.Capture(actualCurrent.Input)),"an equal existing tile/read image issues no native lineage and changes no input/reader");
    }
    internal static void UnreceivedPlayerIdCannotMintJoinLineage()
    {
        var unreceived=new NetPackagePlayerId();
        Require(!RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(unreceived,out _),"a constructed/directly invoked native PlayerId has no authenticated native receive-dequeue witness");
    }
    internal static void ObservedHeaderQueueOrdering()
    {
        var queue=new RebirthRemoteResourceRepairSendQueue();var body=new RebirthRemoteResourceFragments.Body(new byte[RebirthRemoteResourceFragments.ChunkBytes+1]);
        int sends=0,callbacks=0;bool observed=false;
        Require(queue.EnqueueObserved("C-before",body,()=>true,()=>true,index=>{Require(index==sends,"ordered actual send callback index");sends++;},sent=>
        {Require(queue.Count==0&&queue.RetainedBytes==0,"queue drops body and send references before callback");callbacks++;observed=sent;}),"fresh purpose-specific header queues");
        Require(!queue.EnqueueObserved("C-before",body,()=>true,()=>true,index=>{},sent=>{}),"duplicate cannot invent completion");
        queue.Pump(1);Require(sends==1&&callbacks==0,"partial header cannot trigger native sync");
        queue.Pump(1);Require(sends==2&&callbacks==1&&observed,"only all native send callbacks permit one subsequent original sync");
        Require(queue.EnqueueObserved("C-ended",body,()=>false,()=>true,index=>{throw new InvalidOperationException();},sent=>{callbacks++;Require(!sent,"obsolete connection grants no original sync");}),"obsolete observation queued");
        queue.Pump(1);Require(callbacks==2&&sends==2&&queue.Count==0,"obsolete queue retires without send or retry authority");
    }
    internal static void CancellationPreservesUnrelatedNativeRows(RecipeQueueItem[] actualCapturedQueue,int ownSlot,int originalActor)
    {
        var refs=(RecipeQueueItem[])actualCapturedQueue.Clone();string before=RebirthRemoteResourcePlacedStationQueue.Encode(actualCapturedQueue);
        Require(RebirthRemoteResourcePlacedStationOriginal.CancellationQueuePlan(actualCapturedQueue,ownSlot,originalActor,out var after,out var mapping),"actual native one-pass cancellation plan");
        Require(before==RebirthRemoteResourcePlacedStationQueue.Encode(actualCapturedQueue),"planning never mutates current queue");
        for(int slot=0;slot<after.Length;slot++)
        {
            if(mapping[slot]>=0)Require(mapping[slot]!=ownSlot&&ReferenceEquals(after[slot],refs[mapping[slot]]),"unrelated native row and complete recipe object retained");
            else Require(after[slot].Recipe==null&&after[slot].Multiplier==0&&after[slot].Quality==1&&after[slot].StartingEntityId==originalActor,"only native cleared slot defaults authored");
        }
        Require(!RebirthRemoteResourcePlacedStationOriginal.CancellationQueuePlan(actualCapturedQueue,-1,originalActor,out _,out _),"unknown cancellation slot refuses");
    }
    internal static void ActualAcceptedNativeUpload(EntityPlayer actualOwner,RebirthRemoteResourceDurableJournal.Record actualSource,
        RebirthRemoteResourceClientOriginal.Intent actualOriginal,RebirthRemoteResourcePlacedStationCustody.Claim actualClaim,
        RebirthRemoteResourcePlacedStationPreparation.Attempt retainedAttempt)
    {
        var before=actualClaim.CopyBefore();var actor=RebirthCamperEnergyActorImage.Capture(actualOwner);
        Require(RebirthRemoteResourcePlacedStationOriginal.TryObserveAcceptedAdmission(actualOwner,actualSource,actualOriginal,actualClaim,retainedAttempt.Allocation),
            "actual authenticated ProcessPackage+completed FromClient event full pre/raw/post artifact resolves same original admission; current equality alone cannot create its file");
        Require(before.SequenceEqual(actualClaim.CopyBefore())&&actualClaim.ExactBefore()&&actor.Matches(RebirthCamperEnergyActorImage.Capture(actualOwner)),"accepted event observation grants no native/owner setter");
    }
    internal static void ToServerMeterReadonly(byte[] actualInstalledNativeFrame)
    {
        var original=(byte[])actualInstalledNativeFrame.Clone();
        Require(RebirthRemoteResourcePlacedStationNativeReader.TryMeasureToServer(actualInstalledNativeFrame,out int elapsed,out long graph,out long scratch)&&elapsed>=13&&graph>0&&scratch>0,"actual installed ToServer grammar count before hydration");
        Require(actualInstalledNativeFrame.SequenceEqual(original)&&!RebirthRemoteResourcePlacedStationNativeReader.TryMeasureToServer(original.Take(original.Length-1).ToArray(),out _,out _,out _),"measurement preserves bytes and refuses truncated frame");
    }
    internal static void SharedParserScratchExclusive(RebirthRemoteResourcePlacedStationPreparation.Attempt actualRetainedAttempt)
    {
        var allocation=actualRetainedAttempt.Allocation;
        Require(RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(allocation,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.OwnerEnvelope,out var first),"actual retained admission leases scratch");
        using(first)
        {
            Require(first.Current&&!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(allocation,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.Completion,out _),"different read phase cannot overlap same admitted scratch");
        }
        Require(!first.Current,"disposed scratch is not current");
        Require(RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(allocation,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.Completion,out var second),"disposal permits sequential distinct parser phase");second.Dispose();
    }
    internal static void UnrelatedBeforeFlushPasses(XUiC_WorkstationWindowGroup actualWindow)
    {
        var station=actualWindow.WorkstationData.TileEntity;var input=RebirthCamperInventorySnapshot.Capture(station.Input);string queue=RebirthRemoteResourcePlacedStationQueue.Encode(station.Queue);
        Require(RebirthRemoteResourcePlacedStationOriginal.EnterBeforeFlush(actualWindow,true,out var state)&&state==null,"unrelated invocation has no fetched original or fabricated proof");
        RebirthRemoteResourcePlacedStationOriginal.ObserveBeforeFlush(actualWindow,true,state);
        Require(input.SequenceEqual(RebirthCamperInventorySnapshot.Capture(station.Input))&&queue==RebirthRemoteResourcePlacedStationQueue.Encode(station.Queue),"observer contracts alone never perform sync or write rows");
    }
    internal static void ClientUsedRefusesUnauthenticated(EntityPlayerLocal actualOwner,ItemActionEntryCraft actualEntry,RebirthRemoteResourceClientOriginal.Intent actualOriginal,byte[] actualAfter,RecipeQueueItem[] actualAfterQueue)
    {
        Require(RebirthRemoteResourcePlacedStationScope.TryObserveWindow(actualOwner,actualEntry,out var station,out _),"actual original native lock/window prerequisite");
        var actor=RebirthCamperEnergyActorImage.Capture(actualOwner);var rows=(RecipeQueueItem[])station.Queue.Clone();var input=RebirthCamperInventorySnapshot.Capture(station.Input);
        Require(!RebirthRemoteResourcePlacedStationClientCustody.HydrateUsed(actualOwner,actualEntry,actualOriginal,actualAfter,actualAfterQueue,()=>false),"caller frame/current coincidence without authenticated durable ClientUsed grants no setter");
        Require(actor.Matches(RebirthCamperEnergyActorImage.Capture(actualOwner))&&input.SequenceEqual(RebirthCamperInventorySnapshot.Capture(station.Input))&&rows.Select((row,slot)=>ReferenceEquals(row,station.Queue[slot])).All(value=>value),"refused hydration preserves full owner and every unrelated native row object");
    }
    internal static void PublishedDescriptorExact(RebirthRemoteResourceNativePublication.Watch actualPublishedWatch)
    {
        Require(actualPublishedWatch.TryReadPublishedTileDescriptor(out var descriptor,out long offset,out int length,out string digest),
            "actual current native published tile and payload prerequisite, not synthetic digest authority");
        Require(actualPublishedWatch.TryCopyPublishedTile(out var full)&&descriptor.PayloadDigest==full.Publication.PayloadDigest&&
            offset==full.TileOffset&&length==full.CopyTile().Length&&digest==RebirthStationPublicationRecord.Digest(full.CopyTile()),
            "allocation-free descriptor agrees with actual full native publication evidence");
        actualPublishedWatch.Dispose();
        Require(!actualPublishedWatch.TryReadPublishedTileDescriptor(out _,out _,out _,out _),"disposed generation never returns historical descriptor as current proof");
        Require(actualPublishedWatch.Closed&&actualPublishedWatch.Source==null&&actualPublishedWatch.Original==null&&
            actualPublishedWatch.Items==null&&actualPublishedWatch.Locks==null&&actualPublishedWatch.Markers==null&&
            actualPublishedWatch.TileSpan==null&&actualPublishedWatch.ChunkProof==null&&actualPublishedWatch.Tile==null&&actualPublishedWatch.Chunk==null&&
            actualPublishedWatch.Vehicle==null&&actualPublishedWatch.Drone==null,
            "closed watch retained by a native worker no longer owns cloned inventory/marker/original-plan graphs");
        actualPublishedWatch.Dispose(); // Idempotent; no second allocation release.
    }
    internal static void CompositePublishedPayload(RebirthRemoteResourceNativePublication.Watch actualCompositeWatch)
    {
        Require(actualCompositeWatch.Tile is TileEntityComposite,"actual native composite prerequisite");
        Require(actualCompositeWatch.TryCopyPublishedTile(out var full)&&
            full.PayloadPublication!=null&&full.Publication==null,"actual composite native publication has generic payload proof, not fabricated workstation spans");
        Require(RebirthStationPublicationRecord.Digest(full.CopyPayload())==full.PayloadPublication.PayloadDigest,
            "generic descriptor binds actual retained full native payload");
        Require(!actualCompositeWatch.TryReadPublishedTileDescriptor(out _,out _,out _,out _),"composite generic proof cannot authorize station descriptor contract");
    }
    internal static void NativeFrameAllocationBeforeHydration(byte[] actualNativePersistencyFrame)
    {
        byte[] original=(byte[])actualNativePersistencyFrame.Clone();
        Require(RebirthRemoteResourcePlacedStationNativeReader.TryMeasure(actualNativePersistencyFrame,out long graph,out long scratch)&&graph>0&&scratch>0,
            "exact installed v19/v50 framing and rich item topology counted before native hydration");
        Require(RebirthRemoteResourcePlacedStationNativeReader.TryRead(actualNativePersistencyFrame,out var hydrated)&&
            RebirthRemoteResourcePlacedStationNativeReader.Encode(hydrated).SequenceEqual(original),"measured original still passes full canonical native hydration and roundtrip");
        Require(actualNativePersistencyFrame.SequenceEqual(original),"meter and canonical reader preserve supplied original bytes");
        Require(!RebirthRemoteResourcePlacedStationNativeReader.TryMeasure(actualNativePersistencyFrame.Take(actualNativePersistencyFrame.Length-1).ToArray(),out _,out _),
            "truncated final native field refuses before graph hydration");
        var version=(byte[])original.Clone();version[0]^=1;
        Require(!RebirthRemoteResourcePlacedStationNativeReader.TryMeasure(version,out _,out _),"unsupported native frame version cannot enter allocation admission");
    }
    internal static void PlacedFullPublication(EntityPlayer actualOwner,RebirthRemoteResourceDurableJournal.Record actualUsedRoot)
    {
        Require(actualUsedRoot?.Phase==4&&(string)actualUsedRoot.Copy().Attribute("disposition")=="used",
            "actual placed original SourceRoot4 used prerequisite; native2 alone is insufficient");
        Require(RebirthRemoteResourcePlacedStationOriginal.TryObservePlacedPublication(actualOwner,actualUsedRoot,out string proof)&&
            proof==(string)actualUsedRoot.Copy().Attribute("ownerProof"),
            "full retained Before/native After chunk+queue+markers+owner receipt and typed Applied command reparse to exact saved Root4 proof");
        Require(RebirthRemoteResourcePlacedStationPreparation.TryReadUsedAllocation(actualOwner,actualUsedRoot,out var original,out var allocation)&&
            allocation.ReservedBytes<=RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace&&original.Operation!=Guid.Empty,
            "terminal observation uses captured immutable allocation without requiring retired source leases or granting effects");
        // Execute once immediately after publication and again after normal
        // SourceCustody.Update has retired original native source leases. Lost
        // reply must still return the same positive historical proof.
    }
    internal static void UnscopedPlacedSetterDenied(TileEntityWorkstation actualHeldTarget)
    {
        Require(RebirthRemoteResourcePlacedStationCustody.Blocks(actualHeldTarget),"actual held native placed target prerequisite");
        byte[] before=RebirthCamperInventorySnapshot.Capture(actualHeldTarget.Input);
        var setter=HarmonyLib.AccessTools.PropertySetter(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.Input));
        Require(!RebirthRemoteResourcePlacedStationOriginal.ConsumeStationSetter(actualHeldTarget,setter,new object[]{ItemStack.Clone(actualHeldTarget.Input)}),
            "hold and coincidental planned image cannot create typed original native setter permission");
        Require(before.SequenceEqual(RebirthCamperInventorySnapshot.Capture(actualHeldTarget.Input)),"readonly permission probe has no target effect");
    }
    internal static void NativeBuffReceiptSkip(PlayerDataFile actualSamePrimary,string actualReceipt)
    {
        Require(RebirthCookingRemoteAdmissionBackend.NativePhase(actualSamePrimary,actualReceipt,out float native,out bool nativePresent),
            "actual installed native v3 same-primary buff receipt prerequisite");
        Require(RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(actualSamePrimary,actualReceipt,out float skipped,out bool skippedPresent)&&
            skippedPresent==nativePresent&&skipped==native,"bounded zero-BuffValue reader agrees with actual native grammar/receipt");
        byte[] bytes=actualSamePrimary.buffData.ToArray();
        Require(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(bytes.Take(bytes.Length-1).ToArray(),actualReceipt,out _,out _),"truncated CVar frame refuses");
        var version=(byte[])bytes.Clone();version[0]=2;
        Require(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(version,actualReceipt,out _,out _),"unsupported historical buff grammar grants no payment receipt");
        byte[] hostile={3,1,0,255,255,255,255,7};
        Require(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(hostile,actualReceipt,out _,out _),"oversized declared name refuses before allocation or catalogue lookup");
    }
    internal static void ScratchPhaseExclusion(RebirthRemoteResourceDurableJournal.Record actualRoot,RebirthRemoteResourcePlacedStationAllocation.Admission actualAllocation)
    {
        Require(RebirthRemoteResourcePlacedStationPreparation.OwnsAllocation(actualAllocation),"actual retained placed operation allocation prerequisite");
        Require(RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(actualAllocation,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.OwnerEnvelope,out var first),
            "one explicit parser phase claims the already reserved operation scratch");
        using(first)
        {
            Require(!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(actualAllocation,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.Artifact,out _),
                "reentrant/concurrent parser cannot borrow the same operation scratch");
            Require(!RebirthRemoteResourcePlacedStationArtifactStore.TryRead(actualRoot,out _,out _,actualAllocation),
                "artifact entry refuses before allocating whole bytes while another phase owns scratch");
        }
        Require(RebirthRemoteResourcePlacedStationArtifactStore.TryRead(actualRoot,out _,out var full,actualAllocation)&&full.HasCapturedAllocation,
            "next phase can read the actual full immutable artifact after prior scratch release; retained images remain charged");
    }
    internal static void CapturedHistoricalAllocation(RebirthRemoteResourceDurableJournal.Record current,RebirthRemoteResourcePlacedStationArtifactReader.Image fullArtifact)
    {
        Require(fullArtifact.HasCapturedAllocation&&fullArtifact.SourceReadWorkspace<=RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace,
            "fresh v2 captures measured allocation before source effects");
        Require(RebirthRemoteResourceDurableJournal.TryReadPreparedRevisionBounded(current,fullArtifact.SourceRevision,fullArtifact.SourcePreparedBytes,fullArtifact.SourceReadWorkspace,out var prepared)&&
            RebirthRemoteResourcePlacedStationAllocation.TryMeasureSourceGraph(prepared,out long graph)&&graph==fullArtifact.SourceGraphBytes,
            "actual complete source history/candidates validate under exact captured immutable allocation");
        Require(!RebirthRemoteResourceDurableJournal.TryReadPreparedRevisionBounded(current,fullArtifact.SourceRevision,fullArtifact.SourcePreparedBytes,1,out _),
            "undersized explicit historical budget refuses before original graph hydration");
        var legacy=fullArtifact.Copy();legacy.SetAttributeValue("version",1);legacy.Attribute("sourceGraphBytes").Remove();legacy.Attribute("sourceReadWorkspace").Remove();
        byte[] legacyBytes=System.Text.Encoding.UTF8.GetBytes(legacy.ToString(System.Xml.Linq.SaveOptions.DisableFormatting));
        Require(RebirthRemoteResourcePlacedStationArtifactReader.TryRead(legacyBytes,out var observed)&&!observed.HasCapturedAllocation,
            "v1 full native artifact remains readable observation without fabricating cold allocation/financial authority");
    }
    internal static void ColdAggregateRead(EntityPlayer actor,Guid source)
    {
        int before=RebirthRemoteResourcePlacedStationPreparation.ActiveAttemptCount;long reserved=RebirthRemoteResourcePlacedStationPreparation.RetainedAllocationBytes;
        Require(RebirthRemoteResourcePlacedStationColdRead.TryOpen(actor,source,out var actual,out bool unknown)&&!unknown&&actual.Current&&actual.Artifact.HasCapturedAllocation,
            "bounded v2 header precedes source history and full native/source artifact cold observation");
        Require(RebirthRemoteResourcePlacedStationPreparation.RetainedAllocationBytes>reserved&&
            !RebirthRemoteResourcePlacedStationColdRead.TryOpen(actor,source,out _,out _),"one retained memory original prevents duplicate cold allocation");
        actual.Dispose();
        Require(!actual.Current&&actual.Source==null&&actual.Artifact==null&&
            RebirthRemoteResourcePlacedStationPreparation.ActiveAttemptCount==before&&RebirthRemoteResourcePlacedStationPreparation.RetainedAllocationBytes==reserved,
            "cold read disposal releases memory only, never effects or financial original custody");
    }
    internal static void FullArtifactReference(RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceClientOriginal.Intent original,
        RebirthRemoteResourcePlacedStationPreparation.Attempt retainedAttempt)
    {
        Require(retainedAttempt.HasSavedArtifact,"actual published full artifact and retained allocation prerequisite");
        Require(RebirthRemoteResourcePlacedStationReference.TryCapture(root,original,retainedAttempt.Allocation,out var reference),"small descriptor created only after actual full artifact final readback");
        Require(System.Text.Encoding.UTF8.GetByteCount(reference.Encode())<=4096&&
            RebirthRemoteResourcePlacedStationReference.TryResolve(root,reference.Encode(),retainedAttempt.Allocation,out var resolved)&&resolved.Original.Digest==original.Digest,
            "small descriptor resolves full original native/source artifacts, not hash-only evidence");
        var changed=reference.Copy();changed.SetAttributeValue("artifact",new string(reference.Artifact[0]=='a'?'b':'a',64));
        Require(!RebirthRemoteResourcePlacedStationReference.TryResolve(root,changed.ToString(System.Xml.Linq.SaveOptions.DisableFormatting),retainedAttempt.Allocation,out _),
            "syntactically valid different digest cannot reconstruct or adopt another artifact");
    }
    internal static void StructureMeterReadonly(ItemValue actualRichItem)
    {
        string native=RebirthNativeItemCodec.Encode(actualRichItem);
        Require(RebirthNativeItemConformanceReader.TryMeasureCanonicalV9Structure(native,out var measure)&&measure.Nodes>0&&
            measure.CloneAllocationBound>=512&&measure.ParserScratchBound>0,"existing structural parser counts full item topology without native hydration");
        Require(measure.CloneAllocationBound>=checked(512L*measure.Nodes+528L*measure.Slots),
            "every native empty modification/cosmetic slot includes its separately materialized ItemValue object allowance");
        Require(RebirthNativeItemCodec.Encode(actualRichItem)==native,"meter preserves actual native original");
        Require(!RebirthNativeItemConformanceReader.TryMeasureCanonicalV9Structure(native+"!",out _),"noncanonical base64 refuses before graph admission");
        byte[] bytes=Convert.FromBase64String(native);
        Require(!RebirthNativeItemConformanceReader.TryMeasureCanonicalV9Structure(Convert.ToBase64String(bytes.Take(bytes.Length-1).ToArray()),out _),"truncated native node refuses");
    }
    internal static void OnePreparationPerOriginal(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,
        RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourcePlacedStationCustody.Claim claim,
        IReadOnlyDictionary<string,RebirthRemoteResourceSourceCustody.Lease> leases)
    {
        Require(RebirthRemoteResourcePlacedStationPreparation.TryBegin(actor,root,original,claim,leases,out var first),"actual fully guarded root1 planning admission prerequisite");
        Require(!first.HasSavedArtifact,"fixture owns fresh pre-artifact observation, not an unresolved published original");
        int count=RebirthRemoteResourcePlacedStationPreparation.ActiveAttemptCount;long bytes=RebirthRemoteResourcePlacedStationPreparation.RetainedAllocationBytes;
        Require(RebirthRemoteResourcePlacedStationPreparation.TryBegin(actor,root,original,claim,leases,out var duplicate)&&ReferenceEquals(first,duplicate)&&
            count==RebirthRemoteResourcePlacedStationPreparation.ActiveAttemptCount&&bytes==RebirthRemoteResourcePlacedStationPreparation.RetainedAllocationBytes,
            "same original retries reuse exact retained plan/observer and allocation reservation");
        Require(first.Allocation.PlanningBytes<=RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace&&
            !first.Allocation.ReserveArtifact(RebirthRemoteResourcePlacedStationArtifact.MaximumBytes+1L),"artifact expansion remains bounded before allocation");
        first.Dispose();
        Require(RebirthRemoteResourcePlacedStationPreparation.ActiveAttemptCount==count-1&&
            RebirthRemoteResourcePlacedStationPreparation.RetainedAllocationBytes<bytes&&claim.TargetCurrent(),"disposing transient attempt releases only its memory/watch reservation, not target/source custody");
    }
    internal static void StreamingActorExact(RebirthCamperEnergyActorImage actualActor)
    {
        long count=RebirthRemoteResourcePlacedStationStreams.CountActor(actualActor);
        using(var native=new System.IO.MemoryStream())using(var streamed=new System.IO.MemoryStream())
        {
            using(var writer=new System.IO.BinaryWriter(native,System.Text.Encoding.UTF8,true)){actualActor.Write(writer);writer.Flush();}
            using(var writer=new System.IO.BinaryWriter(streamed,System.Text.Encoding.UTF8,true)){RebirthRemoteResourcePlacedStationStreams.WriteActor(writer,actualActor);writer.Flush();}
            Require(native.Length==count&&streamed.Length==count&&native.ToArray().SequenceEqual(streamed.ToArray()),
                "nonmaterializing actor frame writer exactly preserves existing full native item codec/cursor/locks bytes");
        }
    }
    internal static void FreshClientTargetHeld(EntityPlayerLocal player,ItemActionEntryCraft entry,
        RebirthRemoteResourceClientOriginal.Intent actualCapturedOriginal,ItemStack differentInput)
    {
        Require(RebirthRemoteResourcePlacedStationClientCustody.IsInstalled&&
            RebirthRemoteResourceOwnerAction.HoldsOriginalScope(player,actualCapturedOriginal),"actual installed client guards and original native10 hold prerequisite");
        Require(RebirthRemoteResourcePlacedStationScope.TryObserveWindow(player,entry,out var station,out _),"actual native workstation window prerequisite");
        Require(station.Input.Length>0&&differentInput!=null&&!RebirthStationGridIngredients.IsSameStackSnapshot(station.Input[0],differentInput),"different full input image prerequisite");
        Require(RebirthRemoteResourcePlacedStationClientCustody.TryHoldFresh(player,entry,actualCapturedOriginal),"exact original capture references acquire client UI custody before RPC");
        byte[] before=RebirthCamperInventorySnapshot.Capture(station.Input);var actor=RebirthCamperEnergyActorImage.Capture(player);
        entry.ItemController.GetParentByType<XUiC_WorkstationWindowGroup>().WorkstationData.SetInputInSlot(0,differentInput.Clone());
        Require(before.SequenceEqual(RebirthCamperInventorySnapshot.Capture(station.Input))&&actor.Matches(RebirthCamperEnergyActorImage.Capture(player)),
            "actual native model write is refused before UI/native array or owner effects");
          var direct=ItemStack.Clone(station.Input);direct[0]=differentInput.Clone();station.Input=direct;
          Require(before.SequenceEqual(RebirthCamperInventorySnapshot.Capture(station.Input)),"direct native TE setter cannot borrow client target custody without exact one-use hydration permission");
        Require(RebirthRemoteResourcePlacedStationClientCustody.TryHoldFresh(player,entry,actualCapturedOriginal),"duplicate exact transient hold is idempotent");
        // Decoded/cold original references are not a fresh-capture capability.
        // Cold binding requires separately authenticated original server proof.
    }
    internal static RebirthRemoteResourcePlacedStationPreparation.Progress PumpNativePreparation(
        RebirthRemoteResourcePlacedStationPreparation.Attempt actualAttempt,
        RebirthRemoteResourcePlacedStationCustody.Claim actualClaim)
    {
        Require(actualAttempt!=null&&actualClaim!=null&&actualClaim.ExactBefore(),"actual guarded target and original preparation prerequisite");
        var result=actualAttempt.Pump(out string artifact);
        Require(actualClaim.ExactBefore()&&RebirthRemoteResourcePlacedStationCustody.Blocks(actualClaim.Station),
            "observer/artifact publication preserves target custody and native Before");
        if(result==RebirthRemoteResourcePlacedStationPreparation.Progress.ArtifactSaved)
            Require(artifact?.Length==64,"actual immutable full artifact publication fingerprint");
        // Pump around actual native save completion. ArtifactSaved must not
        // be mapped to source Ready: complete allocation/client/settlement
        // qualification is intentionally absent from this observer contract.
        return result;
    }
    internal static void NativeInputShortfall(ItemValue actualIngredient)
    {
        Require(actualIngredient!=null&&actualIngredient.type>0,"actual installed native ingredient prerequisite");
        var input=new[]{new ItemStack(actualIngredient.Clone(),3)};
        var perBatch=new[]{new ItemStack(actualIngredient.Clone(),2),new ItemStack(actualIngredient.Clone(),1)};
        byte[] before=RebirthCamperInventorySnapshot.Capture(input);int queried=0;
        Require(RebirthRemoteResourcePlacedStationProjection.TryShortfall(input,perBatch,2,item=>{queried++;return 3;},out var full,out var remote)&&
            full.Count==1&&full[0].count==6&&remote.Count==1&&remote[0].count==3&&queried==1,
            "duplicate native per-batch costs aggregate before exact input-only shortfall");
        Require(before.SequenceEqual(RebirthCamperInventorySnapshot.Capture(input)),"readonly projection preserves full native input metadata");
        Require(!RebirthRemoteResourcePlacedStationProjection.TryShortfall(input,perBatch,2,item=>2,out _,out _),
            "insufficient remote stock cannot be replaced by unrelated owner bag stock");
        Require(!RebirthRemoteResourcePlacedStationProjection.TryShortfall(input,perBatch,2,item=>-1,out _,out _),"unknown remote availability refuses");
        Require(!RebirthRemoteResourcePlacedStationProjection.TryShortfall(input,new[]{new ItemStack(actualIngredient.Clone(),int.MaxValue)},2,item=>int.MaxValue,out _,out _),
            "overflow refuses before returned plan allocation");
        Require(RebirthRemoteResourcePlacedStationProjection.TryShortfall(input,new[]{new ItemStack(actualIngredient.Clone(),3)},1,item=>{throw new Exception("must not query remote");},out _,out var none)&&none.Count==0,
            "fully owned native input needs no remote availability query");
    }
    internal static void NativeEligibilityReadonly(ItemActionEntryCraft actualEntry,bool expected)
    {
        Require(RebirthRemoteResourcePlacedStationEligibility.IsInstalled,"native eligibility installation prerequisite");
        Require(RebirthRemoteResourcePlacedStationProjection.TryActualInputPool(actualEntry,out var player,out var input),"actual qualified native input window prerequisite");
        byte[] before=RebirthCamperInventorySnapshot.Capture(input);var actor=RebirthCamperEnergyActorImage.Capture(player);
        var recipe=(actualEntry.ItemController as XUiC_RecipeEntry).Recipe;
        Require(actualEntry.hasItems(actualEntry.ItemController.xui,recipe)==expected,"actual native eligibility result including native early-return scan");
        Require(before.SequenceEqual(RebirthCamperInventorySnapshot.Capture(input))&&actor.Matches(RebirthCamperEnergyActorImage.Capture(player)),
            "eligibility changes neither native station input nor owner inventory/cursor");
        // Run with actual full input, split input+remote, insufficient remote,
        // native modded/quality rejection and last-ingredient rejection. No
        // synthetic list or caller bool replaces the native eligibility loop.
    }
    internal static void SavedBeforeObservationRelease(RebirthRemoteResourceSourceCustody.Lease actualLease,string durableBeforeProof)
    {
        Require(actualLease.Source.Endpoint is TileEntity&&actualLease.Current()&&actualLease.HasActiveObservation,"actual tile saved0 watch and original source custody prerequisite");
        byte[] before=RebirthCamperInventorySnapshot.Capture(actualLease.Source.CopySlots());int watches=RebirthRemoteResourceNativePublication.ActiveSnapshotWatchCount;
        Require(actualLease.ReleaseSavedBeforeObservation(durableBeforeProof)&&actualLease.Current()&&!actualLease.HasActiveObservation,
            "only completed observer released after exact durable proof; source lease stays held");
        Require(RebirthRemoteResourceNativePublication.ActiveSnapshotWatchCount==watches-1&&watches<=RebirthStationSnapshotEvidence.MaximumRequests,
            "one bounded request becomes available for fifth source without raising global limit");
        Require(before.SequenceEqual(RebirthCamperInventorySnapshot.Capture(actualLease.Source.CopySlots()))&&
            actualLease.TryPreviewTileDebit(out _,out _,out _,out _),"unchanged physical source and private actual saved0 capacity witness survive observation release");
    }
    internal static void ColdSavedBeforeNotAdopted(RebirthRemoteResourceSourceCustody.Lease actualColdLease)
    {
        Require(actualColdLease.Current()&&actualColdLease.Original.AttemptedStage()==0&&!actualColdLease.HasActiveObservation,
            "fresh actual cold-bound source with no newly observed native saved0 prerequisite");
        Require(!actualColdLease.TryPreviewTileDebit(out _,out _,out _,out _),"stored hash and coincidental current Before cannot reconstruct private positive event");
        // Caller must pump actual native saved0 and then run the release case;
        // no fixture supplies a hash, marker or synthetic saved event.
    }
    internal static void SavedDebitObservationRelease(RebirthRemoteResourceSourceCustody.Lease actualLease,string durableDebitProof)
    {
        Require(actualLease.Current()&&actualLease.HasActiveObservation&&actualLease.Original.AttemptedStage()==1,
            "actual exclusive original debit and native saved1 observation prerequisite");
        byte[] debited=RebirthCamperInventorySnapshot.Capture(actualLease.Source.CopySlots());
        Require(actualLease.TryObserveSaved(1,out string observed)&&observed==durableDebitProof,
            "actual native ledger, original full stage1 image and final native save precede observer retirement");
        Require(actualLease.ReleaseSavedDebitObservation(durableDebitProof)&&actualLease.Current()&&!actualLease.HasActiveObservation,
            "exact durable published1 retires only observation, never source custody");
        Require(actualLease.TryObserveSaved(1,out string retained)&&retained==observed&&
            debited.SequenceEqual(RebirthCamperInventorySnapshot.Capture(actualLease.Source.CopySlots())),
            "uninterrupted original private saved1 event remains valid without repeated native watches or mutation");
    }
    internal static void ColdSavedDebitNotAdopted(RebirthRemoteResourceSourceCustody.Lease actualColdLease,string storedDebitProof)
    {
        Require(actualColdLease.Current()&&!actualColdLease.HasActiveObservation&&actualColdLease.Original.AttemptedStage()==1,
            "actual cold original debit with no new native observation prerequisite");
        Require(!actualColdLease.ReleaseSavedDebitObservation(storedDebitProof),
            "stored digest alone cannot create an uninterrupted private saved1 publication event");
    }
    internal static void NativePublishedArtifact(RebirthRemoteResourceNativePublication.Watch actualSavedWatch)
    {
        Require(actualSavedWatch.TryCopyPublishedTile(out var native),"actual positive same-request native publication, not caller digest");
        var raw=native.CopySnapshot();var payload=native.CopyPayload();
        Require(payload.Length<=RebirthStationRegionPayload.MaximumPayloadBytes&&RebirthStationPublicationRecord.Digest(payload)==native.Publication.PayloadDigest&&
            RebirthStationChunkInflate.TryDecode(payload,raw.Length-8,out var inflated)&&inflated.SequenceEqual(raw.Skip(8)),"retained full native compressed bytes reparse exact raw snapshot");
        var tile=native.CopyTile();Require(raw.Skip((int)native.TileOffset).Take(tile.Length).SequenceEqual(tile),"full native historical tile span");
    }
    internal static void ArtifactMaterialization(RebirthRemoteResourceDurableJournal.Record source,RebirthRemoteResourceClientOriginal.Intent original,
        string birth,RebirthRemoteResourcePlacedStationPlan.Plan plan,RebirthRemoteResourcePlacedStationBefore.Evidence before)
    {
        Require(RebirthRemoteResourcePlacedStationArtifact.TryEncode(source,original,birth,plan,before,out var bytes)&&bytes.Length<=RebirthRemoteResourcePlacedStationArtifact.MaximumBytes,"actual immutable artifact writer budget/count passes");
        Require(RebirthRemoteResourceDurableJournal.TryRead(source.Owner,source.Operation,out var after,out _)&&after.Revision==source.Revision&&after.Binding==source.Binding&&after.SourcesUnattempted&&!after.HasOwnerAction,"materialization grants no source attempt or owner permission");
        Require(!RebirthRemoteResourcePlacedStationArtifact.TryEncode(source,original,"",plan,before,out _),"missing original target incarnation is never inferred");
        // Full durable readback/cold recovery assertions require the unfinished
        // placed artifact reader and production coordinator, not this fixture.
    }
    internal static void NativeItemPrefix(ItemValue richItem)
    {
        byte[] item=Convert.FromBase64String(RebirthNativeItemCodec.Encode(richItem));var frame=new byte[item.Length+9];
        Buffer.BlockCopy(item,0,frame,4,item.Length);frame[frame.Length-1]=123;
        Require(RebirthNativeItemConformanceReader.TryReadCanonicalV9Prefix(frame,4,out var decoded,out int used)&&used==item.Length&&
            RebirthNativeItemCodec.Encode(decoded)==RebirthNativeItemCodec.Encode(richItem),"exact rich metadata/texture prefix, unrelated trailing frame preserved");
        Require(!RebirthNativeItemConformanceReader.TryReadCanonicalV9Prefix(frame,-1,out _,out _)&&
            !RebirthNativeItemConformanceReader.TryReadCanonicalV9Prefix(frame,frame.Length,out _,out _),"offset bounds");
        Require(!RebirthNativeItemConformanceReader.TryReadCanonicalV9Prefix(item.Take(item.Length-1).ToArray(),0,out _,out _),"truncated item never consumes guessed suffix");
        Require(!RebirthNativeItemConformanceReader.TryReadCanonicalV9Prefix(new byte[8*1024*1024+1],0,out _,out _),"whole-frame bound before parsing");
        Require(RebirthNativeItemConformanceReader.TryReadCanonicalV9Prefix(new byte[]{0,123},0,out var empty,out int nullLength)&&empty==null&&nullLength==1,"exact native null prefix");
    }
    internal static void PaidRowRemaining(Recipe nativeTemplate,ItemStack[] originalPaid,int batches,int remaining)
    {
        Require(batches>1&&remaining>0&&remaining<batches,"fixture requires partially completed multi-batch original");
        Guid job=Guid.NewGuid(),source=Guid.NewGuid();string binding=new string('a',64);
        byte[] before=RebirthCamperInventorySnapshot.Capture(originalPaid);
        Require(RebirthRemoteResourcePlacedStationPaidRow.TryCreate(job,source,binding,nativeTemplate,originalPaid,batches,1,7,true,out var row),"real paid-row builder");
        Require(RebirthRemoteResourceCraftRecipe.TryRead(RebirthRemoteResourceOwnerAction.RecipeImage(row.Recipe),out var authored),"detached canonical row recipe");
        authored.craftingToolType=row.Recipe.craftingToolType;authored.craftingTier=row.Recipe.craftingTier;authored.tags=row.Recipe.tags;
        RebirthRecipeOutputs.ClearFrozen(authored.ingredients[0].itemValue);
        Require(RebirthRecipeOutputs.TryPlan(authored,1,out var expectedOutputs,out _)&&RebirthRecipeOutputs.TryPlan(row.Recipe,1,out var actualOutputs,out _)&&
            RebirthCamperInventorySnapshot.Capture(expectedOutputs.ToArray()).SequenceEqual(RebirthCamperInventorySnapshot.Capture(actualOutputs.ToArray())),"new recipe outputs derive canonical catalogue, never inherited input frozen marker");
        Require(before.SequenceEqual(RebirthCamperInventorySnapshot.Capture(originalPaid)),"no original fragment mutation");
        Require(RebirthRemoteResourcePlacedStationPaidRow.TryRemaining(row,job,source,binding,out var full)&&before.SequenceEqual(RebirthCamperInventorySnapshot.Capture(full.ToArray())),"full cancellation returns exact original metadata");
        Require(RebirthRemoteResourcePlacedStationQueue.TryDecode(RebirthRemoteResourcePlacedStationQueue.Encode(new[]{row}),out var savedRows),"full immutable paid row retained independently");
        row.Multiplier=(short)remaining;
        Require(RebirthRemoteResourcePlacedStationPaidRow.TryRemaining(row,job,source,binding,out var suffix),"partial cancellation uses remaining batches");
        Require(RebirthRemoteResourcePlacedStationPaidRow.TryRemainingFromOriginal(row,savedRows[0],job,source,binding,out var exact)&&
            RebirthCamperInventorySnapshot.Capture(exact.ToArray()).SequenceEqual(RebirthCamperInventorySnapshot.Capture(suffix.ToArray())),"full-original progress reader returns exact rich suffix");
        row.Quality++;
        Require(!RebirthRemoteResourcePlacedStationPaidRow.TryRemainingFromOriginal(row,savedRows[0],job,source,binding,out _),"same markers/counts cannot substitute changed full original quality");row.Quality--;
        row.StartingEntityId++;
        Require(!RebirthRemoteResourcePlacedStationPaidRow.TryRemainingFromOriginal(row,savedRows[0],job,source,binding,out _),"current new actor ID cannot substitute original native attribution");row.StartingEntityId--;
        foreach(var group in originalPaid.GroupBy(item=>item.itemValue.type))
            Require(suffix.Where(item=>item.itemValue.type==group.Key).Sum(item=>(long)item.count)==group.Sum(item=>(long)item.count)/batches*remaining,"only original unconsumed fragment quantities owed");
        Require(!RebirthRemoteResourcePlacedStationPaidRow.TryRemaining(row,Guid.NewGuid(),source,binding,out _),"different original cannot borrow row");
        Require(!RebirthRemoteResourcePlacedStationPaidRow.TryRemaining(row,job,source,new string('b',64),out _),"different immutable source binding refused");
        row.Multiplier=(short)(batches+1);
        Require(!RebirthRemoteResourcePlacedStationPaidRow.TryRemaining(row,job,source,binding,out _),"remaining count cannot exceed original paid batches");
    }
    internal static void HistoricalOutputImage(Recipe nativeTemplate,ItemStack[] originalPaid,int batches,string authenticatedOriginalImage)
    {
        Guid job=Guid.NewGuid(),source=Guid.NewGuid();string binding=new string('c',64);
        Require(RebirthRemoteResourcePlacedStationPaidRow.TryCreate(job,source,binding,nativeTemplate,originalPaid,batches,1,7,true,out var row,authenticatedOriginalImage),
            "historical detached row accepts only strict canonical original frozen image");
        Require(RebirthRecipeOutputs.TryCaptureFrozenOutputs(row.Recipe,out string actual)&&actual==authenticatedOriginalImage,
            "historical reconstruction preserves exact original outputs independently of later catalogue definition");
        Require(!RebirthRemoteResourcePlacedStationPaidRow.TryCreate(job,source,binding,nativeTemplate,originalPaid,batches,1,7,true,out _,authenticatedOriginalImage+"!"),
            "corrupt historical output body cannot fall back to current catalogue");
        // Caller supplies actual host-authenticated immutable image from the
        // original artifact; this fixture never makes it payment authority.
    }
    // Only a disposable real native station fixture may call this: unlike
    // the pure planner, the reference DecItem below deliberately pays inputs.
    internal static void InputPaymentMatchesNative(XUiC_WorkstationInputGrid actualGrid,List<ItemStack> totalRequirements,List<ItemStack> canonicalRemotePaid)
    {
        var original=ItemStack.Clone(actualGrid.GetSlots());
        Require(RebirthRemoteResourcePlacedStationInputPlan.TryCreate(original,totalRequirements,canonicalRemotePaid,out var plan),"actual native input route, not owner bag shortfall");
        Require(original.Select((item,index)=>RebirthStationGridIngredients.IsSameStackSnapshot(item,actualGrid.GetSlots()[index])).All(value=>value),"pure plan leaves actual native input unchanged");
        var remote=canonicalRemotePaid.GroupBy(item=>item.itemValue.type).ToDictionary(group=>group.Key,group=>group.Sum(item=>item.count));
        var local=new List<ItemStack>();
        foreach(var group in totalRequirements.GroupBy(item=>item.itemValue.type))
        {remote.TryGetValue(group.Key,out int fetched);int amount=group.Sum(item=>item.count)-fetched;if(amount>0)local.Add(new ItemStack(group.First().itemValue.Clone(),amount));}
        var removed=new List<ItemStack>();actualGrid.RemoveItems(local,1,removed);
        var expected=plan.CopyAfter();
        Require(expected.Select((item,index)=>RebirthStationGridIngredients.IsSameStackSnapshot(item,actualGrid.GetSlots()[index])).All(value=>value),"actual native DecItem order/modded/zero/material/nonstackable behavior matches plan");
        removed.AddRange(canonicalRemotePaid.Select(item=>item.Clone()));
        Require(RebirthCamperInventorySnapshot.Capture(removed.ToArray()).SequenceEqual(RebirthCamperInventorySnapshot.Capture(plan.CopyPaid())),"exact full rich local and original remote paid fragments retained");
    }
    internal static void QueueRoundTrip(RecipeQueueItem[] realQueue)
    {
        string image=RebirthRemoteResourcePlacedStationQueue.Encode(realQueue);
        Require(RebirthRemoteResourcePlacedStationQueue.TryDecode(image,out var detached),"native wire and omitted runtime recipe fields roundtrip");
        Require(RebirthRemoteResourcePlacedStationQueue.Encode(detached)==image,"exact rich original queue canonical body");
        Require(!ReferenceEquals(realQueue,detached)&&realQueue.Where((row,index)=>ReferenceEquals(row,detached[index])).Count()==0,"detached rows cannot alter actual TE");
        Require(!RebirthRemoteResourcePlacedStationQueue.TryDecode(image+" ",out _),"noncanonical queue image refused");
    }
    internal static void CandidateNoMutation(TileEntityWorkstation station,ItemStack[] plannedInput,RecipeQueueItem[] plannedQueue,bool originalTurnsOn)
    {
        RebirthRemoteResourcePlacedStationCandidate.Install();
        Require(RebirthRemoteResourcePlacedStationCandidate.IsInstalled,"exact native callsite qualification must succeed before candidate use");
        var input=station.Input;var queue=station.Queue;bool burning=station.IsBurning;ulong clock=station.lastTickTime;
        byte[] cells=RebirthCamperInventorySnapshot.Capture(station.Input);string rows=RebirthRemoteResourcePlacedStationQueue.Encode(station.Queue);
        Require(RebirthRemoteResourcePlacedStationCandidate.TrySerialize(station,plannedInput,plannedQueue,burning||originalTurnsOn,out var candidate),"actual native Persistency candidate");
        Require(candidate.Length>0&&ReferenceEquals(input,station.Input)&&ReferenceEquals(queue,station.Queue)&&station.IsBurning==burning&&station.lastTickTime==clock&&
            cells.SequenceEqual(RebirthCamperInventorySnapshot.Capture(station.Input))&&rows==RebirthRemoteResourcePlacedStationQueue.Encode(station.Queue),"candidate never installs input/queue/burn state");
        Require(RebirthRemoteResourcePlacedStationNativeReader.TryRead(candidate,out var parsed)&&parsed.Burning==(burning||originalTurnsOn)&&
            RebirthCamperInventorySnapshot.Capture(parsed.Input).SequenceEqual(RebirthCamperInventorySnapshot.Capture(plannedInput))&&
            RebirthCookingRemoteAdmissionBackend.Queue(parsed.Queue)==RebirthCookingRemoteAdmissionBackend.Queue(plannedQueue),"full native v19/v50 canonical artifact parser preserves rich input and native queue");
        Require(!RebirthRemoteResourcePlacedStationNativeReader.TryRead(candidate.Concat(new byte[]{0}).ToArray(),out _)&&
            !RebirthRemoteResourcePlacedStationNativeReader.TryRead(candidate.Take(candidate.Length-1).ToArray(),out _),"trailing/truncated full artifacts refuse");
        Require(!RebirthRemoteResourcePlacedStationCandidate.TrySerialize(station,new ItemStack[256],plannedQueue,out _),"native byte array-length overflow rejected before native write");
    }
    internal static void NativeChunkCapacity(byte[] actualPublishedSnapshot,long actualTileOffset,byte[] actualTile,byte[] candidateTile)
    {
        Require(RebirthRemoteResourcePlacedStationCandidate.TryMeasureChunk(actualPublishedSnapshot,actualTileOffset,actualTile,candidateTile,out int raw,out int encoded),"native deflate candidate sector preflight");
        Require(raw==actualPublishedSnapshot.Length-actualTile.Length+candidateTile.Length&&encoded<=RebirthStationRegionPayload.MaximumPayloadBytes,"full chunk native bounds, not isolated queue size");
        var changed=(byte[])actualTile.Clone();changed[changed.Length-1]^=1;
        Require(!RebirthRemoteResourcePlacedStationCandidate.TryMeasureChunk(actualPublishedSnapshot,actualTileOffset,changed,candidateTile,out _,out _),"nonmatching source TE span cannot borrow another publication");
        Require(!RebirthRemoteResourcePlacedStationCandidate.TryMeasureChunk(actualPublishedSnapshot,-1,actualTile,candidateTile,out _,out _),"unknown span retained");
    }
    internal static void CandidateSourceOutput(TileEntityWorkstation station,ItemStack[] exactSourceAfter)
    {
        var reference=station.Output;byte[] before=RebirthCamperInventorySnapshot.Capture(reference);
        RebirthRemoteResourcePlacedStationCandidate.Install();
        Require(RebirthRemoteResourcePlacedStationCandidate.TrySerialize(station,station.Input,exactSourceAfter,station.Queue,station.IsBurning,out var bytes,out _)&&
            RebirthRemoteResourcePlacedStationNativeReader.TryRead(bytes,out var parsed)&&RebirthCamperInventorySnapshot.Capture(parsed.Output).SequenceEqual(RebirthCamperInventorySnapshot.Capture(exactSourceAfter)),"same-target exact source output After participates in full candidate");
        Require(ReferenceEquals(reference,station.Output)&&before.SequenceEqual(RebirthCamperInventorySnapshot.Capture(station.Output)),"no source/output mutation from candidate");
    }
    internal static void NativeSourceCapacityPreview(RebirthRemoteResourceDurableJournal.Record root,TileEntityWorkstation target,
        IReadOnlyDictionary<string,RebirthRemoteResourceSourceCustody.Lease> actualSavedZeroLeases)
    {
        var original=actualSavedZeroLeases.ToDictionary(pair=>pair.Key,pair=>RebirthCamperInventorySnapshot.Capture(pair.Value.Source.CopySlots()));
        Require(RebirthRemoteResourcePlacedStationSources.TryPreview(root,target,actualSavedZeroLeases,out var output,out int growth),"actual retained sources and positive native saved0 previews");
        int sameChunk=actualSavedZeroLeases.Values.Count(lease=>lease.Source.Endpoint is TileEntity tile&&ReferenceEquals(tile.GetChunk(),target.GetChunk()));
        Require(growth==sameChunk*114,"exact existing native marker ledger growth, no arbitrary headroom");
        var own=actualSavedZeroLeases.Values.SingleOrDefault(lease=>ReferenceEquals(lease.Source.Endpoint,target));
        Require(RebirthCamperInventorySnapshot.Capture(output).SequenceEqual(RebirthCamperInventorySnapshot.Capture(own==null?target.Output:own.Original.CopyStage(1))),"target output composes exact original native source debit");
        foreach(var pair in actualSavedZeroLeases)Require(original[pair.Key].SequenceEqual(RebirthCamperInventorySnapshot.Capture(pair.Value.Source.CopySlots())),"preflight never pays sources");
    }
    internal static void ImmutableArtifactIntegrity(byte[] actualArtifact)
    {
        Require(RebirthRemoteResourcePlacedStationArtifactReader.TryRead(actualArtifact,out var parsed)&&parsed.Original!=null,"full actual immutable artifact native integrity");
        Require(!RebirthRemoteResourcePlacedStationArtifactReader.TryRead(actualArtifact.Take(actualArtifact.Length-1).ToArray(),out _),"truncated artifact refuses");
        var root=parsed.Copy();root.Element("plan").SetAttributeValue("slot",255);
        Require(!RebirthRemoteResourcePlacedStationArtifactReader.TryRead(System.Text.Encoding.UTF8.GetBytes(root.ToString(System.Xml.Linq.SaveOptions.DisableFormatting)),out _),"native queue slot overflow refuses");
        root=parsed.Copy();root.Element("savedBefore").SetAttributeValue("nativeDigest",new string('0',64));
        Require(!RebirthRemoteResourcePlacedStationArtifactReader.TryRead(System.Text.Encoding.UTF8.GetBytes(root.ToString(System.Xml.Linq.SaveOptions.DisableFormatting)),out _),"caller digest cannot replace real compressed native payload proof");
        root=parsed.Copy();root.SetAttributeValue("sourcePreparedBytes",128L*1024*1024);
        Require(!RebirthRemoteResourcePlacedStationArtifactReader.TryRead(System.Text.Encoding.UTF8.GetBytes(root.ToString(System.Xml.Linq.SaveOptions.DisableFormatting)),out _),"forged aggregate workspace refuses before graph materialization");
        root=parsed.Copy();root.Element("savedBefore").Add(new System.Xml.Linq.XElement("unknown","data"));
        Require(!RebirthRemoteResourcePlacedStationArtifactReader.TryRead(System.Text.Encoding.UTF8.GetBytes(root.ToString(System.Xml.Linq.SaveOptions.DisableFormatting)),out _),"unknown proof field refuses");
        root=parsed.Copy();root.Element("savedBefore").Element("markers").RemoveNodes();
        Require(!RebirthRemoteResourcePlacedStationArtifactReader.TryRead(System.Text.Encoding.UTF8.GetBytes(root.ToString(System.Xml.Linq.SaveOptions.DisableFormatting)),out _),"missing same-snapshot native target incarnation never becomes raw-block equivalence");
        root=parsed.Copy();root.AddFirst(new System.Xml.Linq.XComment("unexpected"));
        Require(!RebirthRemoteResourcePlacedStationArtifactReader.TryRead(System.Text.Encoding.UTF8.GetBytes(root.ToString(System.Xml.Linq.SaveOptions.DisableFormatting)),out _),"comment node expansion refused before graph allocation");
        root=parsed.Copy();root.Add(Enumerable.Range(0,RebirthRemoteResourceDurableJournal.MaximumSources*4).Select(index=>new System.Xml.Linq.XElement("unexpected")));
        Require(!RebirthRemoteResourcePlacedStationArtifactReader.TryRead(System.Text.Encoding.UTF8.GetBytes(root.ToString(System.Xml.Linq.SaveOptions.DisableFormatting)),out _),"source-derived structure cap refuses node expansion before graph allocation");
    }
    internal static void ImmutableArtifactSourceHistory(byte[] actualArtifact,RebirthRemoteResourceDurableJournal.Record current)
    {
        Require(RebirthRemoteResourcePlacedStationArtifactReader.TryRead(actualArtifact,out var parsed)&&
            RebirthRemoteResourcePlacedStationArtifactReader.TryValidateSource(parsed,current),"exact prepared source history and rich paid/native frame reconstruction");
        var changed=parsed.Copy();changed.SetAttributeValue("sourcePreparedBytes",parsed.SourcePreparedBytes+1);
        var bytes=System.Text.Encoding.UTF8.GetBytes(changed.ToString(System.Xml.Linq.SaveOptions.DisableFormatting));
        Require(!RebirthRemoteResourcePlacedStationArtifactReader.TryRead(bytes,out var falseLength)||
            !RebirthRemoteResourcePlacedStationArtifactReader.TryValidateSource(falseLength,current),"declared source bytes must equal actual retained prepared revision");
        changed=parsed.Copy();changed.Element("plan").SetAttributeValue("binding",new string('0',64));changed.Element("savedBefore").SetAttributeValue("binding",new string('0',64));
        bytes=System.Text.Encoding.UTF8.GetBytes(changed.ToString(System.Xml.Linq.SaveOptions.DisableFormatting));
        Require(!RebirthRemoteResourcePlacedStationArtifactReader.TryRead(bytes,out var falseBinding)||
            !RebirthRemoteResourcePlacedStationArtifactReader.TryValidateSource(falseBinding,current),"self-consistent caller binding cannot replace source original");
        changed=parsed.Copy();changed.Element("plan").SetAttributeValue("entity",-1);
        bytes=System.Text.Encoding.UTF8.GetBytes(changed.ToString(System.Xml.Linq.SaveOptions.DisableFormatting));
        Require(!RebirthRemoteResourcePlacedStationArtifactReader.TryRead(bytes,out _),"missing authenticated original native actor attribution refuses");
    }
    internal static void NativeTargetClaimFreeze(RebirthRemoteResourcePlacedStationCustody.Claim actualClaim,ItemStack[] differentInput)
    {
        Require(actualClaim!=null&&actualClaim.TargetCurrent()&&RebirthRemoteResourcePlacedStationGuards.IsInstalled,"actual qualified native target claim");
        var station=actualClaim.Station;var before=RebirthCamperInventorySnapshot.Capture(station.Input);ulong clock=station.lastTickTime;
        Require(differentInput!=null&&differentInput.Length==station.Input.Length&&differentInput.Length>0&&
            !before.SequenceEqual(RebirthCamperInventorySnapshot.Capture(differentInput)),"independent genuinely different attempted native input");
        station.Input=differentInput;
        Require(before.SequenceEqual(RebirthCamperInventorySnapshot.Capture(station.Input)),"exact held native property write refuses");
        new XUiM_Workstation(station).SetInputStacks(differentInput);
        Require(before.SequenceEqual(RebirthCamperInventorySnapshot.Capture(station.Input)),"native model bulk write refuses before effects");
        int slot=Enumerable.Range(0,differentInput.Length).First(index=>!RebirthStationGridIngredients.IsSameStackSnapshot(station.Input[index],differentInput[index]));
        new XUiM_Workstation(station).SetInputInSlot(slot,differentInput[slot]);
        Require(before.SequenceEqual(RebirthCamperInventorySnapshot.Capture(station.Input)),"direct native model array slot write refuses");
        station.UpdateTick(actualClaim.World);
        Require(station.lastTickTime==clock&&actualClaim.ExactBefore(),"exact original target clock/fuel/queue/material remain frozen");
        Require(!RebirthRemoteResourceSourceCustody.IsExecutingExactOutput(station,station.Output,actualClaim.Owner,actualClaim.Source,actualClaim.Binding),"a target claim alone never fabricates source write permission");
    }
    internal static RebirthRemoteResourceNativePublication.Watch BeginActualPlacedMarkerObservation(RebirthRemoteResourceNativeBinding target,
        RebirthRemoteResourceDurableJournal.Record original,IReadOnlyDictionary<string,RebirthRemoteResourceSourceCustody.Lease> sourceLeases,
        RebirthRemoteResourcePlacedStationCustody.Claim actualClaim)
    {
        Require(actualClaim!=null&&actualClaim.ExactBefore(),"actual exclusively held original target Before");
        return RebirthRemoteResourceNativePublication.BeginPlacedTarget(target,original,sourceLeases,actualClaim.ExactBefore);
    }
    internal static void ActualPlacedMarkerObservation(RebirthRemoteResourceNativePublication.Watch actualWatch,TileEntityWorkstation target,
        RebirthRemoteResourceDurableJournal.Record original,IReadOnlyDictionary<string,RebirthRemoteResourceSourceCustody.Lease> sourceLeases)
    {
        Require(actualWatch!=null,"actual pending native target observation");
        Require(actualWatch.TryCopyPublishedTile(out var saved),"actual completed same-region native target snapshot/readback, not caller hash");
        int sameChunk=original.Copy().Element("sources").Elements("source").Where(source=>source.Element("npc")==null)
            .Select(source=>sourceLeases[(string)source.Attribute("id")]).Count(lease=>lease.Source.Endpoint is TileEntity tile&&ReferenceEquals(tile.GetChunk(),target.GetChunk()));
        Require(saved.CopyMarkers().Length==sameChunk+1&&saved.CopyMarkers().Length<=RebirthRemoteResourceDurableJournal.MaximumSources+1,
            "only exact admitted same-chunk source markers plus one native target identity, actual source limit128");
    }
    internal static void FinalAckRetiresActualGraphs(EntityPlayer actualOwner,RebirthRemoteResourceDurableJournal.Record actualUsed,
        RebirthRemoteResourceClientOriginal.Intent original,System.Xml.Linq.XElement actualUsedReply,
        IReadOnlyDictionary<string,RebirthRemoteResourceSourceCustody.Lease> retained,bool nativeAck2ActuallySaved)
    {
        long prior=RebirthRemoteResourcePlacedStationPreparation.RetainedAllocationBytes;
        bool admitted=RebirthRemoteResourcePlacedStationOriginal.TerminalArchive.TryCreate(actualOwner,actualUsed,original,actualUsedReply,out var receipt);
        Require(admitted==nativeAck2ActuallySaved,"only actual same-primary saved client host-ACK receipt2 plus full durable completion can mint terminal archive");
        if(!admitted)
        {
            Require(retained.Values.All(value=>!value.Original.GraphRetired)&&prior==RebirthRemoteResourcePlacedStationPreparation.RetainedAllocationBytes,
                "receipt0/1, request bit, digest or current equality cannot retire source graphs/allocation");return;
        }
        Require(RebirthRemoteResourcePlacedStationPreparation.RetireTerminal(receipt),"actual typed positive terminal retires retained graph owners");
        Require(retained.Values.All(value=>value.Original.GraphRetired&&value.Source==null&&value.Grid==null&&!value.Current()),
            "all exact leases retain only immutable compact effect witnesses, no endpoint/item graph or setter");
        foreach(var lease in retained.Values)
        {
            bool refused=false;try{lease.Original.CopyStage(0);}catch(InvalidOperationException){refused=true;}
            Require(refused,"terminal compact witness cannot replay original rich stack array");lease.Dispose();
        }
        Require(RebirthRemoteResourcePlacedStationPreparation.RetainedAllocationBytes<prior,"aggregate budget released after actual graph retirement");
    }
    internal static void ClockOnlyComparison(byte[] actualNativeTile,int qualifiedClockOffset,byte[] canonicalTile)
    {
        var original=(byte[])actualNativeTile.Clone();
        Require(RebirthRemoteResourcePlacedStationCandidate.TryNormalizeClock(actualNativeTile,qualifiedClockOffset,out var normalized,out _)&&normalized.SequenceEqual(canonicalTile),"only exact recorded native clock field normalized");
        Require(actualNativeTile.SequenceEqual(original),"historical actual bytes preserved");
        int other=qualifiedClockOffset==0?actualNativeTile.Length-1:0;var changed=(byte[])actualNativeTile.Clone();changed[other]^=1;
        Require(RebirthRemoteResourcePlacedStationCandidate.TryNormalizeClock(changed,qualifiedClockOffset,out var mismatch,out _)&&!mismatch.SequenceEqual(canonicalTile),"unrelated native byte cannot become clock equivalence");
        Require(!RebirthRemoteResourcePlacedStationCandidate.TryNormalizeClock(actualNativeTile,-1,out _,out _),"unknown clock offset refuses");
    }
    internal static void CancellationOwnerNativeCheckpoint(RebirthRemoteResourcePlacedStationOriginal.CancellationClient actual,int completed)
    {
        var progress=actual?.OwnerProgress;
        Require(progress!=null&&progress.BaselineSaved&&progress.Completed==completed&&completed>=0&&completed<=progress.Steps.Count,
            "actual production plan and positive baseline save, not fixture-created metadata");
        Require(actual.Actor.Buffs.GetCustomVar("RebirthPlacedCancelV1_"+actual.Operation.ToString("N"))==1f,"checkpoint before receipt2");
        var selector=typeof(RebirthRemoteResourcePlacedStationOriginal).GetMethod("OwnerProgressJournal",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        var plan=(RebirthRemoteResourceClientOriginalJournal)selector.Invoke(null,new object[]{progress,actual.Operation,"OwnerPlan",progress.PlanBytes.Length});
        var manifest=plan.Read(actual.Operation);
        Require(manifest?.State==RebirthCamperTransferJournal.Phase.Applied&&manifest.Before.SequenceEqual(progress.PlanBytes)&&manifest.After.SequenceEqual(progress.PlanBytes),"complete immutable applied plan before original setter");
        foreach(var step in progress.Steps)
        {
            var journal=(RebirthRemoteResourceClientOriginalJournal)selector.Invoke(null,new object[]{progress,step.Key,"OwnerSlot",step.Command.Length});var record=journal.Read(step.Key);
            Require(record!=null&&record.Vehicle==actual.Source&&record.OwnerCreation==actual.Original.Creation&&record.Before.SequenceEqual(step.Command)&&record.After.SequenceEqual(step.Command),"same original full slot command");
            Require(record.State==(step.Ordinal<completed?RebirthCamperTransferJournal.Phase.Applied:RebirthCamperTransferJournal.Phase.Prepared),"only positively saved actual setter returns advance completed prefix");
            Require(step.Ordinal>=completed||step.NativeReturned,"no consumed-permit-only native proof");
        }
        var keyMethod=typeof(RebirthRemoteResourcePlacedStationOriginal).GetMethod("OwnerProgressKey",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        var key=(Guid)keyMethod.Invoke(null,new object[]{actual.Operation,completed-1});
        var checkpoint=(RebirthRemoteResourceClientOriginalJournal)selector.Invoke(null,new object[]{progress,key,"OwnerCheckpoint",12*1024*1024});var saved=checkpoint.Read(key);
        Require(saved?.State==RebirthCamperTransferJournal.Phase.Applied&&saved.Before.SequenceEqual(saved.After),"positive full native saved checkpoint");
        var body=System.Xml.Linq.XElement.Parse(System.Text.Encoding.UTF8.GetString(saved.Before));
        Require(body.Element("completePlan").Value==progress.Plan&&(int)body.Attribute("ordinal")==completed-1,"checkpoint exact full plan and completed prefix");
        var image=Convert.FromBase64String(body.Element("nativeOwner").Value);
        Require(RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(System.IO.Path.Combine(GameIO.GetPlayerDataDir(),actual.Original.Owner+".ttp"),image),"actual primary native PDS full bytes, not digest or return flag");
        Require(RebirthRemoteResourceClientOriginal.ReadActor(body.Element("actor").Value).Matches(RebirthCamperEnergyActorImage.Capture(actual.Actor)),"entire live prefix rich inventory equals saved native checkpoint actor");
    }
    internal static void CancellationOwnerUnknownAttemptCannotReplay(RebirthRemoteResourcePlacedStationOriginal.CancellationClient actual)
    {
        var progress=actual?.OwnerProgress;Require(progress!=null&&progress.Completed<progress.Steps.Count,"real interrupted prefix");
        var step=progress.Steps[progress.Completed];Require(!step.NativeReturned,"actual original setter return was not observed");
        Require(RebirthRemoteResourcePlacedStationAllocation.HasScratch(actual.Memory,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.OwnerEnvelope),"inside actual original envelope; scratch refusal cannot fake uncertain-step check");
        var selector=typeof(RebirthRemoteResourcePlacedStationOriginal).GetMethod("OwnerProgressJournal",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        var journal=(RebirthRemoteResourceClientOriginalJournal)selector.Invoke(null,new object[]{progress,step.Key,"OwnerSlot",step.Command.Length});var record=journal.Read(step.Key);
        Require(record?.State==RebirthCamperTransferJournal.Phase.Uncertain&&record.Before.SequenceEqual(step.Command)&&record.After.SequenceEqual(step.Command),"actual attempted original durable Uncertain before replay refusal");
        var before=RebirthRemoteResourceClientOriginal.ActorImage(RebirthCamperEnergyActorImage.Capture(actual.Actor));
        Require(!RebirthRemoteResourcePlacedStationOriginal.EnterCancellationOwnerStep(progress,step.Bag?actual.Actor.bag.ItemGrid:actual.Actor.inventory.ItemGrid,step.Slot,out _),"uncertain attempted slot never enters again even when observed value equals Before/After");
        Require(before==RebirthRemoteResourceClientOriginal.ActorImage(RebirthCamperEnergyActorImage.Capture(actual.Actor)),"refusal changed no owner material");
    }
    internal static object CancellationHostCustodyCheckpoint(Guid actualOperation,int phase,string actualBoundary=null)
    {
        Require(phase>=0&&phase<=2,"actual custody stage");
        var field=typeof(RebirthRemoteResourcePlacedStationOriginal).GetField("cancellationServers",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        var actual=((System.Collections.IDictionary)field.GetValue(null))[actualOperation];Require(actual!=null,"actual retained authenticated cancellation original");
        var custody=NativeField<object>(actual,"HostCustody");var anchor=NativeField<byte[]>(custody,"Anchor");
        Require(anchor!=null&&NativeField<Guid>(custody,"Operation")==actualOperation&&!NativeField<bool>(custody,"Closed"),"actual original already armed, never fabricated receipt");
        var selector=typeof(RebirthRemoteResourcePlacedStationOriginal).GetMethod("HostCustodyJournal",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        var journal=(RebirthRemoteResourceClientOriginalJournal)selector.Invoke(null,new object[]{custody,false});var record=journal.Read(actualOperation);
        Require(record!=null&&record.State==RebirthCamperTransferJournal.Phase.Prepared&&record.Before.SequenceEqual(anchor)&&record.After.SequenceEqual(anchor),"positive complete custody state, not absence of escape record");
        var root=System.Xml.Linq.XElement.Parse(System.Text.Encoding.UTF8.GetString(anchor));
        Require(root.Element("request").Value==NativeField<string>(actual,"Header")&&root.Element("permission").Value==NativeField<string>(actual,"Permission"),"entire same financial original request and permission");
        var before=new RebirthRemoteResourceClientOriginalJournal(System.IO.Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativePlacedCancellationBefore")).Read(actualOperation);
        var accepted=new RebirthRemoteResourceClientOriginalJournal(System.IO.Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativePlacedCancellationAccepted")).Read(actualOperation);
        Require(before?.State==RebirthCamperTransferJournal.Phase.Applied&&accepted?.State==RebirthCamperTransferJournal.Phase.Applied&&
            root.Element("savedBefore").Value==System.Text.Encoding.UTF8.GetString(before.Before)&&root.Element("acceptedNativeOriginal").Value==System.Text.Encoding.UTF8.GetString(accepted.Before),"full actual saved native publication and original accepted network event");
        var saved=System.Xml.Linq.XElement.Parse(root.Element("savedBefore").Value);
        Require(Convert.FromBase64String(saved.Element("snapshot").Value).SequenceEqual(NativeField<byte[]>(custody,"Snapshot")),"entire native snapshot retained, no hash/count substitute");
        if(phase>0)
        {
            Require(!string.IsNullOrEmpty(actualBoundary),"actual observed native method/command boundary");
            Guid key;using(var sha=System.Security.Cryptography.SHA256.Create())
            {var digest=sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(actualOperation.ToString("N")+"|host-custody-refusal|"+actualBoundary));var id=new byte[16];Buffer.BlockCopy(digest,0,id,0,16);key=new Guid(id);}
            var receipt=new RebirthRemoteResourceClientOriginalJournal(System.IO.Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativePlacedCancellationHostRefusal")).Read(key);
            Require(receipt!=null&&receipt.Operation==key&&receipt.State==(phase==1?RebirthCamperTransferJournal.Phase.Uncertain:RebirthCamperTransferJournal.Phase.Applied)&&receipt.Before.SequenceEqual(receipt.After),"original attempt write-ahead or actual refused-original decision receipt");
            var body=System.Xml.Linq.XElement.Parse(System.Text.Encoding.UTF8.GetString(receipt.Before));
            Require(body.Element("completeCustodyOriginal").Value==System.Text.Encoding.UTF8.GetString(anchor)&&body.Attribute("boundary").Value==actualBoundary&&body.Attribute("decision").Value=="refuse-original","full immutable original and actual refused boundary");
            var current=typeof(RebirthRemoteResourcePlacedStationOriginal).GetMethod("CancellationHostCustodyCurrent",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            Require((bool)current.Invoke(null,new[]{actual}),"refused native boundary preserves positive custody so original refund settlement can finish");
        }
        return custody;
    }
    internal static void CancellationHostCustodyRetired(object actualCustody)
    {
        Require(actualCustody!=null&&NativeField<bool>(actualCustody,"Closed")&&NativeField<int>(actualCustody,"Entered")==0,"actual entered callbacks drained after durable retirement");
        Require(NativeField<byte[]>(actualCustody,"Anchor")==null&&NativeField<byte[]>(actualCustody,"Snapshot")==null&&NativeField<byte[]>(actualCustody,"RetirementBytes")==null,"all charged native/original buffers gone before allocation can retire");
    }
    internal static void CancellationHostGuardPreservesNative(Guid operation,byte[] actualBefore,BlockValue actualBlockBefore)
    {
        var field=typeof(RebirthRemoteResourcePlacedStationOriginal).GetField("cancellationServers",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        var original=((System.Collections.IDictionary)field.GetValue(null))[operation];var station=NativeField<TileEntityWorkstation>(original,"Station");
        Require(RebirthRemoteResourcePlacedStationCandidate.TrySerialize(station,station.Input,station.Queue,station.IsBurning,out var current,out _)&&current.SequenceEqual(actualBefore),"actual refused boundary preserves entire rich native body, not merely row/count");
        Require(GameManager.Instance.World.GetBlock(station.ToWorldPos()).Equals(actualBlockBefore),"native damage/explosion/pickup guard preceded block mutation");
    }
    internal static object Receipt1DiscoveredBeforeNativeLoad(Guid operation)
    {
        var field=typeof(RebirthRemoteResourcePlacedStationOriginal).GetField("restartCancellations",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        var archive=((System.Collections.IDictionary)field.GetValue(null))[operation];
        Require(NativeField<bool>(archive,"BeforeRestart"),"actual region-manager startup selected owner receipt1 before native load");
        var token=NativeField<object>(archive,"Load");Require(token!=null&&NativeField<bool>(token,"Receipt1Before")&&NativeField<object>(token,"Loaded")==null,"discovery registered immutable expected payload BEFORE original LoadChunk");
        var custody=NativeField<object>(archive,"HostCustody");byte[] anchor=NativeField<byte[]>(custody,"Anchor");
        var record=new RebirthRemoteResourceClientOriginalJournal(NativeField<string>(custody,"Root")).Read(operation);
        var body=System.Xml.Linq.XElement.Parse(System.Text.Encoding.UTF8.GetString(anchor));
        Require((int)body.Attribute("version")==2&&record?.State==RebirthCamperTransferJournal.Phase.Prepared&&record.Before.SequenceEqual(anchor)&&record.After.SequenceEqual(anchor),"positive never-entered custody original, not absent Mutation");
        Require(body.Element("permission").Value==NativeField<string>(archive,"Permission")&&body.Element("savedBefore").Value==NativeField<string>(archive,"BeforeEvidence"),"full exact owner/native permission retained before native reconstruction");
        var saved=System.Xml.Linq.XElement.Parse(body.Element("savedBefore").Value);
        Require(Convert.FromBase64String(saved.Element("snapshot").Value).SequenceEqual(NativeField<byte[]>(token,"Snapshot")),"entire actual saved native snapshot registered, not hash/count");return token;
    }
    internal static void Receipt1ActualNativeReconstructed(object discoveredToken,TileEntityWorkstation actualReturnedTile)
    {
        var loaded=NativeField<object>(discoveredToken,"Loaded");Require(loaded!=null&&ReferenceEquals(NativeField<TileEntityWorkstation>(loaded,"Tile"),actualReturnedTile),"actual original factory/read/load returns registered source");
        Require(NativeField<long>(loaded,"Generation")>0&&ReferenceEquals(NativeField<object>(loaded,"Original"),discoveredToken),"actual native generation retains startup original");
        Require((bool)loaded.GetType().GetMethod("Installed",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(loaded,null),"actual returned chunk/TE/provider currently installed");
        Require(RebirthRemoteResourcePlacedStationOriginal.CancellationBlocks(actualReturnedTile),"reconstructed native body remains frozen before authenticated owner continuation");
    }
    internal static void Receipt1ActualOwnerHydrated(RebirthRemoteResourcePlacedStationOriginal.CancellationClient actual,int savedPrefix,bool resumable)
    {
        Require(actual?.RestartBeforeOwner?.Accepted==true&&actual.Stage==1&&actual.Actor.Buffs.GetCustomVar("RebirthPlacedCancelV1_"+actual.Operation.ToString("N"))==1f,"actual received PlayerId and original ToPlayer1 completed");
        Require(RebirthRemoteResourceOwnerAction.HoldsPlacedCancellation(actual),"full original owner hold exists before a new grant");
        Require(actual.RestartBeforeOwner.MayResume==resumable&&!actual.NativeLease(),"native hydration alone never grants a financial setter");
        if(!resumable){Require(actual.OwnerProgress==null,"uncertain or mismatched native checkpoint cannot become an executable progress prefix");return;}
        var progress=actual.OwnerProgress;Require(progress!=null&&progress.Completed==savedPrefix&&progress.BaselineSaved,"complete saved contiguous Applied prefix and Prepared remainder");
        Require(progress.Steps.All(step=>!step.NativeReturned)&&!actual.OwnerAttempted,"restart fabricates neither prior native return nor new owner attempt");
        Require(RebirthRemoteResourceClientOriginal.ReadActor(actual.RestartBeforeOwner.Prefix).Matches(RebirthCamperEnergyActorImage.Capture(actual.Actor)),"actual complete hydrated owner financial prefix matches checkpoint");
    }
    internal static Guid Receipt1CheckpointBeforeSlotMetadata(RebirthRemoteResourcePlacedStationOriginal.CancellationClient actual,int ordinal)
    {
        var progress=actual?.OwnerProgress;Require(progress!=null&&progress.Completed==ordinal&&ordinal>=0&&ordinal<progress.Steps.Count,"actual original held after its checkpoint and before slot metadata");
        var step=progress.Steps[ordinal];Require(step.NativeReturned,"actual native setter did return before checkpoint publication");
        var selector=typeof(RebirthRemoteResourcePlacedStationOriginal).GetMethod("OwnerProgressJournal",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        var journal=(RebirthRemoteResourceClientOriginalJournal)selector.Invoke(null,new object[]{progress,step.Key,"OwnerSlot",step.Command.Length});var record=journal.Read(step.Key);
        Require(record?.State==RebirthCamperTransferJournal.Phase.Uncertain&&record.Before.SequenceEqual(step.Command)&&record.After.SequenceEqual(step.Command),"same slot remains Uncertain; no fixture phase was written");
        var checkpoint=(RebirthRemoteResourceClientOriginalJournal)selector.Invoke(null,new object[]{progress,step.Key,"OwnerCheckpoint",12*1024*1024});var saved=checkpoint.Read(step.Key);
        Require(saved?.State==RebirthCamperTransferJournal.Phase.Applied&&saved.Before.SequenceEqual(saved.After),"positive actual saved checkpoint already published");
        var body=System.Xml.Linq.XElement.Parse(System.Text.Encoding.UTF8.GetString(saved.Before));
        Require((bool)body.Attribute("setterReturned")&&(int)body.Attribute("ordinal")==ordinal&&body.Element("completePlan").Value==progress.Plan&&body.Element("actor").Value==step.After,"full actual original setter/checkpoint identity");
        Require(RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(System.IO.Path.Combine(GameIO.GetPlayerDataDir(),actual.Original.Owner+".ttp"),Convert.FromBase64String(body.Element("nativeOwner").Value)),"actual complete native owner primary equals positive checkpoint");
        return step.Key;
    }
    internal static void Receipt1SavedSlotMetadataRecovered(RebirthRemoteResourcePlacedStationOriginal.CancellationClient actual,int restoredPrefix,Guid originalSlot)
    {
        Receipt1ActualOwnerHydrated(actual,restoredPrefix,true);
        var progress=actual.OwnerProgress;Require(restoredPrefix>0&&progress.Steps[restoredPrefix-1].Key==originalSlot,"same historical original slot completed through actual hydration");
        var step=progress.Steps[restoredPrefix-1];var selector=typeof(RebirthRemoteResourcePlacedStationOriginal).GetMethod("OwnerProgressJournal",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        var journal=(RebirthRemoteResourceClientOriginalJournal)selector.Invoke(null,new object[]{progress,step.Key,"OwnerSlot",step.Command.Length});var record=journal.Read(step.Key);
        Require(record?.State==RebirthCamperTransferJournal.Phase.Applied&&record.Before.SequenceEqual(step.Command)&&record.After.SequenceEqual(step.Command)&&!step.NativeReturned,"metadata completed from positive saved checkpoint, no historical setter reentered");
    }
    internal static void Receipt1OnlyRemainingSettersCompleted(RebirthRemoteResourcePlacedStationOriginal.CancellationClient actual,int restoredPrefix,int actualNewSetterReturns)
    {
        var progress=actual?.OwnerProgress;Require(actual?.RestartBeforeOwner?.Accepted==true&&progress!=null&&progress.Completed==progress.Steps.Count,"same actual hydrated original completed every remaining slot");
        Require(actualNewSetterReturns==progress.Steps.Count-restoredPrefix&&progress.Steps.Take(restoredPrefix).All(step=>!step.NativeReturned)&&progress.Steps.Skip(restoredPrefix).All(step=>step.NativeReturned),"no historical setter replay, exactly one observed native return for each remaining slot");
        Require(actual.Stage==2&&actual.OwnerSavedAfterVersion?.IsCurrent==true&&actual.After.Matches(RebirthCamperEnergyActorImage.Capture(actual.Actor)),"ordinary fullAfter/native2 save and owner version reached without ACK inventory replacement");
    }
    internal static void Receipt1QueueAttemptConsumedPositiveCustody(Guid operation)
    {
        var field=typeof(RebirthRemoteResourcePlacedStationOriginal).GetField("cancellationServers",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        var server=((System.Collections.IDictionary)field.GetValue(null))[operation];Require(NativeField<bool>(server,"QueueAttempted"),"actual original native queue attempt is sticky");
        var custody=NativeField<object>(server,"HostCustody");var record=new RebirthRemoteResourceClientOriginalJournal(NativeField<string>(custody,"Root")).Read(operation);
        Require(record?.State==RebirthCamperTransferJournal.Phase.Uncertain&&record.Before.SequenceEqual(NativeField<byte[]>(custody,"Anchor")),"v2 Prepared consumed before queue/input original; nativeBefore equality cannot reopen receipt1");
    }
}
