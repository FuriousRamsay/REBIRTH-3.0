using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

public static partial class RebirthRecipeOutputIntegration
{
    private sealed class StationOutputBefore:IDisposable
    {
        internal Guid Operation=Guid.NewGuid();internal World World;internal EntityPlayerLocal Actor;
        internal XUiC_RecipeStack Card;internal XUiC_WorkstationWindowGroup Window;internal TileEntityWorkstation Tile;
        internal Recipe Recipe;internal int Count,Deaths;internal string Save,Birth,Creation,Owner,Queue,Path;
        internal byte[] Output,Input,Fuel,Tools,Bag,Belt,Equipment,Body;internal int Elapsed;
        internal bool ReturnedUpdate,SyncEntered,SyncReturned,Saved;internal XElement SavedRecord,OwnerSaved,OwnerFrame;
        internal float NextOwnerSave;
        internal long Session;
        internal IDisposable Memory;internal RebirthRemoteResourceNativePublication.Watch Watch;
        public void Dispose(){Watch?.Dispose();Watch=null;Memory?.Dispose();Memory=null;}
    }
    private static readonly List<StationOutputBefore> stationOutputBefores=new List<StationOutputBefore>();
    [ThreadStatic]private static StationOutputBefore syncingStationOutputBefore;
    private static bool StationOutputBeforeCurrent(StationOutputBefore source)
    {
        if(source==null||!ThreadManager.IsMainThread()||!ReferenceEquals(GameManager.Instance?.World,source.World)||source.World.IsRemote()||
            Path.GetFullPath(GameIO.GetSaveGameDir())!=source.Save||!ReferenceEquals(source.Actor.world,source.World)||
            !ReferenceEquals(source.World.GetPrimaryPlayer(),source.Actor)||!ReferenceEquals(source.World.GetEntity(source.Actor.entityId),source.Actor)||
            source.Actor.IsDead()||source.Actor.Died!=source.Deaths||RebirthCamperCharacterAccess.CurrentCreation(source.Actor)!=source.Creation||
            !ReferenceEquals(source.Card.GetParentByType<XUiC_WorkstationWindowGroup>(),source.Window)||
            source.Window.windowGroup?.isShowing!=true||!ReferenceEquals(source.Card.xui,source.Actor.PlayerUI?.xui)||
            !RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(source.Window,out var session)||session!=source.Session||
            !ReferenceEquals(source.Window.WorkstationData?.TileEntity,source.Tile)||!ReferenceEquals(source.Card.GetRecipe(),source.Recipe)||
            source.Card.GetRecipeCount()!=source.Count||!ReferenceEquals(source.World.GetTileEntity(source.Tile.ToWorldPos()),source.Tile)||
            !RebirthLogisticsEndpointIdentity.TryGetIncarnation(source.World,source.Tile,out var birth)||birth!=source.Birth||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner.StorageKey!=source.Owner)return false;
        if(!source.ReturnedUpdate)return true;
        return source.Queue==RebirthRemoteResourcePlacedStationQueue.Encode(source.Window.craftingQueue.GetRecipesToCraft().Select(ActualCardRow).ToArray())&&
            source.Output.SequenceEqual(OwningGrid(source.Window.outputWindow.GetSlots()))&&
            source.Input.SequenceEqual(OwningGrid(source.Window.inputWindow?.GetSlots()??source.Tile.Input))&&
            source.Fuel.SequenceEqual(OwningGrid(source.Window.fuelWindow?.GetSlots()??source.Tile.Fuel))&&
            source.Tools.SequenceEqual(OwningGrid(source.Window.GetChildByType<XUiC_WorkstationToolGrid>()?.GetSlots()??source.Tile.Tools))&&
            source.Bag.SequenceEqual(OwningGrid(source.Actor.bag.ItemGrid.items))&&source.Belt.SequenceEqual(OwningGrid(source.Actor.inventory.ItemGrid.items))&&
            source.Equipment.SequenceEqual(OwningEquipmentNative(source.Actor))&&source.OwnerFrame!=null&&
            XNode.DeepEquals(source.OwnerFrame,RebirthProductiveEarnedUiOwnerFile.CaptureDeferredLiveFrame(source.Actor,new[]{source.Recipe.GetName()}))&&
            (source.Body==null||RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Tile,source.Body,source.Elapsed));
    }
    private static bool EnsureStationOutputBefore(XUiC_RecipeStack card)
    {
        var window=card.GetParentByType<XUiC_WorkstationWindowGroup>();var actor=card.xui.playerUI.entityPlayer;
        if(actor.world.IsRemote()||window?.outputWindow==null||window.WorkstationData?.TileEntity is not TileEntityWorkstation tile||tile is RebirthCamperStationState)return true;
        var existing=stationOutputBefores.FirstOrDefault(value=>ReferenceEquals(value.Card,card));
        if(existing!=null)return existing.Saved&&StationOutputBeforeCurrent(existing);
        if(stationOutputBefores.Any(value=>ReferenceEquals(value.Window,window)))return false;
        if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(actor.world,tile,out string birth)||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||
            !RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(window,out long session)||
            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out var memory))return false;
        var source=new StationOutputBefore{World=actor.world,Actor=actor,Card=card,Window=window,Tile=tile,Recipe=card.GetRecipe(),Count=card.GetRecipeCount(),
            Deaths=actor.Died,Creation=RebirthCamperCharacterAccess.CurrentCreation(actor),Owner=owner.StorageKey,Birth=birth,Memory=memory,
            Save=Path.GetFullPath(GameIO.GetSaveGameDir()),Session=session};
        source.Path=Path.Combine(source.Save,"rebirth","recipe-outputs","station-before",source.Operation.ToString("N")+".xml");
        stationOutputBefores.Add(source);return false;
    }
    // Native Update changes its timer even when outputStack returns false.
    // Freeze only its actual returned row, before the next Update can run.
    internal static void ObserveStationOutputBeforeUpdate(XUiC_RecipeStack card,bool ran,Exception error)
    {
        var source=stationOutputBefores.FirstOrDefault(value=>ReferenceEquals(value.Card,card));
        if(source==null||source.ReturnedUpdate||!ran||error!=null||!StationOutputBeforeCurrent(source))return;
        source.Queue=RebirthRemoteResourcePlacedStationQueue.Encode(source.Window.craftingQueue.GetRecipesToCraft().Select(ActualCardRow).ToArray());
        source.Output=OwningGrid(source.Window.outputWindow.GetSlots());source.Input=OwningGrid(source.Window.inputWindow?.GetSlots()??source.Tile.Input);
        source.Fuel=OwningGrid(source.Window.fuelWindow?.GetSlots()??source.Tile.Fuel);source.Tools=OwningGrid(source.Window.GetChildByType<XUiC_WorkstationToolGrid>()?.GetSlots()??source.Tile.Tools);
        source.Bag=OwningGrid(source.Actor.bag.ItemGrid.items);source.Belt=OwningGrid(source.Actor.inventory.ItemGrid.items);source.Equipment=OwningEquipmentNative(source.Actor);
        if(!RebirthProductiveEarnedUiOwnerFile.TryMeasureDeferredLiveFrame(source.Actor,new[]{source.Recipe.GetName()},out int measured)||measured>8*1024*1024)return;
        source.OwnerFrame=RebirthProductiveEarnedUiOwnerFile.CaptureDeferredLiveFrame(source.Actor,new[]{source.Recipe.GetName()});
        source.ReturnedUpdate=true;
    }
    private static bool CanAdvanceStationOutputBefore(XUiC_RecipeStack card)
    {
        var window=card.GetParentByType<XUiC_WorkstationWindowGroup>();
        var source=stationOutputBefores.FirstOrDefault(value=>ReferenceEquals(value.Window,window));
        return source==null||ReferenceEquals(source.Card,card)&&source.Saved&&StationOutputBeforeCurrent(source);
    }
    internal static void ObserveStationOutputBeforeSync(XUiC_WorkstationWindowGroup window,bool ran,Exception error)
    {
        try{ObserveStationOutputQueueSync(window,ran,error);}catch{/* Independent exact queue purpose; preserve native outcome. */}
        // A later existing normal UI flush may finish the same preparatory
        // purpose after an earlier skipped flush. The purpose never resends a
        // sync; only an actual returned native callback can qualify its tuple.
        var source=syncingStationOutputBefore??stationOutputBefores.SingleOrDefault(value=>ReferenceEquals(value.Window,window)&&value.SyncEntered&&value.ReturnedUpdate);
        if(source==null||!source.SyncEntered||source.SyncReturned||!ReferenceEquals(window,source.Window)||!ran||error!=null||
            !StationOutputBeforeCurrent(source)||source.Queue!=RebirthRemoteResourcePlacedStationQueue.Encode(source.Tile.Queue)||
            !source.Output.SequenceEqual(OwningGrid(source.Tile.Output))||!source.Input.SequenceEqual(OwningGrid(source.Tile.Input))||
            !source.Fuel.SequenceEqual(OwningGrid(source.Tile.Fuel))||!source.Tools.SequenceEqual(OwningGrid(source.Tile.Tools)))return;
        if(!RebirthProductiveEarnedNativeBody.TryCaptureCurrent(source.Tile,out var body,out int offset))return;
        source.Body=body;source.Elapsed=offset;source.SyncReturned=true;
    }
    private static void PumpStationOutputBefores()
    {
        foreach(var source in stationOutputBefores.ToArray())
        {
            if(!ReferenceEquals(GameManager.Instance?.World,source.World)){source.Dispose();stationOutputBefores.Remove(source);continue;}
            try
            {
                // Membership ends synchronously when the output original is
                // retained, before its first effect. A source still in this
                // collection therefore has never generated or paid an output.
                // A normal owner/UI edit may discard this unused preparation;
                // its old saved images never qualify a replacement purpose.
                if(!StationOutputBeforeCurrent(source))
                {source.Dispose();stationOutputBefores.Remove(source);continue;}
                if(source.Saved||!source.ReturnedUpdate)continue;
                if(!source.SyncEntered)
                {
                    var intent=new XElement("recipeOutputStationBefore",new XAttribute("operation",source.Operation.ToString("N")),
                        new XAttribute("world",source.World.Guid),new XAttribute("owner",source.Owner),new XAttribute("creation",source.Creation),
                        new XAttribute("deaths",source.Deaths),new XAttribute("birth",source.Birth),new XElement("queue",source.Queue),
                        new XElement("output",Convert.ToBase64String(source.Output)));
                    if(!PublishPersonalColdBinding(source.Path,UiOriginalBytes(intent)))continue;
                    source.SyncEntered=true;var previous=syncingStationOutputBefore;syncingStationOutputBefore=source;
                    try{source.Window.syncTEfromUI();}finally{syncingStationOutputBefore=previous;}
                }
                if(!source.SyncReturned||!StationOutputBeforeCurrent(source))continue;
                if(source.OwnerSaved==null)
                {
                    if(UnityEngine.Time.realtimeSinceStartup>=source.NextOwnerSave)
                    {source.NextOwnerSave=UnityEngine.Time.realtimeSinceStartup+1f;GameManager.Instance.SaveLocalPlayerData();}
                    if(source.OwnerSaved==null)continue;
                }
                if(source.Watch?.Current()!=true)
                {
                    source.Watch?.Dispose();source.Watch=null;
                    if(!RebirthRemoteResourceNativeBinding.TryResolve(source.World,RemoteResourceIdentity.Workstation(source.Tile.ToWorldPos()),
                        RemoteResourceSourceKind.WorkstationOutput,source.Birth,out var binding))continue;
                    source.Watch=RebirthRemoteResourceNativePublication.Begin(binding,()=>StationOutputBeforeCurrent(source));
                }
                if(source.Watch==null||!source.Watch.TryReadPublishedTileDescriptor(out var publication,out long offset,out int length,out string digest)||
                    !StationOutputBeforeCurrent(source))continue;
                var saved=new XElement("recipeOutputStationBeforeSaved",new XAttribute("operation",source.Operation.ToString("N")),
                    new XAttribute("birth",source.Birth),new XAttribute("nativePath",publication.Path),new XAttribute("nativeDigest",publication.PayloadDigest),
                    new XAttribute("tileOffset",offset),new XAttribute("tileLength",length),new XAttribute("tileDigest",digest),new XAttribute("elapsedOffset",source.Elapsed),
                    new XElement("queue",source.Queue),new XElement("output",Convert.ToBase64String(source.Output)),new XElement("nativeBody",Convert.ToBase64String(source.Body)),
                    new XElement(source.OwnerSaved));
                if(!PublishPersonalColdBinding(source.Path+".saved",UiOriginalBytes(saved)))continue;
                source.SavedRecord=saved;source.Saved=true;
            }
            catch{/* No generation/effect has entered this gate; no sync replay. */}
        }
    }
    private static XElement StationOutputSavedBefore(UiOutputOriginal original)
    {
        if(original.World.IsRemote()||original.Scope.Cached||original.Scope.Grid==null||original.StationBirth==null)return null;
        var source=stationOutputBefores.SingleOrDefault(value=>ReferenceEquals(value.Card,original.Scope.Card));
        if(source?.Saved!=true||!StationOutputBeforeCurrent(source)||source.SavedRecord==null)
            throw new InvalidOperationException("Station output has no saved native ready-card original.");
        return new XElement("stationSavedBefore",new XAttribute("path",source.Path),new XElement(source.SavedRecord));
    }
    private static void ConsumeStationOutputBefore(UiOutputOriginal original)
    {
        var source=stationOutputBefores.FirstOrDefault(value=>ReferenceEquals(value.Card,original.Scope.Card));
        if(source?.Saved!=true)return;
        stationOutputBefores.Remove(source);source.Dispose();
    }
}
