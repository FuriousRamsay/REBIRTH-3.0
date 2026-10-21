using System;
using System.IO;
using System.Linq;

// Inactive native owning-card staging contract. Dedicated coordinator stages
// consume the SAME retained original before native sync/wear/output/retirement;
// descriptor/current matching is never a saved/effect/completion permission.
internal static partial class RebirthProductiveEarnedUiOriginal
{
    internal static bool Enabled {get;private set;} // No source calls the review-only entry.
    internal static void EnableReviewedSource(){RebirthProductiveEarnedUiCoordinator.Install();Enabled=true;}
    internal sealed class BindingToken
    {
        internal EntityPlayerLocal Actor;internal World World;internal XUiC_WorkstationWindowGroup Window;
        internal XUiC_CraftingQueue Queue;internal XUiC_RecipeStack Card;internal TileEntityWorkstation Station;
        internal Recipe Recipe;internal RebirthCraftingPresentation Presentation;internal long Generation;internal string Definition;internal int Index,Deaths;internal string Owner,Creation,WorldKey,SaveDirectory,PlayerDirectory;internal Guid Operation,Birth;
        internal RebirthStationGridAdmission Admission;
        internal RebirthProductiveEarnedUiQueueOwner.Witness EarlierOwner;internal string OwnerPurpose;
        internal int StartingOwnerEntityId;
        internal Vector3i Position;internal int BlockType;
        internal bool Current()
        {
            try
            {
                if(!ThreadManager.IsMainThread()||Presentation==null||Presentation.CraftIntentEpoch!=Generation||
                    !ReferenceEquals(RebirthCraftingPresentation.Resolve(Card),Presentation)||Definition!=DefinitionHash(Recipe)||Actor==null||Actor.IsDead()||Actor.Died!=Deaths||
                    !ReferenceEquals(World,GameManager.Instance?.World)||!ReferenceEquals(Actor.world,World)||!ReferenceEquals(World.GetPrimaryPlayer(),Actor)||
                    RebirthCamperCharacterAccess.CurrentCreation(Actor)!=Creation||RebirthProductiveEarnedArtifact.WorldIdentity(World)!=WorldKey||
                    SaveDirectory!=Path.GetFullPath(GameIO.GetSaveGameDir())||PlayerDirectory!=Path.GetFullPath(GameIO.GetPlayerDataDir())||
                    !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||identity.StorageKey!=Owner||
                    Window.WindowGroup?.isShowing!=true||!ReferenceEquals(Card.GetParentByType<XUiC_WorkstationWindowGroup>(),Window)||
                    !ReferenceEquals(Window.WorkstationData?.TileEntity,Station)||!ReferenceEquals(Window.craftingQueue,Queue)||
                    !ReferenceEquals(Card.xui?.playerUI?.entityPlayer,Actor)||LockManager.Instance?.IsLockedByLocalPlayer(Station,0)!=true||
                    !Station.ToWorldPos().Equals(Position)||World.GetBlock(Position).type!=BlockType||
                    !ReferenceEquals(World.GetTileEntity(Position),Station)||!ReferenceEquals(Card.GetRecipe(),Recipe)||Card.StartingEntityId!=StartingOwnerEntityId)return false;
                var cards=Queue.GetRecipesToCraft();
                if(cards==null||cards.Length!=Station.Queue?.Length||Index<0||Index>=cards.Length||!ReferenceEquals(cards[Index],Card)||
                    cards.Count(value=>ReferenceEquals(value,Card))!=1)return false;
                if(!ReferenceEquals(Station.Queue[Index]?.Recipe,Recipe)&&
                    (World.IsRemote()||EarlierOwner!=null||RebirthStationGridQueue.IsMarked(Recipe)||
                    !RebirthProductiveEarnedUiQueueOwner.IsPendingOwningInsertion(Card,Station)))return false;
                if(Admission!=null&&(!Admission.MatchesOwningCard(Window,Card,Index)||
                    !RebirthStationObservationDispatcher.TryGetVerifiedQueuedAdmission(Station,Station.Queue[Index],out var liveAdmission)||
                    !System.Xml.Linq.XNode.DeepEquals(Admission.Write(),liveAdmission.Write())))return false;
                if(World.IsRemote())return RebirthProductiveEarnedWire.TryReadReply(Operation,out var reply)&&reply.Operation==Operation&&reply.Birth==Birth&&
                    reply.World==WorldKey&&reply.Owner==Owner&&reply.Creation==Creation&&reply.Deaths==Deaths&&reply.Position.Equals(Station.ToWorldPos())&&reply.QueueOwner==OwnerPurpose;
                return RebirthLogisticsEndpointIdentity.TryGetIncarnation(World,Station,out var birth)&&birth==Birth.ToString("N");
            }
            catch{return false;}
        }
    }
    // Birth comes only from the actual native server marker or existing scoped
    // Describe reply for this operation. It still grants no effect authority.
    internal static bool TryBind(XUiC_RecipeStack card,Guid operation,out BindingToken token)
    {
        token=null;
        try
        {
            var actor=card?.xui?.playerUI?.entityPlayer;var world=GameManager.Instance?.World;
            var window=card?.GetParentByType<XUiC_WorkstationWindowGroup>();var station=window?.WorkstationData?.TileEntity;
            var queue=window?.craftingQueue;var cards=queue?.GetRecipesToCraft();var recipe=card?.GetRecipe();
            if(!ThreadManager.IsMainThread()||operation==Guid.Empty||actor==null||recipe==null||recipe.IsScrap||RebirthCookingHeat.Managed(recipe)||
                RebirthRecyclerRuntime.IsMarked(recipe)||card.AmountToRepair!=0||card.OriginalItem!=null&&!card.OriginalItem.IsEmpty()||
                station==null||station is RebirthCamperStationState||cards==null||cards.Length!=station.Queue?.Length||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity))return false;
            int index=Array.FindIndex(cards,value=>ReferenceEquals(value,card));if(index<0)return false;
            string creation=RebirthCamperCharacterAccess.CurrentCreation(actor),worldKey=RebirthProductiveEarnedArtifact.WorldIdentity(world),ownerPurpose=null;Guid birth;
            RebirthProductiveEarnedUiQueueOwner.Witness earlierOwner=null;
            if(!RebirthOriginalActionScope.Validate(identity.StorageKey,creation))return false;
            if(world.IsRemote())
            {
                if(!RebirthProductiveEarnedWire.TryReadReply(operation,out var reply)||reply.State!=RebirthProductiveEarnedWire.Status.Described||
                    reply.World!=worldKey||reply.Owner!=identity.StorageKey||reply.Creation!=creation||reply.Deaths!=actor.Died||
                    !reply.Position.Equals(station.ToWorldPos())||reply.Birth==Guid.Empty)return false;birth=reply.Birth;
                ownerPurpose=reply.QueueOwner;if(!RebirthProductiveEarnedUiQueueOwner.MatchesPurposeCard(ownerPurpose,CardRow(card),birth,worldKey))return false;
            }
            else
            {
                if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out var value)||!Guid.TryParseExact(value,"N",out birth))return false;
                if(!RebirthProductiveEarnedUiQueueOwner.TryObserveQueueOwner(station,index,station.Queue[index],out earlierOwner)&&
                    (RebirthStationGridQueue.IsMarked(recipe)||!RebirthProductiveEarnedUiQueueOwner.IsPendingOwningInsertion(card,station)))return false;
                ownerPurpose=earlierOwner==null?null:RebirthProductiveEarnedUiQueueOwner.Export(earlierOwner);
                if(earlierOwner!=null&&earlierOwner.StartingEntityId==actor.entityId&&
                    (earlierOwner.OriginalOwnerKey!=identity.StorageKey||earlierOwner.Creation!=creation||earlierOwner.Deaths!=actor.Died))return false;
            }
            var presentation=RebirthCraftingPresentation.Resolve(card);if(presentation==null)return false;
            var result=new BindingToken{Presentation=presentation,Generation=presentation.CraftIntentEpoch,Definition=DefinitionHash(recipe),Actor=actor,World=world,Window=window,Queue=queue,Card=card,Station=station,Recipe=recipe,
                Index=index,Deaths=actor.Died,Owner=identity.StorageKey,Creation=creation,WorldKey=worldKey,Operation=operation,Birth=birth,
                SaveDirectory=Path.GetFullPath(GameIO.GetSaveGameDir()),PlayerDirectory=Path.GetFullPath(GameIO.GetPlayerDataDir()),EarlierOwner=earlierOwner,OwnerPurpose=ownerPurpose,StartingOwnerEntityId=card.StartingEntityId};
            result.Position=station.ToWorldPos();result.BlockType=world.GetBlock(result.Position).type;
            if(!world.IsRemote()&&RebirthStationGridQueue.IsMarked(recipe))
            {
                if(!RebirthStationObservationDispatcher.TryGetVerifiedQueuedAdmission(station,station.Queue[index],out var admission)||
                    !admission.MatchesOwningCard(window,card,index))return false;
                result.Admission=admission;
            }
            if(!result.Current())return false;token=result;return true;
        }
        catch{return false;}
    }
    internal sealed class Original:IDisposable
    {
        private BindingToken binding;private byte[][] cards;private byte[] input,output,fuel,tools,afterTools,cardAfter,image,bag,belt;
        private RebirthProductiveEarnedRowProof.Frame frame;private IDisposable retention,ownerRetention;private byte[] ownerBefore=Array.Empty<byte>();private bool closed;
        internal Guid Operation=>binding.Operation;internal Guid Birth=>binding.Birth;internal int QueueIndex=>binding.Index;
        internal BindingToken Binding=>closed?null:binding;
        internal byte[] CopyArtifact()
        {using(var count=new RebirthCamperSavedVehicle.CountStream(RebirthProductiveEarnedRowProof.MaximumArtifactBytes))WriteArtifact(count);
         using(var stream=new MemoryStream()){WriteArtifact(stream);return stream.ToArray();}}
        internal bool AttachOwnerBefore(byte[] bytes)
        {if(closed||ownerBefore.Length!=0||bytes==null||bytes.Length<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4L*bytes.Length),out ownerRetention))return false;ownerBefore=(byte[])bytes.Clone();return true;}
        internal RebirthProductiveEarnedRowProof.Frame ReadFrame()
        {if(closed)throw new InvalidOperationException();return RebirthProductiveEarnedRowProof.Decode(image);}
        internal bool CurrentUi(bool earned)
        {
            if(closed||binding?.Current()!=true||!OwnerInventoryCurrent())return false;
            var live=binding.Queue.GetRecipesToCraft();if(live.Length!=cards.Length)return false;
            for(int i=0;i<live.Length;i++)if(!Row(live[i]).SequenceEqual(earned&&i==binding.Index?cardAfter:cards[i]))return false;
            return Grid(binding.Window.inputWindow?.GetSlots()).SequenceEqual(input)&&Grid(binding.Window.outputWindow?.GetSlots()).SequenceEqual(output)&&
                Grid(binding.Window.fuelWindow?.GetSlots()).SequenceEqual(fuel)&&Grid(binding.Window.toolWindow?.GetSlots()).SequenceEqual(earned?afterTools:tools);
        }
        internal bool CurrentBefore()=>Current(false);
        internal bool CurrentEarnedProjection()=>Current(true);
        private bool Current(bool earned)
        {
            if(closed||binding?.Current()!=true||!OwnerInventoryCurrent())return false;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null)return false;
                var live=binding.Queue.GetRecipesToCraft();if(live.Length!=cards.Length)return false;
                for(int i=0;i<live.Length;i++)if(!Row(live[i]).SequenceEqual(earned&&i==binding.Index?cardAfter:cards[i]))return false;
                if(!Grid(binding.Window.inputWindow?.GetSlots()).SequenceEqual(input)||!Grid(binding.Window.outputWindow?.GetSlots()).SequenceEqual(output)||
                    !Grid(binding.Window.fuelWindow?.GetSlots()).SequenceEqual(fuel)||!Grid(binding.Window.toolWindow?.GetSlots()).SequenceEqual(earned?afterTools:tools))return false;
                return RebirthProductiveEarnedNativeBody.MatchesCurrent(binding.Station,earned?frame.AfterNative:frame.BeforeNative,
                    earned?frame.AfterElapsedOffset:frame.BeforeElapsedOffset);
            }
        }
        // Exact whole body comparison; no supplied image can replace the frozen
        // original or authorize native sync. Native accepted-clock checks remain
        // the parent observer's independent requirement.
        internal bool MatchesNativeSync(byte[] native,int elapsedOffset,bool earned)
            =>!closed&&RebirthProductiveEarnedNativeBody.SameExceptElapsed(native,elapsedOffset,
                earned?frame.AfterNative:frame.BeforeNative,earned?frame.AfterElapsedOffset:frame.BeforeElapsedOffset);
        internal void WriteArtifact(Stream destination)
        {
            if(closed||destination==null||!destination.CanWrite)throw new InvalidOperationException();
            using(var writer=new BinaryWriter(destination,System.Text.Encoding.UTF8,true))
            {
                writer.Write(0x314F4955u); // UIO1: never decode as native-loop REW2.
                writer.Write(binding.Operation.ToByteArray());writer.Write(binding.Birth.ToByteArray());writer.Write(binding.Owner);
                writer.Write(binding.WorldKey);writer.Write(binding.Creation);writer.Write(binding.Deaths);writer.Write(binding.Index);
                writer.Write(cards.Length);foreach(var row in cards){writer.Write(row.Length);writer.Write(row);}
                foreach(var value in new[]{input,output,fuel,tools,afterTools,cardAfter,image}){writer.Write(value.Length);writer.Write(value);}
                writer.Write(ownerBefore.Length);writer.Write(ownerBefore);
                foreach(var value in new[]{bag,belt}){writer.Write(value.Length);writer.Write(value);}
                writer.Write(binding.OwnerPurpose);
                writer.Flush();
            }
        }
        internal bool OwnerInventoryCurrent()=>bag!=null&&belt!=null&&Grid(binding.Actor.bag.ItemGrid.items).SequenceEqual(bag)&&Grid(binding.Actor.inventory.ItemGrid.items).SequenceEqual(belt);
        public void Dispose(){if(closed)return;closed=true;cards=null;input=output=fuel=tools=afterTools=cardAfter=image=ownerBefore=bag=belt=null;frame=null;retention?.Dispose();retention=null;ownerRetention?.Dispose();ownerRetention=null;}
        internal static Original Prepare(BindingToken token,float target,float worked,ItemStack[] plannedTools)
        {
            if(token?.Current()!=true||token.Card.recipeCount<1||!token.Card.IsCrafting||token.Card.isInventoryFull||
                token.Card.craftingTimeLeft<=0||target>0||float.IsNaN(target)||float.IsInfinity(target)||worked<=0||
                float.IsNaN(worked)||float.IsInfinity(worked)||Math.Abs(Math.Min(token.Card.craftingTimeLeft,token.Card.craftingTimeLeft-target)-worked)>0.0001f)return null;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!token.Current())return null;var live=token.Queue.GetRecipesToCraft();
                var result=new Original{binding=token,cards=live.Select(Row).ToArray(),input=Grid(token.Window.inputWindow?.GetSlots()),
                    output=Grid(token.Window.outputWindow?.GetSlots()),fuel=Grid(token.Window.fuelWindow?.GetSlots()),tools=Grid(token.Window.toolWindow?.GetSlots()),afterTools=Grid(plannedTools),
                    bag=Grid(token.Actor.bag.ItemGrid.items),belt=Grid(token.Actor.inventory.ItemGrid.items)};
                try
                {
                    if(!RebirthProductiveEarnedArtifact.SameTools(token.Station.Tools,result.tools))return null;
                    // One retained original earns exactly this application.
                    // Native negative completion is represented without carrying
                    // an uncharged overshoot into the next queued unit.
                    var after=CardRow(token.Card);after.CraftingTimeLeft=-float.Epsilon;result.cardAfter=RebirthProductiveEarnedRowProof.CaptureRow(after);
                    if(!RebirthProductiveEarnedNativeBody.TryCapturePlan(token.Station,plannedTools,token.Index,after.CraftingTimeLeft,
                        out var beforeNative,out var afterNative,out int beforeOffset,out int afterOffset))return null;
                    var nativeBefore=token.Station.Queue[token.Index];var nativeAfter=new RecipeQueueItem{Recipe=nativeBefore.Recipe,Multiplier=nativeBefore.Multiplier,
                        CraftingTimeLeft=after.CraftingTimeLeft,IsCrafting=nativeBefore.IsCrafting,Quality=nativeBefore.Quality,StartingEntityId=nativeBefore.StartingEntityId,
                        OneItemCraftTime=nativeBefore.OneItemCraftTime,RepairItem=nativeBefore.RepairItem,AmountToRepair=nativeBefore.AmountToRepair};
                    result.frame=new RebirthProductiveEarnedRowProof.Frame{Operation=token.Operation,Birth=token.Birth,World=token.WorldKey,RowIndex=token.Index,
                        Multiplier=nativeBefore.Multiplier,WorkedSeconds=worked,BeforeRow=RebirthProductiveEarnedRowProof.CaptureRow(nativeBefore),
                        AfterRow=RebirthProductiveEarnedRowProof.CaptureRow(nativeAfter),BeforeTools=result.tools,AfterTools=result.afterTools,
                        BeforeNative=beforeNative,AfterNative=afterNative,BeforeElapsedOffset=beforeOffset,AfterElapsedOffset=afterOffset};
                    result.image=RebirthProductiveEarnedRowProof.Encode(result.frame);
                    using(var count=new RebirthCamperSavedVehicle.CountStream(RebirthProductiveEarnedRowProof.MaximumArtifactBytes))result.WriteArtifact(count);
                    long bytes=checked(4L*result.image.Length+result.cards.Sum(value=>(long)value.Length)+result.input.Length+result.output.Length+result.fuel.Length+result.cardAfter.Length+4L*(result.bag.Length+result.belt.Length));
                    if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(bytes,out result.retention))return null;
                    var retained=result;result=null;return retained;
                }
                finally{result?.Dispose();}
            }
        }
    }
    // Called only by the dedicated retained-card coordinator. The complete
    // producer remains inactive until its cumulative source chain is reviewed.
    internal static bool TryFreezeFinal(BindingToken token,float target,float worked,ItemStack[] plannedTools,out Original original)
    {original=null;try{original=Original.Prepare(token,target,worked,plannedTools);return original!=null;}catch{return false;}}
    private static string DefinitionHash(Recipe recipe)
    {
        var row=new RecipeQueueItem{Recipe=recipe,Multiplier=1,StartingEntityId=-1,Quality=1,OneItemCraftTime=-1};
        string source=RebirthRemoteResourcePlacedStationQueue.Encode(new[]{row})+"|"+RebirthRecipeOutputs.DefinitionIdentity(recipe);
        using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(source))).Replace("-","");
    }
    internal static RecipeQueueItem CardRow(XUiC_RecipeStack card)
        =>new RecipeQueueItem{Recipe=card.GetRecipe(),Multiplier=checked((short)card.GetRecipeCount()),CraftingTimeLeft=card.GetRecipeCraftingTimeLeft(),
            IsCrafting=card.IsCrafting,Quality=checked((byte)card.OutputQuality),StartingEntityId=card.StartingEntityId,OneItemCraftTime=card.GetOneItemCraftTime()};
    private static byte[] Row(XUiC_RecipeStack card)=>RebirthProductiveEarnedRowProof.CaptureRow(CardRow(card));
    private static byte[] Grid(ItemStack[] slots)
    {if(slots==null)return Array.Empty<byte>();if(!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(slots,262144,349528,out var value))throw new InvalidDataException();return Convert.FromBase64String(value);}
}
