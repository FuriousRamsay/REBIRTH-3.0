using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

// Authored, not run. Actual canonical native payment frames are required;
// private codec reflection here grants no Begin/save/owner mutation authority.
internal static class RemoteNativeStationPaymentFrameChecks
{
    internal static void FetchedPreflight(EntityPlayer actualActor,RebirthCamperStationState actualStation,RebirthCamperStationAddress address,string creation,
        RebirthRemoteResourceFetchedStationPlan.Plan actualDetachedPlan,byte[] independentlyCapturedBeforeFrame,byte[] independentlyCapturedAfterFrame)
    {
        var clock=actualStation.lastTickTime;long revision=actualStation.Revision;var rows=(RecipeQueueItem[])actualStation.Queue.Clone();
        string live=Convert.ToBase64String(RebirthCamperStationSnapshot.Capture(address.Installation,actualStation));
        Require(RebirthCamperStationNativeActorPayment.TryPreflightFetchedFrames(actualActor,actualStation,address,creation,actualDetachedPlan,out var before,out var after),
            "actual paired-frame preflight");
        Require(before.SequenceEqual(independentlyCapturedBeforeFrame)&&after.SequenceEqual(independentlyCapturedAfterFrame),"exact native coupled frames, not a layout-size estimate");
        Require(actualStation.lastTickTime==clock&&actualStation.Revision==revision&&rows.Where((row,index)=>!ReferenceEquals(row,actualStation.Queue[index])).Count()==0&&
            live==Convert.ToBase64String(RebirthCamperStationSnapshot.Capture(address.Installation,actualStation)),"preflight mutated live clock/queue/stock");
        // Required vectors include >9 rich paid fragments (native count is
        // Int32), full owner bag with zero local remainder, exact mixed local
        // and fetched cost, full metadata/texture preservation, metadata key
        // capacity and paired8MiB UTF8/base64 boundary. No source payment or
        // native application is performed by this fixture.
    }
    internal static void SavedPublication(World actualWorld,Guid actualOperation,string actualAddress,string originalOwnerKey,string originalCreation,
        int originalDeaths,int independentlyObservedQueueSlot,string independentlyObservedRecipe)
    {
        Require(RebirthRemoteResourceStationPaymentWitness.TryReadSavedPayment(actualWorld,actualOperation,actualAddress,originalOwnerKey,originalCreation,out var saved),
            "actual private publication and complete native artifacts required");
        Require(saved.OriginalDeaths==originalDeaths&&saved.QueueSlot==independentlyObservedQueueSlot,"original publication life and native slot");
        var first=saved.RestoreAfter();var row=first.Queue[saved.QueueSlot];
        Require(row?.Recipe?.GetName()==independentlyObservedRecipe,"independently observed saved native recipe binding");
        first.Queue[saved.QueueSlot]=null;
        Require(saved.RestoreAfter().Queue[saved.QueueSlot]?.Recipe?.GetName()==independentlyObservedRecipe,"detached caller cannot rewrite original publication");
        Require(!RebirthRemoteResourceStationPaymentWitness.TryReadSavedPayment(actualWorld,actualOperation,actualAddress,originalOwnerKey+"-wrong",originalCreation,out _),"wrong original owner refused");
        // Separate actual disposable vectors must withhold/corrupt one native
        // artifact or original definition, replace later live installation,
        // and retain a legacy publication: none may infer historical queue
        // from the later current station or Applied alone. Never alter real
        // user saves merely to prepare these deferred vectors.
    }
    internal static void FetchedRetirementOrdering(EntityPlayerLocal actualOwner,Guid actualJob,Action deliverActualRoot4Used,
        Action deliverActualNativeRetirement,Action duplicateActualNativeRetirement)
    {
        Require(RebirthRemoteResourceOwnerAction.HasFetchedStationHandoff(actualJob),"actual paid original owner hold before retirement");
        Require(!RebirthCamperStationNativeActorPayment.TryGetSavedFetchedRetirement(actualOwner,actualJob,out _),"no invented saved retirement before native receipt");
        deliverActualRoot4Used();
        Require(RebirthRemoteResourceOwnerAction.HasFetchedStationHandoff(actualJob),"Root4 arriving before native retirement cannot release original owner custody");
        deliverActualNativeRetirement();
        Require(RebirthCamperStationNativeActorPayment.TryGetSavedFetchedRetirement(actualOwner,actualJob,out _),"positive actual final native retired1 plus exact owner After");
        deliverActualRoot4Used();
        Require(!RebirthRemoteResourceOwnerAction.HasFetchedStationHandoff(actualJob),"saved native retirement and durable own terminal mirror release original hold");
        var before=RebirthCamperEnergyActorImage.Capture(actualOwner);
        duplicateActualNativeRetirement();
        Require(before.Matches(RebirthCamperEnergyActorImage.Capture(actualOwner)),"lost ACK retry is metadata-only, with no second owner debit/refund/cursor mutation");
        // Delegates must deliver captured real authenticated native/domain
        // packages through actual dispatch, never fabricate a saved proof.
        // Separate vectors reorder duplicate Root4 replies, interrupt retirement
        // save, replace peer/lease and alter immutable frames. NOT EXECUTED.
    }
    internal static void FetchedNativeCapacity(EntityVehicle actualVehicle,RebirthRemoteResourceDurableJournal.Record actualSource,
        RebirthRemoteResourceFetchedStationPlan.Plan actualPlan,int independentlySerializedExpectedBytes)
    {
        var item=RebirthNativeItemCodec.Encode(actualVehicle.vehicle.itemValue);
        var bag=RebirthCamperInventorySnapshot.Capture(actualVehicle.bag.ItemGrid.items);
        Require(RebirthRemoteResourceFetchedStationCapacity.TryMeasure(actualVehicle,actualSource,actualPlan,out int measured),"actual native candidate serialization supported");
        Require(measured==independentlySerializedExpectedBytes&&measured<=ushort.MaxValue,"exact independent full native entity byte length");
        Require(item==RebirthNativeItemCodec.Encode(actualVehicle.vehicle.itemValue)&&bag.SequenceEqual(RebirthCamperInventorySnapshot.Capture(actualVehicle.bag.ItemGrid.items)),
            "readonly measurement did not adopt candidate module metadata or source cargo");
        // Actual vectors must include original same-vehicle source cargo and
        // exact next actual-effect ledger, existing/prunable ledger capacity,
        // rich metadata/paint, native65535 boundary and after-bound rejection.
        // A queue-only estimate or before-source Entity length is insufficient.
    }
    internal static void FetchedPreparedWait(EntityPlayer actualOwner,RebirthRemoteResourceDurableJournal.Record actualRoot,
        RebirthCamperStationState actualStation,Guid actualJob,Action publishActualBeforeVehicleSave)
    {
        var first=RebirthRemoteResourceFetchedStationOriginal.PrepareBeforeSources(actualOwner,actualRoot);
        Require(first==RebirthRemoteResourceFetchedStationOriginal.PreparationStatus.Waiting,"unpublished native save is waiting, never unused");
        Require(RebirthCamperStationNativeActorPayment.MatchesPendingOperation(actualStation,actualJob),"whole-vehicle native Pending precedes source payment");
        Require(RebirthRemoteResourceDurableJournal.TryRead(actualRoot.Owner,actualRoot.Operation,out var waiting,out bool unknown)&&!unknown&&waiting.Phase<=1&&
            waiting.Copy().Element("ownerAction")==null&&waiting.Copy().Element("sources").Elements("source").All(node=>(int)node.Attribute("attempted")==0),
            "waiting native publication grants neither source attempts nor owner permission");
        publishActualBeforeVehicleSave();
        Require(RebirthRemoteResourceFetchedStationOriginal.PrepareBeforeSources(actualOwner,waiting)==RebirthRemoteResourceFetchedStationOriginal.PreparationStatus.Ready,
            "positive actual saved original artifacts allow source progression");
        // Deferred cold vectors restore the SAME native Prepared record and
        // immutable plan, then authenticate the original lease/life. After a
        // same-vehicle source debit, retained before artifacts must remain
        // readable without comparing old cargo against the later current save.
        // Missing/changed artifacts, lease, definition or life remain unknown.
    }
    internal static void FetchedNativeFragmentFrame(Guid actualOriginal,string actualCanonicalFrameResult)
    {
        var xml=XElement.Parse(actualCanonicalFrameResult);Require(xml.Name=="fetchedStationNativeFrame","actual domain frame envelope");
        byte[] payload=Convert.FromBase64String(xml.Element("payload").Value);
        Require(payload.Length<=8*1024*1024,"raw native payload bound");
        var bytes=new UTF8Encoding(false,true).GetBytes(actualCanonicalFrameResult);Require(bytes.Length<=12*1024*1024,"separate bounded result without paid-array duplication");
        var body=new RebirthRemoteResourceFragments.Body(bytes);Require(body.Count>1,"fixture requires actual multi-fragment native frame");
        string scope="deferred-fetched-native|"+Guid.NewGuid().ToString("N");byte[] complete=null;
        for(int index=body.Count-1;index>=0;index--)
        {
            Require(RebirthRemoteResourceFragments.TryAdd(scope,actualOriginal,body.Hash,body.Length,body.Count,index,body.Part(index),1d,out complete),"reordered bounded fragment accepted");
            if(index>0)
            {Require(complete==null,"partial native payload cannot reach domain receiver");Require(RebirthRemoteResourceFragments.TryAdd(scope,actualOriginal,body.Hash,body.Length,body.Count,index,body.Part(index),1d,out var duplicate)&&duplicate==null,"duplicate part grants no completion");}
        }
        Require(complete!=null&&complete.SequenceEqual(bytes),"complete immutable body reconstructed exactly once");
        // This pure framing check deliberately grants no native setter or
        // retirement. Genuine MP cases separately require final .ttp proof.
    }
    internal static void FetchedUnusedEvidence(RebirthRemoteResourceDurableJournal.Record actualPaidUnusedRoot)
    {
        var root=actualPaidUnusedRoot.Copy();
        Require((int?)root.Attribute("ownerRejecting")==1&&root.Element("ownerAction")==null,"original monotone rejection without issued permission");
        Require(RebirthRemoteResourceFetchedStationOriginal.ValidateFetchedNoUseRoot(root),"actual retained native5/fullBefore event plus never-entered native Rejected history");
        var forged=new XElement(root);forged.SetAttributeValue("ownerNoUseProof",new string('a',64));
        Require(!RebirthRemoteResourceFetchedStationOriginal.ValidateFetchedNoUseRoot(forged),"arbitrary caller hex is not no-use authority");
        var issued=new XElement(root);issued.Add(new XElement("ownerAction",new XAttribute("domain","native-mobile-station")));
        Require(!RebirthRemoteResourceFetchedStationOriginal.ValidateFetchedNoUseRoot(issued),"existing permission excludes original-unused path");
        var changed=new XElement(root);changed.SetAttributeValue("deaths",(int)root.Attribute("deaths")+1);
        Require(!RebirthRemoteResourceFetchedStationOriginal.ValidateFetchedNoUseRoot(changed),"no ended-life inference from a same-life no-use receipt");
        // Actual fixture vectors additionally retain .1 candidates/native
        // Uncertain, missing preflight artifacts, wrong original lease, a full
        // refund bag, and crash after event-before-Rejected-before-root proof.
        // Refund completion still requires native child2, never source rollback.
    }
    internal static void FetchedColdReceipt(EntityPlayerLocal actualOwner,Guid actualEffect,byte[] actualAuthenticatedNativePayload,
        RebirthCamperEnergyActorImage originalBefore,RebirthCamperEnergyActorImage originalAfter,bool existingAppliedPayload,int expectedReceipt)
    {
        var live=RebirthCamperEnergyActorImage.Capture(actualOwner);
        Require(RebirthCamperStationNativeActorPayment.TryReadFetchedClientRecovery(actualOwner,actualEffect,actualAuthenticatedNativePayload,
            originalBefore,originalAfter,existingAppliedPayload,out int receipt)&&receipt==expectedReceipt,"actual same-primary original receipt permits only named cold binding");
        Require(live.Matches(RebirthCamperEnergyActorImage.Capture(actualOwner)),"readonly cold witness changes no native inventory/cursor");
        if(expectedReceipt==1||expectedReceipt==2)
            Require(!RebirthCamperStationNativeActorPayment.TryReadFetchedClientRecovery(actualOwner,actualEffect,actualAuthenticatedNativePayload,
                originalBefore,originalAfter,false,out _),"partial/applied recovery without prior immutable Applied payload remains unknown");
        var wrong=new byte[actualAuthenticatedNativePayload.Length];Array.Copy(actualAuthenticatedNativePayload,wrong,wrong.Length);
        if(wrong.Length>0)wrong[0]=0;
        Require(!RebirthCamperStationNativeActorPayment.TryReadFetchedClientRecovery(actualOwner,actualEffect,wrong,originalBefore,originalAfter,existingAppliedPayload,out _),
            "changed authenticated native frame cannot authorize rebind");
        // Actual MP vectors use lost/replaced local lease and actor/peer refs,
        // then run real RetainClientPayload/TryApplyRemote. Wrong source10,
        // native1 with mixed images, missing saved native2, changed cursor/locks,
        // another life/world/creation and foreign holds must remain unknown.
    }
    internal static void FetchedUnpaidCancellation(EntityPlayer actualOwner,RebirthRemoteResourceClientOriginal.Intent actualOriginal,
        RebirthRemoteResourceDurableJournal.Record actualUnpaidRoot,Func<byte[]> captureActualNativeSources)
    {
        Require(actualUnpaidRoot.Phase<=1&&actualUnpaidRoot.Copy().Element("sources").Elements("source").All(node=>(int)node.Attribute("attempted")==0),
            "actual preeffect original required");
        byte[] sourceBefore=captureActualNativeSources();var ownerBefore=RebirthCamperEnergyActorImage.Capture(actualOwner);
        Require(RebirthRemoteResourceFetchedStationOriginal.RefuseUnpaid(actualOwner,actualOriginal),"exact known original publishes monotone no-effect refusal");
        Require(RebirthRemoteResourceDurableJournal.TryRead(actualUnpaidRoot.Owner,actualUnpaidRoot.Operation,out var refused,out bool unknown)&&!unknown&&refused.Phase==3&&
            (int)refused.Copy().Attribute("outcome")==2&&refused.Copy().Element("ownerAction")==null&&
            RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(refused.Copy().Element("removed").Value)).Length==0&&
            refused.Copy().Element("sources").Elements("source").All(node=>(int)node.Attribute("attempted")==0),"no delayed source/native permission after refusal");
        Require(sourceBefore.SequenceEqual(captureActualNativeSources())&&ownerBefore.Matches(RebirthCamperEnergyActorImage.Capture(actualOwner)),
            "refusal does not mutate owner/source inventory");
        // Native metadata retirement requires actual current same-job lease and
        // exact immutable Prepared frames; orphan/.1/temp candidates remain
        // unknown. Absent-root tests separately require the global tombstone
        // before ACK and must never manufacture a source root or refund.
    }
    internal static void FetchedServerReconnect(EntityPlayer actualNewOwner,RebirthCamperStationState actualStation,Guid job,byte[] before,byte[] after,
        bool expectUsed,Action installActualFreshLease,Func<bool> resumeActualPending)
    {
        var owner=RebirthCamperEnergyActorImage.Capture(actualNewOwner);var station=RebirthCamperStationSnapshot.Capture(actualStation.InstallationId,actualStation);
        Require(RebirthRemoteResourceFetchedStationOriginal.CanRebindNativeServer(actualNewOwner,actualStation,job,before,after,out bool used)&&used==expectUsed,
            "retained source root/frames/artifacts and same-primary original owner required");
        Require(owner.Matches(RebirthCamperEnergyActorImage.Capture(actualNewOwner))&&station.SequenceEqual(RebirthCamperStationSnapshot.Capture(actualStation.InstallationId,actualStation)),
            "reconnect proof itself has no inventory/station effects");
        installActualFreshLease();Require(resumeActualPending(),"actual ended actor AND sender references may rebind through named native TryResume");
        // Separate actual-world vectors retain one old reference alive, replace
        // definition/life/cursor/locks, remove native10, mutate mixed cells or
        // insert an orphan journal candidate. Every one must refuse binding.
    }
    internal static void FetchedPreparedReconnect(EntityPlayer owner,RebirthCamperStationState station,Guid job,byte[] before,byte[] after,bool expected,
        Func<byte[]> captureSources,Func<bool> resumeActualEndedReferences)
    {
        var inventory=RebirthCamperEnergyActorImage.Capture(owner);byte[] sources=captureSources();
        Require(RebirthRemoteResourceFetchedStationOriginal.CanRebindPreparedServer(owner,station,job,before,after)==expected,"exact never-entered pre-source/no-effect branch");
        Require(inventory.Matches(RebirthCamperEnergyActorImage.Capture(owner))&&sources.SequenceEqual(captureSources()),"prepared/refusal proof has no owner/source effects");
        if(expected)Require(resumeActualEndedReferences(),"actual same original replaces ended refs and reruns native tri-state");
        // Distinct fixtures supply root0/1 +same-file native10/0/fullBefore,
        // and root3/4 no-effect AFTER legitimate owner movement. Neither can
        // use .1/temp/corrupt history, source attempts or an ownerAction.
    }
    internal static void FetchedCancellationRecovery(EntityPlayer serverOwner,RebirthCamperStationState station,Guid cancellation,byte[] before,byte[] after,
        ItemStack[] beforeBag,ItemStack[] beforeBelt,ItemStack[] afterBag,ItemStack[] afterBelt,bool[] locks,Func<byte[]> capturePaidSources)
    {
        byte[] sourceBefore=capturePaidSources();var actorBefore=RebirthCamperEnergyActorImage.Capture(serverOwner);
        Require(RebirthRemoteResourceFetchedStationOriginal.CanRebindCancellationServer(serverOwner,station,cancellation,before,after,beforeBag,beforeBelt,afterBag,afterBelt,locks,out _,out _),
            "actual derived cancellation/Root4/remaining rich fragments/native frame/read-only same-primary proof");
        Require(sourceBefore.SequenceEqual(capturePaidSources())&&actorBefore.Matches(RebirthCamperEnergyActorImage.Capture(serverOwner)),"cancellation recovery never rolls back sources or grants an inventory delta itself");
        // Include a real native cycleRecipeQueue rotation before cancellation:
        // its actual current row slot may differ from original enqueue slot.
    }
    internal static void FetchedCancellationClient(EntityPlayerLocal owner,Guid originalJob,Guid nativeEffect,Guid cancellation,byte[] authenticatedPayload,
        bool expected,Func<byte[]> captureOriginalSource)
    {
        var before=RebirthCamperEnergyActorImage.Capture(owner);byte[] sources=captureOriginalSource();
        Require(RebirthCamperStationNativeActorPayment.TryRetainFetchedCancellation(owner,originalJob,nativeEffect,authenticatedPayload)==expected,"persist exact authenticated cancellation payload before setters");
        Require(before.Matches(RebirthCamperEnergyActorImage.Capture(owner))&&sources.SequenceEqual(captureOriginalSource()),"payload admission performs no native/source mutation");
        if(expected)Require(RebirthRemoteResourceFetchedStationOriginal.HasCancellationPayload(cancellation),"positive immutable Applied cancellation payload exists before native callback");
        // Cold native2 requires PRE-EXISTING payload; changed raw frames,
        // native1/mixed image, missing queue/refund proof, different life,
        // orphan payload/temp and foreign holds are independent denial vectors.
    }
    internal static void FetchedLateCancellationTerminal(EntityPlayerLocal owner,Guid parent,Guid child,Guid retirementEffect,byte[] exactAuthenticatedPayload)
    {
        Require(!RebirthCamperStationWindowSession.TryGetLeaseIdentity(owner,out _),"actual station UI closed after previously saved native retired1");
        var before=RebirthCamperEnergyActorImage.Capture(owner);
        Require(RebirthCamperStationNativeActorPayment.TryRetainFetchedCancellation(owner,parent,retirementEffect,exactAuthenticatedPayload),"late duplicate observes immutable prior payload and SAME primary retired1 without inventory regrant");
        Require(RebirthCamperStationNativeActorPayment.TryApplyRemote(owner,retirementEffect,exactAuthenticatedPayload),"terminal ACK only after legitimate later native inventory/locks movement");
        Require(before.Matches(RebirthCamperEnergyActorImage.Capture(owner)),"late metadata ACK performs no inventory/cursor setter");
        Require(RebirthRemoteResourceFetchedStationOriginal.HasCancellationPayload(child),"terminal evidence remains after transient lease pruning");
        var altered=(byte[])exactAuthenticatedPayload.Clone();if(altered.Length>0)altered[0]=0;
        Require(!RebirthCamperStationNativeActorPayment.TryApplyRemote(owner,retirementEffect,altered),"changed original cannot inherit terminal receipt");
    }
    internal static void FetchedEndedZeroEffect(EntityPlayer newLife,RebirthCamperStationState station,RebirthRemoteResourceDurableJournal.Record oldRoot,
        Guid job,byte[] before,byte[] after,Func<byte[]> captureSources,Func<byte[]> captureNativeStationContents,Action advanceActualNativeTick)
    {
        Require(newLife.Died>(int)oldRoot.Copy().Attribute("deaths")&&oldRoot.Phase<=1&&oldRoot.Copy().Element("sources").Elements("source").All(node=>(int)node.Attribute("attempted")==0),"actual ended original life with no source attempts");
        var actor=RebirthCamperEnergyActorImage.Capture(newLife);byte[] sources=captureSources(),contents=captureNativeStationContents();
        Require(RebirthRemoteResourceFetchedStationOriginal.PrepareEndedZeroEffect(station,job,before,after),"positive exact ended zero-effect source refusal");
        Require(RebirthRemoteResourceDurableJournal.TryRead(oldRoot.Owner,oldRoot.Operation,out var refused,out bool unknown)&&!unknown&&refused.Phase==3&&
            (int)refused.Copy().Attribute("outcome")==2&&refused.Copy().Element("ownerAction")==null,"durable source3 precedes native rejection");
        var journal=new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativeActorPayments"));
        Require(journal.Read(job)?.State==RebirthCamperTransferJournal.Phase.Prepared,"own source helper does not forge native rejection or apply queue");
        advanceActualNativeTick();Require(journal.Read(job)?.State==RebirthCamperTransferJournal.Phase.Rejected,"actual native metadata rejection after source3");
        Require(actor.Matches(RebirthCamperEnergyActorImage.Capture(newLife))&&sources.SequenceEqual(captureSources())&&contents.SequenceEqual(captureNativeStationContents()),"no new-life inventory/refund/source/queue writes");
        // Attempted source, nonempty removed, any ownerAction/.1/temp, changed
        // stationBefore, owner/world/creation mismatch must all stay retained.
    }
    internal static void FetchedEndedZeroObservation(EntityPlayer newLife,RebirthRemoteResourceClientOriginal.Intent original)
    {
        var owner=RebirthCamperEnergyActorImage.Capture(newLife);
        Require(RebirthRemoteResourceFetchedStationOriginal.ReadEndedZeroTerminal(newLife,original,out var root,out var proof)&&root.Phase==3&&proof?.Length==64,
            "actual source3 plus exact immutable nativeRejected and never-effect history, not current owner inventory inference");
        Require(owner.Matches(RebirthCamperEnergyActorImage.Capture(newLife)),"historical metadata query performs no current-life setters");
        var altered=original.Copy();altered.SetAttributeValue("creation",original.Creation+"changed");
        Require(!RebirthRemoteResourceFetchedStationOriginal.ReadEndedZeroTerminal(newLife,new RebirthRemoteResourceClientOriginal.Intent(original.Operation,altered),out _,out _),"changed scope cannot inherit ended terminal metadata");
        Require(RebirthRemoteResourceDurableJournal.TryRead(root.Owner,root.Operation,out var unchanged,out bool unknown)&&!unknown&&unchanged.Phase==3,
            "read-only bootstrap query does not silently promote source4 or grant payment");
    }
    internal static void FetchedEndedZeroAck(EntityPlayer newLife,RebirthRemoteResourceClientOriginal.Intent original,Action advanceUnrelatedStation)
    {
        var image=RebirthCamperEnergyActorImage.Capture(newLife);
        Require(RebirthRemoteResourceFetchedStationOriginal.AcknowledgeEndedZeroTerminal(newLife,original),"stage9 exact nativeRejected and no-effects revalidation");
        Require(RebirthRemoteResourceFetchedStationOriginal.ReadEndedZeroTerminal(newLife,original,out var terminal,out _)&&terminal.Phase==4&&
            (string)terminal.Copy().Attribute("disposition")=="unused-no-payment","positive source4 only after metadata ACK");
        advanceUnrelatedStation();
        Require(RebirthRemoteResourceFetchedStationOriginal.AcknowledgeEndedZeroTerminal(newLife,original),"lost ACK retry after unrelated station progress");
        Require(image.Matches(RebirthCamperEnergyActorImage.Capture(newLife)),"ended ACK has no current-life owner mutation");
    }
    internal static void FetchedEndedClientMirror(EntityPlayerLocal actualOwner,RebirthRemoteResourceClientOriginal.Intent original,string authenticatedTerminal,Action addExactActionCandidate)
    {
        var image=RebirthCamperEnergyActorImage.Capture(actualOwner);
        Require(RebirthRemoteResourceFetchedStationOriginal.ValidateEndedClientTerminal(actualOwner,original,authenticatedTerminal),"actual authenticated ended terminal shape and exact original scope");
        addExactActionCandidate();
        Require(!RebirthRemoteResourceClientOriginal.CompleteEndedFetchedNoEffect(actualOwner,original,authenticatedTerminal),"unknown exact local action candidate retains intent");
        Require(image.Matches(RebirthCamperEnergyActorImage.Capture(actualOwner)),"candidate refusal does not rewrite current-life inventory/cursor");
    }
    internal static void FetchedEndedClientTerminalDuplicate(EntityPlayerLocal actualOwner,RebirthRemoteResourceClientOriginal.Intent original,string authenticatedTerminal)
    {
        var image=RebirthCamperEnergyActorImage.Capture(actualOwner);
        Require(RebirthRemoteResourceClientOriginal.CompleteEndedFetchedNoEffect(actualOwner,original,authenticatedTerminal),"authenticated ended terminal journal before exact intent mirror");
        Require(RebirthRemoteResourceClientOriginal.CompleteEndedFetchedNoEffect(actualOwner,original,authenticatedTerminal),"duplicate metadata terminal is idempotent");
        Require(RebirthRemoteResourceClientOriginal.TryRead(original.Operation,out var retained)&&retained.Digest==original.Digest,"terminal preserves original immutable input");
        Require(image.Matches(RebirthCamperEnergyActorImage.Capture(actualOwner)),"terminal duplicate cannot grant native items/cursor");
    }
    internal static void FetchedRetirementWithoutLease(EntityPlayerLocal actualOwner,Guid job,RebirthCamperStationAddress address,string creation,
        Guid retirementEffect,byte[] authenticatedRetirementPayload,Action advanceActualStationQueue)
    {
        Require(!RebirthCamperStationWindowSession.TryGetLeaseIdentity(actualOwner,out _),"actual closed UI lease, not a synthetic lease bypass");
        Require(RebirthRemoteResourceFetchedStationOriginal.HasRetirementMetadataBinding(actualOwner,job,address,creation,authenticatedRetirementPayload),
            "authenticated host-used attestation and pre-existing Applied original payload required");
        advanceActualStationQueue();var stationOwner=RebirthCamperEnergyActorImage.Capture(actualOwner);
        Require(RebirthCamperStationNativeActorPayment.TryApplyRemote(actualOwner,retirementEffect,authenticatedRetirementPayload),"metadata-only native retirement after legitimate queue progression");
        Require(stationOwner.Matches(RebirthCamperEnergyActorImage.Capture(actualOwner)),"retirement changes no inventory/cursor");
        Require(RebirthCamperStationNativeActorPayment.TryGetSavedFetchedRetirement(actualOwner,job,out _),"positive actual final native retired1 precedes Root4 mirror");
        // Lost Root4-before-native-retirement and reversed replies rerun this
        // exact same original; no source permission, setter or queue replay.
    }
    private static readonly MethodInfo Decode=typeof(RebirthCamperStationNativeActorPayment).GetMethod("Decode",BindingFlags.Static|BindingFlags.NonPublic);
    internal static void FetchedFrozenHold(RebirthCamperStationState actualStation,Guid actualJob,Action attemptUnrelatedNativeQueueMutation,
        Action advanceActualVehicleTick,Func<bool> beginActualSameOriginal)
    {
        Require(RebirthRemoteResourceFetchedStationOriginal.BlocksStation(actualStation),"actual retained pre-source hold required, no synthetic flag");
        Require(RebirthRemoteResourceFetchedStationOriginal.MatchesFrozenBefore(actualStation),"initial full frozen native Before");
        attemptUnrelatedNativeQueueMutation();advanceActualVehicleTick();
        // Clock normalization is the sole permitted paused-tick change; exact
        // unrelated queue/tools/fuel/input/output must stay at the frozen image.
        Require(actualStation.lastTickTime==0,"whole native fuel/queue clock paused before processing");
        Require(RebirthRemoteResourceFetchedStationOriginal.MatchesFrozenBefore(actualStation),"unrelated native queue/tools/fuel/input/output remain exact frozen image");
        Require(RebirthRemoteResourceFetchedStationOriginal.BlocksStation(actualStation),"unrelated mutation cannot release original custody");
        Require(beginActualSameOriginal(),"actual authenticated same-original native handoff");
        Require(!RebirthRemoteResourceFetchedStationOriginal.BlocksStation(actualStation)&&RebirthCamperStationNativeActorPayment.MatchesPendingOperation(actualStation,actualJob),
            "native Pending already owns station before pre-source hold retires");
        // Deferred harness separately attempts different job/source/binding, replaced lease,
        // different lifetime, changed installed definition and unknown artifact.
        // This method is authored only; no callback/fixture has been executed.
    }
    internal static void FetchedCancellation(EntityPlayer actualOwner,RebirthCamperStationState actualStation,RebirthCamperStationAddress address,
        string creation,Guid job,ItemStack[] independentlyExpectedUnconsumedNativeFragments)
    {
        Require(RebirthRemoteResourceFetchedStationOriginal.TryPlanCancellation(actualOwner,actualStation,address,creation,job,out var plan),
            "actual source Root4-used and saved native publication must precede cancellation planning");
        var actual=plan.CopyRefunds();
        Require(RebirthCamperInventorySnapshot.Capture(actual).SequenceEqual(RebirthCamperInventorySnapshot.Capture(independentlyExpectedUnconsumedNativeFragments)),
            "exact remaining rich native fragments, not type-only replacement or whole original regrant");
        if(actual.Length>0){actual[0].count=0;Require(plan.CopyRefunds()[0].count>0,"detached refund cannot mutate original owed image");}
        // Required genuine-native vectors: partially completed batches with
        // distinct metadata per source, all-fetch/fullbag, mixed bag/belt/fetch,
        // foreign owner/life, corrupt own marker, changed row and unavailable
        // saved artifact. This method performs no queue/owner/source mutation.
    }
    private static readonly MethodInfo Encode=typeof(RebirthCamperStationNativeActorPayment).GetMethod("Encode",BindingFlags.Static|BindingFlags.NonPublic);
    private static void Require(bool value,string label){if(!value)throw new Exception(label);}
    private static void Reject(XElement image,string label)
    {
        try{Decode.Invoke(null,new object[]{Encoding.UTF8.GetBytes(image.ToString(SaveOptions.DisableFormatting))});}
        catch(TargetInvocationException error)when(error.InnerException is InvalidDataException){return;}
        throw new Exception(label);
    }
    internal static void Run(byte[] actualLegacyFrame,byte[] actualNewFrame,int originalDeaths)
    {
        Require(Decode!=null&&Encode!=null,"actual native payment codecs available");
        var legacy=XElement.Parse(new UTF8Encoding(false,true).GetString(actualLegacyFrame));
        var fresh=XElement.Parse(new UTF8Encoding(false,true).GetString(actualNewFrame));
        Require(legacy.Attributes().Count()==8&&fresh.Attributes().Count()==9,"actual encoder's eight base fields plus one v2 death field");
        Require((int)legacy.Attribute("version")==1&&legacy.Attribute("deaths")==null,"actual legacy frame has no invented lifetime");
        Require((int)fresh.Attribute("version")==2&&(int)fresh.Attribute("deaths")==originalDeaths,"new original lifetime captured before payment");
        foreach(var frame in new[]{actualLegacyFrame,actualNewFrame})
        {var value=Decode.Invoke(null,new object[]{frame});Require(((byte[])Encode.Invoke(null,new[]{value})).SequenceEqual(frame),"exact v1/v2 persisted metadata compatibility");}
        var missing=new XElement(fresh);missing.Attribute("deaths").Remove();Reject(missing,"missing lifetime cannot be inferred from current player");
        var negative=new XElement(fresh);negative.SetAttributeValue("deaths",-1);Reject(negative,"negative original lifetime refused");
        var relabelled=new XElement(legacy);relabelled.SetAttributeValue("version",2);Reject(relabelled,"legacy bytes cannot silently become a new-life original");
        var extra=new XElement(legacy);extra.SetAttributeValue("deaths",originalDeaths);Reject(extra,"legacy v1 cannot carry unvalidated lifetime trailer");
    }
}
