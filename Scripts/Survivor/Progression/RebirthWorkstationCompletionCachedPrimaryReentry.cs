using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal sealed class CachedPrimaryReentry:IDisposable
    {
        private CachedCustody source;private IDisposable retention;private int references=1;private bool ownerReleased;
        internal readonly Guid Operation;internal readonly World World;internal readonly TileEntityWorkstation Station;
        internal readonly Guid CustodyBirth;
        internal readonly XUiC_WorkstationWindowGroup Window;internal readonly XUiC_RecipeStack Card;internal readonly long Session;
        internal readonly string OwnerPurpose,Owner,Creation,Path;internal byte[] Image;
        internal XElement Before,Full;internal RebirthProductiveEarnedUiQueueOwner.Witness QueueOwner;
        private CachedPrimaryReentry(CachedCustody value,XUiC_WorkstationWindowGroup window,XUiC_RecipeStack card,long session)
        {
            source=value;retention=value.RetainResource();Operation=value.Operation;World=value.World;Station=value.Station;Window=window;Card=card;Session=session;Path=value.Path;Image=value.Image;
            if(RebirthLogisticsEndpointIdentity.TryGetIncarnation(World,Station,out var birth)&&Guid.TryParseExact(birth,"N",out var parsedBirth))CustodyBirth=parsedBirth;
            Before=value.Original.Element("before").Elements().Single();var original=value.Original.Element("cachedOriginal");
            Owner=(string)original.Attribute("owner");Creation=(string)original.Attribute("creation");OwnerPurpose=(string)original.Attribute("queueOwner");
            Full=value.CurrentFull??original.Element("nativeOwningFullBoundary");
        }
        internal bool SourceCurrent()=>source!=null&&retention!=null&&CustodyBirth!=Guid.Empty&&source.Active&&source.SourceCurrent()&&ReferenceEquals(source.World,World)&&ReferenceEquals(source.Station,Station)&&
            RebirthLogisticsEndpointIdentity.TryGetIncarnation(World,Station,out var currentBirth)&&currentBirth==CustodyBirth.ToString("N")&&
            (source.Load?.Loaded==null||source.Load.Loaded.Installed(World));
        internal bool CurrentBefore()=>SourceCurrent()&&source.Saved&&!source.Physical&&source.NativeCurrent()&&
            RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(World,Station,out var fence)&&fence==source.AdmissionFence&&
            RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(Window,out var session)&&session==Session&&
            ReferenceEquals(Window.WorkstationData?.TileEntity,Station)&&ReferenceEquals(World.GetPrimaryPlayer(),Window.xui?.playerUI?.entityPlayer)&&
            source.Frame?.Observation.RanUpdate!=true&&QueueOwner?.Current()==true;
        internal bool IsIssuing()=>ReferenceEquals(issuingCachedReentry,this)&&
            RebirthProductiveEarnedUiDeferredTransport.IsActualCachedWindowOpen(Window,Session)&&SourceCurrent();
        internal XElement NativeOwnerOriginal=>source.Original.Element("cachedOriginal")?.Element("nativeOwnerOriginal")?.Elements().SingleOrDefault();
        internal IDisposable RetainOriginal()
        {if(!SourceCurrent())return null;references++;return new ReentryLease(this);}
        private sealed class ReentryLease:IDisposable
        {private CachedPrimaryReentry value;internal ReentryLease(CachedPrimaryReentry source){value=source;}public void Dispose(){System.Threading.Interlocked.Exchange(ref value,null)?.Release();}}
        private void Release(){if(--references!=0)return;Before=Full=null;Image=null;QueueOwner=null;source=null;retention?.Dispose();retention=null;}
        public void Dispose(){if(ownerReleased)return;ownerReleased=true;Release();}
        internal static CachedPrimaryReentry FromActualOpen(CachedCustody value,XUiC_WorkstationWindowGroup group,XUiC_RecipeStack card,long session)
            =>RebirthProductiveEarnedUiDeferredTransport.IsActualCachedWindowOpen(group,session)?new CachedPrimaryReentry(value,group,card,session):null;
    }
    [ThreadStatic]private static CachedPrimaryReentry issuingCachedReentry;
    internal static bool MatchesCachedGrantCard(XUiC_WorkstationWindowGroup group,XUiC_RecipeStack card,XElement before)
    {
        try
        {
            int slot=(int)before.Attribute("slot");var station=group?.WorkstationData?.TileEntity;var cards=group?.craftingQueue?.GetRecipesToCraft();
            if(station==null||cards==null||slot<0||slot>=cards.Length||slot>=station.Queue.Length||!ReferenceEquals(cards[slot],card)||
                !Eligible(card)||card.GetRecipeCount()!=(int)before.Attribute("beforeCount")||card.StartingEntityId!=(int)before.Attribute("startingOwner")||
                !RebirthRemoteResourcePlacedStationQueue.TryDecode((string)before.Element("recipeFull"),out var frozen)||frozen.Length!=1)return false;
            return RecipeBinding(station.Queue[slot])==RecipeBinding(frozen[0])&&ReferenceEquals(card.GetRecipe(),station.Queue[slot].Recipe);
        }
        catch{return false;}
    }
    internal static void ObserveCachedPrimaryWindowOpened(XUiC_WorkstationWindowGroup group,long session)
    {
        if(!RebirthProductiveEarnedUiDeferredTransport.IsActualCachedWindowOpen(group,session)||!ThreadManager.IsMainThread())return;
        RebirthProductiveEarnedUiDeferredTransport.ObserveRemoteCachedPrimaryWindowOpened(group,session);
        var world=GameManager.Instance?.World;var station=group?.WorkstationData?.TileEntity;var actor=group?.xui?.playerUI?.entityPlayer;
        if(world==null||world.IsRemote()||actor==null||!ReferenceEquals(world.GetPrimaryPlayer(),actor))return;
        var matches=cachedCustodies.Values.Where(value=>value.Active&&
            (ReferenceEquals(value.World,world)&&ReferenceEquals(value.Station,station)||ReferenceEquals(value.Load?.Loaded?.Tile,station)&&value.Load.Loaded.Installed(world))).ToArray();
        if(matches.Length!=1)return;var source=matches[0];CachedPrimaryReentry pending=null;
        try
        {
            if(!source.Saved||!source.PrimaryCurrent())return;
            var before=source.Original.Element("before").Elements().Single();var full=source.CurrentFull??source.Original.Element("cachedOriginal").Element("nativeOwningFullBoundary");
            if(!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||identity.StorageKey!=(string)before.Attribute("viewer")||
                RebirthCamperCharacterAccess.CurrentCreation(actor)!=(string)before.Attribute("creation")||actor.Died!=(int)before.Attribute("deaths")||actor.IsDead())return;
            source.World=world;source.Station=station;source.Chunk=station.GetChunk();source.WindowSession=session;
            if(source.Memory==null)
            {
                if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out var actualBirth)||
                    !Guid.TryParseExact(actualBirth,"N",out var birth)||birth==Guid.Empty)return;
                source.Memory=RebirthProductiveEarnedObservationBudget.TryOpenNativeCompletion(world,birth,source.Operation,source.Image);
            }
            if(!source.NativeCurrent())return;
            if(source.PhysicalObserved){TryRetireActualLoadedCachedPhysical(source,group,session);return;}
            if(source.RestoreAttempted)return;
            if(!RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(world,station,out source.AdmissionFence))return;
            int index=(int)before.Attribute("slot");var uiCards=group.craftingQueue.GetRecipesToCraft();
            if(index<0||index>=uiCards.Length||index>=station.Queue.Length||uiCards[index].GetRecipe()==null||
                uiCards[index].GetRecipeCount()!=(int)before.Attribute("beforeCount")||uiCards[index].StartingEntityId!=(int)before.Attribute("startingOwner")||
                !RebirthRemoteResourcePlacedStationQueue.TryDecode((string)before.Element("recipeFull"),out var frozen)||frozen.Length!=1||
                RecipeBinding(station.Queue[index])!=RecipeBinding(frozen[0]))return;
            var card=uiCards[index];if(!Eligible(card))return;
            var token=CachedPrimaryReentry.FromActualOpen(source,group,card,session);pending=token;if(token==null)return;
            issuingCachedReentry=token;
            try{if(!RebirthProductiveEarnedUiQueueOwner.TryBindCachedOriginal(token,station.Queue[index],out token.QueueOwner))return;}
            finally{issuingCachedReentry=null;}
            ItemStack[] primary;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {if(decoder==null)return;primary=RebirthProductiveEarnedUiRemote.ReadGrid(full,"primaryNative");}
            if(primary==null||primary.Length!=1||primary[0].IsEmpty()||primary[0].count!=card.GetRecipe().count)return;
            float cachedTime=(float)full.Attribute("cachedTime"),totalTime=(float)full.Attribute("cachedTotalTime");
            if(float.IsNaN(cachedTime)||float.IsInfinity(cachedTime)||cachedTime>0||float.IsNaN(totalTime)||float.IsInfinity(totalTime))return;
            var attempt=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("world",source.WorldGuid),
                new XElement(source.Original.Element("before")),new XElement("cachedPrimaryRestoreAttempt",new XAttribute("session",session),
                    new XAttribute("original",HistoricalHash(source.Image)),new XElement("primaryNative",(string)full.Element("primaryNative"))));
            if(!RebirthWorkstationCompletionPublication.Write(source.Path+".restore-attempt."+source.Epoch,attempt)||!token.SourceCurrent()||!source.NativeCurrent())return;
            source.RestoreAttempted=true;
            // These are native cached-output fields, not an inventory/output
            // insertion. The subsequent real native cached branch owns delivery.
            card.outputItemValue=primary[0].itemValue;card.isInventoryFull=true;card.craftingTimeLeft=cachedTime;
            card.totalCraftTimeLeft=totalTime;card.isCrafting=(bool)full.Attribute("cachedCrafting");
            var observed=RebirthRecipeOutputIntegration.BeginOwningObservation(card,true,source.Operation);if(observed==null)return;
            try
            {
                observed.CachedPredecessor=new XElement(full);
                if(!observed.TryCopyBeforeEvidence(out var continued))return;
                var frame=source.Frame;
                if(frame==null)
                {
                    string path=IntentPath("server",source.Operation);
                    frame=new Frame{Path=path,Intent=RebirthWorkstationCompletionPublication.ReadPrimary(path)};
                }
                else
                {if(cardsTryRemove(frame))frame.Observation.Dispose();}
                frame.Observation=observed;frame.Owner=token.Owner;frame.Creation=token.Creation;frame.EarlierOwner=token.QueueOwner;
                frame.CachedReentry?.Dispose();frame.CachedReentry=token;frame.Intent.Add(new XElement("cachedContinuation",continued));frame.Prepared=true;frame.Saved=false;frame.Closed=false;
                source.Frame=frame;source.Closed=false;pending=null;
                if(frame.NativeOriginal==null&&token.NativeOwnerOriginal!=null)frame.NativeOriginal=NativeAddOriginal.FromCachedOriginal(token,frame);
                if(!SaveFrame(frame))return;
                if(cards.TryGetValue(card,out var previous)&&!ReferenceEquals(previous,frame))return;
                cards.Remove(card);cards.Add(card,frame);frames[source.Operation]=frame;observed=null;
            }
            finally
            {RebirthRecipeOutputIntegration.EndOwningObservation(observed??source.Frame?.Observation);observed?.Dispose();}
        }
        catch{}
        finally{pending?.Dispose();}
    }
    private static bool cardsTryRemove(Frame frame)
    {if(cards.TryGetValue(frame.Observation.Card,out var actual)&&ReferenceEquals(actual,frame)){cards.Remove(frame.Observation.Card);return true;}return false;}
    internal sealed partial class NativeAddOriginal
    {
        internal static NativeAddOriginal FromCachedOriginal(CachedPrimaryReentry source,Frame frame)
        {
            if(source?.SourceCurrent()!=true||frame.CachedReentry!=source||source.NativeOwnerOriginal==null||nativeSources.ContainsKey(source.Operation))return null;
            var original=source.NativeOwnerOriginal;
            if((string)original.Attribute("operation")!=source.Operation.ToString("N")||(string)original.Attribute("world")!=frame.Observation.WorldGuid)return null;
            long size=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(original);
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8*size+65536),out var allocation))return null;
            IDisposable retention=null;
            try
            {
                string path=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(frame.Path),"nativeOwners",source.Operation.ToString("N")+".xml");
                if(!XNode.DeepEquals(original,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return null;
                retention=source.QueueOwner.RetainForCompletion();if(retention==null)return null;
                var result=new NativeAddOriginal(frame,path,new XElement(original),allocation,retention){cacheRestored=true};
                nativeSources.Add(result.Operation,result);allocation=retention=null;return result;
            }
            finally{retention?.Dispose();allocation?.Dispose();}
        }
    }
}
