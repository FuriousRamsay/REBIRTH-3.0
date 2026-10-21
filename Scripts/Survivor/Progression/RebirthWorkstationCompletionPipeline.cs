using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Original native delivery observer + durable notice custody. No path here
// executes/replays crafting or derives payment authority from an output receipt.
internal static partial class RebirthWorkstationCompletionPipeline
{
    internal sealed class Frame
    {
        internal RebirthRecipeOutputIntegration.OwningObservation Observation;
        internal XElement Intent;internal string Path;internal bool Prepared,Attempted,Saved,Closed;
        internal string Owner,Creation;internal double Retry;
        internal RebirthWorkstationCompletionOriginal ConfirmedOriginal;
        internal bool NativeAccepted;
        internal bool NeedsContinuationBefore;
        internal long SavedFullBoundaryVersion;
        internal bool CachedEffectTransferEnqueued;
        internal long CachedEffectTransferVersion;
        internal bool CachedBoundarySelected;
        internal bool CachedBoundaryEnqueued;internal long CachedBoundaryVersion;
        internal RebirthProductiveEarnedUiQueueOwner.Witness EarlierOwner;
        internal NativeAddOriginal NativeOriginal;
        internal NativeViewerEffectSource NativeViewerEffect;
        internal bool SameOwnerTerminalAckSaved,SameOwnerTerminalReleased;internal string SameOwnerTerminalDigest;
        internal float SameOwnerTerminalNextQuery;
        internal Guid RemoteNativeNonce;internal string RemoteNativeSourceHash;
        internal NativeBeforePublication NativeBefore;
        internal LocalNativeBefore LocalBefore;
        internal CachedPrimaryReentry CachedReentry;
        internal CachedPhysicalSource CachedPhysical;
        internal bool PrepareUploadSent;
    }
    private static readonly ConditionalWeakTable<XUiC_RecipeStack,Frame> cards=new ConditionalWeakTable<XUiC_RecipeStack,Frame>();
    private static readonly Dictionary<Guid,Frame> frames=new Dictionary<Guid,Frame>();
    [ThreadStatic]private static Frame current;
    private static readonly Dictionary<string,XElement> prepared=new Dictionary<string,XElement>(StringComparer.Ordinal);
    private static readonly Dictionary<string,string> preparedPaths=new Dictionary<string,string>(StringComparer.Ordinal);
    private sealed class NativeToken{internal readonly string Value=Guid.NewGuid().ToString("N");}
    private static readonly ConditionalWeakTable<object,NativeToken> nativeTokens=new ConditionalWeakTable<object,NativeToken>();
    private sealed class Outgoing
    {internal World World;internal object Peer;internal bool ToServer;internal int Viewer,Index;internal Guid Operation;internal RebirthRemoteResourceFragments.Body Body;internal string Challenge,Kind;}
    private static readonly List<Outgoing> outgoing=new List<Outgoing>();
    private static double nextPump;private static bool pumping;
    private static string NativeScope(object native)=>nativeTokens.GetOrCreateValue(native).Value;
    private static string ClientChallenge()
    {var world=GameManager.Instance?.World;var peers=SingletonMonoBehaviour<ConnectionManager>.Instance?.GetConnectionToServer();return world?.IsRemote()==true&&peers!=null&&peers.Length>0&&peers[0]!=null?NativeScope(world)+NativeScope(peers[0]):null;}
    private static bool Eligible(XUiC_RecipeStack card)=>RebirthSurvivorMode.IsEnabledForCurrentWorld()&&card?.recipe!=null&&card.AmountToRepair==0&&
        (card.OriginalItem==null||card.OriginalItem.IsEmpty())&&(!card.recipe.IsScrap||RebirthRecipeOutputIntegration.HasNativeScrapOutputs(card.recipe))&&
        !RebirthCookingBatch.IsBatch(card.recipe)&&card.windowGroup?.Controller is XUiC_WorkstationWindowGroup group&&
        !(group.WorkstationData?.TileEntity is RebirthCamperStationState)&&
        (RebirthRecipeOutputIntegration.HasNativeScrapOutputs(card.recipe)||!string.IsNullOrEmpty(RebirthServiceCraftSkillService.ClassifyRecipe(card.recipe)));
    internal static bool HandlesOwningCard(XUiC_RecipeStack card)=>Eligible(card);
    internal static object CaptureFrames()=>frames.Values.Select(frame=>new{operation=frame.Observation.Operation.ToString("N"),
        world=frame.Observation.WorldGuid,prepared=frame.Prepared,attempted=frame.Attempted,saved=frame.Saved,nativeAccepted=frame.NativeAccepted,
        original=frame.ConfirmedOriginal?.Digest,before=RebirthWorkstationCompletionOriginal.Hash(ActiveBefore(frame.Intent).ToString(SaveOptions.DisableFormatting))}).ToArray();
    internal static bool AllowOutput(XUiC_RecipeStack card)
    {return !Eligible(card)||current?.Observation?.Card==card&&!current.Observation.Retry&&current.Prepared&&current.Attempted&&current.Saved;}
    internal static bool Begin(XUiC_RecipeStack card,out Frame frame)
    {
        frame=null;if(!Eligible(card))return true;
        bool cachedAdmitted=TryBeginCachedPhysical(card,out var cachedHandled,out frame);if(cachedHandled)return cachedAdmitted;
        // The first countdown crossing is held at outputStack BEFORE its native
        // body. The next Update starts at the actual completion boundary.
        if(!card.isInventoryFull&&card.GetRecipeCraftingTimeLeft()>0)return true;
        try
        {
            if(cards.TryGetValue(card,out var retained))
            {
                if(retained.NeedsContinuationBefore&&!CaptureContinuationBefore(retained))return false;
                if(retained.Observation.RanUpdate)
                {
                    // Keep the actual full-boundary generation alive while its
                    // saved owner-effect and same native transfer packet are
                    // pending. Replacing it would invalidate both producers.
                    if(retained.Observation.World.IsRemote()&&retained.RemoteNativeNonce!=Guid.Empty&&
                        (retained.NativeOriginal==null&&retained.NativeViewerEffect==null||!retained.CachedBoundaryEnqueued||retained.CachedBoundaryVersion!=retained.Observation.NativeVersionAfter||
                         retained.NativeViewerEffect!=null&&!NativeViewerCachedEffectSaved(retained)||!retained.CachedEffectTransferEnqueued||
                         retained.CachedEffectTransferVersion!=retained.Observation.NativeVersionAfter))return false;
                    // A positive native full-output boundary alone permits the
                    // same original to continue. Unknown attempts remain held.
                    if(retained.Intent.Elements("cachedContinuation").Count()>=64||
                        !RebirthRecipeOutputIntegration.TryContinueOwningCached(retained.Observation,out var successor))return false;
                    retained.Observation=successor;
                    retained.CachedEffectTransferEnqueued=false;
                    retained.CachedBoundarySelected=retained.CachedBoundaryEnqueued=false;
                    retained.NeedsContinuationBefore=true;
                    if(!CaptureContinuationBefore(retained))return false;
                }
                if(!RebirthRecipeOutputIntegration.ResumeOwningObservation(retained.Observation))return false;
                frame=retained;
            }
            else
            {
                // Freeze no preimage until the actual native introduction is
                // published. Remote attestation can arrive after the first
                // completion boundary; a saved unknown-owner frame could not
                // later acquire an earlier birth from current ID equality.
                var window=card.GetParentByType<XUiC_WorkstationWindowGroup>();
                var station=window?.WorkstationData?.TileEntity;
                if(station!=null)
                {
                    int index=Array.FindIndex(window.craftingQueue.GetRecipesToCraft(),value=>ReferenceEquals(value,card));
                    if(index<0||index>=station.Queue.Length||!RebirthProductiveEarnedUiQueueOwner.TryObserveQueueOwner(station,index,station.Queue[index],out var initialOwner)||
                        !initialOwner.Current()||!initialOwner.SourcePublished())return false;
                }
                if(!RebirthRecipeOutputIntegration.CanFitOwningCompletion(card,card.isInventoryFull))return false;
                var observation=RebirthRecipeOutputIntegration.BeginOwningObservation(card,card.isInventoryFull);
                if(observation==null)return false;
                if(!observation.TryCopyBeforeEvidence(out var before)||frames.Count>=1024)
                {RebirthRecipeOutputIntegration.EndOwningObservation(observation);observation.Dispose();return false;}
                if(!observation.World.IsRemote())
                {
                    var rows=observation.Station.Queue;int slot=observation.Index;
                    if(rows==null||slot<0||slot>=rows.Length||rows[slot]?.Recipe==null||
                        !RebirthRemoteResourcePlacedStationQueue.TryDecode(observation.RecipeFull,out var frozen)||frozen.Length!=1||
                        RecipeBinding(rows[slot])!=RecipeBinding(frozen[0])||rows[slot].Multiplier!=observation.BeforeCount)
                    {RebirthRecipeOutputIntegration.EndOwningObservation(observation);observation.Dispose();return false;}
                }
                frame=new Frame{Observation=observation,Owner=observation.StartingOwnerKey,Creation=observation.StartingOwnerCreation};
                string role=observation.World.IsRemote()?"client":"server";
                frame.Path=IntentPath(role,observation.Operation);
                frame.Intent=new XElement("workstationCompletionIntent",new XAttribute("version",1),new XAttribute("operation",observation.Operation.ToString("N")),
                    new XAttribute("world",observation.WorldGuid),new XAttribute("prepared",false),new XAttribute("attempted",false),
                    new XAttribute("owner",frame.Owner??""),new XAttribute("creation",frame.Creation??""),new XElement("before",before));
                cards.Add(card,frame);
                frames.Add(observation.Operation,frame);
            }
            current=frame;
            if(!frame.Saved&&!SaveFrame(frame))return false;
            if(frame.Observation.World.IsRemote())
            {
                if(!frame.Prepared)
                {
                    if(UnityEngine.Time.realtimeSinceStartup>=frame.Retry)
                    {frame.Retry=UnityEngine.Time.realtimeSinceStartup+1;
                        if(!frame.PrepareUploadSent){frame.PrepareUploadSent=true;Upload(frame.Observation);}
                        Send(true,new XElement("prepare",frame.Intent),frame.Observation.Operation,frame.Observation.Viewer.entityId);}
                    return false;
                }
                var remoteRows=frame.Observation.Station.Queue;int remoteIndex=frame.Observation.Index;
                if(remoteRows==null||remoteIndex<0||remoteIndex>=remoteRows.Length||
                    !RebirthProductiveEarnedUiQueueOwner.TryObserveRemoteNativeSource(card,frame.Observation.Station,remoteIndex,remoteRows[remoteIndex],
                        (string)frame.Intent.Attribute("queueOwnerPurpose"),out var remoteOwner)||
                    remoteOwner.OriginalOwnerKey!=frame.Owner||remoteOwner.Creation!=frame.Creation||!remoteOwner.Current()||!remoteOwner.SourcePublished())return false;
                frame.EarlierOwner=remoteOwner;
            }
            else if(!frame.Prepared)
            {
                var native=frame.Observation.Station.Queue;int slot=frame.Observation.Index;
                // Earlier native insertion/publication supplies original birth.
                // A current entity-ID lookup cannot establish that history.
                if(native==null||slot<0||slot>=native.Length||
                    !RebirthProductiveEarnedUiQueueOwner.TryObserveQueueOwner(frame.Observation.Station,slot,native[slot],out var queuedOwner)||
                    !queuedOwner.Current()||queuedOwner.StartingEntityId!=frame.Observation.StartingOwnerEntityId)return false;
                if(!CorroborateQueuedOwner(frame.Observation.World,queuedOwner))return false;
                frame.Owner=queuedOwner.OriginalOwnerKey;frame.Creation=queuedOwner.Creation;
                frame.EarlierOwner=queuedOwner;
                frame.Intent.SetAttributeValue("owner",frame.Owner);frame.Intent.SetAttributeValue("creation",frame.Creation);
                if(native==null||slot<0||slot>=native.Length||native[slot]?.Recipe==null||
                    !RebirthRemoteResourcePlacedStationQueue.TryDecode(frame.Observation.RecipeFull,out var frozen)||frozen.Length!=1||
                    RecipeBinding(native[slot])!=RecipeBinding(frozen[0])||native[slot].Multiplier!=frame.Observation.BeforeCount)return false;
                frame.Prepared=true;if(!SaveFrame(frame))return false;
            }
            if(!frame.Observation.World.IsRemote()&&(frame.EarlierOwner?.Current()!=true||!CorroborateQueuedOwner(frame.Observation.World,frame.EarlierOwner)))return false;
            if(!RebirthProductiveEarnedUiDeferredTransport.CanAdmitAuthorityNative(frame))return false;
            if(!frame.Observation.World.IsRemote()&&(frame.CachedReentry!=null?!frame.CachedReentry.CurrentBefore():!TryPrepareLocalNativeBefore(frame)))return false;
            if(frame.RemoteNativeNonce!=Guid.Empty&&(frame.NativeBefore==null||!frame.NativeBefore.Current()||
                !RebirthProductiveEarnedUiCoordinator.AcceptNativeBeforePublication(frame.NativeBefore)))return false;
            if(!EnsureNativeViewerBefore(frame))return false;
            frame.Attempted=true;return SaveFrame(frame); // durable BEFORE native Update is admitted
        }
        catch
        {
            if(frame!=null&&(!frames.TryGetValue(frame.Observation.Operation,out var published)||!ReferenceEquals(published,frame)))
            {
                if(cards.TryGetValue(card,out var attached)&&ReferenceEquals(attached,frame))cards.Remove(card);
                RebirthRecipeOutputIntegration.EndOwningObservation(frame.Observation);frame.Observation.Dispose();frame=null;
            }
            return false;
        }
    }
    private static bool CaptureContinuationBefore(Frame frame)
    {
        if(!frame.Observation.TryCopyBeforeEvidence(out var continued))return false;
        frame.LocalBefore?.Dispose();frame.LocalBefore=null;frame.NativeBefore=null;
        frame.Intent.Add(new XElement("cachedContinuation",continued));
        frame.Prepared=false;frame.Saved=false;frame.NeedsContinuationBefore=false;return true;
    }
    private static bool SaveFrame(Frame frame)
    {
        frame.Saved=false;frame.Intent.SetAttributeValue("prepared",frame.Prepared);frame.Intent.SetAttributeValue("attempted",frame.Attempted);
        frame.Intent.SetAttributeValue("owner",frame.Owner??"");frame.Intent.SetAttributeValue("creation",frame.Creation??"");
        frame.Intent.SetAttributeValue("nativeNonce",frame.RemoteNativeNonce==Guid.Empty?"":frame.RemoteNativeNonce.ToString("N"));
        frame.Intent.SetAttributeValue("nativeSource",frame.RemoteNativeSourceHash??"");
        if(!SaveXml(frame.Path,frame.Intent))return false;
        var retained=frame.Observation.RemoteOutputRestart?.RetainedCached;
        if(retained!=null&&!retained.Current()&&(frame.ConfirmedOriginal==null||!frame.Observation.RanUpdate||!frame.Observation.Updated||
            !frame.Observation.Accepted&&!frame.Observation.RemoteFinalQueueObserved||!frame.Observation.Taken||!frame.Observation.Current(true)||!frame.Observation.RecipeMatches()||
            !retained.ObserveConfirmedFramePublication(frame.ConfirmedOriginal.Write(),frame.Intent)))return false;
        frame.Saved=true;return true;
    }
    private static string IntentPath(string role,Guid operation)=>Path.Combine(RebirthWorkstationCompletionOutbox.Root(role),"intents",operation.ToString("N")+".xml");
    internal static void After(XUiC_RecipeStack card,bool runOriginal,Frame frame)
    {
        if(frame==null)return;
        RebirthRecipeOutputIntegration.ObserveOwningUpdate(card,runOriginal,frame.Observation);
        if(!runOriginal||!frame.Attempted||!frame.Saved)return;
        frame.LocalBefore?.Dispose();frame.LocalBefore=null;
        SaveActualCachedFullBoundary(frame);
        ObserveNativeViewerFullBoundary(frame);
        SelectRemoteCachedBoundary(frame);
        if(!RebirthRecipeOutputIntegration.TryTakeOwningDelivery(frame.Observation,out var delivery))return;
        try
        {
            RebirthProductiveEarnedUiCoordinator.ObserveOwningDelivery(delivery);
            RebirthProductiveEarnedUiOwnerFile.ObserveNativeDelivery(delivery);
            if(!RebirthWorkstationCompletionOriginal.TryCreate(delivery,frame.Owner,frame.Creation,GameUtils.WorldTimeToDays(delivery.World.GetWorldTime()),out var original))return;
            if(frame.Observation.RemoteOutputRestart?.FinalCallbackSaved==true)
            {
                if(!RebirthWorkstationCompletionOriginal.TryRead(frame.Intent.Element("confirmedOriginal")?.Elements().SingleOrDefault(),out var prior))return;
                var candidate=original.Write();candidate.SetAttributeValue("day",prior.Day);
                if(!XNode.DeepEquals(candidate,prior.Write()))return;original=prior;
            }
            frame.ConfirmedOriginal=original;
            frame.Intent.SetElementValue("confirmedOriginal",null);
            frame.Intent.Add(new XElement("confirmedOriginal",original.Write()));
            SaveFrame(frame); // retain actual delivery even if later native synchronization is unknown
            if(frame.RemoteNativeNonce!=Guid.Empty)ResumePhysicalNativePackets(frame,RemoteNativePhase.PhysicalAccepted);
            if(!delivery.World.IsRemote())return; // local custody requires the actual native sync observer below
            string role=delivery.World.IsRemote()?"client":"server";
            var state=RebirthWorkstationCompletionOutbox.Retain(original,role,frame.CachedPhysical==null?delivery.World:null,frame.CachedPhysical==null?delivery.Station:null);
            if(state==null)return;
            if(frame.RemoteNativeNonce==Guid.Empty)
            {cards.Remove(card);frames.Remove(frame.Observation.Operation);frame.Closed=true;frame.NativeOriginal?.ReleaseFrame();frame.NativeViewerEffect?.ReleaseFrame();}
            if(state.Saved)ArchiveIntent(role,frame.Observation.Operation);
            if(!delivery.World.IsRemote())Settle(state);
            // Remote snapshots/notice are pumped AFTER the enclosing native UI
            // update has synchronized its actual decremented queue to the model.
        }
        catch{/* Confirmed original/frame is retained; observation failure cannot replace native Update behavior. */}
        finally{delivery.Dispose();}
    }
    internal sealed class NativeSync
    {
        internal Frame Frame;internal World World;internal TileEntityWorkstation Station;
        internal EntityPlayerLocal Viewer;internal string Queue,Output,Bag,Belt,Equipment;
        internal object LocalCached;
        internal object CachedClose;
        internal object CachedEmpty;
        internal CachedUnusedClose CachedUnused;
    }
    internal static void BeforeNativeSync(XUiC_WorkstationWindowGroup group,out NativeSync snapshot)
    {
        snapshot=null;
        try
        {
            if(CanSyncCachedUnused(group)){closingCachedUnused.SyncEntered=true;snapshot=new NativeSync{CachedUnused=closingCachedUnused};return;}
            if(cachedClose?.Prepared==true&&ReferenceEquals(cachedClose.Window,group)&&cachedClose.Source.NativeCurrent())
            {snapshot=new NativeSync{CachedClose=cachedClose};return;}
            var empty=CaptureSavedCachedEmptySync(group);if(empty!=null){snapshot=new NativeSync{CachedEmpty=empty};return;}
            var frame=frames.Values.SingleOrDefault(value=>value.ConfirmedOriginal!=null&&!value.NativeAccepted&&
                (!value.Observation.World.IsRemote()||value.RemoteNativeNonce!=Guid.Empty)&&ReferenceEquals(value.Observation.Window,group));
            if(frame==null||!ThreadManager.IsMainThread())return;var observation=frame.Observation;var world=GameManager.Instance?.World;
            if(!ReferenceEquals(world,observation.World)||world.GetBlock(observation.Position).type!=observation.BlockType||
                observation.Viewer.Died!=observation.ViewerDeaths||!RebirthSurvivorRequestScope.Matches(observation.ViewerCreation,RebirthCamperCharacterAccess.CurrentCreation(observation.Viewer))||
                !ReferenceEquals(world.GetTileEntity(observation.Position),observation.Station)||
                !ReferenceEquals(world.GetEntity(observation.Viewer.entityId),observation.Viewer)||!ReferenceEquals(group.WorkstationData.TileEntity,observation.Station))return;
            snapshot=new NativeSync{Frame=frame,World=world,Station=observation.Station,Viewer=observation.Viewer,
                Queue=RebirthRemoteResourcePlacedStationQueue.Encode(observation.Station.Queue),Output=Grid(observation.Station.Output),
                Bag=Grid(observation.Viewer.bag.ItemGrid.items),Belt=Grid(observation.Viewer.inventory.ItemGrid.items),
                Equipment=Convert.ToBase64String(RebirthRecipeOutputIntegration.OwningEquipmentNative(observation.Viewer))};
            snapshot.LocalCached=RebirthProductiveEarnedUiOwnerFile.CaptureLocalCachedSync(group,frame.NativeOriginal);
            if(frame.Observation.World.IsRemote()&&frame.RemoteNativeNonce!=Guid.Empty)remoteNativeSync=snapshot;
        }
        catch{snapshot=null;}
    }
    internal static void AfterNativeSync(XUiC_WorkstationWindowGroup group,bool runOriginal,NativeSync snapshot)
    {
        try
        {
            if(snapshot?.CachedUnused is CachedUnusedClose unused)
            {
                if(runOriginal&&ReferenceEquals(unused,closingCachedUnused)&&unused.Current()&&unused.Enqueued&&unused.Setups==1)
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                    if(decoder!=null&&RebirthProductiveEarnedNativeBody.MatchesCurrent(unused.Source.Station,unused.After,unused.AfterOffset)&&
                        unused.Source.Station.lastTickTime==GameTimer.Instance.ticks)unused.SyncRan=true;
                return;
            }
            if(snapshot?.CachedEmpty!=null){ObserveSavedCachedEmptySync(group,runOriginal,snapshot.CachedEmpty);return;}
            if(snapshot?.CachedClose is CachedClose closing)
            {
                if(runOriginal&&ReferenceEquals(cachedClose,closing)&&closing.Prepared)
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                    if(decoder!=null&&RebirthProductiveEarnedNativeBody.MatchesCurrent(closing.Source.Station,closing.After,closing.Offset))closing.SyncObserved=true;
                return;
            }
            if(!runOriginal||snapshot==null||!ThreadManager.IsMainThread()||!ReferenceEquals(GameManager.Instance?.World,snapshot.World)||
                snapshot.Viewer.Died!=snapshot.Frame.Observation.ViewerDeaths||
                !RebirthSurvivorRequestScope.Matches(snapshot.Frame.Observation.ViewerCreation,RebirthCamperCharacterAccess.CurrentCreation(snapshot.Viewer))||
                snapshot.World.GetBlock(snapshot.Frame.ConfirmedOriginal.Position).type!=snapshot.Frame.Observation.BlockType||
                !ReferenceEquals(group.WorkstationData.TileEntity,snapshot.Station)||
                !ReferenceEquals(snapshot.World.GetTileEntity(snapshot.Frame.ConfirmedOriginal.Position),snapshot.Station)||
                !ReferenceEquals(snapshot.World.GetEntity(snapshot.Viewer.entityId),snapshot.Viewer))return;
            var frame=snapshot.Frame;var before=ActiveBefore(frame.Intent);if(before==null)return;
            var delivery=frame.ConfirmedOriginal.Write().Element("delivery").Elements().Single();
            if(delivery.Element("equipmentAfter")!=null&&
                (snapshot.Equipment!=(string)delivery.Element("equipmentAfter")||
                snapshot.Equipment!=Convert.ToBase64String(RebirthRecipeOutputIntegration.OwningEquipmentNative(snapshot.Viewer))))return;
            if(snapshot.Bag!=(string)delivery.Element("bagAfter")||snapshot.Belt!=(string)delivery.Element("beltAfter")||
                Grid(snapshot.Viewer.bag.ItemGrid.items)!=snapshot.Bag||Grid(snapshot.Viewer.inventory.ItemGrid.items)!=snapshot.Belt)return;
            // UpdateData may already have installed the physically observed
            // output before queue synchronization. Require that exact accepted
            // delivery image here; do not claim sync inserted it a second time.
            if(snapshot.Output!=(string)delivery.Element("outputAfter")||!QueueStableEqual(snapshot.Queue,(string)before.Element("nativeQueueFull"))||
                !QueueDecrement((string)before.Element("nativeQueueFull"),RebirthRemoteResourcePlacedStationQueue.Encode(snapshot.Station.Queue),(string)before.Element("recipeFull")))return;
            var accepted=new XElement(frame.Intent);accepted.Element("tileAccepted")?.Remove();accepted.Element("inventoryAccepted")?.Remove();
            accepted.SetAttributeValue("blockName",snapshot.Station.block.GetBlockName());
            accepted.Add(new XElement("tileAccepted",new XElement("outputBefore",(string)before.Element("outputBefore")),new XElement("outputAfter",Grid(snapshot.Station.Output)),
                new XElement("nativeSyncOutputBefore",snapshot.Output),
                new XElement("queueBefore",snapshot.Queue),new XElement("queueAfter",RebirthRemoteResourcePlacedStationQueue.Encode(snapshot.Station.Queue))));
            // Inventory insertion already happened inside the observed original
            // Update. Its full accepted before/after is frozen in that delivery.
            if((string)delivery.Attribute("sink")=="Inventory")
            {
                if(snapshot.Bag!=(string)delivery.Element("bagAfter")||snapshot.Belt!=(string)delivery.Element("beltAfter")||
                    Grid(snapshot.Viewer.bag.ItemGrid.items)!=snapshot.Bag||Grid(snapshot.Viewer.inventory.ItemGrid.items)!=snapshot.Belt)return;
                accepted.Add(new XElement("inventoryAccepted",new XElement("bagBefore",(string)before.Element("bagBefore")),new XElement("bagAfter",snapshot.Bag),
                    new XElement("beltBefore",(string)before.Element("beltBefore")),new XElement("beltAfter",snapshot.Belt)));
                if(delivery.Element("equipmentAfter")!=null)
                {
                    accepted.Element("inventoryAccepted").Add(new XElement("equipmentBefore",(string)before.Element("equipmentBefore")),
                        new XElement("equipmentAfter",snapshot.Equipment));
                }
            }
            if(!ValidateDelivery(accepted,frame.ConfirmedOriginal))return;
            frame.Intent=accepted;frame.NativeAccepted=true;SaveFrame(frame);
            ObserveCachedRecipientSync(frame);
            RebirthProductiveEarnedUiDeferredTransport.AuthorityNativePublication.ObserveSync(frame,snapshot);
            ObserveCachedPhysicalAccepted(snapshot);
            if(frame.RemoteNativeNonce!=Guid.Empty)ResumePhysicalNativePackets(frame,RemoteNativePhase.QueueAccepted);
            RebirthProductiveEarnedUiOwnerFile.ObserveLocalCachedSync(group,frame.NativeOriginal,snapshot.LocalCached);
        }
        catch{} // observer failure never supplies effect authority
        finally{EndRemoteNativeSync(snapshot);}
    }
    internal static void End(XUiC_RecipeStack card,bool runOriginal,Frame frame)
    {
        if(frame==null)return;
        try{RebirthRecipeOutputIntegration.NoteOwningUpdateExit(card,runOriginal,frame.Observation);}
        catch{/* No positive continuation is granted by a failed observer. */}
        finally
        {
            if(runOriginal){frame.LocalBefore?.Dispose();frame.LocalBefore=null;}
            RebirthRecipeOutputIntegration.EndOwningObservation(frame.Observation);if(ReferenceEquals(current,frame))current=null;
        }
    }
    private static void Upload(RebirthRecipeOutputIntegration.OwningObservation observation)
    {
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ReferenceEquals(observation.World,GameManager.Instance?.World)||!ReferenceEquals(observation.Viewer.world,observation.World)||
            !ReferenceEquals(observation.World.GetEntity(observation.Viewer.entityId),observation.Viewer)||
            !ReferenceEquals(observation.World.GetTileEntity(observation.Position),observation.Station))return;
        connection.SendToServer(NetPackageManager.GetPackage<NetPackageTileEntity>().Setup(observation.Station,StreamModeWrite.ToServer));
        connection.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerInventory>().Setup(observation.Viewer,true,true,false,false));
    }
    internal static bool SaveXml(string path,XElement image)
    {
        if(!RebirthWorkstationCompletionPublication.Write(path,image))return false;
        return XNode.DeepEquals(image,RebirthWorkstationCompletionPublication.ReadPrimary(path));
    }
    private static XElement LoadXml(string path)
    {return RebirthWorkstationCompletionPublication.ReadPrimary(path);}
    private static string RecordKey(string operation)=>RebirthWorkstationCompletionOutbox.Root("server")+"|"+operation;
    private static XElement ActiveBefore(XElement intent)=>intent.Elements("cachedContinuation").LastOrDefault()?.Elements().SingleOrDefault()??intent.Element("before")?.Elements().SingleOrDefault();
    private static void ArchiveIntent(string role,Guid operation)
    {
        string source=IntentPath(role,operation),target=Path.Combine(RebirthWorkstationCompletionOutbox.Root(role),"intentArchive",operation.ToString("N")+".xml");
        RebirthWorkstationCompletionPublication.RequireSafeDirectory(Path.GetDirectoryName(source));
        RebirthWorkstationCompletionPublication.RequireSafeDirectory(Path.GetDirectoryName(target));
        RebirthWorkstationCompletionPublication.RequireClearDestination(target);
        if(File.Exists(source))
        {
            LoadXml(source);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            RebirthWorkstationCompletionPublication.RequireSafeDirectory(Path.GetDirectoryName(target));
            if(File.Exists(target)){if(!XNode.DeepEquals(LoadXml(source),LoadXml(target)))throw new InvalidDataException("Original intent archive conflict");}
            else File.Move(source,target);
        }
        if(role=="server"){string key=RecordKey(operation.ToString("N"));prepared.Remove(key);preparedPaths.Remove(key);}
    }
    private static XElement GetPrepared(string operation)
    {
        string key=RecordKey(operation);if(prepared.TryGetValue(key,out var value))return value;
        if(!Guid.TryParseExact(operation,"N",out var id)||id==Guid.Empty)return null;
        string path=IntentPath("server",id);if(!File.Exists(path))path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"intentArchive",operation+".xml");if(!File.Exists(path))return null;
        value=LoadXml(path);if((string)value.Attribute("operation")!=operation||(string)value.Attribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(GameManager.Instance?.World))return null;
        prepared.Add(key,value);preparedPaths.Add(key,path);return value;
    }
    internal static void Receive(bool server,NetPackage packet,World world,int viewer,Guid operation,string hash,int total,int count,int index,string part)
    {
        try
        {
            var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(!ThreadManager.IsMainThread()||world==null||!ReferenceEquals(GameManager.Instance?.World,world)||world.IsRemote()==server||connection==null)return;
            EntityPlayer player=world.GetEntity(viewer) as EntityPlayer;object peer;
            if(player==null||!ReferenceEquals(player.world,world))return;
            if(server)
            {
                if(!connection.IsServer||packet.Sender==null||!packet.ValidEntityIdForSender(viewer)||
                    !ReferenceEquals(connection.Clients?.ForEntityId(viewer),packet.Sender)||!packet.Sender.loginDone||packet.Sender.disconnecting||
                    player==null||!ReferenceEquals(player.world,world))return;
                peer=packet.Sender;
            }
            else
            {var transports=connection.GetConnectionToServer();if(connection.IsServer||transports==null||transports.Length==0||transports[0]==null||!ReferenceEquals(world.GetPrimaryPlayer(),player))return;peer=transports[0];}
            RebirthStablePlayerIdentity identity;
            if(!(server?RebirthStablePlayerIdentity.TryFromClientInfo(packet.Sender,out identity):RebirthStablePlayerIdentity.TryFromLocalPlatform(out identity))||
                !RebirthSurvivorRequestScope.TryNormalize(RebirthCamperCharacterAccess.CurrentCreation(player),out var creation))return;
            string scope="mp015|"+NativeScope(world)+"|"+NativeScope(peer)+"|"+identity.StorageKey+"|"+creation;
            if(!RebirthRemoteResourceFragments.TryAdd(scope,operation,hash,total,count,index,part,UnityEngine.Time.realtimeSinceStartup,out var complete)||complete==null)return;
            if(complete.Length>12*1024*1024)return;
            XElement body;using(var stream=new MemoryStream(complete,false))using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=12*1024*1024}))body=XElement.Load(reader);
            if(server)
            {
                if(body.Name=="prepare")PrepareServer(player,packet.Sender,identity.StorageKey,creation,operation,body);
                else if(body.Name=="complete")CompleteServer(player,identity.StorageKey,creation,operation,body);
                else if(body.Name=="nativeViewer")ReceiveNativeViewer(player,packet.Sender,identity.StorageKey,creation,operation,body);
                else if(body.Name=="nativeOwnerQuery")QueryRemoteNativeOwner(player,packet.Sender,identity.StorageKey,creation,operation,body);
                else if(body.Name=="nativeSameOwnerQuery")QuerySameOwnerTerminal(player,packet.Sender,identity.StorageKey,creation,operation,body);
                else if(body.Name=="cachedPrimaryQuery")QueryCachedPrimarySettled(player,operation,body);
                else if(body.Name=="cachedOutputFinalQuery")QueryCachedPrimarySettled(player,operation,body);
                else if(body.Name=="cachedPhysicalPrepare")PrepareCachedPhysical(player,packet.Sender,operation,body);
                else if(body.Name=="cachedUnusedClose")ReceiveCachedUnusedClose(player,packet.Sender,operation,body);
                else if(body.Name=="cachedViewer")ReceiveCachedViewer(player,packet.Sender,identity.StorageKey,creation,operation,body);
                else if(body.Name=="cachedOutputRestart")ReceiveCachedOutputRestart(player,packet.Sender,identity.StorageKey,creation,operation,body);
                else if(body.Name=="remoteOutputRestart")ReceiveRemoteOutputRestart(player,packet.Sender,identity.StorageKey,creation,operation,body);
            }
            else if(body.Name=="ack")AcceptAck(player,operation,body);
        }
        catch{/* Original files/confirmed custody remain; no malformed body grants a stage. */}
    }
    private static void PrepareServer(EntityPlayer viewer,ClientInfo sender,string viewerKey,string creation,Guid operation,XElement message)
    {
        var request=message.Element("workstationCompletionIntent");var before=request==null?null:ActiveBefore(request);
        if(request==null||before?.Name!="nativeOwningBefore"||(string)request.Attribute("operation")!=operation.ToString("N")||
            (string)before.Attribute("operation")!=operation.ToString("N")||(string)before.Attribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(viewer.world)||
            (string)before.Attribute("viewer")!=viewerKey||!RebirthSurvivorRequestScope.Matches(creation,(string)before.Attribute("creation")))return;
        var retained=GetPrepared(operation.ToString("N"));
        if(retained!=null)
        {
            if(!XNode.DeepEquals(retained.Element("before"),request.Element("before")))return;
            var retainedPosition=new Vector3i((int)before.Attribute("x"),(int)before.Attribute("y"),(int)before.Attribute("z"));
            var retainedTile=viewer.world.GetTileEntity(retainedPosition) as TileEntityWorkstation;
            // Durable prepare is evidence, never a permit to enter a replacement
            // native World/body. Cold accepted originals use Complete instead.
            if(retainedTile==null||(string)retained.Attribute("nativeScope")!=NativeScope(viewer.world)+NativeScope(retainedTile)+NativeScope(viewer))return;
            if(XNode.DeepEquals(ActiveBefore(retained),before))
            {
                int retainedSlot=(int)before.Attribute("slot");
                if(retainedTile.Queue==null||retainedSlot<0||retainedSlot>=retainedTile.Queue.Length||
                    !RebirthProductiveEarnedUiQueueOwner.TryObserveQueueOwner(retainedTile,retainedSlot,retainedTile.Queue[retainedSlot],out var originalOwner)||
                    originalOwner.OriginalOwnerKey!=(string)retained.Attribute("owner")||
                    !RebirthSurvivorRequestScope.Matches(originalOwner.Creation,(string)retained.Attribute("creation"))||!CorroborateQueuedOwner(viewer.world,originalOwner))return;
                string purpose=RebirthProductiveEarnedUiQueueOwner.Export(originalOwner);if(string.IsNullOrEmpty(purpose)||purpose!=(string)retained.Attribute("queueOwnerPurpose"))return;
                if(!TryPrepareNativeRemoteSource(viewer,sender,viewerKey,creation,operation,retainedTile,originalOwner,request,out var native))return;
                Ack(viewer,operation,"prepared",(string)retained.Attribute("owner"),(string)retained.Attribute("creation"),RebirthWorkstationCompletionOriginal.Hash(before.ToString(SaveOptions.DisableFormatting)),(string)message.Attribute("challenge"),purpose,native);return;
            }
            var prior=retained.Elements("cachedContinuation").ToArray();var next=request.Elements("cachedContinuation").ToArray();
            if(next.Length!=prior.Length+1||next.Length>64||prior.Where((value,i)=>!XNode.DeepEquals(value,next[i])).Any()||
                retained.Element("tileAccepted")!=null||before.Element("cachedPredecessor")==null)return;
            var first=retained.Element("before").Elements().Single();
            foreach(string field in new[]{"operation","world","viewer","creation","startingOwner","x","y","z","block","slot","beforeCount","extrasDefinition"})
                if((string)before.Attribute(field)!=(string)first.Attribute(field))return;
            if(!QueueStableEqual((string)before.Element("nativeQueueFull"),(string)first.Element("nativeQueueFull")))return;
        }
        var position=new Vector3i((int)before.Attribute("x"),(int)before.Attribute("y"),(int)before.Attribute("z"));
        var station=viewer.world.GetTileEntity(position) as TileEntityWorkstation;
        int slot=(int)before.Attribute("slot");
        if(station==null||(viewer.position-station.ToWorldCenterPos()).sqrMagnitude>144f||viewer.world.GetBlock(position).type!=(int)before.Attribute("block")||
            !RebirthProductiveEarnedNativeBody.MatchesCurrent(station,Convert.FromBase64String((string)before.Element("nativeBefore")),(int)before.Attribute("nativeElapsedOffset"))||
            station.Queue==null||slot<0||slot>=station.Queue.Length||station.Queue[slot]?.Recipe==null||station.Queue[slot].Multiplier!=(int)before.Attribute("beforeCount")||
            !RebirthRemoteResourcePlacedStationQueue.TryDecode((string)before.Element("recipeFull"),out var frozen)||frozen.Length!=1||
            RecipeBinding(station.Queue[slot])!=RecipeBinding(frozen[0])||station.Queue[slot].StartingEntityId!=(int)before.Attribute("startingOwner")||
            RebirthRecipeOutputs.DefinitionIdentity(station.Queue[slot].Recipe)!=(string)before.Attribute("extrasDefinition")||
            !RebirthProductiveEarnedUiQueueOwner.TryObserveQueueOwner(station,slot,station.Queue[slot],out var queuedOwner)||!queuedOwner.Current())return;
        if(!CorroborateQueuedOwner(viewer.world,queuedOwner))return;
        if(retained!=null&&((string)retained.Attribute("owner")!=queuedOwner.OriginalOwnerKey||
            !RebirthSurvivorRequestScope.Matches((string)retained.Attribute("creation"),queuedOwner.Creation)))return;
        var saved=new XElement(request);saved.SetAttributeValue("prepared",true);saved.SetAttributeValue("owner",queuedOwner.OriginalOwnerKey);saved.SetAttributeValue("creation",queuedOwner.Creation);
        string ownerPurpose=RebirthProductiveEarnedUiQueueOwner.Export(queuedOwner);if(string.IsNullOrEmpty(ownerPurpose))return;saved.SetAttributeValue("queueOwnerPurpose",ownerPurpose);
        saved.SetAttributeValue("blockName",station.block.GetBlockName());
        saved.SetAttributeValue("nativeScope",NativeScope(viewer.world)+NativeScope(station)+NativeScope(viewer));
        saved.SetAttributeValue("viewer",viewerKey);saved.SetAttributeValue("viewerCreation",creation);
        string path=IntentPath("server",operation);if(!SaveXml(path,saved))return;
        string key=RecordKey(operation.ToString("N"));prepared[key]=saved;preparedPaths[key]=path;
        if(!TryPrepareNativeRemoteSource(viewer,sender,viewerKey,creation,operation,station,queuedOwner,request,out var nativeSource))return;
        Ack(viewer,operation,"prepared",queuedOwner.OriginalOwnerKey,queuedOwner.Creation,RebirthWorkstationCompletionOriginal.Hash(before.ToString(SaveOptions.DisableFormatting)),(string)message.Attribute("challenge"),ownerPurpose,nativeSource);
    }
    private static bool CorroborateQueuedOwner(World world,RebirthProductiveEarnedUiQueueOwner.Witness original)
    {
        // A privately restored cached branch never invokes fresh outputStack,
        // AddCraftComplete or GiveExp. Its historical owner comes from actual
        // saved cache/load custody, independently of recycled native IDs.
        if(original.CachedSource!=null||original.CachedIdentity!=null)return original.SourceCurrent();
        var body=world.GetEntity(original.StartingEntityId);
        if(world.IsRemote())
        {
            if(!original.SourceCurrent())return false;
            var viewer=world.GetPrimaryPlayer();if(viewer==null)return false;
            if(original.StartingEntityId!=viewer.entityId)
                return original.NativeHydration?.Current()==true&&original.SourcePublished();
            // Native ID equality chooses immediate local GiveExp. It must
            // never turn an earlier account/life into the current viewer.
            return ReferenceEquals(body,viewer)&&viewer.Died==original.Deaths&&
                RebirthCamperCharacterAccess.CurrentCreation(viewer)==original.Creation&&
                RebirthStablePlayerIdentity.TryFromLocalPlatform(out var nativeOwner)&&nativeOwner.StorageKey==original.OriginalOwnerKey;
        }
        return body==null||body is EntityPlayer player&&player.Died==original.Deaths&&ActualOwner(player,out var identity,out var character)&&
            identity.StorageKey==original.OriginalOwnerKey&&RebirthSurvivorRequestScope.Matches(original.Creation,character.Origin?.CreationId);
    }
    internal static bool ActualOwner(EntityPlayer player,out RebirthStablePlayerIdentity identity,out RebirthWorldCharacterRecord character)
    {
        identity=null;character=null;
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||player==null||!ReferenceEquals(player.world,world)||
            !ReferenceEquals(world.GetEntity(player.entityId),player)||
            !RebirthSkillAwardService.TryGetEligible(player,out var cached,out character)||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(character))return false;
        if(player is EntityPlayerLocal)
        {if(!RebirthStablePlayerIdentity.TryFromLocalPlatform(out identity))return false;}
        else
        {
            var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;var peer=connection?.Clients?.ForEntityId(player.entityId);
            if(connection?.IsServer!=true||peer==null||!peer.loginDone||peer.disconnecting||peer.entityId!=player.entityId||
                !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out identity))return false;
        }
        var persistent=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        if(persistent!=null)
        {if(!RebirthStablePlayerIdentity.TryFromPersistentPlayer(persistent,out var native)||native.StorageKey!=identity.StorageKey)return false;}
        else if(!(player is EntityPlayerLocal))return false;
        return identity.StorageKey==cached.StorageKey&&character.StablePlayerKey==identity.StorageKey;
    }
    internal static string RecipeBinding(RecipeQueueItem row)
    {
        if(row?.Recipe==null)return "empty";
        var identity=new RecipeQueueItem{Recipe=row.Recipe,Multiplier=1,StartingEntityId=row.StartingEntityId,Quality=row.Quality,
            OneItemCraftTime=row.OneItemCraftTime,RepairItem=row.RepairItem,AmountToRepair=row.AmountToRepair,IsCrafting=true,CraftingTimeLeft=0};
        return RebirthRemoteResourcePlacedStationQueue.Encode(new[]{identity});
    }
    private static bool QueueDecrement(string before,string after,string recipe)
    {
        if(!RebirthRemoteResourcePlacedStationQueue.TryDecode(before,out var old)||!RebirthRemoteResourcePlacedStationQueue.TryDecode(after,out var next)||
            !RebirthRemoteResourcePlacedStationQueue.TryDecode(recipe,out var frozen)||frozen.Length!=1)return false;
        var quantities=new Dictionary<string,long>(StringComparer.Ordinal);
        foreach(var row in old)if(row?.Recipe!=null&&row.Multiplier>0){string key=RecipeBinding(row);quantities.TryGetValue(key,out long count);quantities[key]=count+row.Multiplier;}
        string original=RecipeBinding(frozen[0]);if(!quantities.TryGetValue(original,out long originalCount)||originalCount<1)return false;quantities[original]=originalCount-1;
        foreach(var row in next)if(row?.Recipe!=null&&row.Multiplier>0){string key=RecipeBinding(row);if(!quantities.TryGetValue(key,out long count)||count<row.Multiplier)return false;quantities[key]=count-row.Multiplier;}
        return quantities.Values.All(value=>value==0);
    }
    private static bool QueueStableEqual(string left,string right)
    {
        if(!RebirthRemoteResourcePlacedStationQueue.TryDecode(left,out var a)||!RebirthRemoteResourcePlacedStationQueue.TryDecode(right,out var b)||a.Length!=b.Length)return false;
        for(int i=0;i<a.Length;i++)if(a[i].Multiplier!=b[i].Multiplier||RecipeBinding(a[i])!=RecipeBinding(b[i]))return false;
        return true;
    }
    internal static void ObserveAccepted(RebirthWorkstationCraftCompletion.Snapshot snapshot)
    {
        RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.ObserveInventory(snapshot);
        if(snapshot?.World==null||snapshot.World.IsRemote()||!RebirthWorkstationCompletionOriginal.ValidKey(snapshot.NativeViewerKey))return;
        string creation=RebirthCamperCharacterAccess.CurrentCreation(snapshot.Player);
        if(!RebirthSurvivorRequestScope.Matches(creation,snapshot.NativeViewerCreation))return;
        string root=RebirthWorkstationCompletionOutbox.Root("server");
        foreach(var pair in prepared.Where(pair=>pair.Key.StartsWith(root+"|",StringComparison.Ordinal)).ToArray())
        {
            var intent=pair.Value;string operation=(string)intent.Attribute("operation");if(intent==null||(string)intent.Attribute("viewer")!=snapshot.NativeViewerKey||
                !RebirthSurvivorRequestScope.Matches((string)intent.Attribute("viewerCreation"),creation))continue;
            var before=ActiveBefore(intent);if(before==null)continue;
            var originalTile=snapshot.World.GetTileEntity(new Vector3i((int)before.Attribute("x"),(int)before.Attribute("y"),(int)before.Attribute("z")));
            if(originalTile==null||(string)intent.Attribute("nativeScope")!=NativeScope(snapshot.World)+NativeScope(originalTile)+NativeScope(snapshot.Player))continue;
            XElement upload=null;
            if(snapshot.Tile!=null)
            {
                if(snapshot.Tile.ToWorldPos()!=new Vector3i((int)before.Attribute("x"),(int)before.Attribute("y"),(int)before.Attribute("z"))||
                    snapshot.NativeOutput!=(string)before.Element("outputBefore")||!QueueStableEqual(snapshot.NativeQueue,(string)before.Element("nativeQueueFull"))||
                    !QueueDecrement((string)before.Element("nativeQueueFull"),RebirthRemoteResourcePlacedStationQueue.Encode(snapshot.Tile.Queue),(string)before.Element("recipeFull")))continue;
                upload=new XElement("tileAccepted",new XElement("outputBefore",snapshot.NativeOutput),new XElement("outputAfter",Grid(snapshot.Tile.Output)),
                    new XElement("queueBefore",snapshot.NativeQueue),new XElement("queueAfter",RebirthRemoteResourcePlacedStationQueue.Encode(snapshot.Tile.Queue)));
            }
            else if(snapshot.NativeBag==(string)before.Element("bagBefore")&&snapshot.NativeBelt==(string)before.Element("beltBefore"))
            {
                if(before.Element("equipmentBefore")!=null&&snapshot.NativeEquipment!=(string)before.Element("equipmentBefore"))continue;
                upload=new XElement("inventoryAccepted",new XElement("bagBefore",snapshot.NativeBag),new XElement("bagAfter",Grid(snapshot.Player.bag.ItemGrid.items)),
                    new XElement("beltBefore",snapshot.NativeBelt),new XElement("beltAfter",Grid(snapshot.Player.inventory.ItemGrid.items)));
                if(before.Element("equipmentBefore")!=null)
                    upload.Add(new XElement("equipmentBefore",snapshot.NativeEquipment),
                        new XElement("equipmentAfter",Convert.ToBase64String(RebirthRecipeOutputIntegration.OwningEquipmentNative(snapshot.Player))));
            }
            if(upload==null)continue;
            var old=intent.Element(upload.Name);if(old!=null&&!XNode.DeepEquals(old,upload))continue;
            if(old==null){intent.Add(upload);SaveXml(preparedPaths[RecordKey(operation)],intent);}
        }
    }
    internal static bool NeedsNativeSnapshot(string viewer)
    {if(viewer==null)return false;string root=RebirthWorkstationCompletionOutbox.Root("server")+"|";return prepared.Any(pair=>pair.Key.StartsWith(root,StringComparison.Ordinal)&&(string)pair.Value.Attribute("viewer")==viewer);}
    internal static string Grid(ItemStack[] slots)=>Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(slots));
    private static void CompleteServer(EntityPlayer viewer,string viewerKey,string creation,Guid operation,XElement message)
    {
        if(!RebirthWorkstationCompletionOriginal.TryRead(message.Elements().SingleOrDefault(),out var original)||original.Operation!=operation.ToString("N")||
            original.ViewerKey!=viewerKey||!RebirthSurvivorRequestScope.Matches(original.ViewerCreation,creation))return;
        if(RebirthWorkstationCompletionOutbox.TryReadSaved(original,out var savedOriginal))
        {Ack(viewer,operation,"accepted",savedOriginal.Original.OwnerKey,savedOriginal.Original.Creation,savedOriginal.Original.Digest,(string)message.Attribute("challenge"));Settle(savedOriginal);return;}
        if(TryAcceptCachedPhysicalNotice(viewer,operation,original,(string)message.Attribute("challenge")))return;
        var intent=GetPrepared(original.Operation);if(intent==null||(string)intent.Attribute("owner")!=original.OwnerKey||
            !RebirthSurvivorRequestScope.Matches((string)intent.Attribute("creation"),original.Creation)||!ValidateDelivery(intent,original))return;
        if(!SaveXml(preparedPaths[RecordKey(original.Operation)],intent))return; // actual accepted uploads durable before custody ACK
        var station=viewer.world.GetTileEntity(original.Position) as TileEntityWorkstation;
        bool sameNative=station!=null&&(string)intent.Attribute("nativeScope")==NativeScope(viewer.world)+NativeScope(station)+NativeScope(viewer);
        string acceptedBirth=(string)original.Write().Element("delivery")?.Elements().SingleOrDefault()?.Attribute("stationBirth");
        if(string.IsNullOrEmpty(acceptedBirth))acceptedBirth=null;
        var state=RebirthWorkstationCompletionOutbox.Retain(original,"server",sameNative?viewer.world:null,sameNative?station:null,acceptedBirth);
        if(state==null||!state.Saved&&!RebirthWorkstationCompletionOutbox.Save(state))return;
        ArchiveIntent("server",operation);
        if(!RebirthWorkstationCompletionFixture.Hold("acceptedAck",original.Operation))
            Ack(viewer,operation,"accepted",original.OwnerKey,original.Creation,original.Digest,(string)message.Attribute("challenge"));
        Settle(state); // owner lookup uses durable original, never the viewer lifetime
    }
    private static bool ValidateDelivery(XElement intent,RebirthWorkstationCompletionOriginal original)
    {
        var before=ActiveBefore(intent);var delivery=original.Write().Element("delivery")?.Elements().SingleOrDefault();
        var tile=intent.Element("tileAccepted");if(before==null||delivery==null||tile==null||
            (string)delivery.Attribute("operation")!=original.Operation||(string)delivery.Attribute("world")!=original.WorldGuid||
            (int)delivery.Attribute("beforeCount")-(int)delivery.Attribute("afterCount")!=1||
            (string)delivery.Element("recipeFull")!=(string)before.Element("recipeFull"))return false;
        if(original.Block!=(string)intent.Attribute("blockName"))return false;
        foreach(string field in new[]{"viewer","creation","deaths","startingOwner","stationBirth","extrasDefinition","x","y","z","block","slot","beforeCount"})
            if((string)delivery.Attribute(field)!=(string)before.Attribute(field))return false;
        if((string)delivery.Attribute("extrasDefinition")!=(string)before.Attribute("extrasDefinition"))return false;
        foreach(string field in new[]{"outputBefore","bagBefore","beltBefore","recipeNative","equipmentBefore"})
            if((string)delivery.Element(field)!=(string)before.Element(field))return false;
        if((string)delivery.Attribute("sink")=="Output")
        {if((string)delivery.Element("outputAfter")!=(string)tile.Element("outputAfter")||
            (string)delivery.Element("bagAfter")!=(string)before.Element("bagBefore")||(string)delivery.Element("beltAfter")!=(string)before.Element("beltBefore"))return false;}
        else if((string)delivery.Attribute("sink")=="Inventory")
        {
            var inventory=intent.Element("inventoryAccepted");if(inventory==null||(string)delivery.Element("outputAfter")!=(string)before.Element("outputBefore")||
                (string)delivery.Element("bagAfter")!=(string)inventory.Element("bagAfter")||(string)delivery.Element("beltAfter")!=(string)inventory.Element("beltAfter"))return false;
            if(delivery.Element("equipmentAfter")!=null&&
                ((string)delivery.Element("equipmentBefore")!=(string)inventory.Element("equipmentBefore")||
                (string)delivery.Element("equipmentAfter")!=(string)inventory.Element("equipmentAfter")))return false;
        }
        else return false;
        return RebirthWorkstationCompletionWitnessValidator.Matches(before,delivery,original.Count);
    }
    private static void AcceptAck(EntityPlayer viewer,Guid operation,XElement ack)
    {
        if(!ThreadManager.IsMainThread()||viewer?.world?.IsRemote()!=true||
            !ReferenceEquals(GameManager.Instance?.World,viewer.world)||
            !ReferenceEquals(viewer.world.GetPrimaryPlayer(),viewer)||
            !ReferenceEquals(viewer.world.GetEntity(viewer.entityId),viewer)||
            (string)ack.Attribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(viewer.world)||
            (string)ack.Attribute("challenge")!=ClientChallenge()||
            !RebirthSurvivorRequestScope.Matches((string)ack.Attribute("viewerCreation"),RebirthCamperCharacterAccess.CurrentCreation(viewer)))return;
        if((string)ack.Attribute("stage")=="cachedOutputRestartReady"){AcceptRetainedCachedOutputRestart(viewer,operation,ack);return;}
        if((string)ack.Attribute("stage")=="cachedOutputFinalSettled"){AcceptRemoteOutputRestartAck(viewer,operation,ack);return;}
        if((string)ack.Attribute("stage")=="remoteOutputRestartReady"){AcceptRemoteOutputRestartAck(viewer,operation,ack);return;}
        if((string)ack.Attribute("stage")=="prepared")
        {
            foreach(var frame in frames.Values)
            {
                if(frame.Observation.Operation!=operation||frame.Prepared||frame.Observation.RanUpdate||
                    (string)ack.Attribute("digest")!=RebirthWorkstationCompletionOriginal.Hash(ActiveBefore(frame.Intent).ToString(SaveOptions.DisableFormatting)))continue;
                if(RebirthWorkstationCompletionOriginal.ValidKey(frame.Owner)&&
                    (frame.Owner!=(string)ack.Attribute("owner")||!RebirthSurvivorRequestScope.Matches(frame.Creation,(string)ack.Attribute("creation"))))return;
                var rows=frame.Observation.Station.Queue;int slot=frame.Observation.Index;string purpose=(string)ack.Attribute("queueOwnerPurpose");
                if(rows==null||slot<0||slot>=rows.Length||!RebirthProductiveEarnedUiQueueOwner.TryObserveRemoteNativeSource(frame.Observation.Card,frame.Observation.Station,slot,rows[slot],purpose,out var source)||
                    !source.Current()||!source.SourcePublished()||source.OriginalOwnerKey!=(string)ack.Attribute("owner")||source.Creation!=(string)ack.Attribute("creation"))return;
                frame.EarlierOwner=source;frame.Intent.SetAttributeValue("queueOwnerPurpose",purpose);
                if(!Guid.TryParseExact((string)ack.Attribute("nativeNonce"),"N",out var nonce)||nonce==Guid.Empty||
                    ((string)ack.Attribute("nativeSource"))?.Length!=64)return;
                frame.RemoteNativeNonce=nonce;frame.RemoteNativeSourceHash=(string)ack.Attribute("nativeSource");
                frame.Owner=(string)ack.Attribute("owner");frame.Creation=(string)ack.Attribute("creation");frame.Prepared=true;
                if(SaveFrame(frame)&&frame.RemoteNativeNonce!=Guid.Empty)
                {
                    authenticatedNativeBeforeAck=frame;
                    try{frame.NativeBefore=NativeBeforePublication.FromAcceptedAck(frame);}finally{authenticatedNativeBeforeAck=null;}
                }
                return;
            }
        }
        else if(((string)ack.Attribute("stage"))?.StartsWith("nativeViewer",StringComparison.Ordinal)==true||
            ((string)ack.Attribute("stage"))?.StartsWith("cachedViewer",StringComparison.Ordinal)==true)
        {AcceptNativeViewerAck(viewer,operation,ack);}
        else if((string)ack.Attribute("stage")=="cachedPhysicalPrepared"||(string)ack.Attribute("stage")=="cachedPhysicalReady"||(string)ack.Attribute("stage")=="cachedPhysicalSaved"||(string)ack.Attribute("stage")=="cachedPhysicalSpaceSaved")
        {AcceptCachedPhysicalControl(viewer,operation,ack);}
        else if((string)ack.Attribute("stage")=="cachedUnusedCloseSaved")AcceptCachedUnusedClose(viewer,operation,ack);
        else if((string)ack.Attribute("stage")=="nativeOwnerTerminal")
        {AcceptRemoteNativeOwnerAck(viewer,operation,ack);}
        else if((string)ack.Attribute("stage")=="nativeSameOwnerTerminal"||(string)ack.Attribute("stage")=="nativeSameOwnerRetired")
        {AcceptSameOwnerTerminal(viewer,operation,ack);}
        else if((string)ack.Attribute("stage")=="cachedPrimarySettled")
        {RebirthProductiveEarnedUiDeferredTransport.AcceptCachedPrimarySettled(viewer,operation,ack);}
        else if((string)ack.Attribute("stage")=="accepted")
        {
            var state=RebirthWorkstationCompletionOutbox.Pending("client").SingleOrDefault(s=>s.Original.Operation==operation.ToString("N"));
            if(state==null||state.Original.Digest!=(string)ack.Attribute("digest")||state.Original.OwnerKey!=(string)ack.Attribute("owner")||
                !RebirthSurvivorRequestScope.Matches(state.Original.Creation,(string)ack.Attribute("creation"))||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var actualViewer)||actualViewer.StorageKey!=state.Original.ViewerKey||
                !RebirthSurvivorRequestScope.Matches(state.Original.ViewerCreation,RebirthCamperCharacterAccess.CurrentCreation(viewer)))return;
            if(RebirthWorkstationCompletionOutbox.Acknowledge(state))
            {ObserveCachedPhysicalCourierAccepted(operation,state.Original.Digest);RebirthWorkstationCompletionOutbox.Archive(state);}
        }
    }
    private static void Ack(EntityPlayer viewer,Guid operation,string stage,string owner,string creation,string digest,string challenge,string purpose=null,NativeHostOriginal native=null)
    {Send(false,new XElement("ack",new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(viewer.world)),new XAttribute("viewerCreation",RebirthCamperCharacterAccess.CurrentCreation(viewer)),
        new XAttribute("stage",stage),new XAttribute("owner",owner),new XAttribute("creation",creation),new XAttribute("digest",digest),new XAttribute("challenge",challenge??""),purpose==null?null:new XAttribute("queueOwnerPurpose",purpose),
        native==null?null:new XAttribute("nativeNonce",native.Nonce.ToString("N")),native==null?null:new XAttribute("nativeSource",native.SourceHash)),operation,viewer.entityId);}
    private static void Send(bool server,XElement body,Guid operation,int viewer)
    {
        string challenge=server?ClientChallenge():(string)body.Attribute("challenge");if(challenge?.Length!=64)return;
        if(server)body.SetAttributeValue("challenge",challenge);
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;var world=GameManager.Instance?.World;
        object peer=server?(object)connection.GetConnectionToServer()[0]:connection.Clients.ForEntityId(viewer);
        string kind=body.Name+":"+(string)body.Attribute("stage");
        if(peer==null||world==null||outgoing.Any(row=>ReferenceEquals(row.World,world)&&ReferenceEquals(row.Peer,peer)&&row.ToServer==server&&row.Operation==operation&&row.Kind==kind&&row.Challenge==challenge))return;
        var bytes=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));if(bytes.Length>12*1024*1024)return;
        var fragments=new RebirthRemoteResourceFragments.Body(bytes);
        if(outgoing.Count>=8||outgoing.Sum(row=>(long)row.Body.Length)>48L*1024*1024-bytes.Length)return;
        outgoing.Add(new Outgoing{World=world,Peer=peer,ToServer=server,Viewer=viewer,Operation=operation,Body=fragments,Challenge=challenge,Kind=kind});
    }
    private static void PumpOutgoing()
    {
        int allowance=8;var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        foreach(var row in outgoing.ToArray())
        {
            var peers=row.ToServer?connection?.GetConnectionToServer():null;
            if(!ReferenceEquals(GameManager.Instance?.World,row.World)||connection==null||
                (row.ToServer?(peers==null||peers.Length==0||!ReferenceEquals(peers[0],row.Peer)||ClientChallenge()!=row.Challenge):!ReferenceEquals(connection.Clients.ForEntityId(row.Viewer),row.Peer)))
            {outgoing.Remove(row);continue;}
            while(allowance>0&&row.Index<row.Body.Count)
            {
                if(row.ToServer)connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthStationCompletionRequestV2>().Setup(row.Viewer,row.Operation,row.Body,row.Index));
                else connection.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthStationCompletionAckV2>().Setup(row.Viewer,row.Operation,row.Body,row.Index),_attachedToEntityId:row.Viewer);
                row.Index++;allowance--;
            }
            if(row.Index==row.Body.Count)outgoing.Remove(row);
            if(allowance==0)break;
        }
    }
    private static void Settle(RebirthWorkstationCompletionOutbox.State state)
    {
        if(state==null||!ThreadManager.IsMainThread()||state.Settling)return;state.Settling=true;
        try
        {
        if(state.Terminal&&state.Saved){RebirthWorkstationCompletionOutbox.Archive(state);return;}
        var original=state.Original;var world=GameManager.Instance?.World;
        if(world?.IsRemote()!=false||original.WorldGuid!=RebirthWorkstationCompletionOutbox.CurrentGuid(world)||world.Players?.list==null)return;
        var owner=world.Players.list.FirstOrDefault(player=>original.MatchesOwner(player));if(owner==null)return;
        if(!state.Saved&&!RebirthWorkstationCompletionOutbox.Save(state))return;
        if(!RebirthWorkstationCompletionOutbox.TryReadSaved(original,out var savedHead)||!ReferenceEquals(savedHead,state))return;
        if(original.NativeAdditionalScrap)
        {
            if(RebirthWorkstationCompletionOutbox.CompleteNativeScrapNoAwards(state))RebirthWorkstationCompletionOutbox.Archive(state);
            return;
        }
        if(!state.Skill)
        {
            if(RebirthWorkstationCompletionFixture.Hold("skill",original.Operation))return;
            var recipe=original.ResolveRecipe();if(recipe==null)return;string skill=RebirthServiceCraftSkillService.ClassifyRecipe(recipe);if(string.IsNullOrEmpty(skill))return;
            string receipt="wc2:"+original.Operation+":"+original.Digest;
            if(!RebirthSkillAwardService.TryGetEligible(owner,out _,out var character))return;
            var model=RebirthCraftTrainingRules.BuildModel(owner,recipe,skill);
            if(!state.SkillAttempted){state.SkillAttempted=true;state.SkillMayInvoke=true;if(!RebirthWorkstationCompletionOutbox.Save(state))return;}
            if(state.SkillRefusedBeforeEffect)
            {
                // Consume the durably recorded positive refusal before entering
                // another invocation. A crash after consumption is UNKNOWN.
                state.SkillRefusedBeforeEffect=false;state.SkillMayInvoke=true;
                if(!RebirthWorkstationCompletionOutbox.Save(state))return;
            }
            if(!state.SkillMayInvoke&&!character.Progression.SkillAwardReceipts.Contains(receipt))return;
            state.SkillMayInvoke=false;
            var outcome=RebirthSkillAwardService.TryAwardWorkstationOriginal(owner,original,skill,model);
            if(outcome==RebirthSkillAwardService.WorkstationCraftAwardOutcome.RefusedBeforeAwardEffect)
            {state.SkillRefusedBeforeEffect=true;RebirthWorkstationCompletionOutbox.Save(state);return;}
            if(outcome!=RebirthSkillAwardService.WorkstationCraftAwardOutcome.Accepted)return;
            state.Skill=true;if(!RebirthWorkstationCompletionOutbox.Save(state))return;
        }
        if(!state.Statistics)
        {
            if(RebirthWorkstationCompletionFixture.Hold("statistics",original.Operation))return;
            if(!state.StatisticsAttempted){state.StatisticsAttempted=true;state.StatisticsMayInvoke=true;if(!RebirthWorkstationCompletionOutbox.Save(state))return;}
            var statistics=RebirthStatisticsRepository.GetOrCreate(owner);if(statistics==null)return;
            if(!state.StatisticsMayInvoke&&(!statistics.WorkstationOriginalReceipts.TryGetValue(original.Operation,out var witness)||witness!=original.Digest))return;
            state.StatisticsMayInvoke=false;
            if(!RebirthStatisticsService.RecordWorkstationCraftOriginal(owner,original))return;state.Statistics=true;if(!RebirthWorkstationCompletionOutbox.Save(state))return;
        }
        if(RebirthWorkstationCompletionFixture.Hold("reagent",original.Operation)||!RebirthWorkstationReagentOriginal.Settle(state,owner))return;
        state.Terminal=true;if(RebirthWorkstationCompletionOutbox.Save(state))RebirthWorkstationCompletionOutbox.Archive(state);
        }
        finally{state.Settling=false;}
    }
    internal static void Pump()
    {
        if(pumping||!ThreadManager.IsMainThread()||UnityEngine.Time.realtimeSinceStartup<nextPump)return;
        var world=GameManager.Instance?.World;if(world==null)return;nextPump=UnityEngine.Time.realtimeSinceStartup+1;pumping=true;
        try
        {
            SweepPendingNativeWorld(world);
            foreach(var old in frames.Values.Where(value=>!ReferenceEquals(value.Observation.World,world)).ToArray())
            {
                if(cards.TryGetValue(old.Observation.Card,out var retainedCard)&&ReferenceEquals(retainedCard,old))cards.Remove(old.Observation.Card);
                frames.Remove(old.Observation.Operation);old.Closed=true;old.LocalBefore?.Dispose();old.LocalBefore=null;
                old.CachedReentry?.Dispose();old.CachedReentry=null;old.Observation.Dispose();old.NativeOriginal?.ReleaseFrame();old.NativeViewerEffect?.ReleaseFrame();
            }
            PumpNativeViewers(world);RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.Sweep();RebirthProductiveEarnedUiDeferredTransport.PumpAuthorityNative(world);
            foreach(var old in frames.Values.Where(value=>value.LocalBefore!=null&&!ReferenceEquals(value.Observation.World,world)).ToArray())
            {old.LocalBefore.Dispose();old.LocalBefore=null;}
            PumpOutgoing();
            PumpRemoteNativeOwnerQueries(world);
            PumpSameOwnerTerminalQueries(world);
            PumpColdNativeNotices(world);
            if(!world.IsRemote())foreach(var source in nativeHostOriginals.Values.ToArray())
                RebirthProductiveEarnedUiDeferredTransport.TrySealRemoteNativeStation(source);
            RebirthProductiveEarnedUiQueueOwner.Pump();
            RebirthProductiveEarnedUiOwnerFile.PumpNativeTerminals();
            RebirthProductiveEarnedUiDeferredTransport.PumpRecipients();
            RebirthWorkstationReagentPublication.Sweep();
            PumpCachedOwnerEffects(world);
            foreach(var original in frames.Values.Where(value=>value.Observation.FullBoundary&&value.ConfirmedOriginal==null).ToArray())
            {ObserveCachedCustody(original);if(original.Observation.World.IsRemote())ResumePhysicalNativePackets(original,RemoteNativePhase.CachedBoundary);}
            foreach(var cache in cachedCustodies.Values.Where(value=>value.World!=null&&(!value.Physical||!value.Saved&&value.OwnerSavedImage!=null)).ToArray())cache.TrySave();
            foreach(var cache in cachedCustodies.Values.Where(value=>value.World!=null&&value.Physical).ToArray())TryPublishCachedSettledNotice(cache);
            RebirthProductiveEarnedUiDeferredTransport.PumpCachedPrimaryHolds(world);
            PumpCachedPhysicalSources(world);
            PumpCachedUnusedCloses(world);
            RecoverConfirmedIntents(world);
            foreach(var frame in frames.Values.ToArray())
            {
                if(frame.ConfirmedOriginal==null||frame.ConfirmedOriginal.WorldGuid!=RebirthWorkstationCompletionOutbox.CurrentGuid(world))continue;
                if(!frame.Observation.World.IsRemote()&&!frame.NativeAccepted)continue;
                if(frame.RemoteNativeNonce!=Guid.Empty&&(!frame.NativeAccepted||!NativeViewerCanRetireFrame(frame)))continue;
                if(frame.CachedPhysical!=null&&!frame.CachedPhysical.PhysicalSaved)continue;
                if(!string.Equals(Path.GetFullPath(GameIO.GetSaveGameDir()),frame.Observation.SaveDirectory,RebirthStationCompletionOriginalScope.PathComparison))continue;
                if(!frame.Saved&&!SaveFrame(frame))continue;
                string role=frame.Observation.World.IsRemote()?"client":"server";
                var retained=RebirthWorkstationCompletionOutbox.Retain(frame.ConfirmedOriginal,role,frame.Observation.World,frame.Observation.Station);
                if(retained==null||!retained.Saved&&!RebirthWorkstationCompletionOutbox.Save(retained))continue;
                ArchiveIntent(role,frame.Observation.Operation);cards.Remove(frame.Observation.Card);frames.Remove(frame.Observation.Operation);frame.Observation.Dispose();
                frame.NativeOriginal?.ReleaseFrame();
                frame.NativeViewerEffect?.ReleaseFrame();
            }
            foreach(var state in RebirthWorkstationCompletionOutbox.Pending(world.IsRemote()?"client":"server"))
            {
                try
                {
                if(!state.Saved&&!RebirthWorkstationCompletionOutbox.Save(state))continue;
                if(!world.IsRemote()){Settle(state);continue;}
                if(state.Acknowledged){RebirthWorkstationCompletionOutbox.Archive(state);continue;}
                var viewer=world.GetPrimaryPlayer();if(viewer==null||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||identity.StorageKey!=state.Original.ViewerKey||
                    !RebirthSurvivorRequestScope.Matches(state.Original.ViewerCreation,RebirthCamperCharacterAccess.CurrentCreation(viewer)))continue;
                var station=state.NativeStation;
                if(station!=null&&ReferenceEquals(state.NativeWorld,world)&&ReferenceEquals(world.GetTileEntity(state.Original.Position),station))
                {
                    var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
                    connection.SendToServer(NetPackageManager.GetPackage<NetPackageTileEntity>().Setup(station,StreamModeWrite.ToServer));
                    connection.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerInventory>().Setup(viewer,true,true,false,false));
                }
                Send(true,new XElement("complete",state.Original.Write()),Guid.ParseExact(state.Original.Operation,"N"),viewer.entityId);
                }
                catch{/* This exact original stays held; unrelated completions may continue. */}
            }
        }
        catch{/* Failed/corrupt original remains pending; no empty reset or effect replay. */}
        finally{pumping=false;}
    }
    private static void RecoverConfirmedIntents(World world)
    {
        string role=world.IsRemote()?"client":"server",folder=Path.Combine(RebirthWorkstationCompletionOutbox.Root(role),"intents");
        RebirthWorkstationCompletionPublication.RequireSafeDirectory(folder);
        if(!Directory.Exists(folder))return;
        int count=0;
        foreach(string path in Directory.EnumerateFiles(folder,"*.xml"))
        {
            if(++count>4096)throw new InvalidDataException("Completion intent bound");
            try
            {
                var intent=LoadXml(path);string operation=(string)intent.Attribute("operation");
                if(!Guid.TryParseExact(operation,"N",out var id)||frames.ContainsKey(id)||
                    (string)intent.Attribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(world)||
                    !RebirthWorkstationCompletionOriginal.TryRead(intent.Element("confirmedOriginal")?.Elements().SingleOrDefault(),out var original)||original.Operation!=operation||
                    Path.GetFileNameWithoutExtension(path)!=operation)continue;
                // Only a persisted positive accepted native sync can recover a
                // local original. Prepared/attempted/absent receipt is UNKNOWN.
                if(!world.IsRemote()&&!ValidateDelivery(intent,original))continue;
                // ValidateDelivery above positively binds this immutable birth
                // to the accepted native before/after; never inspect a current
                // same-shaped station to reconstruct the lost first outbox.
                string acceptedBirth=world.IsRemote()?null:(string)original.Write().Element("delivery")?.Elements().SingleOrDefault()?.Attribute("stationBirth");
                if(string.IsNullOrEmpty(acceptedBirth))acceptedBirth=null;
                var state=RebirthWorkstationCompletionOutbox.Retain(original,role,acceptedStationBirth:acceptedBirth);
                if(state!=null&&(state.Saved||RebirthWorkstationCompletionOutbox.Save(state)))ArchiveIntent(role,id);
            }
            catch{} // retain this exact uncertain primary; other originals continue
        }
    }
}

