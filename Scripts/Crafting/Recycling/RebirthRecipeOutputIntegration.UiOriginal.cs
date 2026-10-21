using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using HarmonyLib;

public static partial class RebirthRecipeOutputIntegration
{
    private const int UiOriginalMaximum=12*1024*1024;
    private sealed class UiOriginalStream:MemoryStream
    {
        public override void Write(byte[] bytes,int offset,int count)
        {if(count<0||Position>UiOriginalMaximum-count)throw new InvalidDataException();base.Write(bytes,offset,count);}
        public override void WriteByte(byte value)
        {if(Position>=UiOriginalMaximum)throw new InvalidDataException();base.WriteByte(value);}
    }
    private sealed class UiOutputPending
    {
        internal World World;internal EntityPlayerLocal Actor;internal Recipe Recipe;internal int Count,Deaths;
        internal string Creation,Path,Hash;internal Guid Operation;internal bool Returned,QueueObserved,NativeContinuationEntered,NativeCallbackEntered;
        internal bool NativeCosmeticEntered;
        internal volatile bool QueueSaved;
        internal float NextQueueSave;
        internal UiOutputOriginal Source;
        ~UiOutputPending(){Source?.Dispose();}
    }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<XUiC_RecipeStack,UiOutputPending> pendingUiOutputs=
        new System.Runtime.CompilerServices.ConditionalWeakTable<XUiC_RecipeStack,UiOutputPending>();
    private static bool HasPendingUiOutput(XUiC_RecipeStack card)=>card!=null&&pendingUiOutputs.TryGetValue(card,out _);
    private static bool RetainsUiOutput(UiOutputOriginal original)=>original!=null&&
        pendingUiOutputs.TryGetValue(original.Scope.Card,out var pending)&&ReferenceEquals(pending.Source,original);
    private static void RetainUiOutput(UiOutputOriginal original)
    {
        ConsumeStationOutputBefore(original);
        pendingUiOutputs.Add(original.Scope.Card,new UiOutputPending{World=original.World,Actor=original.Actor,Recipe=original.Recipe,Count=original.Count,Deaths=original.Deaths,
            Creation=original.Creation,Path=original.Path,Hash=UiOriginalHash(original.Original),Operation=original.Operation,Source=original});
        ObserveOutputAuditOriginal(original.Actor,original.Operation,original.Scope.Card);
    }
    private static bool PrepareUiCachedOutput(UiScope scope)
    {
        IDisposable workspace=null;
        try
        {
            if(!scope.Cached||!ReferenceEquals(ui,scope)||scope.Original!=null||HasPendingUiOutput(scope.Card)||
                !scope.Card.isInventoryFull||scope.Card.outputItemValue==null||scope.Card.outputItemValue.IsEmpty()||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(16L*UiOriginalMaximum+65536,out workspace))return false;
            var original=new UiOutputOriginal(scope,scope.Card.outputItemValue);original.Memory=workspace;workspace=null;
            scope.Original=original;return true;
        }
        catch{return false;}
        finally{workspace?.Dispose();}
    }
    private static void EnterUiCachedOutput(UiScope scope)
    {
        if(!scope.Cached)return;
        var original=scope.Original;
        if(original==null||!original.Current())throw new InvalidOperationException("Cached additional-output selection changed before native insertion.");
        if(original.Entered)return;
        // This source is the actual native cached-primary branch, not a replay
        // of giveExp/AddCraftComplete. No prior effect is called or inferred.
        original.Enter("cached-native-delivery");RetainUiOutput(original);original.Observe(false,null);
        if(!original.Returned)throw new InvalidOperationException("Cached additional-output selection could not be durably observed.");
    }
    private static void ObserveUiCachedDelivery(UiOutputOriginal original)
    {
        if(original.Scope.Cached&&original.Current()&&pendingUiOutputs.TryGetValue(original.Scope.Card,out var pending)&&
            ReferenceEquals(pending.Source,original)&&(original.Scope.Grid==null?original.OutputPrefix==original.Scope.Outputs.Count+1:original.GridAccepted))
            pending.Returned=true;
    }
    private static void GuardUiCachedGrid(UiScope scope,XUiC_WorkstationGrid grid)
    {
        if(scope?.Cached==true&&!scope.Inserted&&scope.Original?.IsCapturedGrid(grid)==true&&!ReferenceEquals(scope.Grid,grid))
            throw new InvalidOperationException("Cached additional-output sink changed from its prepared native inventory.");
    }
    private static bool ResumeUiOutput(XUiC_RecipeStack card)
    {
        if(ui!=null||!pendingUiOutputs.TryGetValue(card,out var pending)||pending.Returned||pending.NativeContinuationEntered||pending.Source==null)return false;
        var original=pending.Source;var scope=original.Scope;
        // A returned native call with an exact accepted postimage is a prefix.
        // An entered call without that observation is never replay permission.
        if(!original.Returned||original.OutputEntered||original.OutputPrefix<0||original.OutputPrefix==0&&original.OutputAttempt<1||
            original.OutputPrefix>scope.Outputs.Count+1||scope.Grid!=null)return false;
        if(original.World.IsRemote()&&original.OutputPrefix>0&&!RebirthWorkstationCompletionPipeline.CanResumeAdditionalOutputPrefix(card,original.OutputPrefix))return false;
        var previous=ui;ui=scope;
        try
        {
            if(!original.Current()||!CanInsertOutputs(original.Actor))return false;
            if(original.OutputPrefix==0)
            {
                if(!original.OwnerBeforeBag.SequenceEqual(OwningGrid(original.Actor.bag.ItemGrid.items))||
                    !original.OwnerBeforeBelt.SequenceEqual(OwningGrid(original.Actor.inventory.ItemGrid.items))||
                    !original.OwnerBeforeEquipment.SequenceEqual(OwningEquipmentNative(original.Actor)))return false;
            }
            else if(!original.AcceptedOwnerCurrent())return false;
            var remaining=(original.OutputPrefix==0?
                new[]{new ItemStack(original.Primary,original.Recipe.count)}.Concat(scope.Outputs.Select(value=>value.Clone())):
                scope.Outputs.Skip(original.OutputPrefix-1).Select(value=>value.Clone())).ToArray();
            if(remaining.Length>0&&!RebirthRecipeOutputInventoryPrediction.TryPlan(original.Actor,remaining,out _,out _,out _))return false;
            scope.Inserted=true;
            foreach(var extra in remaining)
            {
                int index=original.OutputPrefix;original.EnterOutput(index,extra);
                bool accepted=scope.Card.xui.PlayerInventory.AddItemNoPartial(extra,false);
                original.ObserveOutput(index,accepted);
            }
            if(!original.Current()||!original.AcceptedOwnerCurrent()||original.OutputPrefix!=scope.Outputs.Count+1)return false;
            pending.Returned=true;
            return true; // The caller's actual native Update still owns queue decrement.
        }
        finally{ui=previous;}
    }
    private static bool FinishUiOutputInventoryResume(XUiC_RecipeStack card)
    {
        if(ui!=null||!pendingUiOutputs.TryGetValue(card,out var pending)||!pending.Returned||pending.NativeContinuationEntered||pending.Source==null)return false;
        var original=pending.Source;var scope=original.Scope;
        if(scope.Grid!=null||card.OriginalItem!=null&&!card.OriginalItem.IsEmpty()||original.OutputEntered||original.OutputPrefix!=scope.Outputs.Count+1)return false;
        var previous=ui;ui=scope;
        try
        {
            if(!original.Current()||!original.AcceptedOwnerCurrent()||!CanInsertOutputs(original.Actor))return false;
            // ResumeUiOutput completed only the remaining physical prefix.
            // The actual native helper still owns CraftedItem and sound. Its
            // already-accepted primary is acknowledged once by our existing
            // cached-primary seam, without another inventory insertion.
            pending.NativeContinuationEntered=true;pending.Returned=false;
            var entry=new XElement("recipeOutputNativeContinuationEntered",new XAttribute("operation",original.Operation.ToString("N")),
                new XAttribute("original",UiOriginalHash(original.Original)),new XAttribute("outputPrefix",original.OutputPrefix));
            if(!PublishUiOriginal(original.Path+".native-continuation-entered",UiOriginalBytes(entry)))return false;
            scope.CachedContinuation=true;scope.CachedPrimaryConsumed=false;
            bool accepted=card.AddItemToInventory();
            if(!accepted||!scope.CachedPrimaryConsumed||!original.Current()||!original.AcceptedOwnerCurrent())return false;
            // The helper's Boolean is not proof that Harmony allowed the actual
            // quest callback. Personal sources require its own positive native
            // return observer (including the scrap-specific callback).
            if((IsUiOutputPersonalCard(original)||original.RemoteCallbackFence)&&(!pending.NativeCallbackEntered||!pending.Returned))return false;
            var returned=new XElement("recipeOutputNativeContinuationReturned",new XAttribute("operation",original.Operation.ToString("N")),
                new XAttribute("original",UiOriginalHash(original.Original)),new XAttribute("outputPrefix",original.OutputPrefix));
            if(!PublishPersonalColdBinding(original.Path+".native-continuation-returned",UiOriginalBytes(returned)))return false;
            pending.Returned=true;return true;
        }
        finally{scope.CachedContinuation=false;scope.CachedPrimaryConsumed=false;ui=previous;}
    }
    private static bool BeginUiOutputCachedResume(XUiC_RecipeStack card,out object identity)
    {
        identity=null;
        // The native cached inventory branch owns its existing CraftedItem and
        // queue decrement. Finish remaining extras, then acknowledge its one
        // already-accepted primary without entering that native setter again.
        if(ui!=null||card==null||!card.isInventoryFull||!pendingUiOutputs.TryGetValue(card,out var pending)||pending.Source?.Scope.Grid!=null||
            !ResumeUiOutput(card)||pending.Source==null)return false;
        var original=pending.Source;var scope=original.Scope;var previous=ui;ui=scope;
        if(!original.Current()||!original.AcceptedOwnerCurrent()||!CanInsertOutputs(original.Actor))
        {ui=previous;return false;}
        scope.Previous=previous;scope.CachedContinuation=true;scope.CachedPrimaryConsumed=false;
        identity=scope;return true;
    }
    private static bool ConsumeUiOutputCachedPrimary(XUiM_PlayerInventory inventory,ItemStack primary,ref bool result)
    {
        var scope=ui;var original=scope?.Original;
        if(original==null||!scope.CachedContinuation||scope.CachedPrimaryConsumed||!original.Current()||
            !original.CallbackOwnerCurrent()||!CanInsertOutputs(original.Actor)||
            !ReferenceEquals(scope.Card.xui.PlayerInventory,inventory)||primary?.itemValue==null||
            original.OutputEntered||!original.CallbackPhysicalComplete||
            !OwningGrid(new[]{primary}).SequenceEqual(OwningGrid(new[]{new ItemStack(original.Primary,original.Recipe.count)})))
            throw new InvalidOperationException("Cached additional-output continuation changed its accepted original primary.");
        scope.CachedPrimaryConsumed=true;primary.count=0;result=true;return false;
    }
    internal static void ObserveUiOutputReturn(XUiC_RecipeStack card,bool ran,bool result)
    {
        var original=ui?.Original;
        if(!ran||!result||original==null||!ReferenceEquals(original.Scope.Card,card)||!original.Current()||
            !pendingUiOutputs.TryGetValue(card,out var pending)||pending.Operation!=original.Operation)return;
        if(!original.Returned||(original.Scope.Grid==null?original.OutputPrefix!=original.Scope.Outputs.Count+1:!original.GridAccepted))return;
        if((original.GridCallbackFence||original.RemoteCallbackFence||IsUiOutputPersonalCard(original))&&(!pending.NativeCallbackEntered||!pending.NativeContinuationEntered||!pending.Returned))return;
        pending.Returned=true;
    }
    private static void ObserveUiOutputQueue(XUiC_RecipeStack card)
    {
        try
        {
            if(!pendingUiOutputs.TryGetValue(card,out var pending)||!pending.Returned||!ThreadManager.IsMainThread()||
                !ReferenceEquals(GameManager.Instance?.World,pending.World)||!ReferenceEquals(pending.World.GetPrimaryPlayer(),pending.Actor)||
                !ReferenceEquals(pending.World.GetEntity(pending.Actor.entityId),pending.Actor)||pending.Actor.IsDead()||pending.Actor.Died!=pending.Deaths||
                RebirthCamperCharacterAccess.CurrentCreation(pending.Actor)!=pending.Creation||card.GetRecipeCount()!=pending.Count-1||
                (pending.Count==1?card.GetRecipe()!=null:!ReferenceEquals(card.GetRecipe(),pending.Recipe)))return;
            var evidence=new XElement("recipeOutputQueueObserved",new XAttribute("operation",pending.Operation.ToString("N")),
                new XAttribute("original",pending.Hash),new XAttribute("beforeCount",pending.Count),new XAttribute("afterCount",card.GetRecipeCount()));
            if(IsUiOutputPersonalQueue(pending))evidence.Add(new XElement("personalQueueAfter",RebirthRemoteResourceClientOriginal.QueueImage(card.xui.GetCraftingData())));
            if(!pending.QueueObserved)
            {
                if(!PublishUiOriginal(pending.Path+".queue-observed",UiOriginalBytes(evidence)))return;
                pending.QueueObserved=true;
            }
            // Personal output and queue acceptance share the same native player
            // file. Retain this card until that complete saved image is proved.
            if(IsUiOutputPersonalQueue(pending)){RegisterUiOutputPersonalQueue(pending);return;}
            if(RegisterUiOutputStationQueue(pending))return;
            pendingUiOutputs.Remove(card);pending.Source?.Dispose();pending.Source=null;
        }
        catch{/* Retain the exact pending application; never infer queue acceptance. */}
    }
    // Actual additional-output source only. This is not an MP015 earned original,
    // an owner grant, a saved native player prefix or cold reentry permission.
    private sealed class UiOutputOriginal:IDisposable
    {
        internal readonly Guid Operation=Guid.NewGuid();
        internal readonly UiScope Scope;internal readonly EntityPlayerLocal Actor;internal readonly World World;
        private readonly object NativeUi,NativeWindow,NativeStation,NativeGrid;
        internal readonly Recipe Recipe;internal readonly int Deaths,Count;internal readonly string Creation,Save,WorldId,Owner,Path;
        internal readonly long NativeVersionBefore;
        internal readonly string StationBirth;
        internal readonly bool GridCallbackFence;
        internal readonly bool RemoteCallbackFence;
        internal readonly int PersonalQueueIndex=-1,StationQueueIndex=-1;internal readonly string PersonalQueueBefore;
        internal readonly ItemValue Primary;internal readonly string RecipeImage;internal readonly byte[] Original,GridBefore;
        internal readonly byte[] OwnerBeforeBag,OwnerBeforeBelt,OwnerBeforeEquipment;
        internal byte[] GridExpected;internal bool GridAccepted;
        internal ItemStack[] ExpectedBag,ExpectedBelt;internal ItemValue[] ExpectedEquipment;
        internal byte[] AcceptedBag,AcceptedBelt,AcceptedEquipment;
        internal byte[] CallbackEquipmentAfter;
        internal StationOutputNativeRequest StationCold;
        internal RemoteOutputColdClient RemoteCold;
        internal IDisposable Memory;internal bool Entered,Returned,OutputEntered,OutputEquipmentCallbacks;internal int OutputPrefix,OutputAttempt;
        internal UiOutputOriginal(UiScope scope,ItemValue primary)
        {
            Scope=scope;Actor=scope.Card.xui.playerUI.entityPlayer;World=Actor.world;Recipe=scope.Card.GetRecipe();
            var window=scope.Card.GetParentByType<XUiC_WorkstationWindowGroup>();
            NativeUi=scope.Card.xui;NativeWindow=window;NativeStation=window?.WorkstationData?.TileEntity;NativeGrid=window?.GetChildByType<XUiC_WorkstationOutputGrid>();
            Primary=primary;Deaths=Actor.Died;Count=scope.Card.GetRecipeCount();Creation=RebirthCamperCharacterAccess.CurrentCreation(Actor);
            NativeVersionBefore=OwningNativeVersion(scope.Card);
            if(string.IsNullOrEmpty(Creation)||Count<1)throw new InvalidDataException();
            Save=System.IO.Path.GetFullPath(GameIO.GetSaveGameDir());
            WorldId=World.IsRemote()?GamePrefs.GetString(EnumGamePrefs.GameGuidClient):World.Guid;
            if(!Guid.TryParse(WorldId,out var world)||world==Guid.Empty||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))throw new InvalidDataException();
            Owner=owner.StorageKey;
            if(!World.IsRemote()&&NativeStation is TileEntityWorkstation placed&&placed is not RebirthCamperStationState)
            {
                if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(World,placed,out string birth))throw new InvalidDataException();
                StationBirth=birth;
                StationQueueIndex=Array.FindIndex(window.craftingQueue.GetRecipesToCraft(),value=>ReferenceEquals(value,scope.Card));
                if(StationQueueIndex<0)throw new InvalidDataException();
            }
            if(window==null&&scope.Grid==null)
            {
                var personal=scope.Card.xui.FindWindowGroupByName("crafting")?.GetChildByType<XUiC_CraftingQueue>()?.GetRecipesToCraft();
                if(personal!=null)PersonalQueueIndex=Array.FindIndex(personal,value=>ReferenceEquals(value,scope.Card));
                if(PersonalQueueIndex>=0)PersonalQueueBefore=RebirthRemoteResourceClientOriginal.QueueImage(scope.Card.xui.GetCraftingData());
            }
            RecipeImage=RebirthRemoteResourcePlacedStationQueue.Encode(new[]{ActualCardRow(scope.Card)});
            OwnerBeforeBag=OwningGrid(Actor.bag.ItemGrid.items);OwnerBeforeBelt=OwningGrid(Actor.inventory.ItemGrid.items);OwnerBeforeEquipment=OwningEquipmentNative(Actor);
            Path=System.IO.Path.Combine(Save,"rebirth","recipe-outputs","ui",Operation.ToString("N")+".xml");
            var image=new XElement("recipeOutputOriginal",new XAttribute("version",1),new XAttribute("operation",Operation.ToString("N")),
                new XAttribute("world",WorldId),new XAttribute("owner",Owner),new XAttribute("creation",Creation),new XAttribute("deaths",Deaths),
                new XAttribute("entity",Actor.entityId),new XAttribute("beforeCount",Count),new XAttribute("startingOwner",scope.Card.StartingEntityId),new XAttribute("cached",scope.Cached),
                new XAttribute("nativeVersion",NativeVersionBefore),new XAttribute("sink",scope.Grid==null?"Inventory":"Output"),
                new XElement("recipe",RecipeImage),new XElement("primaryBeforeEffects",RebirthNativeItemCodec.Encode(primary)),
                new XElement("outputs",Convert.ToBase64String(OwningGrid(new[]{new ItemStack(primary.Clone(),Recipe.count)}.Concat(scope.Outputs.Select(value=>value.Clone())).ToArray()))),
                new XElement("bagBefore",Convert.ToBase64String(OwnerBeforeBag)),
                new XElement("beltBefore",Convert.ToBase64String(OwnerBeforeBelt)),
                new XElement("equipmentBefore",Convert.ToBase64String(OwnerBeforeEquipment)));
            if(scope.Grid!=null){GridBefore=OwningGrid(scope.Grid.GetSlots());image.Add(new XElement("outputBefore",Convert.ToBase64String(GridBefore)));}
            if(StationBirth!=null&&NativeStation is TileEntityWorkstation station)
            {
                var position=station.ToWorldPos();image.Add(new XAttribute("stationBirth",StationBirth),new XAttribute("stationQueueIndex",StationQueueIndex),new XAttribute("stationX",position.x),
                    new XAttribute("stationY",position.y),new XAttribute("stationZ",position.z));
            }
            if(PersonalQueueIndex>=0)image.Add(new XAttribute("personalQueueIndex",PersonalQueueIndex),new XAttribute("callbackFence",1),new XElement("personalQueueBefore",PersonalQueueBefore));
            if(World.IsRemote()&&NativeStation is TileEntityWorkstation)
            {
                if(!RebirthWorkstationCompletionPipeline.TryCaptureRemoteOutputSource(scope.Card,out var remoteSource))
                    throw new InvalidDataException("Actual remote output source is not saved and current.");
                image.Add(remoteSource);
                RemoteCallbackFence=true;image.Add(new XAttribute("callbackFence",1));
            }
            var stationBefore=StationOutputSavedBefore(this);if(stationBefore!=null)image.Add(stationBefore);
            if(stationBefore!=null){GridCallbackFence=true;image.Add(new XAttribute("callbackFence",1));}
            Original=UiOriginalBytes(image);
        }
        // Only the independently qualified native Load and immutable personal
        // prefix reader create this binding. No generation or effect executes.
        internal UiOutputOriginal(UiScope scope,UiOutputLoadedOwner loaded,XElement image,byte[] bytes,ItemValue primary,
            Guid operation,int prefix,byte[] acceptedBag,byte[] acceptedBelt,byte[] acceptedEquipment)
        {
            Operation=operation;Scope=scope;Actor=loaded.Actor;World=loaded.World;Recipe=scope.Card.GetRecipe();
            NativeUi=scope.Card.xui;NativeWindow=NativeStation=NativeGrid=null;
            Primary=primary;Deaths=loaded.Deaths;Count=(int)image.Attribute("beforeCount");Creation=loaded.Creation;
            Save=loaded.Save;WorldId=loaded.World.Guid;Owner=loaded.Owner;Path=System.IO.Path.Combine(Save,"rebirth","recipe-outputs","ui",operation.ToString("N")+".xml");
            NativeVersionBefore=(long)image.Attribute("nativeVersion");PersonalQueueIndex=(int)image.Attribute("personalQueueIndex");
            PersonalQueueBefore=image.Element("personalQueueBefore").Value;RecipeImage=image.Element("recipe").Value;Original=bytes;
            OwnerBeforeBag=Convert.FromBase64String(image.Element("bagBefore").Value);OwnerBeforeBelt=Convert.FromBase64String(image.Element("beltBefore").Value);
            OwnerBeforeEquipment=Convert.FromBase64String(image.Element("equipmentBefore").Value);
            AcceptedBag=acceptedBag;AcceptedBelt=acceptedBelt;AcceptedEquipment=acceptedEquipment;
            Entered=Returned=true;OutputPrefix=prefix;
        }
        internal UiOutputOriginal(UiScope scope,StationOutputNativeRequest source,ItemValue primary)
        {
            var loaded=source.Owner;var image=source.Image;var window=scope.Card.GetParentByType<XUiC_WorkstationWindowGroup>();
            Operation=source.Operation;Scope=scope;Actor=loaded.Actor;World=loaded.World;Recipe=scope.Card.GetRecipe();
            NativeUi=scope.Card.xui;NativeWindow=window;NativeStation=window.WorkstationData.TileEntity;NativeGrid=scope.Grid;
            Primary=primary;Deaths=loaded.Deaths;Count=(int)image.Attribute("beforeCount");Creation=loaded.Creation;
            Save=loaded.Save;WorldId=World.Guid;Owner=loaded.Owner;Path=System.IO.Path.Combine(Save,"rebirth","recipe-outputs","ui",Operation.ToString("N")+".xml");
            NativeVersionBefore=(long)image.Attribute("nativeVersion");StationBirth=source.Birth;StationQueueIndex=(int)image.Attribute("stationQueueIndex");
            GridCallbackFence=true;RecipeImage=image.Element("recipe").Value;Original=source.Original;
            OwnerBeforeBag=PersonalColdImage(image,"bagBefore");OwnerBeforeBelt=PersonalColdImage(image,"beltBefore");OwnerBeforeEquipment=PersonalColdImage(image,"equipmentBefore");
            GridBefore=PersonalColdImage(image,"outputBefore");GridExpected=source.Grid;GridAccepted=Entered=Returned=true;
            if(source.CallbackReturned)CallbackEquipmentAfter=loaded.Equipment;
            StationCold=source;
        }
        internal UiOutputOriginal(UiScope scope,RemoteOutputColdClient source,ItemValue primary)
        {
            var image=source.Original;var actor=source.Actor;var window=scope.Card.GetParentByType<XUiC_WorkstationWindowGroup>();
            Operation=source.LocalOperation;Scope=scope;Actor=actor;World=actor.world;Recipe=scope.Card.GetRecipe();
            NativeUi=scope.Card.xui;NativeWindow=window;NativeStation=window.WorkstationData.TileEntity;NativeGrid=window.GetChildByType<XUiC_WorkstationOutputGrid>();
            Primary=primary;Deaths=actor.Died;Count=(int)image.Attribute("beforeCount");Creation=RebirthCamperCharacterAccess.CurrentCreation(actor);
            Save=System.IO.Path.GetFullPath(GameIO.GetSaveGameDir());WorldId=(string)image.Attribute("world");Owner=(string)image.Attribute("owner");
            Path=System.IO.Path.Combine(Save,"rebirth","recipe-outputs","ui",Operation.ToString("N")+".xml");
            NativeVersionBefore=OwningNativeVersion(scope.Card);RemoteCallbackFence=true;RecipeImage=image.Element("recipe").Value;Original=source.OriginalBytes;
            OwnerBeforeBag=PersonalColdImage(image,"bagBefore");OwnerBeforeBelt=PersonalColdImage(image,"beltBefore");OwnerBeforeEquipment=PersonalColdImage(image,"equipmentBefore");
            var prefix=source.Request.Element(source.FinalCallbackSaved?"nativeOwnerFrame":"nativeOutputPrefix");
            AcceptedBag=PersonalColdImage(prefix,source.FinalCallbackSaved?"bag":"bagAfter");AcceptedBelt=PersonalColdImage(prefix,source.FinalCallbackSaved?"belt":"beltAfter");AcceptedEquipment=PersonalColdImage(prefix,source.FinalCallbackSaved?"equipment":"equipmentAfter");
            Entered=Returned=true;OutputPrefix=source.Prefix;RemoteCold=source;
        }
        internal bool UiContextCurrent()
        {
            var window=Scope.Card.GetParentByType<XUiC_WorkstationWindowGroup>();
            return ReferenceEquals(Scope.Card.xui,NativeUi)&&ReferenceEquals(window,NativeWindow)&&
            ReferenceEquals(window?.WorkstationData?.TileEntity,NativeStation)&&ReferenceEquals(window?.GetChildByType<XUiC_WorkstationOutputGrid>(),NativeGrid)&&
            (StationCold==null||StationCold.Loaded?.Current()==true)&&(RemoteCold==null||RemoteCold.BindingCurrent());
        }
        internal bool IsCapturedGrid(object grid)=>grid!=null&&ReferenceEquals(grid,NativeGrid);
        internal bool Current()=>ThreadManager.IsMainThread()&&ReferenceEquals(ui,Scope)&&UiContextCurrent()&&ReferenceEquals(GameManager.Instance?.World,World)&&
            ReferenceEquals(Actor.world,World)&&ReferenceEquals(World.GetPrimaryPlayer(),Actor)&&ReferenceEquals(World.GetEntity(Actor.entityId),Actor)&&
            Actor.IsSpawned()&&!Actor.IsDead()&&Actor.Died==Deaths&&RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&
            System.IO.Path.GetFullPath(GameIO.GetSaveGameDir())==Save&&ReferenceEquals(Scope.Card.xui.playerUI.entityPlayer,Actor)&&
            ReferenceEquals(Scope.Card.GetRecipe(),Recipe)&&Scope.Card.GetRecipeCount()==Count&&ReferenceEquals(Scope.Card.outputItemValue,Primary)&&
            RebirthRemoteResourcePlacedStationQueue.Encode(new[]{ActualCardRow(Scope.Card)})==RecipeImage&&
            (World.IsRemote()?GamePrefs.GetString(EnumGamePrefs.GameGuidClient):World.Guid)==WorldId&&
            RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&owner.StorageKey==Owner;
        internal void Enter(string effect)
        {
            if(Entered||!Current()||!PublishUiOriginal(Path,Original))throw new InvalidOperationException("Additional-output original could not be published before native effects.");
            var entry=new XElement("recipeOutputEffectEntered",new XAttribute("operation",Operation.ToString("N")),new XAttribute("effect",effect),
                new XAttribute("original",UiOriginalHash(Original)));
            if(!PublishUiOriginal(Path+".effect-entered",UiOriginalBytes(entry)))throw new InvalidOperationException("Additional-output effect entry could not be published.");
            Entered=true;
        }
        internal bool HasStationGrid=>Scope.Grid!=null&&(GridCallbackFence||Scope.Cached);
        internal bool CallbackPhysicalComplete=>HasStationGrid?GridAccepted:OutputPrefix==Scope.Outputs.Count+1;
        internal int CallbackPrefix=>HasStationGrid?Scope.Outputs.Count+1:OutputPrefix;
        internal bool CallbackOwnerCurrent()
        {
            if(!HasStationGrid)return AcceptedOwnerCurrent();
            return GridAccepted&&GridExpected!=null&&GridExpected.SequenceEqual(OwningGrid(Scope.Grid.GetSlots()))&&
                NativeStation is TileEntityWorkstation station&&GridExpected.SequenceEqual(OwningGrid(station.Output))&&
                OwnerBeforeBag.SequenceEqual(OwningGrid(Actor.bag.ItemGrid.items))&&OwnerBeforeBelt.SequenceEqual(OwningGrid(Actor.inventory.ItemGrid.items))&&
                (CallbackEquipmentAfter??OwnerBeforeEquipment).SequenceEqual(OwningEquipmentNative(Actor));
        }
        internal void Observe(bool nativeRan,Exception error)
        {
            if(!Entered||Returned||error!=null||!Current())return;
            var observed=new XElement("recipeOutputEffectObserved",new XAttribute("operation",Operation.ToString("N")),new XAttribute("nativeRan",nativeRan),
                new XAttribute("original",UiOriginalHash(Original)),new XElement("primaryAfterEffects",RebirthNativeItemCodec.Encode(Primary)));
            if(GridCallbackFence&&!CaptureStationOutputReturnedEffect(this,observed))return;
            Returned=PublishUiOriginal(Path+".effect-observed",UiOriginalBytes(observed));
        }
        internal void EnterOutput(int index,ItemStack actual)
        {
            if(!Entered||!Returned||OutputEntered||!Current()||!CanInsertOutputs(Actor)||index!=OutputPrefix||actual?.itemValue==null||actual.count<1)
                throw new InvalidOperationException("Additional-output native insertion has no current original prefix.");
            if(index==0?(!ReferenceEquals(actual.itemValue,Primary)||actual.count!=Recipe.count):
                index>Scope.Outputs.Count||!OwningGrid(new[]{actual}).SequenceEqual(OwningGrid(new[]{Scope.Outputs[index-1]})))
                throw new InvalidOperationException("Additional-output insertion changed the original ordered item.");
            if(index==0&&(!OwnerBeforeBag.SequenceEqual(OwningGrid(Actor.bag.ItemGrid.items))||
                !OwnerBeforeBelt.SequenceEqual(OwningGrid(Actor.inventory.ItemGrid.items))||!OwnerBeforeEquipment.SequenceEqual(OwningEquipmentNative(Actor))))
                throw new InvalidOperationException("Additional-output owner changed after original capture and before the first insertion.");
            if(AcceptedBag!=null&&(!AcceptedBag.SequenceEqual(OwningGrid(Actor.bag.ItemGrid.items))||
                !AcceptedBelt.SequenceEqual(OwningGrid(Actor.inventory.ItemGrid.items))||!AcceptedEquipment.SequenceEqual(OwningEquipmentNative(Actor))))
                throw new InvalidOperationException("Additional-output owner state changed between accepted prefixes.");
            if(!RebirthRecipeOutputInventoryPrediction.TryPlan(Actor,new[]{actual},out ExpectedBag,out ExpectedBelt,out ExpectedEquipment))
                throw new InvalidOperationException("Additional-output next native insertion has no complete plan.");
            // Native armor insertion can fire WoreItem before a later false
            // return, even when the final equipment value equals its preimage.
            OutputEquipmentCallbacks=actual.itemValue.ItemClass is ItemClassArmor;
            var image=new XElement("recipeOutputEntered",new XAttribute("operation",Operation.ToString("N")),new XAttribute("index",index),new XAttribute("attempt",OutputAttempt),
                new XAttribute("equipmentCallbacks",OutputEquipmentCallbacks),
                new XAttribute("original",UiOriginalHash(Original)),new XElement("incoming",Convert.ToBase64String(OwningGrid(new[]{actual.Clone()}))),
                new XElement("bag",Convert.ToBase64String(OwningGrid(Actor.bag.ItemGrid.items))),
                new XElement("belt",Convert.ToBase64String(OwningGrid(Actor.inventory.ItemGrid.items))),
                new XElement("equipment",Convert.ToBase64String(OwningEquipmentNative(Actor))),
                new XElement("expectedBag",Convert.ToBase64String(OwningGrid(ExpectedBag))),
                new XElement("expectedBelt",Convert.ToBase64String(OwningGrid(ExpectedBelt))),
                new XElement("expectedEquipmentItems",Convert.ToBase64String(OwningGrid(ExpectedEquipment.Select(value=>new ItemStack(value.Clone(),value.IsEmpty()?0:1)).ToArray()))));
            if(!PublishUiOriginal(OutputEntryPath(index),UiOriginalBytes(image)))
                throw new InvalidOperationException("Additional-output insertion entry could not be published.");
            OutputEntered=true;
        }
        internal bool AcceptedOwnerCurrent()=>AcceptedBag!=null&&AcceptedBag.SequenceEqual(OwningGrid(Actor.bag.ItemGrid.items))&&
            AcceptedBelt.SequenceEqual(OwningGrid(Actor.inventory.ItemGrid.items))&&AcceptedEquipment.SequenceEqual(OwningEquipmentNative(Actor));
        private string OutputEntryPath(int index)=>Path+".output-"+index+(OutputAttempt==0?"":".retry-"+OutputAttempt)+".entered";
        internal void ObserveOutput(int index,bool accepted)
        {
            if(!accepted&&OutputEntered&&!OutputEquipmentCallbacks&&index==OutputPrefix&&Current())
            {
                // Only a returned false with the complete original preimage
                // still present proves this attempt did not insert anything.
                // Exceptions, changed grids and unobserved calls stay latched.
                var refusedBag=index==0?OwnerBeforeBag:AcceptedBag;
                var refusedBelt=index==0?OwnerBeforeBelt:AcceptedBelt;
                var refusedEquipment=index==0?OwnerBeforeEquipment:AcceptedEquipment;
                if(refusedBag!=null&&refusedBelt!=null&&refusedEquipment!=null&&refusedBag.SequenceEqual(OwningGrid(Actor.bag.ItemGrid.items))&&
                    refusedBelt.SequenceEqual(OwningGrid(Actor.inventory.ItemGrid.items))&&refusedEquipment.SequenceEqual(OwningEquipmentNative(Actor)))
                {
                    var refused=new XElement("recipeOutputRefused",new XAttribute("operation",Operation.ToString("N")),new XAttribute("index",index),
                        new XAttribute("attempt",OutputAttempt),new XAttribute("original",UiOriginalHash(Original)),
                        new XElement("bag",Convert.ToBase64String(refusedBag)),new XElement("belt",Convert.ToBase64String(refusedBelt)),new XElement("equipment",Convert.ToBase64String(refusedEquipment)));
                    if(OutputAttempt<int.MaxValue&&PublishUiOriginal(OutputEntryPath(index)+".refused",UiOriginalBytes(refused)))
                    {OutputAttempt++;OutputEntered=false;ExpectedBag=ExpectedBelt=null;ExpectedEquipment=null;}
                }
            }
            if(!accepted||!OutputEntered||index!=OutputPrefix||!Current())throw new InvalidOperationException("Additional-output native insertion did not return an accepted current prefix.");
            if(!SameOutputGrid(Actor.bag.ItemGrid.items,ExpectedBag)||!SameOutputGrid(Actor.inventory.ItemGrid.items,ExpectedBelt)||
                ExpectedEquipment==null||Actor.equipment.GetSlotCount()!=ExpectedEquipment.Length)
                throw new InvalidOperationException("Additional-output native prefix contradicted its expected grids.");
            for(int i=0;i<ExpectedEquipment.Length;i++)
                if(RebirthNativeItemCodec.Encode(Actor.equipment.GetSlotItem(i))!=RebirthNativeItemCodec.Encode(ExpectedEquipment[i]))
                    throw new InvalidOperationException("Additional-output native prefix contradicted its expected equipment.");
            var bag=OwningGrid(Actor.bag.ItemGrid.items);var belt=OwningGrid(Actor.inventory.ItemGrid.items);var equipment=OwningEquipmentNative(Actor);
            var image=new XElement("recipeOutputAccepted",new XAttribute("operation",Operation.ToString("N")),new XAttribute("index",index),new XAttribute("attempt",OutputAttempt),
                new XAttribute("original",UiOriginalHash(Original)),new XElement("bag",Convert.ToBase64String(bag)),
                new XElement("belt",Convert.ToBase64String(belt)),new XElement("equipment",Convert.ToBase64String(equipment)));
            var acceptedRecord=UiOriginalBytes(image);
            if(!PublishUiOriginal(Path+".output-"+index+".accepted",acceptedRecord))
                throw new InvalidOperationException("Additional-output accepted prefix could not be published.");
            RegisterUiOutputOwnerPrefix(this,index,bag,belt,equipment,UiOriginalHash(acceptedRecord));
            AcceptedBag=bag;AcceptedBelt=belt;AcceptedEquipment=equipment;
            ExpectedBag=ExpectedBelt=null;ExpectedEquipment=null;OutputPrefix++;OutputAttempt=0;OutputEntered=false;
            ObserveUiCachedDelivery(this);
            SaveUiOutputPersonalPrefix(this,index);
            var remote=AdditionalOutputPrefix.Capture(this,index,acceptedRecord,image);
            if(remote!=null)RebirthWorkstationCompletionPipeline.ObserveAdditionalOutputPrefix(Scope.Card,remote);
        }
        public void Dispose(){Memory?.Dispose();Memory=null;if(StationCold!=null){lock(stationNativeLoadGate){StationCold.Dispose();stationNativeLoadRequests.Remove(StationCold);}StationCold=null;}}
        internal void EnterGrid(ItemStack[] cells)
        {
            if(!Entered||!Returned||!Current()||GridBefore==null||GridExpected!=null||
                !PredictOwningOutput(RebirthCamperInventorySnapshot.Restore(GridBefore),new ItemStack(Primary,Recipe.count),Scope.Outputs,false,out var expected)||
                !OwningGrid(expected).SequenceEqual(OwningGrid(cells)))
                throw new InvalidOperationException("Additional-output native grid has no matching original application.");
            GridExpected=OwningGrid(cells);
            var entry=new XElement("recipeOutputGridEntered",new XAttribute("operation",Operation.ToString("N")),new XAttribute("original",UiOriginalHash(Original)),
                new XElement("before",Convert.ToBase64String(GridBefore)),new XElement("after",Convert.ToBase64String(GridExpected)));
            if(!PublishUiOriginal(Path+".grid-entered",UiOriginalBytes(entry)))throw new InvalidOperationException("Additional-output grid entry could not be published.");
        }
        internal void ObserveGrid(bool ran)
        {
            if(!ran||GridAccepted||GridExpected==null||!Current()||!GridExpected.SequenceEqual(OwningGrid(Scope.Grid.GetSlots())))return;
            var accepted=new XElement("recipeOutputGridAccepted",new XAttribute("operation",Operation.ToString("N")),new XAttribute("original",UiOriginalHash(Original)),
                new XElement("after",Convert.ToBase64String(GridExpected)));
            GridAccepted=PublishUiOriginal(Path+".grid-accepted",UiOriginalBytes(accepted));
            if(GridAccepted)ObserveUiCachedDelivery(this);
        }
    }
    private static string UiOriginalHash(byte[] bytes)
    {using(var hash=System.Security.Cryptography.SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(bytes));}
    private static byte[] UiOriginalBytes(XElement image)
    {
        using(var stream=new UiOriginalStream())
        {
            using(var writer=XmlWriter.Create(stream,new XmlWriterSettings{Encoding=new UTF8Encoding(false),CloseOutput=false}))
            {image.Save(writer);writer.Flush();}
            if(stream.Length<1||stream.Length>UiOriginalMaximum)throw new InvalidDataException();return stream.ToArray();
        }
    }
    private static bool PublishUiOriginal(string path,byte[] bytes)
    {
        // Create-only publication. A foreign/stale stage is preserved, never
        // deleted, adopted or used to reconstruct a live action source.
        try
        {
            RebirthWorkstationCompletionPublication.RequireNativeSaveFile(path);
            if(File.Exists(path)||File.Exists(path+".tmp")||File.Exists(path+".bak"))return false;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            RebirthWorkstationCompletionPublication.RequireNativeSaveFile(path);
            using(var file=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {file.Write(bytes,0,bytes.Length);file.Flush(true);}
            RebirthWorkstationCompletionPublication.RequireNativeSaveFile(path);File.Move(path+".tmp",path);
            using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(file.Length!=bytes.Length)return false;var buffer=new byte[8192];int offset=0,read;
                while((read=file.Read(buffer,0,buffer.Length))>0)
                {if(read>bytes.Length-offset)return false;for(int i=0;i<read;i++)if(buffer[i]!=bytes[offset+i])return false;offset+=read;}
                return offset==bytes.Length;
            }
        }
        catch{return false;}
    }
    internal static object BeginUiOutputEffect(XUiC_RecipeStack card,ItemValue primary,string effect,bool nativeRan)
    {
        var scope=ui;
        if(!nativeRan||scope==null||scope.Inserted||!ReferenceEquals(scope.Card,card)||primary==null||
            !ReferenceEquals(card.outputItemValue,primary)||primary.type!=card.GetRecipe()?.itemValueType)return null;
        if(scope.Original!=null)throw new InvalidOperationException("Additional-output native effect reentered its original.");
        if(HasPendingUiOutput(card))throw new InvalidOperationException("An earlier additional-output application still owns this native card.");
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(16L*UiOriginalMaximum+65536,out var workspace))
            throw new InvalidOperationException("Additional-output source workspace is unavailable.");
        try
        {
            var original=new UiOutputOriginal(scope,primary);original.Memory=workspace;workspace=null;scope.Original=original;original.Enter(effect);
            RetainUiOutput(original);
            return original;
        }
        finally{workspace?.Dispose();}
    }
    internal static object BeginUiDeferredOutputEffect(TileEntityWorkstation station,ItemValue primary,bool nativeRan)
    {
        var scope=ui;if(scope==null||!ReferenceEquals(scope.Card.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity,station))return null;
        return BeginUiOutputEffect(scope.Card,primary,"AddCraftComplete",nativeRan);
    }
    internal static void EndUiOutputEffect(object state,bool nativeRan,Exception error)
    {try{if(state is UiOutputOriginal original)original.Observe(nativeRan,error);}catch{/* Native exception/outcome remains authoritative; no accepted observation. */}}
    internal static void ObserveUiOutputGrid(XUiC_WorkstationGrid grid,bool ran)
    {try{var original=ui?.Original;if(original!=null&&ReferenceEquals(original.Scope.Grid,grid))original.ObserveGrid(ran);}catch{/* No accepted native grid evidence. */}}
}
[HarmonyPatch(typeof(XUiC_RecipeStack),"giveExp")]
internal static class RebirthRecipeOutputBeforeExperience
{
    [HarmonyPriority(1100)]
    private static void Prefix(XUiC_RecipeStack __instance,ItemValue __0,bool __runOriginal,out object __state)
        =>__state=RebirthRecipeOutputIntegration.BeginUiOutputEffect(__instance,__0,"giveExp",__runOriginal);
    private static Exception Finalizer(Exception __exception,bool __runOriginal,object __state)
    {RebirthRecipeOutputIntegration.EndUiOutputEffect(__state,__runOriginal,__exception);return __exception;}
}
[HarmonyPatch(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.AddCraftComplete))]
internal static class RebirthRecipeOutputBeforeDeferredCompletion
{
    [HarmonyPriority(1100)]
    private static void Prefix(TileEntityWorkstation __instance,ItemValue __1,bool __runOriginal,out object __state)
        =>__state=RebirthRecipeOutputIntegration.BeginUiDeferredOutputEffect(__instance,__1,__runOriginal);
    private static Exception Finalizer(Exception __exception,bool __runOriginal,object __state)
    {RebirthRecipeOutputIntegration.EndUiOutputEffect(__state,__runOriginal,__exception);return __exception;}
}
