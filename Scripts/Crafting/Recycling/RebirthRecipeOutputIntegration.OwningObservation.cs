using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

// Actual native owning insertion evidence only. No method in this file grants
// a setter, payment, earned work, queue admission or progression permission.
public static partial class RebirthRecipeOutputIntegration
{
    internal enum OwningSink {Output=1,Inventory=2}
    internal sealed class OwningObservation:IDisposable
    {
        internal readonly Guid Operation;
        internal readonly GameManager Game;internal readonly object Ui,WindowGroup;
        internal readonly World World;internal readonly EntityPlayerLocal Viewer;internal readonly TileEntityWorkstation Station;
        internal readonly XUiC_RecipeStack Card;internal readonly XUiC_WorkstationWindowGroup Window;
        internal readonly XUiM_Workstation Model;internal readonly Recipe Recipe;internal readonly XUiC_WorkstationOutputGrid Grid;
        internal readonly string SaveDirectory,WorldGuid,ViewerOwnerKey,ViewerCreation;
        internal string RecipeFull,UiQueueFull,NativeQueueFull;
        internal readonly string StartingOwnerKey,StartingOwnerCreation,StationBirth;
        internal readonly EntityPlayer StartingOwnerBody;internal readonly int StartingOwnerDeaths;
        internal readonly int ViewerDeaths,StartingOwnerEntityId,BeforeCount,NativeQueueCountBefore,BlockType,Index,OutputQuality;
        internal readonly float OneItemTime;internal readonly long NativeVersionBefore;
        internal readonly Vector3i Position;internal readonly bool Retry;
        internal byte[] RecipeNative,OutputBefore,BagBefore,BeltBefore,EquipmentBefore;
        internal byte[] ExtrasNative;internal readonly string ExtrasDefinition;internal bool ExtrasBound;
        internal byte[] CachedPrimaryNative;
        internal byte[] NativeBefore,InputBefore,FuelBefore,ToolsBefore;internal readonly int NativeElapsedOffset;
        internal byte[] OutputAfter,BagAfter,BeltAfter,EquipmentAfter,PrimaryNative;internal int AfterCount;
        internal bool Accepted,OriginalAccepted,ChildrenAccepted,Replacement,Updated,Taken,Closed,RanUpdate,OutputStackRan,OutputStackReturned,FullBoundary,Continued;
        internal int OutputStackCalls;internal long NativeVersionAfter;internal byte[] FullBoundaryPrimary;internal XElement FullBoundaryEvidence,CachedPredecessor;
        internal OwningObservation CachedPrevious;
        internal RemoteOutputColdClient RemoteOutputRestart;
        internal bool RemoteFinalQueueObserved;
        internal IDisposable Allocation;internal OwningObservation Previous;
        internal readonly List<IDisposable> Charges=new List<IDisposable>();internal OwningSink AcceptedSink;
        internal OwningObservation(XUiC_RecipeStack card,bool retry,Guid operation=default(Guid))
        {
            Operation=operation==Guid.Empty?Guid.NewGuid():operation;
            NativeVersionBefore=OwningNativeVersion(card);
            Game=GameManager.Instance;Ui=card.xui;Card=card;Recipe=card.GetRecipe();Viewer=card.xui.playerUI.entityPlayer;World=Viewer.world;
            Window=card.GetParentByType<XUiC_WorkstationWindowGroup>();Model=Window.WorkstationData;Station=Model.TileEntity;
            WindowGroup=Window.WindowGroup;
            Grid=Window.GetChildByType<XUiC_WorkstationOutputGrid>();SaveDirectory=Path.GetFullPath(GameIO.GetSaveGameDir());
            WorldGuid=World.IsRemote()?GamePrefs.GetString(EnumGamePrefs.GameGuidClient):World.Guid;
            if(!Guid.TryParse(WorldGuid,out var worldGuid)||worldGuid==Guid.Empty)throw new InvalidDataException();
            ViewerCreation=RebirthCamperCharacterAccess.CurrentCreation(Viewer);ViewerDeaths=Viewer.Died;
            if(!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))throw new InvalidDataException();ViewerOwnerKey=owner.StorageKey;
            if(!RebirthOriginalActionScope.Validate(ViewerOwnerKey,ViewerCreation))throw new InvalidDataException();
            StartingOwnerEntityId=card.StartingEntityId;BeforeCount=card.GetRecipeCount();OutputQuality=card.OutputQuality;OneItemTime=card.GetOneItemCraftTime();
            Retry=retry;Position=Station.ToWorldPos();BlockType=World.GetBlock(Position).type;
            Index=Array.FindIndex(Window.craftingQueue.GetRecipesToCraft(),value=>ReferenceEquals(value,card));
            if(Index<0||Index>=Station.Queue.Length)throw new InvalidDataException();NativeQueueCountBefore=Station.Queue[Index]?.Multiplier??-1;
            var row=FrozenRow(Recipe,this);RecipeNative=RebirthProductiveEarnedRowProof.CaptureRow(ActualCardRow(card));
            RecipeFull=RebirthRemoteResourcePlacedStationQueue.Encode(new[]{row});
            if(RebirthLogisticsEndpointIdentity.TryGetIncarnation(World,Station,out var stationBirth))StationBirth=stationBirth;
            var cards=Window.craftingQueue.GetRecipesToCraft();
            if(cards.Any(value=>value==null||value.AmountToRepair!=0||value.OriginalItem!=null&&!value.OriginalItem.IsEmpty()))throw new InvalidDataException();
            UiQueueFull=RebirthRemoteResourcePlacedStationQueue.Encode(cards.Select(ActualCardRow).ToArray());
            NativeQueueFull=RebirthRemoteResourcePlacedStationQueue.Encode(Station.Queue);
            RebirthProductiveEarnedNativeBody.Install();
            if(!RebirthProductiveEarnedNativeBody.TryCaptureCurrent(Station,out var nativeBefore,out int elapsedOffset))throw new InvalidDataException();
            NativeBefore=nativeBefore;NativeElapsedOffset=elapsedOffset;
            InputBefore=Window.inputWindow==null?Array.Empty<byte>():OwningGrid(Window.inputWindow.GetSlots());
            FuelBefore=Window.fuelWindow==null?Array.Empty<byte>():OwningGrid(Window.fuelWindow.GetSlots());
            ToolsBefore=Window.toolWindow==null?Array.Empty<byte>():OwningGrid(Window.toolWindow.GetSlots());
            var startingBody=StartingOwnerEntityId==Viewer.entityId?Viewer:!World.IsRemote()?World.GetEntity(StartingOwnerEntityId) as EntityPlayer:null;
            if(RebirthProductiveEarnedUiQueueOwner.TryObserveQueueOwner(Station,Index,Station.Queue[Index],out var earlierOwner)&&
                earlierOwner.StartingEntityId==StartingOwnerEntityId)
            {
                StartingOwnerKey=earlierOwner.OriginalOwnerKey;StartingOwnerCreation=earlierOwner.Creation;StartingOwnerDeaths=earlierOwner.Deaths;
                if(startingBody!=null&&startingBody.Died==StartingOwnerDeaths&&TryOwningStartingIdentity(startingBody,out var startingIdentity)&&
                    startingIdentity.StorageKey==StartingOwnerKey&&RebirthCamperCharacterAccess.CurrentCreation(startingBody)==StartingOwnerCreation)StartingOwnerBody=startingBody;
            }
            else if(RebirthWorkstationCompletionPipeline.TryReadCachedPhysicalStartingOwner(card,out var cachedOwner,out var cachedCreation,out var cachedDeaths))
            {StartingOwnerKey=cachedOwner;StartingOwnerCreation=cachedCreation;StartingOwnerDeaths=cachedDeaths;}
            OutputBefore=OwningGrid(Grid==null?Station.Output:Grid.GetSlots());
            BagBefore=OwningGrid(Viewer.bag.ItemGrid.items);BeltBefore=OwningGrid(Viewer.inventory.ItemGrid.items);
            EquipmentBefore=OwningEquipmentNative(Viewer);
            // Observe the production scope's actual generated extras later;
            // planning again here would generate different native ItemValue
            // seeds and would perturb production behavior.
            ExtrasDefinition=RebirthRecipeOutputs.DefinitionIdentity(Recipe);
            ExtrasNative=OwningGrid(Array.Empty<ItemStack>());ExtrasBound=!RebirthRecipeOutputs.HasOutputs(Recipe);
            if(retry)
            {
                if(!card.isInventoryFull||card.outputItemValue==null||card.outputItemValue.IsEmpty())throw new InvalidDataException();
                CachedPrimaryNative=OwningGrid(new[]{new ItemStack(card.outputItemValue,Recipe.count)});
            }
        }
        internal bool Current(bool after=false)
        {
            try
            {
                if(Closed||!ThreadManager.IsMainThread()||!ReferenceEquals(GameManager.Instance,Game)||!ReferenceEquals(Game.World,World)||!ReferenceEquals(Viewer.world,World)||
                    !ReferenceEquals(World.GetPrimaryPlayer(),Viewer)||Viewer.IsDead()||Viewer.Died!=ViewerDeaths||
                    RebirthCamperCharacterAccess.CurrentCreation(Viewer)!=ViewerCreation||Path.GetFullPath(GameIO.GetSaveGameDir())!=SaveDirectory||
                    (World.IsRemote()?GamePrefs.GetString(EnumGamePrefs.GameGuidClient):World.Guid)!=WorldGuid||
                    !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner.StorageKey!=ViewerOwnerKey||
                    Window.WindowGroup?.isShowing!=true||!ReferenceEquals(Window.WindowGroup,WindowGroup)||!ReferenceEquals(Card.xui,Ui)||!ReferenceEquals(Window.xui,Ui)||
                    !ReferenceEquals(Window.WorkstationData,Model)||!ReferenceEquals(Model.TileEntity,Station)||
                    !ReferenceEquals(Card.GetParentByType<XUiC_WorkstationWindowGroup>(),Window)||!ReferenceEquals(Card.xui?.playerUI?.entityPlayer,Viewer)||
                    !ReferenceEquals(World.GetTileEntity(Position),Station)||World.GetBlock(Position).type!=BlockType||
                    !Station.IsUserAccessing()||LockManager.Instance?.IsLockedByLocalPlayer(Station,0)!=true)return false;
                if(StationBirth!=null&&(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(World,Station,out var birth)||birth!=StationBirth))return false;
                if(StartingOwnerBody!=null&&(!ReferenceEquals(World.GetEntity(StartingOwnerEntityId),StartingOwnerBody)||StartingOwnerBody.Died!=StartingOwnerDeaths||
                    RebirthCamperCharacterAccess.CurrentCreation(StartingOwnerBody)!=StartingOwnerCreation||
                    !TryOwningStartingIdentity(StartingOwnerBody,out var startingOwner)||startingOwner.StorageKey!=StartingOwnerKey))return false;
                var cards=Window.craftingQueue.GetRecipesToCraft();
                if(Index<0||Index>=cards.Length||!ReferenceEquals(cards[Index],Card)||cards.Count(value=>ReferenceEquals(value,Card))!=1)return false;
                if(after&&BeforeCount==1)return Card.GetRecipeCount()==0&&Card.GetRecipe()==null;
                return Card.GetRecipeCount()==(after?BeforeCount-1:BeforeCount)&&ReferenceEquals(Card.GetRecipe(),Recipe)&&Card.StartingEntityId==StartingOwnerEntityId&&Card.OutputQuality==OutputQuality&&Card.GetOneItemCraftTime()==OneItemTime;
            }
            catch{return false;}
        }
        internal bool RecipeMatches()=>RebirthRemoteResourcePlacedStationQueue.Encode(new[]{FrozenRow(Recipe,this)})==RecipeFull&&RebirthRecipeOutputs.DefinitionIdentity(Recipe)==ExtrasDefinition;
        internal bool TryCopyBeforeEvidence(out XElement evidence)
        {
            evidence=null;
            try
            {
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                {
                    if(decoder==null||Accepted||Updated||!Current()||!RecipeMatches()||!CurrentBeforeImages(this)||
                        Card.GetRecipeCount()!=BeforeCount||RebirthRemoteResourcePlacedStationQueue.Encode(Window.craftingQueue.GetRecipesToCraft().Select(ActualCardRow).ToArray())!=UiQueueFull||
                        !RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,NativeBefore,NativeElapsedOffset))return false;
                    var result=OwningEvidenceHeader(this,"nativeOwningBefore");
                    result.Add(new XAttribute("primaryType",Recipe.itemValueType),new XAttribute("primaryCount",Recipe.count),new XAttribute("quality",OutputQuality),
                        new XAttribute("nativeElapsedOffset",NativeElapsedOffset),new XElement("recipeFull",RecipeFull),new XElement("uiQueueFull",UiQueueFull),new XElement("nativeQueueFull",NativeQueueFull));
                    AddOwningImages(result,new[]{"recipeNative","nativeBefore","inputBefore","fuelBefore","toolsBefore","outputBefore","bagBefore","beltBefore","extrasNative","equipmentBefore"},
                        new[]{RecipeNative,NativeBefore,InputBefore,FuelBefore,ToolsBefore,OutputBefore,BagBefore,BeltBefore,OwningGrid(Array.Empty<ItemStack>()),EquipmentBefore});
                    if(CachedPredecessor!=null)result.Add(new XElement("cachedPredecessor",new XElement(CachedPredecessor)));
                    evidence=result;return true;
                }
            }
            catch{return false;}
        }
        public void Dispose()
        {
            if(Closed)return;Closed=true;
            RecipeNative=OutputBefore=BagBefore=BeltBefore=CachedPrimaryNative=NativeBefore=InputBefore=FuelBefore=ToolsBefore=null;
            OutputAfter=BagAfter=BeltAfter=PrimaryNative=ExtrasNative=FullBoundaryPrimary=null;
            EquipmentBefore=EquipmentAfter=null;
            RecipeFull=UiQueueFull=NativeQueueFull=null;FullBoundaryEvidence=CachedPredecessor=null;
            Allocation?.Dispose();Allocation=null;foreach(var charge in Charges)charge.Dispose();Charges.Clear();CachedPrevious?.Dispose();CachedPrevious=null;
        }
    }
    internal sealed class OwningDelivery:IDisposable
    {
        private readonly OwningObservation observation;
        internal Guid Operation=>observation.Operation;
        internal GameManager Game=>observation.Game;internal object Ui=>observation.Ui;internal object WindowGroup=>observation.WindowGroup;
        internal World World=>observation.World;internal EntityPlayerLocal Viewer=>observation.Viewer;
        internal TileEntityWorkstation Station=>observation.Station;internal XUiC_RecipeStack Card=>observation.Card;
        internal XUiC_WorkstationWindowGroup Window=>observation.Window;internal XUiM_Workstation Model=>observation.Model;internal Recipe Recipe=>observation.Recipe;
        internal string SaveDirectory=>observation.SaveDirectory;internal string WorldGuid=>observation.WorldGuid;
        internal string ViewerOwnerKey=>observation.ViewerOwnerKey;internal string ViewerCreation=>observation.ViewerCreation;
        internal int ViewerDeaths=>observation.ViewerDeaths;internal int StartingOwnerEntityId=>observation.StartingOwnerEntityId;
        internal string StartingOwnerKey=>observation.StartingOwnerKey;internal string StartingOwnerCreation=>observation.StartingOwnerCreation;
        internal string StationBirth=>observation.StationBirth;
        internal EntityPlayer StartingOwnerBody=>observation.StartingOwnerBody;internal int StartingOwnerDeaths=>observation.StartingOwnerDeaths;
        internal int BeforeCount=>observation.BeforeCount;internal int AfterCount=>observation.AfterCount;internal int NativeQueueCountBefore=>observation.NativeQueueCountBefore;
        internal Vector3i Position=>observation.Position;internal int BlockType=>observation.BlockType;internal bool Retry=>observation.Retry;
        internal bool NativeOriginalAccepted=>observation.OriginalAccepted;internal bool NativeChildrenAccepted=>observation.ChildrenAccepted;
        internal bool ReplacementWrapper=>observation.Replacement;internal OwningSink Sink=>observation.AcceptedSink;
        private bool closed;
        internal OwningDelivery(OwningObservation value){observation=value;}
        internal bool IsCurrent()=>!closed&&observation.Current(true)&&observation.Updated&&(observation.Accepted||observation.RemoteFinalQueueObserved)&&
            observation.NativeVersionAfter>0&&OwningNativeVersion(observation.Card)==observation.NativeVersionAfter;
        internal bool TryCopyEvidence(out XElement evidence)
        {
            evidence=null;if(!IsCurrent())return false;
            if(observation.RemoteFinalQueueObserved)return observation.RemoteOutputRestart?.CopySavedFinalQueueDelivery(observation,out evidence)==true;
            try
            {
                var o=observation;
                var result=OwningEvidenceHeader(o,"nativeOwningDelivery");
                result.Add(new XAttribute("afterCount",o.AfterCount),
                    new XAttribute("sink",Sink.ToString()),
                    new XAttribute("nativeOriginalAccepted",o.OriginalAccepted),new XAttribute("nativeChildrenAccepted",o.ChildrenAccepted),new XAttribute("replacementWrapper",o.Replacement),
                    new XElement("recipeFull",o.RecipeFull));
                string[] names={"recipeNative","primaryNative","extrasNative","outputBefore","outputAfter","bagBefore","bagAfter","beltBefore","beltAfter"};
                byte[][] values={o.RecipeNative,o.PrimaryNative,o.ExtrasNative,o.OutputBefore,o.OutputAfter,o.BagBefore,o.BagAfter,o.BeltBefore,o.BeltAfter};
                AddOwningImages(result,names,values);
                AddOwningImages(result,new[]{"equipmentBefore","equipmentAfter"},new[]{o.EquipmentBefore,o.EquipmentAfter});
                  if(o.CachedPredecessor!=null)result.Add(new XElement("cachedPredecessor",new XElement(o.CachedPredecessor)));
                if(!IsCurrent())return false;evidence=result;return true;
            }
            catch{return false;}
        }
        public void Dispose(){if(closed)return;closed=true;observation.Dispose();}
    }
    [ThreadStatic]private static OwningObservation owningObservation;
    private static bool TryOwningStartingIdentity(EntityPlayer body,out RebirthStablePlayerIdentity identity)
    {
        identity=null;var world=GameManager.Instance?.World;
        if(body==null||!ReferenceEquals(body.world,world)||!ReferenceEquals(world?.GetEntity(body.entityId),body))return false;
        if(body is EntityPlayerLocal&&ReferenceEquals(world.GetPrimaryPlayer(),body))return RebirthStablePlayerIdentity.TryFromLocalPlatform(out identity);
        return world.IsRemote()==false&&RebirthStablePlayerIdentity.TryFromPersistentPlayer(GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(body.entityId),out identity);
    }
    private static RecipeQueueItem ActualCardRow(XUiC_RecipeStack card)=>new RecipeQueueItem{Recipe=card.GetRecipe(),Multiplier=checked((short)card.GetRecipeCount()),
        CraftingTimeLeft=card.GetRecipeCraftingTimeLeft(),IsCrafting=card.IsCrafting,Quality=checked((byte)card.OutputQuality),StartingEntityId=card.StartingEntityId,
        OneItemCraftTime=card.GetOneItemCraftTime()};
    private static XElement OwningEvidenceHeader(OwningObservation o,string name)=>new XElement(name,new XAttribute("version",1),new XAttribute("operation",o.Operation.ToString("N")),
        new XAttribute("world",o.WorldGuid),new XAttribute("save",o.SaveDirectory),new XAttribute("viewer",o.ViewerOwnerKey),new XAttribute("creation",o.ViewerCreation),new XAttribute("deaths",o.ViewerDeaths),
        new XAttribute("stationBirth",o.StationBirth??""),
        new XAttribute("extrasDefinition",o.ExtrasDefinition),
        new XAttribute("startingOwner",o.StartingOwnerEntityId),new XAttribute("startingOwnerKnown",o.StartingOwnerKey!=null),new XAttribute("startingOwnerKey",o.StartingOwnerKey??""),
        new XAttribute("startingOwnerCreation",o.StartingOwnerCreation??""),new XAttribute("startingOwnerDeaths",o.StartingOwnerDeaths),
        new XAttribute("x",o.Position.x),new XAttribute("y",o.Position.y),new XAttribute("z",o.Position.z),new XAttribute("block",o.BlockType),new XAttribute("slot",o.Index),new XAttribute("beforeCount",o.BeforeCount),
        new XAttribute("nativeQueueCountBefore",o.NativeQueueCountBefore),new XAttribute("retry",o.Retry));
    private static void AddOwningImages(XElement node,string[] names,byte[][] values)
    {for(int i=0;i<names.Length;i++)node.Add(new XElement(names[i],Convert.ToBase64String(values[i]??Array.Empty<byte>())));}
    private static RecipeQueueItem FrozenRow(Recipe recipe,OwningObservation o)=>new RecipeQueueItem{Recipe=recipe,Multiplier=checked((short)o.BeforeCount),
        CraftingTimeLeft=0,IsCrafting=true,Quality=checked((byte)o.OutputQuality),StartingEntityId=o.StartingOwnerEntityId,OneItemCraftTime=o.OneItemTime};
    internal static byte[] OwningGrid(ItemStack[] slots)
    {
        if(slots==null||!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(slots,262144,349528,out _))throw new InvalidDataException();
        var bytes=RebirthCamperInventorySnapshot.Capture(slots);if(bytes.Length>2*RebirthProductiveEarnedRowProof.MaximumFrameBytes)throw new InvalidDataException();return bytes;
    }
    internal static OwningObservation BeginOwningObservation(XUiC_RecipeStack card,bool retry,Guid operation=default(Guid))
    {
        try
        {
            if(!installed||owningObservation!=null||card?.GetRecipe()==null||card.GetRecipe().IsScrap&&!HasNativeScrapOutputs(card.GetRecipe())||card.AmountToRepair!=0||
                card.OriginalItem!=null&&!card.OriginalItem.IsEmpty()||card.GetRecipeCount()<1||
                !(card.isInventoryFull||card.IsCrafting&&card.GetRecipeCraftingTimeLeft()<=0)||
                card.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity is RebirthCamperStationState)return null;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null)return null;
                var result=new OwningObservation(card,retry,operation);
                long bytes=result.RecipeNative.Length+2L*(result.RecipeFull.Length+result.UiQueueFull.Length+result.NativeQueueFull.Length)+result.NativeBefore.Length+
                    result.InputBefore.Length+result.FuelBefore.Length+result.ToolsBefore.Length+result.OutputBefore.Length+result.BagBefore.Length+result.BeltBefore.Length+result.ExtrasNative.Length+result.EquipmentBefore.Length;
                if(!result.Current()||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12*bytes+65536),out result.Allocation)){result.Dispose();return null;}
                result.Previous=owningObservation;owningObservation=result;return result;
            }
        }
        catch{return null;} // Observation failure cannot suppress native output.
    }
    internal static void ObserveOwningUpdate(XUiC_RecipeStack card,bool runOriginal,OwningObservation original)
    {
        NoteOwningUpdateExit(card,runOriginal,original);
        try
        {
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
            if(decoder==null||!runOriginal||original==null||!ReferenceEquals(owningObservation,original)||!ReferenceEquals(card,original.Card))return;
            if(!original.Accepted)
            {
                if(original.RemoteOutputRestart?.FinalCallbackSaved==true){original.RemoteOutputRestart.ObserveSavedFinalQueue(original);return;}
                TryFreezeOwningFullBoundary(original);return;
            }
            if(original.Updated||!original.Current(true)||card.GetRecipeCount()!=original.BeforeCount-1||
                (!original.Retry&&(!original.OutputStackRan||!original.OutputStackReturned||original.OutputStackCalls!=1)))return;
            if(!original.RecipeMatches()||!CurrentAcceptedImages(original))return;
            original.AfterCount=card.GetRecipeCount();original.Updated=true;
            }
        }
        catch{/* An actual effect stays unknown; no inferred delivery. */}
    }
    internal static bool TryTakeOwningDelivery(OwningObservation original,out OwningDelivery delivery)
    {
        delivery=null;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null||original==null||original.Taken||original.Continued||!original.Updated||!original.Accepted&&!original.RemoteFinalQueueObserved||!original.Current(true)||!original.RecipeMatches()||
                original.NativeVersionAfter<1||OwningNativeVersion(original.Card)!=original.NativeVersionAfter||!CurrentAcceptedImages(original))return false;
            original.Taken=true;delivery=new OwningDelivery(original);return true;
        }
    }
    internal static void EndOwningObservation(OwningObservation original)
    {
        if(original==null)return;if(ReferenceEquals(owningObservation,original))owningObservation=original.Previous;
        // The durable original's owner retains/disposes this object. Ending an
        // invocation is not positive unused evidence and must not mint a new op.
    }
    internal static bool ResumeOwningObservation(OwningObservation original)
    {
        if(original?.RemoteOutputRestart!=null)return ResumeRemoteOutputColdObservation(original);
        try
        {
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||owningObservation!=null||original==null||original.Accepted||original.Updated||original.Taken||original.RanUpdate||original.Continued||
                    !original.Current()||!original.RecipeMatches()||OwningNativeVersion(original.Card)!=original.NativeVersionBefore||original.Card.GetRecipeCount()!=original.BeforeCount||!CurrentBeforeImages(original)||
                    RebirthRemoteResourcePlacedStationQueue.Encode(original.Window.craftingQueue.GetRecipesToCraft().Select(ActualCardRow).ToArray())!=original.UiQueueFull||
                    !RebirthProductiveEarnedNativeBody.MatchesCurrent(original.Station,original.NativeBefore,original.NativeElapsedOffset))return false;
                original.Previous=null;owningObservation=original;return true;
            }
        }
        catch{return false;}
    }
    internal static void NoteOwningUpdateExit(XUiC_RecipeStack card,bool runOriginal,OwningObservation original)
    {
        long version=MarkOwningNativeUpdate(card,runOriginal);
        if(runOriginal&&original!=null&&ReferenceEquals(card,original.Card))
        {original.RanUpdate=true;if(version>0)original.NativeVersionAfter=version;}
    }
    internal static void NoteOwningOutputStackReturn(XUiC_RecipeStack card,bool runOriginal,bool result)
    {
        var original=owningObservation;if(original==null||!ReferenceEquals(original.Card,card))return;
        if(runOriginal){original.OutputStackRan=true;original.OutputStackCalls++;}
        original.OutputStackReturned=runOriginal&&result;
    }
}