internal static partial class RebirthWorkstationCraftCompletion
{
    private static void CaptureNativeSnapshot(Snapshot snapshot)
    {
        try
        {
            if(!RebirthWorkstationCompletionPipeline.NeedsNativeSnapshot(snapshot.NativeViewerKey))return;
            snapshot.NativeViewerCreation=RebirthCamperCharacterAccess.CurrentCreation(snapshot.Player);
            snapshot.NativeOutput=snapshot.Tile==null?"":RebirthWorkstationCompletionPipeline.Grid(snapshot.Tile.Output);
            snapshot.NativeBag=RebirthWorkstationCompletionPipeline.Grid(snapshot.Player.bag.ItemGrid.items);
            snapshot.NativeBelt=RebirthWorkstationCompletionPipeline.Grid(snapshot.Player.inventory.ItemGrid.items);
            snapshot.NativeEquipment=Convert.ToBase64String(RebirthRecipeOutputIntegration.OwningEquipmentNative(snapshot.Player));
            snapshot.NativeQueue=snapshot.Tile==null?"":RebirthRemoteResourcePlacedStationQueue.Encode(snapshot.Tile.Queue);
        }
        catch{snapshot.NativeViewerCreation=null;snapshot.NativeOutput=snapshot.NativeBag=snapshot.NativeBelt=snapshot.NativeQueue=snapshot.NativeEquipment=null;}
    }
    private static void ObserveAcceptedNativeSnapshot(Snapshot snapshot)
    {try{RebirthWorkstationCompletionPipeline.ObserveAccepted(snapshot);}catch{/* native input behavior/exception is unchanged; original remains unconfirmed */}}
}

