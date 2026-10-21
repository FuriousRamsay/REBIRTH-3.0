using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using HarmonyLib;

// Original owner is issued at actual earlier insertion, never reconstructed
// from the entity ID found on a later queue row. Old rows remain unknown.
internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    internal sealed class Witness:IDisposable
    {
        internal World World;internal TileEntityWorkstation Station;internal Recipe Recipe;
        internal string OriginalOwnerKey,Creation,RecipeBinding,NativeRecipeBinding,Save,WorldKey,Path;
        internal int StartingEntityId,OriginalCount,Deaths;internal Guid Operation,Birth;
        internal string ClientPurpose;internal RebirthProductiveEarnedUiOriginal.BindingToken ClientBinding;
        internal string Source4Path;internal byte[] Source4Image;
        internal object InitialLoaded;
        internal RebirthWorkstationCompletionPipeline.RemoteOutputHostSource FinalOutputSource;
        internal RebirthProductiveEarnedUiDeferredTransport.AuthorityLocalLoadedSource AuthorityLocalSource;
        internal RemoteInsertion RemoteInsertion;internal NativeHydrationSource NativeHydration;
        internal RebirthWorkstationCompletionPipeline.CachedPrimaryReentry CachedSource;
        internal RebirthWorkstationCompletionPipeline.CachedIdentitySuccessor CachedIdentity;
        internal RebirthWorkstationCompletionPipeline.CachedHostOriginal CachedHostSource;
        internal Guid CurrentCustodyBirth=>CachedHostSource?.CustodyBirth??CachedIdentity?.CustodyBirth??CachedSource?.CustodyBirth??Birth;
        internal RebirthProductiveEarnedUiDeferredTransport.ImportedContribution ClientReceipt;
        internal byte[] Image;internal int References;internal bool Published,Closed,RowRetired;
        internal IDisposable Retention,HydrationRetention,HydrationCurrentRetention;
        internal bool SourceCurrent()=>!Closed&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&
            Save==System.IO.Path.GetFullPath(GameIO.GetSaveGameDir())&&WorldKey==RebirthProductiveEarnedArtifact.WorldIdentity(World)&&
            ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station)&&
            (AuthorityLocalSource!=null?AuthorityLocalSource.IdentityCurrent():FinalOutputSource!=null?FinalOutputSource.Current()&&(FinalOutputSource.Native?.Current()==true||FinalOutputSource.Native?.IdentityCurrent()==true):CachedHostSource!=null?CachedHostSource.SourceCurrentWithoutRows():CachedIdentity!=null?CachedIdentity.SourceCurrent():CachedSource!=null?CachedSource.SourceCurrent():ClientReceipt!=null?ClientReceipt.SourceCurrentWithoutRow():InitialLoaded!=null?InitialSourceCurrent(InitialLoaded):NativeHydration!=null?NativeHydration.Current():RemoteInsertion!=null?RemoteInsertion.PublishedCurrent():ClientBinding!=null?ClientPurposeCurrent(this):RebirthLogisticsEndpointIdentity.TryGetIncarnation(World,Station,out var birth)&&birth==Birth.ToString("N"))&&
            (ClientReceipt!=null||RebirthProductiveEarnedArtifact.FileMatches(Path,Image))&&
            (Source4Image==null||RebirthProductiveEarnedArtifact.FileMatches(Source4Path,Source4Image));
        private bool MatchesRow(RecipeQueueItem row)=>(CachedIdentity==null||CachedIdentity.RowCurrent())&&(NativeHydration==null||NativeHydration.RowCurrent())&&row?.Recipe!=null&&ReferenceEquals(row.Recipe,Recipe)&&
            row.StartingEntityId==StartingEntityId&&row.Multiplier>0&&row.Multiplier<=OriginalCount&&NativeBinding(row)==NativeRecipeBinding;
        internal bool Matches(RecipeQueueItem row)=>!RowRetired&&SourceCurrent()&&MatchesRow(row);
        internal bool Current()=>!RowRetired&&SourceCurrent()&&(NativeHydration!=null||RemoteInsertion==null||RemoteInsertion.CardCurrent())&&Station.Queue!=null&&Station.Queue.Count(MatchesRow)==1;
        internal IDisposable RetainForCompletion()
        {if(!SourcePublished())return null;References++;return new CompletionLease(this);}
        internal IDisposable RetainPublishedSource()
        {if(!Published||!SourceCurrent()||References==0&&!Current())return null;References++;return new CompletionLease(this);}
        // Durable original identity event only. This deliberately does not
        // assert saved station custody; each effect owner proves that separately.
        internal bool SourcePublished()=>Published&&Current();
        public void Dispose(){if(Closed)return;Closed=true;Image=null;Source4Image=null;Retention?.Dispose();HydrationRetention?.Dispose();HydrationRetention=null;HydrationCurrentRetention?.Dispose();HydrationCurrentRetention=null;}
    }
    private sealed class CompletionLease:IDisposable
    {private Witness value;internal CompletionLease(Witness source){value=source;}public void Dispose(){if(value==null)return;value.References--;value=null;}}
    private sealed class LocalInsertion
    {internal XUiC_CraftingQueue Queue;internal XUiC_RecipeStack Card;internal EntityPlayerLocal Actor;internal TileEntityWorkstation Station;internal string Owner,Creation,FrozenBinding;internal Recipe Recipe;internal int Count,Index,Deaths;internal Guid Operation;internal byte[] Before;internal IDisposable Retention;}
    private static readonly List<Witness> originals=new List<Witness>();
    private static readonly List<LocalInsertion> local=new List<LocalInsertion>();
    private static bool installed;private static World observed;private static double nextPump;
    internal static void Install()
    {
        if(installed)return;RebirthProductiveEarnedNativeBody.Install();
        RebirthProductiveEarnedCancellation.InstallBeforeFlushObserver();RebirthProductiveEarnedRemoteHost.Install();
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.productive.ui.queue.owner"),typeof(Insert));
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.productive.ui.queue.owner"),typeof(PendingCopy));
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.productive.ui.queue.owner"),typeof(AddReceipt));
        RebirthProductiveEarnedUiCoordinator.InstallQueueRotationBoundary();
        RebirthProductiveEarnedUiDeferredTransport.InstallSelectedSendObserver();
        ModEvents.GameUpdate.RegisterHandler(UpdateInitialPublications);
        installed=true;
    }
    internal static bool TryObserveQueueOwner(TileEntityWorkstation station,int rowIndex,RecipeQueueItem exactRow,out Witness witness)
    {
        witness=null;if(!installed||station?.Queue==null||rowIndex<0||rowIndex>=station.Queue.Length||
            !ReferenceEquals(station.Queue[rowIndex],exactRow))return false;
        ObserveWorld();
        if(!originals.Any(value=>ReferenceEquals(value.Station,station)&&value.Matches(exactRow)))
            RebirthRemoteResourcePlacedStationOriginal.TryIssueColdSource4Owner(station,exactRow);
        if(!originals.Any(value=>ReferenceEquals(value.Station,station)&&value.Matches(exactRow)))TryIssueInitialQueueOwner(station,exactRow);
        var matches=originals.Where(value=>ReferenceEquals(value.Station,station)&&value.Matches(exactRow)).ToArray();
        if(matches.Length!=1||!matches[0].SourcePublished())return false;witness=matches[0];return true;
    }
    // This is only an earlier actual UI insertion boundary for one scoped
    // synchronization. It is never a native-row or saved-custody witness.
    internal static bool IsPendingOwningInsertion(XUiC_RecipeStack card,TileEntityWorkstation station)
    {
        if(!installed||!ThreadManager.IsMainThread()||station==null||card==null)return false;
        ObserveWorld();
        var matches=local.Where(value=>ReferenceEquals(value.Card,card)&&ReferenceEquals(value.Station,station)).ToArray();
        if(matches.Length!=1)return false;var item=matches[0];
        var cards=item.Queue.GetRecipesToCraft();var actor=item.Actor;
        return ReferenceEquals(actor.world,GameManager.Instance?.World)&&!actor.world.IsRemote()&&
            ReferenceEquals(actor.world.GetPrimaryPlayer(),actor)&&!actor.IsDead()&&actor.Died==item.Deaths&&
            RebirthCamperCharacterAccess.CurrentCreation(actor)==item.Creation&&
            RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&identity.StorageKey==item.Owner&&
            ReferenceEquals(actor.world.GetTileEntity(station.ToWorldPos()),station)&&
            item.Index>=0&&item.Index<cards.Length&&ReferenceEquals(cards[item.Index],card)&&
            ReferenceEquals(card.GetRecipe(),item.Recipe)&&card.GetRecipeCount()==item.Count&&card.StartingEntityId==actor.entityId&&
            Binding(InsertionRow(card))==item.FrozenBinding;
    }
    private static RecipeQueueItem InsertionRow(XUiC_RecipeStack card)=>new RecipeQueueItem{Recipe=card.GetRecipe(),Multiplier=checked((short)card.GetRecipeCount()),
        Quality=checked((byte)card.OutputQuality),StartingEntityId=card.StartingEntityId,OneItemCraftTime=card.GetOneItemCraftTime()};
    internal static void Pump()
    {
        if(!installed)return;
        if(ReferenceEquals(GameManager.Instance?.World,observed)&&originals.Count==0&&
            remoteInsertions.Count==0&&local.Count==0)return;
        ObserveWorld();
        double now=System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;if(now<nextPump)return;nextPump=now+0.25;
        PumpRemoteInsertions();
        foreach(var value in originals.ToArray())
        {
            try
            {
            if(value.Published&&!value.Current()&&value.References==0){originals.Remove(value);value.Dispose();continue;}
            }
            catch{/* Retain the positively captured original on uncertainty. */}
        }
    }
    private static void ObserveWorld()
    {
        var world=GameManager.Instance?.World;RebirthProductiveEarnedUiDeferredTransport.ReleaseQueueOwnerHydrationHistory(world);if(ReferenceEquals(world,observed))return;
        foreach(var group in aggregates.ToArray())foreach(var member in group.Members.ToArray())member.Dispose();aggregates.Clear();
        foreach(var value in originals)value.Dispose();foreach(var value in local){value.Before=null;value.Retention?.Dispose();}originals.Clear();local.Clear();DropRemoteInsertions();observed=world;nextPump=0;
    }
    internal static string Binding(RecipeQueueItem row)
    {
        var copy=new RecipeQueueItem{Recipe=row.Recipe,Multiplier=1,CraftingTimeLeft=0,IsCrafting=false,Quality=row.Quality,
            StartingEntityId=row.StartingEntityId,OneItemCraftTime=row.OneItemCraftTime,RepairItem=row.RepairItem,AmountToRepair=row.AmountToRepair};
        return RebirthRemoteResourcePlacedStationQueue.Encode(new[]{copy});
    }
    internal static string NativeBinding(RecipeQueueItem row)
    {
        var copy=new RecipeQueueItem{Recipe=row.Recipe,Multiplier=1,CraftingTimeLeft=0,IsCrafting=false,Quality=row.Quality,
            StartingEntityId=row.StartingEntityId,OneItemCraftTime=row.OneItemCraftTime,RepairItem=row.RepairItem,AmountToRepair=row.AmountToRepair};
        return RebirthCookingRemoteAdmissionBackend.Queue(new[]{copy});
    }
    private static bool Capture(TileEntityWorkstation station,out byte[] body,out int offset)
    {
        body=null;offset=-1;if(station?.Queue==null||station.Queue.Length<1||station.Queue[0]==null)return false;
        return RebirthProductiveEarnedNativeBody.TryCapturePlan(station,station.Tools,0,station.Queue[0].CraftingTimeLeft,
            out body,out _,out offset,out _);
    }
    private static bool Issue(EntityPlayer actor,TileEntityWorkstation station,RecipeQueueItem row,string owner,string creation,int deaths,byte[] before,Guid operation=default,string source4Path=null,byte[] source4Image=null,
        RebirthRemoteResourcePlacedStationOriginal.SavedEarnedOwner source4=null)
    {
        var world=source4?.World??actor?.world;int entity=source4?.EntityId??actor?.entityId??-1;
        if(!installed||world==null||!ReferenceEquals(GameManager.Instance?.World,world)||
            (source4==null?actor==null||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||actor.Died!=deaths||RebirthCamperCharacterAccess.CurrentCreation(actor)!=creation:
                source4.Current()!=true||!ReferenceEquals(source4.Station,station)||!ReferenceEquals(source4.Row,row)||source4.Owner!=owner||source4.Creation!=creation||source4.Deaths!=deaths||source4.Source!=operation||source4Image==null)||
            station==null||row?.Recipe==null||row.StartingEntityId!=entity||
            !RebirthOriginalActionScope.Validate(owner,creation)||originals.Count>=64||
            (source4Path==null)!=(source4Image==null)||source4Image!=null&&!RebirthProductiveEarnedArtifact.FileMatches(source4Path,source4Image)||
            !RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out var birth)||!Guid.TryParseExact(birth,"N",out var incarnation))return false;
        var retained=originals.Where(value=>ReferenceEquals(value.Station,station)&&value.Matches(row)).ToArray();
        if(retained.Length>0)return retained.Length==1&&retained[0].OriginalOwnerKey==owner&&retained[0].Creation==creation&&
            (source4Image==null||retained[0].Operation==operation&&retained[0].Source4Path==source4Path&&
                retained[0].Source4Image!=null&&retained[0].Source4Image.SequenceEqual(source4Image));
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null)return false;
            byte[] native;int offset;
            if(source4==null){if(!Capture(station,out native,out offset))return false;}
            else
            {
                // Additional positively saved Source4 producer. These are
                // explicitly its saved native body projected into this
                // descriptor grammar, not an invented AddRecipe callback.
                if(!RebirthRemoteResourcePlacedStationNativeReader.TryRead(source4.After(),out var saved))return false;
                native=RebirthProductiveEarnedNativeBody.Encode(saved,0,out offset);
                if(!RebirthProductiveEarnedNativeBody.TryRead(native,offset,out _,out _)||!source4.Current())return false;
            }
            if(operation==Guid.Empty)operation=Guid.NewGuid();string binding=source4==null?Binding(row):Source4DefinitionBinding(row),save=Path.GetFullPath(GameIO.GetSaveGameDir());
            var node=new XElement("nativeQueueOwnerOriginal",new XAttribute("operation",operation.ToString("N")),new XAttribute("birth",birth),
                new XAttribute("owner",owner),new XAttribute("creation",creation),new XAttribute("entity",entity),new XAttribute("deaths",deaths),
                new XAttribute("world",RebirthProductiveEarnedArtifact.WorldIdentity(world)),new XAttribute("save",save),
                new XElement("recipeBinding",binding),new XElement("beforeNative",Convert.ToBase64String(before??Array.Empty<byte>())),
                new XElement("acceptedNative",Convert.ToBase64String(native)));
            if(source4Image!=null)node.SetAttributeValue("source4",ProofHash(source4Image));
            var image=Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting));
            if(image.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4L*image.Length+4L*binding.Length+(source4Image?.LongLength??0)),out var retention))return false;
            var value=new Witness{World=world,Station=station,Recipe=row.Recipe,OriginalOwnerKey=owner,Creation=creation,StartingEntityId=entity,
                OriginalCount=source4?.OriginalCount??row.Multiplier,Deaths=deaths,RecipeBinding=binding,NativeRecipeBinding=NativeBinding(row),Save=save,WorldKey=RebirthProductiveEarnedArtifact.WorldIdentity(world),
                Operation=operation,Birth=incarnation,Image=image,Retention=retention,Source4Path=source4Path,Source4Image=source4Image==null?null:(byte[])source4Image.Clone()};
            value.Path=Path.Combine(save,"RebirthProductiveEarnedUi","queueOwners",owner,operation.ToString("N")+".original");
            if(!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(value.Path,image)){value.Dispose();return false;}
            value.Published=true;originals.Add(value);RecordRemoteIssuedSource(value);BeginInitialPublication(value,native,offset);
            return true;
        }
    }
    [HarmonyPatch(typeof(XUiC_CraftingQueue),nameof(XUiC_CraftingQueue.AddRecipeToCraftAtIndex))]
    private static class Insert
    {
        private sealed class Scopes{internal LocalInsertion Local;internal object Remote;}
        private static void Prefix(XUiC_CraftingQueue __instance,int _index,Recipe _recipe,int _count,bool recipeModification,int startingEntityId,bool __runOriginal,out Scopes __state)
        {
            __state=new Scopes();BeforeRemoteInsertion(__instance,_index,_recipe,_count,recipeModification,startingEntityId,__runOriginal,out __state.Remote);
            BeforeLocal(__instance,_index,_recipe,_count,recipeModification,startingEntityId,__runOriginal,out __state.Local);
        }
        private static void BeforeLocal(XUiC_CraftingQueue __instance,int _index,Recipe _recipe,int _count,bool recipeModification,int startingEntityId,bool __runOriginal,out LocalInsertion __state)
        {
            __state=null;if(!__runOriginal||!installed||recipeModification||startingEntityId!=-1||_recipe==null)return;
            try
            {
                ObserveWorld();var actor=__instance.xui?.playerUI?.entityPlayer;var station=__instance.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity;
                var cards=__instance.GetRecipesToCraft();if(actor?.world?.IsRemote()!=false||station==null||_index<0||_index>=cards.Length||cards[_index].GetRecipe()!=null||
                    !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||!ReferenceEquals(actor.world.GetPrimaryPlayer(),actor))return;
                string creation=RebirthCamperCharacterAccess.CurrentCreation(actor);if(!RebirthOriginalActionScope.Validate(identity.StorageKey,creation))return;
                __state=new LocalInsertion{Queue=__instance,Card=cards[_index],Actor=actor,Station=station,Owner=identity.StorageKey,Creation=creation,Deaths=actor.Died,Recipe=_recipe,Count=_count,Index=_index,Operation=Guid.NewGuid()};
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                {if(decoder==null||!Capture(station,out __state.Before,out _)||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4L*__state.Before.Length),out __state.Retention))__state.Before=null;}
            }
            catch{__state=null;}
        }
        private static void Postfix(bool __result,bool __runOriginal,Scopes __state)
        {AfterRemoteInsertion(__result,__runOriginal,__state?.Remote);AfterLocal(__result,__runOriginal,__state?.Local);}
        private static void AfterLocal(bool __result,bool __runOriginal,LocalInsertion __state)
        {
            if(__state==null)return;if(!__runOriginal||!__result){__state.Before=null;__state.Retention?.Dispose();return;}
            try{if(ReferenceEquals(__state.Card.GetRecipe(),__state.Recipe)&&__state.Card.GetRecipeCount()==__state.Count&&
                __state.Card.StartingEntityId==__state.Actor.entityId)
                {
                    // Positive new UI insertion retires stale row eligibility
                    // even if retaining the new original later hits its bound.
                    var stale=__state.Station.Queue?[__state.Index];
                    if(stale!=null)foreach(var old in originals.Where(value=>ReferenceEquals(value.Station,__state.Station)&&value.Matches(stale)).ToArray())old.RowRetired=true;
                    if(local.Count>=64||__state.Before==null)return;
                    using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                    {
                        if(decoder==null)return;
                        string frozen=Binding(InsertionRow(__state.Card));
                        if(frozen.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||
                            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4L*__state.Before.Length+12L*frozen.Length),out var retention))return;
                        __state.Retention?.Dispose();__state.Retention=retention;__state.FrozenBinding=frozen;local.Add(__state);
                    }
                }}catch{}
        }
        private static Exception Finalizer(Exception __exception,Scopes __state)
        {
            EndRemoteInsertion(__state?.Remote);var value=__state?.Local;
            if(value!=null&&!local.Contains(value)){value.Before=null;value.Retention?.Dispose();}return __exception;
        }
    }
    internal static void AfterNativeSync(XUiC_WorkstationWindowGroup window,bool ran)
    {
        if(!ran||!installed)return;
        foreach(var value in local.Where(value=>ReferenceEquals(value.Queue,window.craftingQueue)).ToArray())
        {
            try
            {
                if(!IsPendingOwningInsertion(value.Card,value.Station))continue;
                var rows=value.Station.Queue;var row=rows?[value.Index];
                if(row?.Recipe==null||!ReferenceEquals(row.Recipe,value.Recipe)||row.Multiplier!=value.Count||row.StartingEntityId!=value.Actor.entityId||Binding(row)!=value.FrozenBinding)continue;
                if(Issue(value.Actor,value.Station,row,value.Owner,value.Creation,value.Deaths,value.Before,value.Operation))
                {local.Remove(value);value.Before=null;value.Retention?.Dispose();value.Retention=null;}
            }
            catch{}
        }
    }
}






