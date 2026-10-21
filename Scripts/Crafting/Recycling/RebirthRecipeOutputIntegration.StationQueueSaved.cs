using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

public static partial class RebirthRecipeOutputIntegration
{
    // The actual owning UI already inserted its combined grid and decremented
    // its row. Keep that original until the same native tile publication proves
    // output, complete queue and CraftCompleteList together. No replay/setter.
    private sealed class StationOutputQueue:IDisposable
    {
        internal UiOutputPending Pending;internal TileEntityWorkstation Tile;
        internal XUiC_WorkstationWindowGroup Window;internal string Queue;
        internal byte[] Body;internal int Elapsed;internal IDisposable Memory;
        internal bool NativeReturned;
        internal RebirthRemoteResourceNativeBinding Binding;
        internal RebirthRemoteResourceNativePublication.Watch Watch;
        public void Dispose(){Watch?.Dispose();Watch=null;Memory?.Dispose();Memory=null;Body=null;}
    }
    private static readonly List<StationOutputQueue> stationOutputQueues=new List<StationOutputQueue>();
    private static bool StationOutputQueueCurrent(StationOutputQueue saved)
    {
        var pending=saved?.Pending;var source=pending?.Source;var actor=pending?.Actor;
        if(saved?.NativeReturned==true&&source!=null)
            return ThreadManager.IsMainThread()&&!source.World.IsRemote()&&ReferenceEquals(GameManager.Instance?.World,source.World)&&
                Path.GetFullPath(GameIO.GetSaveGameDir())==source.Save&&pending.Returned&&pending.QueueObserved&&source.GridAccepted&&
                RebirthStablePlayerIdentity.TryFromLocalPlatform(out var settledOwner)&&settledOwner.StorageKey==source.Owner&&
                ReferenceEquals(source.World.GetTileEntity(saved.Tile.ToWorldPos()),saved.Tile)&&
                RebirthLogisticsEndpointIdentity.TryGetIncarnation(source.World,saved.Tile,out var settledBirth)&&settledBirth==source.StationBirth&&
                saved.Queue==RebirthRemoteResourcePlacedStationQueue.Encode(saved.Tile.Queue)&&
                source.GridExpected.SequenceEqual(OwningGrid(saved.Tile.Output))&&saved.Body!=null&&
                RebirthProductiveEarnedNativeBody.MatchesCurrent(saved.Tile,saved.Body,saved.Elapsed);
        if(source==null||actor==null||source.World.IsRemote()||!ThreadManager.IsMainThread()||
            !ReferenceEquals(GameManager.Instance?.World,pending.World)||Path.GetFullPath(GameIO.GetSaveGameDir())!=source.Save||
            !ReferenceEquals(actor.world,pending.World)||!ReferenceEquals(pending.World.GetEntity(actor.entityId),actor)||actor.IsDead()||actor.Died!=pending.Deaths||
            RebirthCamperCharacterAccess.CurrentCreation(actor)!=pending.Creation||!pending.Returned||!pending.QueueObserved||!source.GridAccepted||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||identity.StorageKey!=source.Owner||
            !ReferenceEquals(source.Scope.Card.GetParentByType<XUiC_WorkstationWindowGroup>(),saved.Window)||
            !ReferenceEquals(saved.Window.WorkstationData?.TileEntity,saved.Tile)||saved.Tile is RebirthCamperStationState||
            !RebirthLogisticsEndpointIdentity.TryGetIncarnation(pending.World,saved.Tile,out var birth)||birth!=source.StationBirth||
            !pendingUiOutputs.TryGetValue(source.Scope.Card,out var retained)||!ReferenceEquals(retained,pending)||
            source.Scope.Card.GetRecipeCount()!=pending.Count-1||
            (pending.Count==1?source.Scope.Card.GetRecipe()!=null:!ReferenceEquals(source.Scope.Card.GetRecipe(),pending.Recipe))||
            !source.GridExpected.SequenceEqual(OwningGrid(source.Scope.Grid.GetSlots()))||!source.GridExpected.SequenceEqual(OwningGrid(saved.Tile.Output))||
            saved.Queue!=RebirthRemoteResourcePlacedStationQueue.Encode(saved.Window.craftingQueue.GetRecipesToCraft().Select(ActualCardRow).ToArray())||
            saved.Queue!=RebirthRemoteResourcePlacedStationQueue.Encode(saved.Tile.Queue))return false;
        return saved.Body==null||RebirthProductiveEarnedNativeBody.MatchesCurrent(saved.Tile,saved.Body,saved.Elapsed);
    }
    private static bool RegisterUiOutputStationQueue(UiOutputPending pending)
    {
        var source=pending?.Source;var window=source?.Scope.Card.GetParentByType<XUiC_WorkstationWindowGroup>();
        if(source==null||source.World.IsRemote()||source.Scope.Grid==null||source.StationBirth==null||
            window?.WorkstationData?.TileEntity is not TileEntityWorkstation tile||tile is RebirthCamperStationState)return false;
        if(!stationOutputQueues.Any(value=>ReferenceEquals(value.Pending,pending)))
        {
            stationOutputQueues.Add(new StationOutputQueue{Pending=pending,Tile=tile,Window=window,
                Queue=RebirthRemoteResourcePlacedStationQueue.Encode(window.craftingQueue.GetRecipesToCraft().Select(ActualCardRow).ToArray())});
        }
        return true;
    }
    private static bool HoldsUiOutputStationQueue(XUiC_RecipeStack card)
    {
        var window=card?.GetParentByType<XUiC_WorkstationWindowGroup>();
        return window!=null&&stationOutputQueues.Any(value=>ReferenceEquals(value.Tile,window.WorkstationData?.TileEntity));
    }
    internal static bool CanAdvanceStationOutputQueueNative(TileEntityWorkstation tile)
        =>tile==null||!stationOutputQueues.Any(value=>ReferenceEquals(value.Tile,tile));
    private static void ObserveStationOutputQueueSync(XUiC_WorkstationWindowGroup window,bool ran,Exception error)
    {
        if(!ran||error!=null)return;
        foreach(var saved in stationOutputQueues.Where(value=>!value.NativeReturned&&ReferenceEquals(value.Window,window)).ToArray())
        {
            if(!StationOutputQueueCurrent(saved))continue;
            if(saved.Body==null)
            {
                if(!RebirthProductiveEarnedNativeBody.TryCaptureCurrent(saved.Tile,out var body,out int offset)||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*body.Length+65536),out var memory))continue;
                saved.Body=body;saved.Elapsed=offset;saved.Memory=memory;
            }
            var source=saved.Pending.Source;
            var proof=new XElement("recipeOutputStationQueueSyncReturned",new XAttribute("operation",source.Operation.ToString("N")),
                new XAttribute("original",UiOriginalHash(source.Original)),new XAttribute("birth",source.StationBirth),
                new XAttribute("elapsedOffset",saved.Elapsed),new XElement("nativeBody",Convert.ToBase64String(saved.Body)),new XElement("queueAfter",saved.Queue));
            if(PublishPersonalColdBinding(source.Path+".station-queue-sync-returned",UiOriginalBytes(proof))&&StationOutputQueueCurrent(saved))saved.NativeReturned=true;
        }
    }
    private static void PumpStationOutputQueues()
    {
        foreach(var saved in stationOutputQueues.ToArray())
        {
            var pending=saved.Pending;var source=pending?.Source;
            if(source==null||!ReferenceEquals(GameManager.Instance?.World,source.World))
            {saved.Dispose();stationOutputQueues.Remove(saved);continue;}
            try
            {
                if(!StationOutputQueueCurrent(saved))continue;
                if(saved.Body==null)
                {
                    if(!RebirthProductiveEarnedNativeBody.TryCaptureCurrent(saved.Tile,out var body,out int offset)||
                        !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*body.Length+65536),out var memory))continue;
                    saved.Body=body;saved.Elapsed=offset;saved.Memory=memory;
                }
                if(saved.Binding==null&&!RebirthRemoteResourceNativeBinding.TryResolve(source.World,RemoteResourceIdentity.Workstation(saved.Tile.ToWorldPos()),
                    RemoteResourceSourceKind.WorkstationOutput,source.StationBirth,out saved.Binding))continue;
                if(!StationOutputQueueCurrent(saved))continue;
                if(saved.Watch?.Current()!=true)
                {saved.Watch?.Dispose();saved.Watch=RebirthRemoteResourceNativePublication.Begin(saved.Binding,()=>StationOutputQueueCurrent(saved));}
                if(saved.Watch==null||!saved.Watch.TryReadPublishedTileDescriptor(out var publication,out long offsetSaved,out int length,out string digest)||
                    !StationOutputQueueCurrent(saved))continue;
                XElement ownerSaved=null;
                if(source.GridCallbackFence&&!ReadStationOutputCallbackOwnerSaved(source,out ownerSaved))continue;
                var proof=new XElement("recipeOutputStationQueueSaved",new XAttribute("operation",source.Operation.ToString("N")),
                    new XAttribute("original",UiOriginalHash(source.Original)),new XAttribute("birth",source.StationBirth),
                    new XAttribute("beforeCount",pending.Count),new XAttribute("afterCount",pending.Count-1),
                    new XAttribute("nativePath",publication.Path),new XAttribute("nativeDigest",publication.PayloadDigest),
                    new XAttribute("tileOffset",offsetSaved),new XAttribute("tileLength",length),new XAttribute("tileDigest",digest),
                    new XAttribute("elapsedOffset",saved.Elapsed),new XElement("nativeBody",Convert.ToBase64String(saved.Body)),
                    new XElement("outputAfter",Convert.ToBase64String(source.GridExpected)),new XElement("queueAfter",saved.Queue));
                if(ownerSaved!=null)proof.Add(new XElement("callbackOwnerSaved",new XElement(ownerSaved)));
                if(!PublishPersonalColdBinding(source.Path+".station-queue-saved",UiOriginalBytes(proof))||!StationOutputQueueCurrent(saved))continue;
                pending.QueueSaved=true;pendingUiOutputs.Remove(source.Scope.Card);pending.Source=null;
                saved.Dispose();stationOutputQueues.Remove(saved);source.Dispose();
            }
            catch{/* Retain the actual already-applied output; never repeat it. */}
        }
    }
    private static bool ReadStationOutputCallbackOwnerSaved(UiOutputOriginal source,out XElement saved)
    {
        saved=null;int index=source.CallbackPrefix-1;
        if(index<0||!ReadPersonalOutputSidecar(source.Path+".native-continuation-returned",out var returned,out var callback)||
            !PersonalColdIdentity(callback,"recipeOutputNativeContinuationReturned","operation,original,outputPrefix","",source.Operation,UiOriginalHash(source.Original))||
            (int?)callback.Attribute("outputPrefix")!=source.CallbackPrefix||
            !ReadPersonalOutputSidecar(source.Path+".output-"+index+".owner-saved",out _,out var owner)||
            !PersonalColdIdentity(owner,"recipeOutputOwnerPrefixSaved","operation,index,original,accepted,owner,creation,deaths,entity,playerFile,nativeDigest,nativeLength","",source.Operation,UiOriginalHash(source.Original))||
            (int?)owner.Attribute("index")!=index||(string)owner.Attribute("accepted")!=UiOriginalHash(returned)||
            (string)owner.Attribute("owner")!=source.Owner||(string)owner.Attribute("creation")!=source.Creation||(int?)owner.Attribute("deaths")!=source.Deaths||
            (int?)owner.Attribute("entity")!=source.Actor.entityId||(int?)owner.Attribute("nativeLength")<6||(int?)owner.Attribute("nativeLength")>16*1024*1024||
            Convert.FromBase64String((string)owner.Attribute("nativeDigest")).Length!=32||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||identity.StorageKey!=source.Owner||
            Path.GetFullPath((string)owner.Attribute("playerFile"))!=Path.GetFullPath(Path.Combine(GameIO.GetPlayerDataDir(),identity.CanonicalId+"."+PlayerDataFile.EXT)))return false;
        saved=owner;return true;
    }
}
