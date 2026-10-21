using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    // This is the third physical source. It owns no GiveExp/Add invocation:
    // those original effects remain independently conserved by the cache.
    internal sealed partial class CachedPhysicalSource:IDisposable
    {
        internal readonly RebirthProductiveEarnedUiDeferredTransport.RemoteCachedPrimaryGrant Grant;
        internal readonly Guid Operation,Action;
        internal World World=>Grant.World;
        internal TileEntityWorkstation Station=>Grant.Station;
        internal EntityPlayerLocal Viewer=>Grant.Actor;
        internal XUiC_WorkstationWindowGroup Window=>Grant.Window;
        internal XUiC_RecipeStack Card=>Grant.Card;
        internal long WindowSession=>Grant.WindowSession;
        internal INetConnection Connection=>Grant.Connection;
        internal readonly XElement Before,Full;
        internal readonly string Path;
        private byte[] requestImage,readyImage;
        private byte[] preparedImage;
        private string readyPath;
        private IDisposable allocation,retention;
        private bool closed;
        private bool restoring,installed,restoreUnknown;
        internal bool Ready=>readyImage!=null;
        internal bool PhysicalSaved;
        internal bool CourierAcknowledged;
        internal RebirthProductiveEarnedUiDeferredTransport.CachedSettlementPublication Settlement;
        internal RebirthProductiveEarnedUiDeferredTransport.ClientTerminalPublication OwnerRenewal;
        internal IDisposable OwnerRenewalRetention;
        internal bool SpaceSaved;
        internal CachedPhysicalSpace Space;
        internal bool Prepared=>preparedImage!=null;
        internal long NativeVersion;
        internal Frame Frame;
        internal IDisposable Retain()=>Current()?Grant.Retain():null;
        internal string Generation=>Frame==null?null:RebirthWorkstationCompletionOriginal.Hash(ActiveBefore(Frame.Intent).ToString(SaveOptions.DisableFormatting));
        internal bool CurrentUnusedClose(long session,out bool hasPrimary)
        {
            hasPrimary=installed;
            if(retainedPrefix!=null||closed||restoreUnknown||restoring||Frame?.Observation.RanUpdate==true||!Grant.Current()||
                WindowSession!=session||!RebirthProductiveEarnedUiDeferredTransport.TryGetObservedNativeUiSession(Window,out var observed)||observed!=session||
                !ReferenceEquals(Window.WorkstationData?.TileEntity,Station)||!ReferenceEquals(Window.xui?.playerUI?.entityPlayer,Viewer)||
                !RebirthRecipeOutputIntegration.IsCurrentOwningObservedVersion(Card,NativeVersion)||
                !RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Path,requestImage))return false;
            if(!installed)return Card.outputItemValue==null;
            var primary=new[]{new ItemStack(Card.outputItemValue,Card.GetRecipe().count)};
            return Card.isInventoryFull&&RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(primary,262144,349528,out _)&&Grid(primary)==(string)Full.Element("primaryNative");
        }
        internal bool NativeUnusedCloseCurrent()
        {
            if(peerSync?.Saved==true)return PeerNativeCurrent();
            if(OwnerRenewal!=null)return OwnerRenewalNativeCurrent();
            if(Space==null)return Grant.NativeCurrent();
            if(!SpaceSaved||Space.SavedImage==null||!RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Space.SavedPath,Space.SavedImage))return false;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                return decoder!=null&&RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,Space.After,Space.AfterOffset);
        }
        internal double Retry;
        private CachedPhysicalSource(RebirthProductiveEarnedUiDeferredTransport.RemoteCachedPrimaryGrant grant,IDisposable memory,IDisposable lease)
        {
            Grant=grant;Operation=grant.Operation;Action=Guid.NewGuid();allocation=memory;retention=lease;Before=grant.CopyBefore();Full=grant.CopyFull();NativeVersion=grant.NativeVersion;
            Path=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("client"),"cachedPhysical",Operation.ToString("N")+"."+Action.ToString("N")+".xml");
        }
        internal bool Current()=>!closed&&allocation!=null&&retention!=null&&Grant.CurrentWindowSource()&&
            (retainedPrefix==null||retainedPrefix.HostPrefixRestored)&&
            (peerSync==null||peerSync.Current())&&
            (OwnerRenewal==null||OwnerRenewal.CurrentSavedSource()&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(ownerRenewalPath,ownerRenewalImage))&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Path,requestImage)&&
            (!Prepared||RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Path+".prepared",preparedImage))&&
            (!SpaceSaved||Space?.SavedImage!=null&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Space.SavedPath,Space.SavedImage))&&
            (!Ready||RebirthWorkstationCompletionPublication.MatchesPublishedBytes(readyPath,readyImage));
        internal bool NativeCurrent()
        {
            if(peerSync?.Saved==true)return PeerNativeCurrent();
            if(OwnerRenewal!=null)return OwnerRenewalNativeCurrent();
            if(Space==null)return Grant.NativeCurrent();
            if(!SpaceSaved||!Space.Observed||!Space.Current())return false;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                return decoder!=null&&RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,Space.After,Space.AfterOffset);
        }
        private bool ObservedSpaceCurrent()
        {
            if(Space?.Observed!=true||!Space.Current())return false;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                return decoder!=null&&RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,Space.After,Space.AfterOffset);
        }
        internal bool CurrentBeforeAction()=>Current()&&Ready&&Grant.CurrentWindow()&&NativeCurrent()&&OriginalPrimaryCurrent();
        internal bool OriginalPrimaryCurrent()
        {
            if(!Current()||!installed||Card?.GetRecipe()==null||!Card.isInventoryFull||Card.outputItemValue==null||
                !RebirthRecipeOutputIntegration.IsCurrentOwningObservedVersion(Card,NativeVersion))return false;
            var primary=new[]{new ItemStack(Card.outputItemValue,Card.GetRecipe().count)};
            return RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(primary,262144,349528,out _)&&Grid(primary)==(string)Full.Element("primaryNative");
        }
        private XElement Request()=>retainedPrepare!=null?new XElement(retainedPrepare):new XElement("cachedPhysicalPrepare",new XAttribute("ticket",Grant.Ticket.ToString("N")),
            new XAttribute("group",Grant.Group.ToString("N")),new XAttribute("action",Action.ToString("N")),
            new XAttribute("original",Grant.Original),new XAttribute("session",WindowSession));
        internal static CachedPhysicalSource FromActualGrant(RebirthProductiveEarnedUiDeferredTransport.RemoteCachedPrimaryGrant grant)
        {
            // A retained old action has its own saved prefix/native owner
            // binding. It must never enter the constructor that creates a new
            // action, even when a fresh actual native grant has arrived.
            if(grant?.CurrentWindow()!=true||grant.RetainedAction!=Guid.Empty)return null;IDisposable memory=null,lease=null;CachedPhysicalSource result=null;
            try
            {
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*grant.MeasureImage()+65536),out memory))return null;
                lease=grant.Retain();if(lease==null)return null;result=new CachedPhysicalSource(grant,memory,lease);memory=lease=null;
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",result.Operation.ToString("N")),
                    new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(grant.World)),new XElement("before",grant.CopyImage()),result.Request());
                if(!RebirthWorkstationCompletionPublication.Write(result.Path,record))return null;
                result.requestImage=RebirthWorkstationCompletionPublication.CopyPublishedBytes(result.Path);if(!result.Current())return null;
                var owned=result;result=null;return owned;
            }
            finally{result?.Dispose();lease?.Dispose();memory?.Dispose();}
        }
        internal void Pump()
        {
            if(!Current())return;
            if(retainedPrefix!=null&&Frame==null)return;
            if(Frame?.Attempted==true)
            {
                if(!PhysicalSaved&&UnityEngine.Time.realtimeSinceStartup>=Retry)
                {Retry=UnityEngine.Time.realtimeSinceStartup+1;Send(true,Request(),Operation,Viewer.entityId);}
                return;
            }
            if(Space!=null&&!SpaceSaved)
            {
                ResumeCachedPhysicalSpace(this);
                if(UnityEngine.Time.realtimeSinceStartup>=Retry){Retry=UnityEngine.Time.realtimeSinceStartup+1;Send(true,Request(),Operation,Viewer.entityId);}
                return;
            }
            if(Prepared&&!InstallOriginalPrimary())return;
            if(Prepared&&Frame==null)TryFreezeFrame();
            if(Frame!=null&&!Ready)EnsureNativeViewerBefore(Frame);
            if(Ready||UnityEngine.Time.realtimeSinceStartup<Retry)return;
            Retry=UnityEngine.Time.realtimeSinceStartup+1;
            Send(true,Request(),Operation,Viewer.entityId);
        }
        private bool TryFreezeFrame()
        {
            if(!Prepared||!OriginalPrimaryCurrent()||!NativeCurrent())return false;
            RebirthRecipeOutputIntegration.OwningObservation observation=null;
            try
            {
                if(frames.TryGetValue(Operation,out var old)&&!RetireCopiedCachedFrame(this,old))return false;
                observation=RebirthRecipeOutputIntegration.BeginOwningObservation(Card,true,Operation);if(observation==null)return false;
                observation.CachedPredecessor=new XElement(Full);
                if(!observation.TryCopyBeforeEvidence(out var active))return false;
                var value=new Frame{Observation=observation,Path=Path+".intent."+RebirthWorkstationCompletionOriginal.Hash(active.ToString(SaveOptions.DisableFormatting)),Owner=(string)Before.Attribute("startingOwnerKey"),
                    Creation=(string)Before.Attribute("startingOwnerCreation"),Prepared=true,RemoteNativeNonce=Action,RemoteNativeSourceHash=Grant.Original,CachedPhysical=this};
                value.Intent=new XElement("workstationCompletionIntent",new XAttribute("version",1),new XAttribute("operation",Operation.ToString("N")),
                    new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(World)),new XElement("before",active),
                    new XElement("cachedPhysicalSource",new XAttribute("path",Path),new XAttribute("action",Action.ToString("N")),
                        new XAttribute("ticket",Grant.Ticket.ToString("N")),new XAttribute("original",Grant.Original)));
                if(!SaveFrame(value)||cards.TryGetValue(Card,out var attached))return false;
                cards.Add(Card,value);frames.Add(Operation,value);Frame=value;observation=null;return true;
            }
            catch{return false;}
            finally{RebirthRecipeOutputIntegration.EndOwningObservation(observation??Frame?.Observation);observation?.Dispose();}
        }
        internal bool AcceptReady(XElement ack)
        {
            if(!Prepared||!Current()||(string)ack.Attribute("ticket")!=Grant.Ticket.ToString("N")||(string)ack.Attribute("action")!=Action.ToString("N")||
                (string)ack.Attribute("digest")!=Grant.Original||Generation==null||(string)ack.Attribute("generation")!=Generation)return false;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),
                new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(World)),new XElement("before",new XElement(Before)),new XElement("cachedPhysicalReady",ack));
            readyPath=Path+".ready."+Generation;
            if(!RebirthWorkstationCompletionPublication.Write(readyPath,record))return false;
            readyImage=RebirthWorkstationCompletionPublication.CopyPublishedBytes(readyPath);return Current();
        }
        internal bool AcceptSpaceSaved(XElement ack)
        {
            if(!Prepared||!Current()||Space?.Observed!=true||!Space.Current()||Frame?.Attempted==true||
                (string)ack.Attribute("ticket")!=Grant.Ticket.ToString("N")||(string)ack.Attribute("action")!=Action.ToString("N")||
                (string)ack.Attribute("digest")!=Grant.Original||(int?)ack.Attribute("spaceSequence")!=Space.Sequence)return false;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                if(decoder==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,Space.After,Space.AfterOffset))return false;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),
                new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(World)),new XElement("before",new XElement(Before)),new XElement("cachedPhysicalSpaceSaved",ack),new XElement(Space.Evidence));
            string path=Path+".space-saved."+Space.Sequence;
            if(!RebirthWorkstationCompletionPublication.Write(path,record)||
                !XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return false;
            var savedImage=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(savedImage==null)return false;
            if(Frame!=null)
            {
                if(cards.TryGetValue(Card,out var attached)&&ReferenceEquals(attached,Frame))cards.Remove(Card);
                if(frames.TryGetValue(Operation,out attached)&&ReferenceEquals(attached,Frame))frames.Remove(Operation);
                if(nativeViewerClients.TryGetValue(Frame,out var viewer)){nativeViewerClients.Remove(Frame);viewer.Dispose();}
                Frame.Closed=true;Frame.Observation.Dispose();Frame=null;
            }
            Space.SavedPath=path;Space.SavedImage=savedImage;readyImage=null;readyPath=null;SpaceSaved=true;return true;
        }
        internal bool AcceptPhysicalSaved(XElement ack)
        {
            if(!Ready||!Current()||Frame?.ConfirmedOriginal==null||(string)ack.Attribute("ticket")!=Grant.Ticket.ToString("N")||
                (string)ack.Attribute("action")!=Action.ToString("N")||(string)ack.Attribute("digest")!=Grant.Original)return false;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),
                new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(World)),new XElement("before",new XElement(Before)),new XElement("cachedPhysicalSaved",ack),
                new XElement("confirmedOriginal",Frame.ConfirmedOriginal.Write()));
            if(!RebirthWorkstationCompletionPublication.Write(Path+".physical-saved",record)||
                !XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(Path+".physical-saved")))return false;
            PhysicalSaved=true;QualifyCachedPeerSyncs(this);return true;
        }
        internal bool AcceptPrepared(XElement ack)
        {
            if(!Current()||(string)ack.Attribute("ticket")!=Grant.Ticket.ToString("N")||(string)ack.Attribute("action")!=Action.ToString("N")||
                (string)ack.Attribute("digest")!=Grant.Original)return false;
            if(Prepared)return true;
            if(!Grant.CurrentWindow()||!NativeCurrent())return false;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),
                new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(World)),new XElement("before",new XElement(Before)),new XElement("cachedPhysicalPrepared",ack));
            if(!RebirthWorkstationCompletionPublication.Write(Path+".prepared",record))return false;
            preparedImage=RebirthWorkstationCompletionPublication.CopyPublishedBytes(Path+".prepared");return Current();
        }
        internal bool InstallOriginalPrimary()
        {
            if(restoreUnknown||restoring||!Prepared||!Current())return false;
            if(installed)return OriginalPrimaryCurrent();
            if(!Grant.CurrentWindow()||!NativeCurrent())return false;
            try
            {
                ItemStack[] primary;
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                {if(decoder==null)return false;primary=RebirthProductiveEarnedUiRemote.ReadGrid(Full,"primaryNative");}
                float time=(float)Full.Attribute("cachedTime"),total=(float)Full.Attribute("cachedTotalTime");
                if(primary.Length!=1||primary[0].IsEmpty()||primary[0].count!=Card.GetRecipe().count||
                    float.IsNaN(time)||float.IsInfinity(time)||time>0||float.IsNaN(total)||float.IsInfinity(total))return false;
                var attempt=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),
                    new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(World)),new XElement("before",new XElement(Before)),new XElement("cachedRemotePrimaryRestoreAttempt",
                        new XAttribute("action",Action.ToString("N")),new XAttribute("ticket",Grant.Ticket.ToString("N")),
                        new XAttribute("session",WindowSession),new XAttribute("nativeVersion",NativeVersion),new XElement(Full)));
                if(!RebirthWorkstationCompletionPublication.Write(Path+".restore",attempt)||
                    !XNode.DeepEquals(attempt,RebirthWorkstationCompletionPublication.ReadPrimary(Path+".restore"))||
                    !Grant.CurrentWindow()||!NativeCurrent())return false;
                restoring=true;
                // Restore the positively retained original cached fields.
                // The real native cached Update still owns the sole insertion.
                Card.outputItemValue=primary[0].itemValue;Card.isInventoryFull=true;Card.craftingTimeLeft=time;
                Card.totalCraftTimeLeft=total;Card.isCrafting=(bool)Full.Attribute("cachedCrafting");
                installed=true;restoring=false;return true;
            }
            catch{if(restoring)restoreUnknown=true;restoring=false;return false;}
        }
        public void Dispose()
        {
            if(closed)return;closed=true;retainedPrepare=null;requestImage=readyImage=preparedImage=null;Before?.RemoveNodes();Full?.RemoveNodes();
            Space?.Dispose();Space=null;
            peerSync?.Dispose();peerSync=null;
            Settlement?.Dispose();Settlement=null;
            ownerRenewalImage=null;OwnerRenewal=null;OwnerRenewalRetention?.Dispose();OwnerRenewalRetention=null;
            retention?.Dispose();retention=null;allocation?.Dispose();allocation=null;
        }
    }
    internal sealed partial class CachedPhysicalHostAction
    {
        internal CachedCreatorIssue Issue;internal readonly Guid Action;internal readonly long Session;internal readonly string Path;internal byte[] Image;
        internal long ReadFence;
        internal bool PhysicalAccepted;
        internal bool PhysicalFinalObserved;
        internal string FinalAcceptedPath;
        internal byte[] FinalAcceptedImage;
        internal byte[] PhysicalBody;
        internal int PhysicalOffset;
        internal XElement PhysicalActive,PhysicalDelivery;
        internal string PhysicalAcceptedPath;
        internal byte[] PhysicalAcceptedImage;
        internal bool SpaceAccepted;
        internal int SpaceSequence;
        internal bool OwnerRenewed;
        internal bool SavedOwnerBeforeAction {get;private set;}
        internal string BeforeGeneration{get;private set;}
        private byte[] readyImage;
        private string readyPath;
        private CachedPhysicalHostAction(CachedCreatorIssue issue,Guid action,long session,string path,byte[] image)
        {Issue=issue;Action=action;Session=session;Path=path;Image=image;ReadFence=issue.ReadFence;}
        internal static CachedPhysicalHostAction FromActualPrepare(CachedCreatorIssue issue,Guid action,long session,string path,byte[] image)
        {
            if(!ReferenceEquals(preparingCachedPhysicalIssue,issue)||action==Guid.Empty||session<1||!issue.CurrentBeforeAction()||
                path!=issue.Cache.Path+".remote-action."+issue.Cache.Epoch||!RebirthWorkstationCompletionPublication.MatchesPublishedBytes(path,image))return null;
            return new CachedPhysicalHostAction(issue,action,session,path,image);
        }
        internal bool BindSavedBefore(RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.SavedViewerFile proof)
        {
            if(!Current()||proof?.Source?.Cached!=this||proof.After||!proof.Current()||!proof.Source.BeforeInventoryCurrent()||
                !HasSavedCachedOwnerEvent(Issue.Cache)||HasUnpreparedCachedPeers(this)||
                !Issue.Cache.SavedCurrent()||!Issue.Cache.NativeCurrent()||
                BeforeGeneration!=null&&BeforeGeneration!=proof.Source.Generation)return false;
            if(BeforeGeneration!=null)return RebirthWorkstationCompletionPublication.MatchesPublishedBytes(readyPath,readyImage);
            var cache=Issue.Cache;
            long measure=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(proof.Request)+cache.SavedImage.LongLength+65536;
            if(!cache.ReserveRetirementWorkspace(checked(16*measure)))return false;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",WorldGuid),
                new XElement(Issue.Cache.Original.Element("before")),
                new XElement("cachedPhysicalActionReady",new XAttribute("action",Action.ToString("N")),new XAttribute("ticket",Issue.Ticket.ToString("N")),
                    new XAttribute("group",Issue.Group.ToString("N")),new XAttribute("session",Session),new XAttribute("original",SourceHash),
                    new XAttribute("generation",proof.Source.Generation),new XAttribute("ownerNative",proof.NativePath),
                    new XAttribute("ownerDigest",RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.NativeDigest(proof.Native)),
                    new XAttribute("ownerReceipt",proof.ReceiptPath),new XAttribute("nativePublication",cache.SavedPath),
                    new XAttribute("nativePublicationDigest",HistoricalHash(cache.SavedImage)),new XElement(proof.Request)));
            readyPath=Path+".ready."+proof.Source.Generation;
            if(!RebirthWorkstationCompletionPublication.Write(readyPath,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(readyPath))||
                !proof.Current()||!cache.NativeCurrent())return false;
            readyImage=RebirthWorkstationCompletionPublication.CopyPublishedBytes(readyPath);if(readyImage==null)return false;
            BeforeGeneration=proof.Source.Generation;SavedOwnerBeforeAction=true;return true;
        }
        internal World World=>Issue?.Cache?.World;internal TileEntityWorkstation Station=>Issue?.Cache?.Station;
        internal EntityPlayer Viewer=>Issue?.Viewer;internal ClientInfo Sender=>Issue?.Sender;
        internal string ViewerKey=>Issue?.Owner;internal string ViewerCreation=>Issue?.Creation;internal int ViewerDeaths=>Issue?.Deaths??-1;
        internal Guid Operation=>Issue.Cache.Operation;internal Guid Nonce=>Action;
        internal string WorldGuid=>Issue.Cache.WorldGuid;internal string SourceHash=>HistoricalHash(Issue.Cache.Image);
        internal XElement Before=>Issue.Cache.Original.Element("before").Elements().Single();
        internal Recipe Recipe=>Station.Queue[(int)Before.Attribute("slot")].Recipe;
        internal bool Current()=>Issue?.CurrentIdentity()==true&&Issue.Cache.RestoreAttempted&&
            RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(World,Station,out var fence)&&fence==ReadFence&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Path,Image)&&
            (!OwnerRenewed||Issue.Cache.OwnerSavedImage!=null&&CachedCustody.EvidenceCurrent(Issue.Cache.OwnerSavedPath,Issue.Cache.OwnerSavedImage))&&
            (!PhysicalAccepted||RebirthWorkstationCompletionPublication.MatchesPublishedBytes(PhysicalAcceptedPath,PhysicalAcceptedImage))&&
            (BeforeGeneration==null||RebirthWorkstationCompletionPublication.MatchesPublishedBytes(readyPath,readyImage));
        internal IDisposable Retain()=>Current()?Issue.Cache.RetainResource():null;
        internal void ObserveSavedSpaceEpoch(int sequence){BeforeGeneration=null;readyImage=null;SpaceAccepted=true;SpaceSequence=sequence;}
        internal void ObserveOwnerTerminalRead(long fence)
        {ReadFence=fence;BeforeGeneration=null;readyImage=null;OwnerRenewed=true;}
        internal void ClearEvidence(){LoadedFinal?.Dispose();LoadedFinal=null;Image=readyImage=PhysicalBody=PhysicalAcceptedImage=FinalAcceptedImage=null;PhysicalActive=PhysicalDelivery=null;Issue=null;}
    }
    private static readonly Dictionary<Guid,CachedPhysicalSource> cachedPhysicalSources=new Dictionary<Guid,CachedPhysicalSource>();
    private static readonly Dictionary<Guid,CachedPhysicalHostAction> cachedPhysicalActions=new Dictionary<Guid,CachedPhysicalHostAction>();
    [ThreadStatic]private static CachedCreatorIssue preparingCachedPhysicalIssue;
    internal static void ObserveCachedPhysicalGrant(RebirthProductiveEarnedUiDeferredTransport.RemoteCachedPrimaryGrant grant)
    {
        if(grant?.CurrentWindow()!=true||cachedPhysicalSources.ContainsKey(grant.Operation)||cachedPhysicalSources.Count>=4)return;
        var source=CachedPhysicalSource.FromActualGrant(grant);if(source!=null)cachedPhysicalSources.Add(source.Operation,source);
    }
    private static void PumpCachedPhysicalSources(World world)
    {
        if(!world.IsRemote())
        {
            foreach(var value in cachedCustodies.Values.Where(value=>value.Active&&ReferenceEquals(value.World,world)&&(!value.Saved||value.PhysicalObserved)).ToArray())
            {
                if(!value.TrySave()||value.PhysicalPublication==null)continue;
                if(value.PhysicalPublication.Previous==null&&value.RemoteSource==null)
                    RetireLoadedCachedActorPhysical(value,RebirthWorkstationCompletionPublication.ReadPrimary(value.Path+".physical"));
            }
        }
        foreach(var value in cachedPhysicalSources.Values.ToArray())
        {
            if(!ReferenceEquals(value.World,world)){cachedPhysicalSources.Remove(value.Operation);value.Dispose();continue;}
            if(value.CourierAcknowledged&&value.Frame?.NativeAccepted==true&&value.Frame.ConfirmedOriginal!=null&&
                !pendingNativeUploads.Any(pending=>ReferenceEquals(pending.Setup?.Frame,value.Frame)))
            {
                if(!value.EnsureSameOwnerCourier()||value.RequiresSameOwnerCourier&&!value.SameOwnerCourierRetired)continue;
                // The server's durable original ACK already required exact
                // saved physical/native and actor-PDS proof. This drops only
                // the creator's copied observation graph; the independent
                // cached-primary/owner ledger hold remains until settlement.
                var frame=value.Frame;value.PhysicalSaved=true;
                if(cards.TryGetValue(frame.Observation.Card,out var attached)&&ReferenceEquals(attached,frame))cards.Remove(frame.Observation.Card);
                if(frames.TryGetValue(value.Operation,out attached)&&ReferenceEquals(attached,frame))frames.Remove(value.Operation);
                if(nativeViewerClients.TryGetValue(frame,out var viewer)){nativeViewerClients.Remove(frame);viewer.Dispose();}
                frame.Closed=true;frame.Observation.Dispose();
                cachedPhysicalSources.Remove(value.Operation);value.Dispose();continue;
            }
            value.Pump();
        }
    }
    internal static void ObserveCachedPhysicalSettlement(RebirthProductiveEarnedUiDeferredTransport.CachedSettlementPublication proof)
    {
        if(proof?.Current()!=true||!cachedPhysicalSources.TryGetValue(proof.Operation,out var source)||source.Grant.Original!=proof.Original||
            !ReferenceEquals(source.World,proof.World)||!ReferenceEquals(source.Station,proof.Station)||!ReferenceEquals(source.Viewer,proof.Actor)||
            !ReferenceEquals(source.Connection,proof.Connection)){proof?.Dispose();return;}
        source.Settlement?.Dispose();source.Settlement=proof;
    }
    private static void ObserveCachedPhysicalCourierAccepted(Guid operation,string digest)
    {
        if(cachedPhysicalSources.TryGetValue(operation,out var source)&&source.Frame?.ConfirmedOriginal?.Digest==digest)
        {source.CourierAcknowledged=true;QualifyCachedPeerSyncs(source);}
    }
    internal static bool CanAdvanceCachedPhysical(XUiC_RecipeStack card)=>cachedPhysicalSources.Values.Any(value=>
        ReferenceEquals(value.Card,card)&&value.CurrentBeforeAction()&&value.Frame!=null&&!value.Frame.Attempted);
    internal static bool CanSyncCachedPhysical(XUiC_WorkstationWindowGroup group)=>cachedPhysicalSources.Values.Any(value=>
        ReferenceEquals(value.Window,group)&&value.Ready&&value.Current()&&value.Frame?.ConfirmedOriginal!=null&&!value.Frame.NativeAccepted);
    private static bool TryBeginCachedPhysical(XUiC_RecipeStack card,out bool handled,out Frame frame)
    {
        frame=null;var values=cachedPhysicalSources.Values.Where(value=>ReferenceEquals(value.Card,card)).ToArray();handled=values.Length!=0;
        if(values.Length!=1)return false;var source=values[0];source.Pump();
        if(source.retainedPrefix!=null)return source.BeginRetainedPrefix(out frame);
        if(!source.CurrentBeforeAction()||source.Frame==null||source.Frame.Attempted||!RebirthRecipeOutputIntegration.CanFitOwningCompletion(card,true)||!EnsureNativeViewerBefore(source.Frame))return false;
        frame=source.Frame;
        if(!RebirthRecipeOutputIntegration.ResumeOwningObservation(frame.Observation))return false;
        current=frame;frame.Attempted=true;
        // A failed or thrown native invocation stays attempted. Only the
        // actual cached insertion can produce the later delivery event.
        return SaveFrame(frame);
    }
    internal static bool TryReadCachedPhysicalStartingOwner(XUiC_RecipeStack card,out string owner,out string creation,out int deaths)
    {
        owner=creation=null;deaths=-1;
        var values=cachedPhysicalSources.Values.Where(value=>ReferenceEquals(value.Card,card)&&value.Prepared&&value.Current()&&
            RebirthRecipeOutputIntegration.IsCurrentOwningObservedVersion(card,value.NativeVersion)&&value.Grant.CurrentWindowSource()).ToArray();
        if(values.Length!=1)return false;var source=values[0];
        try
        {
            if(!card.isInventoryFull||card.outputItemValue==null||card.GetRecipeCount()!=(int)source.Before.Attribute("beforeCount")||
                card.StartingEntityId!=(int)source.Before.Attribute("startingOwner"))return false;
            var primary=new[]{new ItemStack(card.outputItemValue,card.GetRecipe().count)};
            if(!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(primary,262144,349528,out _)||
                Grid(primary)!=(string)source.Full.Element("primaryNative"))return false;
            owner=(string)source.Before.Attribute("startingOwnerKey");creation=(string)source.Before.Attribute("startingOwnerCreation");
            deaths=(int)source.Before.Attribute("startingOwnerDeaths");return RebirthOriginalActionScope.Validate(owner,creation)&&deaths>=0;
        }
        catch{return false;}
    }
    private static void ForgetCachedPhysicalAction(CachedCustody cache)
    {
        if(cachedPhysicalActions.TryGetValue(cache.Operation,out var value)&&ReferenceEquals(value.Issue?.Cache,cache))
        {cachedPhysicalActions.Remove(cache.Operation);value.ClearEvidence();}
    }
    private static bool RetireCopiedCachedFrame(CachedPhysicalSource source,Frame old)
    {
        if(ReferenceEquals(old,source.Frame))return true;
        if(!source.Current()||!source.Prepared||!source.OriginalPrimaryCurrent()||old.ConfirmedOriginal!=null||
            old.RemoteNativeNonce!=source.Grant.Nonce||old.RemoteNativeSourceHash!=source.Grant.SourceHash||
            old.Observation.Operation!=source.Operation||!old.Observation.FullBoundary||
            !XNode.DeepEquals(old.Observation.FullBoundaryEvidence,source.Full))return false;
        // The selected, saved original and prepared custody copy now own the
        // primary. Retiring this old UI frame grants no native effect retry.
        if(cards.TryGetValue(old.Observation.Card,out var cardFrame)&&ReferenceEquals(cardFrame,old))cards.Remove(old.Observation.Card);
        frames.Remove(source.Operation);old.Closed=true;
        if(nativeViewerClients.TryGetValue(old,out var viewer)){nativeViewerClients.Remove(old);viewer.Dispose();}
        old.Observation.Dispose();old.LocalBefore?.Dispose();old.LocalBefore=null;old.NativeOriginal?.ReleaseFrame();old.NativeViewerEffect?.ReleaseFrame();
        return true;
    }
    private static void PrepareCachedPhysical(EntityPlayer viewer,ClientInfo sender,Guid operation,XElement request)
    {
        try
        {
            if(!cachedCustodies.TryGetValue(operation,out var cache)||cache.CreatorIssue is not CachedCreatorIssue issue||
                !ReferenceEquals(issue.Viewer,viewer)||!ReferenceEquals(issue.Sender,sender)||
                !issue.MatchesPrepareRequest(request)||
                (string)request.Attribute("original")!=HistoricalHash(cache.Image)||
                !Guid.TryParseExact((string)request.Attribute("action"),"N",out var action)||action==Guid.Empty||
                (long?)request.Attribute("session") is not long session||session<1)return;
            if(!cachedPhysicalActions.TryGetValue(operation,out var held))
            {
                if(cachedPhysicalActions.Count>=4||!issue.CurrentBeforeAction())return;
                string path=cache.Path+".remote-action."+cache.Epoch;
                // A historical attempted action is never adopted by a fresh
                // client request. Only this retained producer can retry Ready.
                if(RebirthWorkstationCompletionPublication.PublishedLength(path)>0)return;
                if(!cache.ReserveRetirementWorkspace(checked(16*(cache.Image.LongLength+65536))))return;
                var actionRequest=new XElement(request);actionRequest.Attribute("challenge")?.Remove();
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),
                    new XAttribute("world",cache.WorldGuid),new XElement(cache.Original.Element("before")),new XElement("cachedPhysicalAttempt",actionRequest,new XElement(cache.Original)));
                if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path))||
                    !issue.CurrentBeforeAction())return;
                var image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(image==null)return;
                preparingCachedPhysicalIssue=issue;
                try{held=CachedPhysicalHostAction.FromActualPrepare(issue,action,session,path,image);}
                finally{preparingCachedPhysicalIssue=null;}
                if(held==null)return;cache.RestoreAttempted=true;cachedPhysicalActions.Add(operation,held);
            }
            if(!ReferenceEquals(held.Issue,issue)||held.Action!=action||held.Session!=session||!held.Current())return;
            if(held.OwnerRenewed&&!cache.SavedCurrent())cache.TrySave();
            bool ready=held.BeforeGeneration!=null||RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.TryBindCachedSavedBefore(held);
            bool space=held.SpaceAccepted&&held.BeforeGeneration==null&&cache.TrySave()&&cache.SavedCurrent()&&cache.NativeCurrent();
            bool physical=ready&&cache.PhysicalObserved&&cache.TrySave()&&cache.Physical&&cache.SavedCurrent()&&cache.NativeCurrent()&&
                RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.TryReadCachedAfter(held,held.PhysicalActive,held.PhysicalDelivery,out _,out _);
            Send(false,new XElement("ack",new XAttribute("world",cache.WorldGuid),new XAttribute("viewerCreation",issue.Creation),
                new XAttribute("stage",physical?"cachedPhysicalSaved":space?"cachedPhysicalSpaceSaved":ready?"cachedPhysicalReady":"cachedPhysicalPrepared"),new XAttribute("ticket",issue.Ticket.ToString("N")),
                new XAttribute("action",action.ToString("N")),new XAttribute("digest",HistoricalHash(cache.Image)),
                new XAttribute("generation",held.BeforeGeneration??""),new XAttribute("spaceSequence",held.SpaceSequence),
                new XAttribute("challenge",(string)request.Attribute("challenge")??"")),operation,viewer.entityId);
        }
        catch{}
    }
    private static void ReceiveCachedViewer(EntityPlayer viewer,ClientInfo sender,string key,string creation,Guid operation,XElement request)
    {
        if(!cachedPhysicalActions.TryGetValue(operation,out var action)||!ReferenceEquals(action.Viewer,viewer)||!ReferenceEquals(action.Sender,sender)||
            action.ViewerKey!=key||action.ViewerCreation!=creation||!action.Current())return;
        if(!RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.ReceiveCached(action,request,out var stage,out var digest))return;
        Send(false,new XElement("ack",new XAttribute("world",action.WorldGuid),new XAttribute("viewerCreation",creation),
            new XAttribute("challenge",(string)request.Attribute("challenge")??""),new XAttribute("stage",stage),new XAttribute("digest",digest),
            new XAttribute("nativeNonce",action.Action.ToString("N")),new XAttribute("nativeSource",action.SourceHash)),operation,viewer.entityId);
    }
    private static void AcceptCachedPhysicalControl(EntityPlayer viewer,Guid operation,XElement ack)
    {
        if(!cachedPhysicalSources.TryGetValue(operation,out var source)||!ReferenceEquals(source.Viewer,viewer)||!source.Current())return;
        if((string)ack.Attribute("stage")=="cachedPhysicalPrepared")source.AcceptPrepared(ack);
        else if((string)ack.Attribute("stage")=="cachedPhysicalReady")source.AcceptReady(ack);
        else if((string)ack.Attribute("stage")=="cachedPhysicalSaved")source.AcceptPhysicalSaved(ack);
        else if((string)ack.Attribute("stage")=="cachedPhysicalSpaceSaved")source.AcceptSpaceSaved(ack);
    }
}
