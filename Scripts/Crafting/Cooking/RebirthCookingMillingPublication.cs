using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Xml.Linq;

// A milling result and its queue retirement form one native projection. The
// Output property otherwise sends setModified before the old queue is removed.
internal static partial class RebirthCookingMillingPublication
{
    private sealed class Original:IDisposable
    {
        internal World World;internal TileEntityWorkstation Station;internal Chunk Chunk;internal Recipe Recipe;internal EntityVehicle Vehicle;
        internal XUiC_RebirthCookingStation Window;internal EntityPlayerLocal Actor;internal XUiC_RecipeStack Card;
        internal ItemStack[] Output;internal string Creation;internal int Deaths;internal bool Mutated,Finished,Cleared;internal IDisposable Allocation;
        internal byte[] QueueAfter,NativeBefore,NativeAfter,BagBefore,BeltBefore,Intent;internal int BeforeOffset,AfterOffset;internal string Path;internal bool ChunkOwned,NativeAccepted,CloseUnusedObserved;
        internal Guid Operation;internal XElement OriginalImage;internal bool NativeAttempted,NativeRan,NativeUncertain;internal string NativeAfterMode;
        internal long Session;internal string Owner;internal ItemStack[] UiInput,UiTools,UiFuel;internal bool UnusedAdmission;
        internal string StationBirth;internal byte[] SavedBody;internal int SavedOffset;internal IDisposable SavedMemory;
        internal RebirthRemoteResourceNativePublication.Watch SavedWatch;internal RebirthCamperSavedVehicle.Expected SavedVehicle;
        internal Action ColdRelease;
        internal bool MobileTickReturned=true;
        internal bool StationTickReturned=true;
        internal bool Current()=>RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Path,Intent)&&ReferenceEquals(GameManager.Instance?.World,World)&&
            (Vehicle==null?ReferenceEquals(Station.GetChunk(),Chunk):ReferenceEquals(Vehicle.world,World)&&ReferenceEquals(World.GetEntity(Vehicle.entityId),Vehicle)&&
                ReferenceEquals(RebirthCamperModuleRuntime.For(Vehicle).Stations.Find(((RebirthCamperStationState)Station).InstallationId)?.State,Station)&&!RebirthCamperModuleRuntime.For(Vehicle).TransferPending)&&
            (Window==null?Vehicle!=null||ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station):
            ReferenceEquals(Window.WorkstationData?.TileEntity,Station)&&
            ReferenceEquals(World.GetPrimaryPlayer(),Actor)&&Actor.Died==Deaths&&!Actor.IsDead()&&RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation);
        internal bool NativeTerminal()=>Mutated&&Same(Station.Output,Output)&&QueueAfter!=null&&Queue(Station.Queue).SequenceEqual(QueueAfter);
        internal bool Terminal()=>NativeTerminal()&&
            (NativeAccepted||Window==null||Cleared&&Same(Window.outputWindow?.GetSlots(),Output)&&Window.craftingQueue!=null&&
                Queue(Rows(Window)).SequenceEqual(QueueAfter));
        internal bool NativeUnchanged()=>NativeBefore!=null&&(Vehicle==null?RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,NativeBefore,BeforeOffset):
            Station is RebirthCamperStationState mobile&&ReferenceEquals(mobile.BoundVehicle,Vehicle)&&RebirthCamperStationSnapshot.Capture(mobile.InstallationId,mobile).SequenceEqual(NativeBefore));
        internal bool MetadataCurrent()=>ReferenceEquals(GameManager.Instance?.World,World)&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Path,Intent)&&
            (Vehicle==null?ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station):ReferenceEquals(Vehicle.world,World)&&
                ReferenceEquals(RebirthCamperModuleRuntime.For(Vehicle).Stations.Find(((RebirthCamperStationState)Station).InstallationId)?.State,Station));
        internal bool LiveWindowCurrent()=>Window!=null&&Current()&&
            (RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(Window,out var session)&&session==Session||
             ReferenceEquals(closingMilling?.Original,this)&&RebirthProductiveEarnedUiDeferredTransport.TryGetObservedNativeUiSession(Window,out var closingSession)&&closingSession==Session)&&
            RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&owner.StorageKey==Owner&&Actor.PlayerUI?.xui?.DragAndDropWindow?.IsEmpty()==true&&
            Same(Window.GetChildByType<XUiC_WorkstationInputGrid>()?.GetSlots()??Station.Input,UiInput)&&
            Same(Window.GetChildByType<XUiC_WorkstationToolGrid>()?.GetSlots()??Station.Tools,UiTools)&&
            Same(Window.GetChildByType<XUiC_WorkstationFuelGrid>()?.GetSlots()??Station.Fuel,UiFuel)&&
            RebirthCamperInventorySnapshot.Capture(Actor.bag.ItemGrid.items).SequenceEqual(BagBefore)&&RebirthCamperInventorySnapshot.Capture(Actor.inventory.ItemGrid.items).SequenceEqual(BeltBefore)&&
            (!Mutated||Same(Window.outputWindow.GetSlots(),Output)&&Queue(Rows(Window)).SequenceEqual(QueueAfter));
        internal void ReleaseChunk(){if(ChunkOwned&&Chunk!=null){lock(Chunk)Chunk.InProgressSaving=false;ChunkOwned=false;}}
        public void Dispose()
        {
            SavedWatch?.Dispose();SavedWatch=null;SavedVehicle?.Dispose();SavedVehicle=null;SavedMemory?.Dispose();SavedMemory=null;SavedBody=null;
            Output=UiInput=UiTools=UiFuel=null;Recipe=null;Card=null;Window=null;Actor=null;OriginalImage=null;QueueAfter=NativeBefore=NativeAfter=BagBefore=BeltBefore=Intent=null;
            ReleaseChunk();
            Chunk=null;Station=null;Vehicle=null;World=null;Allocation?.Dispose();Allocation=null;
        }
    }
    private static readonly List<Original> pending=new List<Original>();private static bool installed;
    [ThreadStatic]private static Original mutating;
    private sealed class MillingClose{internal Original Original;internal MillingClose Previous;}
    [ThreadStatic]private static MillingClose closingMilling;
    private sealed class MillingSync{internal Original Original;internal MillingSync Previous;}
    [ThreadStatic]private static MillingSync syncingMilling;
    [ThreadStatic]private static Original publishingTile;
    private static bool Same(ItemStack[] a,ItemStack[] b)=>a!=null&&b!=null&&a.Length==b.Length&&
        a.Select((item,index)=>RebirthStationGridIngredients.IsSameStackSnapshot(item,b[index])).All(value=>value);
    internal static bool Held(TileEntityWorkstation station)=>HasMobileColdHold(station)||HasStaticMillingSource(station)||pending.Any(value=>!value.UnusedAdmission&&ReferenceEquals(value.World,GameManager.Instance?.World)&&ReferenceEquals(value.Station,station));
    internal static bool HoldsOwner(EntityPlayerLocal actor)=>actor!=null&&pending.Any(value=>!value.UnusedAdmission&&ReferenceEquals(value.Actor,actor)&&ReferenceEquals(value.World,GameManager.Instance?.World)&&actor.Died==value.Deaths&&!actor.IsDead());
    internal static bool CanAdvance(XUiC_RecipeStack card)=>!Held(card?.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity);
    internal static bool CanSync(XUiC_WorkstationWindowGroup window)
    {
        var original=pending.FirstOrDefault(value=>!value.UnusedAdmission&&ReferenceEquals(value.Window,window)&&ReferenceEquals(value.Station,window?.WorkstationData?.TileEntity));
        if(original==null)return true;
        try{return !original.NativeAccepted&&!original.NativeRan&&!original.NativeUncertain&&original.Mutated&&original.LiveWindowCurrent()&&original.NativeUnchanged();}
        catch{return false;}
    }
    internal static bool CanRotate(XUiC_CraftingQueue queue)=>mutating?.Window!=null&&ReferenceEquals(queue?.GetParentByType<XUiC_WorkstationWindowGroup>(),mutating.Window)||!Held(queue?.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity);
    internal static void Install()
    {
        if(installed)return;RebirthProductiveEarnedNativeBody.Install();RebirthCookingOutputDelivery.Install();
        InstallMobileNativeSourceObservers();
        RebirthProductiveEarnedCancellation.InstallBeforeFlushObserver();RebirthProductiveEarnedUiDeferredTransport.InstallRecipientWindowObserver();
        ModEvents.GameUpdate.RegisterHandler(Pump);installed=true;
    }
    private static long Measure(ItemStack[] output)
    {
        long length=8;foreach(var stack in output)
        {if(stack?.itemValue==null||stack.count<0)throw new InvalidDataException();
            using(var count=new RebirthCamperSavedVehicle.CountStream(8*1024*1024))using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {var encoding=writer.Encoding;try{writer.Encoding=new UTF8Encoding(false,false);writer.SetBaseStream(count);stack.itemValue.Write(writer);writer.Flush();length=checked(length+8+count.Length);}
             finally{writer.Encoding=encoding;}}}return length;
    }
    private static byte[] Queue(RecipeQueueItem[] rows)
    {using(var memory=new MemoryStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {writer.SetBaseStream(memory);writer.Write(rows.Length);foreach(var row in rows)row.Write(writer);writer.Flush();if(memory.Length>2*1024*1024)throw new InvalidDataException();return memory.ToArray();}}
    private static RecipeQueueItem[] Rows(XUiC_WorkstationWindowGroup window)=>window.craftingQueue.GetRecipesToCraft().Select(card=>new RecipeQueueItem
        {Recipe=card.GetRecipe(),Multiplier=(short)card.GetRecipeCount(),CraftingTimeLeft=card.GetRecipeCraftingTimeLeft(),IsCrafting=card.IsCrafting,
         Quality=(byte)card.OutputQuality,StartingEntityId=card.StartingEntityId,OneItemCraftTime=card.GetOneItemCraftTime()}).ToArray();
    private static Original Begin(TileEntityWorkstation station,Recipe recipe,ItemStack[] output,RecipeQueueItem[] queueAfter,XUiC_RebirthCookingStation window=null,XUiC_RecipeStack card=null,XElement takenOwner=null)
    {
        if(!ThreadManager.IsMainThread()||station==null||recipe==null||output==null||Held(station)||pending.Count>=4||station.bDisableModifiedCheck||RebirthRemoteResourceSourceCustody.BlocksStation(station))return null;
        var mobile=station as RebirthCamperStationState;var chunk=station.GetChunk();if(chunk==null&&mobile==null)return null;
        if(mobile!=null&&(mobile.BoundVehicle==null||mobile.BoundVehicle.world?.IsRemote()==true))return null;
        long size=Measure(output);
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(64L*1024*1024+8L*size),out var allocation))return null;
        Original original=null;
        try
        {
            original=new Original{World=GameManager.Instance.World,Station=station,Chunk=chunk,Vehicle=mobile?.BoundVehicle,Recipe=recipe,Output=ItemStack.Clone(output),Allocation=allocation,QueueAfter=Queue(queueAfter)};
            original.MobileTickReturned=mobile==null||!ReferenceEquals(millingVehicleTick,mobile.BoundVehicle);
            original.StationTickReturned=!ReferenceEquals(millingStationTick,station);
            if(mobile==null&&!original.World.IsRemote()&&!RebirthLogisticsEndpointIdentity.TryGetIncarnation(original.World,station,out original.StationBirth))throw new InvalidDataException();
            if(window!=null)
            {
                original.Window=window;original.Card=card;original.Actor=card.xui.playerUI.entityPlayer;
                original.Creation=RebirthCamperCharacterAccess.CurrentCreation(original.Actor);original.Deaths=original.Actor.Died;
                if(window.windowGroup?.isShowing!=true||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||string.IsNullOrEmpty(original.Creation)||
                    RebirthCookingOutputDelivery.IsHeld(original.Actor)||RebirthDogLifecycleService.IsHireInventoryHeld(original.Actor)||
                    mobile==null&&LockManager.Instance?.IsLockedByLocalPlayer(station,0)!=true||
                    !RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(window,out original.Session))throw new InvalidDataException();
                original.Owner=owner.StorageKey;
                original.UiInput=ItemStack.Clone(window.GetChildByType<XUiC_WorkstationInputGrid>()?.GetSlots()??station.Input);
                original.UiTools=ItemStack.Clone(window.GetChildByType<XUiC_WorkstationToolGrid>()?.GetSlots()??station.Tools);
                original.UiFuel=ItemStack.Clone(window.GetChildByType<XUiC_WorkstationFuelGrid>()?.GetSlots()??station.Fuel);
                original.BagBefore=RebirthCamperInventorySnapshot.Capture(original.Actor.bag.ItemGrid.items);
                original.BeltBefore=RebirthCamperInventorySnapshot.Capture(original.Actor.inventory.ItemGrid.items);
            }
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null)throw new InvalidOperationException();
                if(mobile==null){if(!RebirthProductiveEarnedNativeBody.TryCaptureCurrent(station,out original.NativeBefore,out original.BeforeOffset))throw new InvalidDataException();}
                else original.NativeBefore=RebirthCamperStationSnapshot.Capture(mobile.InstallationId,mobile);
            }
            original.Operation=Guid.NewGuid();original.Path=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("cookingMilling"),original.Operation.ToString("N")+".xml");
            // Mobile stations have no chunk position. Their vehicle/installation IDs below own the binding.
            var position=mobile==null?station.ToWorldPos():default(Vector3i);
            var intent=new XElement("cookingMillingOriginal",new XAttribute("world",RebirthCookingOutputDelivery.WorldKey(original.World)),
                new XAttribute("x",position.x),new XAttribute("y",position.y),new XAttribute("z",position.z),
                new XAttribute("beforeOffset",original.BeforeOffset),new XAttribute("mobile",mobile?.InstallationId.ToString("N")??""),
                new XAttribute("vehicleIdentity",mobile==null?"":RebirthCamperModuleRuntime.For(mobile.BoundVehicle).Identity.ToString("N")),
                new XAttribute("stationBirth",original.StationBirth??""),
                new XElement("nativeBefore",Convert.ToBase64String(original.NativeBefore)),new XElement("queueAfter",Convert.ToBase64String(original.QueueAfter)),
                new XElement("combinedOutput",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(output))));
            if(window!=null)
            {
                if(!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))throw new InvalidDataException();
                intent.Add(new XElement("owningUiBefore",new XAttribute("owner",owner.StorageKey),new XAttribute("creation",original.Creation),new XAttribute("deaths",original.Deaths),new XAttribute("session",original.Session),
                    new XElement("queue",Convert.ToBase64String(Queue(Rows(window)))),new XElement("output",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(window.outputWindow.GetSlots()))),
                    new XElement("input",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(window.GetChildByType<XUiC_WorkstationInputGrid>()?.GetSlots()??station.Input))),
                    new XElement("tools",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(window.GetChildByType<XUiC_WorkstationToolGrid>()?.GetSlots()??station.Tools))),
                    new XElement("fuel",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(window.GetChildByType<XUiC_WorkstationFuelGrid>()?.GetSlots()??station.Fuel))),
                    new XElement("bag",Convert.ToBase64String(original.BagBefore)),new XElement("belt",Convert.ToBase64String(original.BeltBefore))));
            }
            original.OriginalImage=new XElement(intent);intent=Envelope(original,intent);
            if(takenOwner!=null)
            {
                if(!RebirthCookingOutputDelivery.TryReadSavedTakeProof(card,out var actualTaken)||!XNode.DeepEquals(actualTaken,takenOwner))throw new InvalidDataException();
                original.OriginalImage.Add(new XElement("ownerTake",new XElement(takenOwner)));
                intent=Envelope(original,original.OriginalImage);
            }
            if(!RebirthWorkstationCompletionPublication.Write(original.Path,intent))throw new IOException();
            original.Intent=RebirthWorkstationCompletionPublication.CopyPublishedBytes(original.Path);
            if(original.Intent==null||!XNode.DeepEquals(intent,RebirthWorkstationCompletionPublication.ReadPrimary(original.Path)))throw new IOException();
        }
        catch
        {
            if(original?.OriginalImage!=null&&!string.IsNullOrEmpty(original.Path))RetainUnusedAdmission(original);
            else if(original!=null)original.Dispose();else allocation?.Dispose();
            return null;
        }
        if(chunk!=null)
        {
            bool refused;
            lock(chunk)
            {
                refused=chunk.InProgressSaving||chunk.IsLocked||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting;
                if(!refused){chunk.InProgressSaving=true;original.ChunkOwned=true;}
            }
            if(refused){RetainUnusedAdmission(original);return null;}
        }
        bool admitted=false;
        try
        {
            var attempted=Envelope(original,original.OriginalImage,new XElement("cookingMillingAttempted"));
            admitted=original.Current()&&original.NativeUnchanged()&&RebirthWorkstationCompletionPublication.Write(original.Path+".attempted",attempted)&&
                XNode.DeepEquals(attempted,RebirthWorkstationCompletionPublication.ReadPrimary(original.Path+".attempted"));
        }
        catch{/* This private boundary still precedes every native/UI setter. */}
        if(!admitted){original.ReleaseChunk();RetainUnusedAdmission(original);return null;}
        pending.Add(original);ObserveMillingAuditOriginal(original);station.SetDisableModifiedCheck(true);return original;
    }
    private static void RetainUnusedAdmission(Original original)
    {
        // This private branch precedes every UI/native setter. It is a positive
        // unused admission event, not an inference from an absent attempt file.
        original.UnusedAdmission=true;
        // Retain before I/O so classification failure cannot orphan this event.
        if(!pending.Contains(original))pending.Add(original);
        try{TryFinishUnusedAdmission(original);}catch{/* Metadata-only retry. */}
    }
    private static void TryFinishUnusedAdmission(Original original)
    {
        if(!original.UnusedAdmission||!ReferenceEquals(GameManager.Instance?.World,original.World))return;
        if(original.Intent==null)
        {
            // Only this retained pre-setter event can qualify its own primary.
            var expected=Envelope(original,original.OriginalImage);
            if(!RebirthWorkstationCompletionPublication.Write(original.Path,expected)||
                !XNode.DeepEquals(expected,RebirthWorkstationCompletionPublication.ReadPrimary(original.Path)))return;
            original.Intent=RebirthWorkstationCompletionPublication.CopyPublishedBytes(original.Path);
        }
        if(!original.MetadataCurrent())return;
        var record=Envelope(original,original.OriginalImage,new XElement("cookingMillingUnusedAdmission"));
        if(!RebirthWorkstationCompletionPublication.Write(original.Path+".unused",record)||
            !XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(original.Path+".unused")))return;
        Finish(original);
    }
    internal static bool PublishUi(XUiC_RecipeStack card,XUiC_RebirthCookingStation window,ItemStack[] output)
        =>PublishUiCore(card,window,output,null);
    internal static bool PublishOwnerTakenUi(XUiC_RecipeStack card,XUiC_RebirthCookingStation window)
    {
        if(!RebirthCookingOutputDelivery.TryReadSavedTakeProof(card,out var taken)||window?.outputWindow==null)return false;
        return PublishUiCore(card,window,ItemStack.Clone(window.outputWindow.GetSlots()),taken);
    }
    private static bool PublishUiCore(XUiC_RecipeStack card,XUiC_RebirthCookingStation window,ItemStack[] output,XElement takenOwner)
    {
        if(card?.recipe==null||window?.outputWindow==null||!ReferenceEquals(card.GetParentByType<XUiC_WorkstationWindowGroup>(),window))return false;
        var rows=Rows(window);var cards=window.craftingQueue.GetRecipesToCraft();int index=Array.FindIndex(cards,value=>ReferenceEquals(value,card));if(index<0)return false;
        // Native ClearRecipe retains IsCrafting and sets the empty row's owner
        // to this actual local actor. All other original rows remain exact.
        rows[index]=new RecipeQueueItem{StartingEntityId=card.xui.playerUI.entityPlayer.entityId,Quality=1,IsCrafting=card.IsCrafting};
        var original=Begin(window.WorkstationData?.TileEntity,card.recipe,output,rows,window,card,takenOwner);if(original==null)return false;
        try
        {
            // Presentation only. The original native sync performs the output
            // and exact frozen queue setters together under its own suppression.
            if(takenOwner==null)window.outputWindow.SetSlots(output);
            if(!Same(window.outputWindow.GetSlots(),output))return false;
            var previous=mutating;mutating=original;
            try{card.ClearRecipe();original.Cleared=card.recipe==null;if(!original.Cleared||!Queue(Rows(window)).SequenceEqual(original.QueueAfter))return false;original.Mutated=true;}
            finally{mutating=previous;}
            window.syncTEfromUI();return original.Finished;
        }
        catch{return false;} // No second output application; original stays fenced.
        finally{if(!original.Finished&&original.NativeUnchanged()){original.Station.SetDisableModifiedCheck(false);original.ReleaseChunk();}}
    }
    internal static bool PublishTile(TileEntityWorkstation station,RecipeQueueItem row,ItemStack[] output)
    {
        if(GameManager.Instance?.World?.IsRemote()!=false||row==null||station.Queue==null)return false;int index=Array.FindIndex(station.Queue,value=>ReferenceEquals(value,row));if(index<0)return false;
        var after=(RecipeQueueItem[])station.Queue.Clone();after[index]=new RecipeQueueItem{StartingEntityId=row.StartingEntityId,Quality=row.Quality,OneItemCraftTime=row.OneItemCraftTime};
        var original=Begin(station,row.Recipe,output,after);if(original==null)return false;
        try
        {
            station.Output=output;if(!Same(station.Output,output))return false;
            row.Multiplier=0;row.Recipe=null;row.IsCrafting=false;row.CraftingTimeLeft=0f;original.Mutated=true;
            if(!original.Current()||!original.Terminal())return false;
            station.SetDisableModifiedCheck(false);var previous=publishingTile;publishingTile=original;
            try{station.setModified();}finally{publishingTile=previous;}
            original.NativeAccepted=true;original.ReleaseChunk();TryFinish(original);return original.Finished;
        }
        catch{return false;}
    }
    // Called only by the existing sole native sync dispatcher after final-run.
    internal static void ObserveNativeSync(XUiC_WorkstationWindowGroup window,bool ran)
    {
        try
        {
        var original=pending.FirstOrDefault(value=>ReferenceEquals(value.Window,window)&&ReferenceEquals(value.Station,window?.WorkstationData?.TileEntity));
        if(original==null)return;
        if(!ran){if(!original.NativeUncertain)original.NativeAttempted=false;return;}
        if(!ReferenceEquals(syncingMilling?.Original,original))return;
        original.NativeRan=true;
        if(!original.Current()||!original.Terminal())return;original.NativeAccepted=true;original.ReleaseChunk();TryFinish(original);
        }
        catch{/* Actual native outcome remains retained; never repeat output. */}
    }
    internal static object BeginNativeSync(XUiC_WorkstationWindowGroup window,bool willRun)
    {
        if(!willRun)return null;var original=pending.FirstOrDefault(value=>!value.UnusedAdmission&&ReferenceEquals(value.Window,window)&&ReferenceEquals(value.Station,window?.WorkstationData?.TileEntity));if(original==null)return null;
        original.NativeAttempted=true;var scope=new MillingSync{Original=original,Previous=syncingMilling};syncingMilling=scope;return scope;
    }
    internal static void EndNativeSync(XUiC_WorkstationWindowGroup window,Exception error,object state)
    {
        if(state is not MillingSync scope)return;
        if(error!=null&&pending.Contains(scope.Original))scope.Original.NativeUncertain=true;
        if(ReferenceEquals(syncingMilling,scope))syncingMilling=scope.Previous;
    }
    // Actual final native Setup, inside our admitted sync/closed-row callback.
    // Retain its bytes before pooling; never synthesize an after image later.
    internal static void ObserveNativeSetup(NetPackageTileEntity packet,TileEntity tile,StreamModeWrite mode,bool ran)
    {
        var original=syncingMilling?.Original??publishingTile;
        if(!ran||original==null||!ReferenceEquals(original.Station,tile)||packet?.ms==null||packet.ms.Length<1||packet.ms.Length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes)return;
        try
        {
            if(!original.Current()||!original.Terminal())return;
            original.NativeAfter=packet.ms.ToArray();original.AfterOffset=-1;original.NativeAfterMode=mode.ToString();
            original.ReleaseChunk(); // A positive complete tuple; exceptions still stay UNKNOWN.
        }
        catch{/* No packet/source proof issued. */}
    }
    private static bool CaptureActualAfter(Original original)
    {
        if(original.NativeAfter!=null)return true;
        // RCS is a bounded native writer/header check, not a detached native
        // reader. Its buffers are already owned by this source's allocation.
        if(original.Station is RebirthCamperStationState mobile)
        {original.NativeAfter=RebirthCamperStationSnapshot.Capture(mobile.InstallationId,mobile);original.NativeAfterMode="RCS";return true;}
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null)return false;
            original.NativeAfterMode="ToServer";
            return RebirthProductiveEarnedNativeBody.TryCaptureCurrent(original.Station,out original.NativeAfter,out original.AfterOffset);
        }
    }
    private static void TryFinish(Original original)
    {
        if(original.NativeAccepted&&original.MobileTickReturned&&original.StationTickReturned&&original.NativeAfter==null&&
            original.MetadataCurrent()&&original.Terminal()&&!CaptureActualAfter(original))return;
        if(!original.NativeAccepted||!original.MobileTickReturned||!original.StationTickReturned||original.NativeAfter==null||!original.MetadataCurrent()||
            !original.World.IsRemote()&&!ObserveSavedNativeOutput(original))return;
        var record=new XElement("cookingMillingNativeProjection",new XAttribute("original",original.Path),
            new XAttribute("afterOffset",original.AfterOffset),new XAttribute("afterMode",original.NativeAfterMode),new XElement("actualNativeAfter",Convert.ToBase64String(original.NativeAfter)),
            new XElement("immutableOriginal",Convert.ToBase64String(original.Intent)),new XElement("actualQueue",Convert.ToBase64String(original.QueueAfter)),
            new XElement("actualOutput",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(original.Output))));
        var envelope=Envelope(original,original.OriginalImage,record);
        if(!RebirthWorkstationCompletionPublication.Write(original.Path+".completed",envelope)||
            !XNode.DeepEquals(envelope,RebirthWorkstationCompletionPublication.ReadPrimary(original.Path+".completed")))return;Finish(original);
    }
    // Sole real OnClose boundary: no close opcode/equality reconstruction. A
    // skipped sync can abandon only presentation when complete native source
    // and owner grids positively remain the original before images.
    internal static object BeginWindowClose(XUiC_WorkstationWindowGroup window,bool ran)
    {
        if(!ran)return null;
        try
        {
            var original=pending.FirstOrDefault(value=>!value.UnusedAdmission&&ReferenceEquals(value.Window,window));
            if(original?.LiveWindowCurrent()!=true)return null;
            var scope=new MillingClose{Original=original,Previous=closingMilling};closingMilling=scope;return scope;
        }
        catch{return null;}
    }
    internal static void EndWindowClose(object state)
    {if(state is MillingClose scope&&ReferenceEquals(closingMilling,scope))closingMilling=scope.Previous;}
    internal static void ObserveWindowClosed(XUiC_WorkstationWindowGroup window,bool ran,object state)
    {
        try
        {
        if(!ran||state is not MillingClose scope||!ReferenceEquals(closingMilling,scope))return;var original=scope.Original;
        if(original==null||!pending.Contains(original)||!ReferenceEquals(original.Window,window)||!original.Current()||
            !RebirthProductiveEarnedUiDeferredTransport.TryGetObservedNativeUiSession(window,out var session)||session!=original.Session||
            original.NativeAccepted||original.NativeRan||original.NativeAttempted||original.NativeUncertain||!original.NativeUnchanged()||
            !RebirthCamperInventorySnapshot.Capture(original.Actor.bag.ItemGrid.items).SequenceEqual(original.BagBefore)||
            !RebirthCamperInventorySnapshot.Capture(original.Actor.inventory.ItemGrid.items).SequenceEqual(original.BeltBefore))return;
        original.CloseUnusedObserved=true;TryFinishUnused(original);
        }
        catch{/* No unused proof was issued. */}
    }
    private static void TryFinishUnused(Original original)
    {
        if(!original.CloseUnusedObserved||!original.MetadataCurrent())return;
        var record=new XElement("cookingMillingUnusedClose",new XAttribute("original",original.Path),new XElement("immutableOriginal",Convert.ToBase64String(original.Intent)));
        var envelope=Envelope(original,original.OriginalImage,record);
        if(!RebirthWorkstationCompletionPublication.Write(original.Path+".unused",envelope)||!XNode.DeepEquals(envelope,RebirthWorkstationCompletionPublication.ReadPrimary(original.Path+".unused")))return;
        original.Station.SetDisableModifiedCheck(false);Finish(original);
    }
    private static void Finish(Original original)
    {original.Finished=true;pending.Remove(original);original.ColdRelease?.Invoke();original.ColdRelease=null;original.Dispose();}
    private static XElement Envelope(Original original,XElement before,XElement after=null)=>new XElement("workstationCompletionIntent",
        new XAttribute("operation",original.Operation.ToString("N")),new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(original.World)),
        new XElement("before",new XElement(before)),after==null?null:new XElement(after));
    private static void Pump(ref ModEvents.SGameUpdateData data)
    {
        var world=GameManager.Instance?.World;
        PumpStaticNativeSources();
        if(pending.Count==0)return;
        foreach(var original in pending.ToArray())
        {
            try
            {
            if(!ReferenceEquals(original.World,world)){pending.Remove(original);original.Dispose();continue;}
            if(original.UnusedAdmission){TryFinishUnusedAdmission(original);continue;}
            if(original.CloseUnusedObserved){TryFinishUnused(original);continue;}
            if(original.NativeAccepted){TryFinish(original);continue;}
            // Retry only the native projection, never the primary/extras setter.
            if(original.Window!=null&&CanSync(original.Window))try{original.Window.syncTEfromUI();}catch{}
            }
            catch{/* Retain the same original without another output setter. */}
        }
    }
}
