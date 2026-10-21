using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Purpose-specific placed originals. This plan is not an owner setter permit,
// accepted native upload, saved station receipt or reconstructed cold source.
internal static partial class RebirthRecyclerPlacedSettlement
{
    private static bool RecyclerOwnerConflict(EntityPlayerLocal actor)=>actor==null||
        RebirthCamperStationNativeActorPayment.Blocks(actor.bag)||RebirthCamperStationNativeActorPayment.Blocks(actor.inventory)||
        RebirthBackpackLibraryReservation.IsHeld(actor)||RebirthGearOwnerReservation.IsHeld(actor)||
        RebirthDogLifecycleService.IsHireInventoryHeld(actor)||RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(actor);
    internal sealed partial class Original:IDisposable,IStationOriginal
    {
        internal readonly Guid Operation;internal readonly string Purpose,Owner,CanonicalOwner,Creation,Directory,WorldKey;
        internal readonly World World;internal readonly EntityPlayerLocal Actor;internal TileEntityWorkstation Station {get;private set;}
        internal readonly int Deaths,BeforeOffset,AfterOffset;
        internal ItemStack[] BeforeBag,BeforeBelt,AfterBag,AfterBelt,Returns;
        internal bool[] BagLocks,BeltLocks;internal readonly int BeltCount;
        internal byte[] BeforeNative,AfterNative,Image;
        internal RecipeQueueItem[] Queue;internal ItemStack[] Input,Output,Tools;
        internal bool Cycle;
        internal readonly string Receipt,Checkpoint;internal Slot[] Slots;
        Guid IStationOriginal.Operation=>Operation;string IStationOriginal.Purpose=>Purpose;string IStationOriginal.Owner=>Owner;
        string IStationOriginal.Creation=>Creation;string IStationOriginal.WorldKey=>WorldKey;int IStationOriginal.Deaths=>Deaths;
        World IStationOriginal.World=>World;TileEntityWorkstation IStationOriginal.Station=>Station;
        byte[] IStationOriginal.BeforeNative=>BeforeNative;byte[] IStationOriginal.AfterNative=>AfterNative;byte[] IStationOriginal.Image=>Image;
        int IStationOriginal.BeforeOffset=>BeforeOffset;int IStationOriginal.AfterOffset=>AfterOffset;
        bool IStationOriginal.Current()=>Current();bool IStationOriginal.NativeLineageCurrent()=>NativeLineageCurrent();
        private IDisposable allocation;private bool closed;private readonly Func<bool> providerCurrent;
        private Original(Guid operation,string purpose,EntityPlayerLocal actor,TileEntityWorkstation station,RebirthStablePlayerIdentity identity,
            ItemStack[] beforeBag,ItemStack[] beforeBelt,ItemStack[] afterBag,ItemStack[] afterBelt,ItemStack[] returns,
            byte[] before,byte[] after,int beforeOffset,int afterOffset,ItemStack[] input,RecipeQueueItem[] queue,ItemStack[] output,ItemStack[] tools,
            byte[] image,IDisposable allocation)
        {
            Operation=operation;Purpose=purpose;Actor=actor;Station=station;World=actor.world;Owner=identity.StorageKey;CanonicalOwner=identity.CanonicalId;
            Creation=RebirthCamperCharacterAccess.CurrentCreation(actor);Deaths=actor.Died;Directory=Path.GetFullPath(GameIO.GetSaveGameDir());
            WorldKey=RebirthWorkstationCompletionOutbox.CurrentGuid(World);BeforeBag=beforeBag;BeforeBelt=beforeBelt;AfterBag=afterBag;AfterBelt=afterBelt;Returns=returns;
            BeforeNative=before;AfterNative=after;BeforeOffset=beforeOffset;AfterOffset=afterOffset;Input=input;Queue=queue;Output=output;Tools=tools;Image=image;
            BagLocks=RebirthCamperStationNativeActorPayment.Locks(actor.bag.LockedSlots);BeltLocks=RebirthCamperStationNativeActorPayment.Locks(actor.inventory.ItemGrid.SlotLocks);
            BeltCount=RebirthToolbeltCapacity.GetOwnedSlotCount(actor,beforeBelt.Length);
            Receipt="rbRecyclerPlaced_"+operation.ToString("N");Checkpoint="rbRecyclerSlot_"+operation.ToString("N");
            Slots=Enumerable.Range(0,2).SelectMany(area=>Enumerable.Range(0,area==0?beforeBag.Length:beforeBelt.Length)
                .Where(index=>!Same((area==0?beforeBag:beforeBelt)[index],(area==0?afterBag:afterBelt)[index])).Select(index=>new Slot(area,index))).ToArray();
            var nativeProvider=World.ChunkCache?.ChunkProvider;
            var generator=nativeProvider as ChunkProviderGenerateWorld;var regions=generator?.m_RegionFileManager;var access=regions?.regionFileAccess;
            string regionDirectory=regions==null?null:Path.GetFullPath(regions.saveDirectory);
            providerCurrent=()=>nativeProvider!=null&&ReferenceEquals(World.ChunkCache?.ChunkProvider,nativeProvider)&&
                (World.IsRemote()||generator!=null&&regions!=null&&access is RegionFileAccessSectorBased&&
                 ReferenceEquals(generator.m_RegionFileManager,regions)&&ReferenceEquals(regions.regionFileAccess,access)&&Path.GetFullPath(regions.saveDirectory)==regionDirectory);
            this.allocation=allocation;
        }
        internal bool Current()=>!closed&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&ReferenceEquals(World.GetPrimaryPlayer(),Actor)&&
            ReferenceEquals(World.GetEntity(Actor.entityId),Actor)&&(ClosedOwner!=null?ClosedOwner.CurrentOwner(this):Station!=null&&ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station))&&Actor.IsSpawned()&&!Actor.IsDead()&&
            Actor.Died==Deaths&&RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&Path.GetFullPath(GameIO.GetSaveGameDir())==Directory&&
            RebirthWorkstationCompletionOutbox.CurrentGuid(World)==WorldKey&&providerCurrent()&&
            RebirthCamperStationNativeActorPayment.Locks(Actor.bag.LockedSlots).SequenceEqual(BagLocks)&&
            RebirthCamperStationNativeActorPayment.Locks(Actor.inventory.ItemGrid.SlotLocks).SequenceEqual(BeltLocks)&&
            RebirthToolbeltCapacity.GetOwnedSlotCount(Actor,BeforeBelt.Length)==BeltCount&&RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&identity.StorageKey==Owner;
        internal bool NativeLineageCurrent()=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&
            (ClosedOwner!=null?ClosedOwner.CurrentOwner(this):Station!=null&&ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station))&&providerCurrent()&&
            Path.GetFullPath(GameIO.GetSaveGameDir())==Directory&&RebirthWorkstationCompletionOutbox.CurrentGuid(World)==WorldKey;
        internal bool OwnerLineageCurrent()=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&ReferenceEquals(World.GetPrimaryPlayer(),Actor)&&
            ReferenceEquals(World.GetEntity(Actor.entityId),Actor)&&Actor.IsSpawned()&&!Actor.IsDead()&&Actor.Died==Deaths&&
            RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&Path.GetFullPath(GameIO.GetSaveGameDir())==Directory&&
            RebirthWorkstationCompletionOutbox.CurrentGuid(World)==WorldKey&&RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&identity.StorageKey==Owner;
        internal bool RebindActualStation(LoadedStation loaded,OwnerPublication owner,StationPublication previous)
        {
            if(closed||!OwnerLineageCurrent()||loaded?.Current()!=true||loaded.Operation!=Operation||
                !ReferenceEquals(previous?.Original,this)||owner!=null&&!ReferenceEquals(owner.Original,this)||
                !ReferenceEquals(loaded.World,World)||ReferenceEquals(loaded.Station,Station)||
                loaded.Station.ToWorldPos()!=Station.ToWorldPos()||
                (owner!=null?!owner.CanReenterStation(loaded.Stage):loaded.Stage!="before"||Actor.Buffs.GetCustomVar(Receipt)!=0f||
                    Actor.Buffs.GetCustomVar(Checkpoint)!=0f||!MatchesOwnerCheckpoint(Actor.bag.ItemGrid.items,Actor.inventory.ItemGrid.items,0))||
                previous.AfterApplied!=(loaded.Stage=="after")||!loaded.MatchesOriginal(Image))return false;
            // Only the actual native Load/Instantiate/read issuer can change
            // this handle. Native bytes/equality alone cannot enter this API.
            Station=loaded.Station;return true;
        }
        // Deterministic exact checkpoint, not a mixed-image adoption rule.
        internal bool MatchesOwnerCheckpoint(ItemStack[] bag,ItemStack[] belt,int completed)
        {
            if(completed<0||completed>Slots.Length||bag?.Length!=BeforeBag.Length||belt?.Length!=BeforeBelt.Length)return false;
            for(int area=0;area<2;area++)
            {
                var live=area==0?bag:belt;var before=area==0?BeforeBag:BeforeBelt;var after=area==0?AfterBag:AfterBelt;
                for(int slot=0;slot<live.Length;slot++)
                {
                    int index=Array.FindIndex(Slots,value=>value.Area==area&&value.Index==slot);
                    if(!Same(live[slot],index>=0&&index<completed?after[slot]:before[slot]))return false;
                }
            }
            return true;
        }
        internal static Original Capture(EntityPlayerLocal actor,TileEntityWorkstation station,Guid operation,string purpose,
            ItemStack[] input,RecipeQueueItem[] queue,IList<ItemStack> returns,ItemStack[] output,ItemStack[] tools,Func<bool> current,bool cycle=false,float workSeconds=0,XElement nativeViewer=null)
        {
            if(!ThreadManager.IsMainThread()||actor==null||station==null||station is RebirthCamperStationState||operation==Guid.Empty||
                purpose!="enqueue"&&purpose!="cancel"&&purpose!="complete"||current?.Invoke()!=true||
                !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetPrimaryPlayer(),actor)||
                !ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||!ReferenceEquals(actor.world.GetTileEntity(station.ToWorldPos()),station)||
                actor.IsDead()||RecyclerOwnerConflict(actor)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||string.IsNullOrEmpty(RebirthCamperCharacterAccess.CurrentCreation(actor))||
                input==null||queue==null||output==null||tools==null||returns==null||returns.Count>64||float.IsNaN(workSeconds)||float.IsInfinity(workSeconds)||workSeconds<0||
                purpose!="complete"&&workSeconds!=0||actor.PlayerUI?.xui?.DragAndDropWindow?.IsEmpty()!=true)return null;
            var sourceWorld=actor.world;int sourceDeaths=actor.Died;string sourceCreation=RebirthCamperCharacterAccess.CurrentCreation(actor);
            string sourceDirectory=Path.GetFullPath(GameIO.GetSaveGameDir()),sourceWorldKey=RebirthWorkstationCompletionOutbox.CurrentGuid(sourceWorld);
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out var allocation))return null;
            Original issued=null;
            try
            {
                var beforeBag=ItemStack.Clone(actor.bag.ItemGrid.items);var beforeBelt=ItemStack.Clone(actor.inventory.ItemGrid.items);
                var bagLocks=RebirthCamperStationNativeActorPayment.Locks(actor.bag.LockedSlots);var beltLocks=RebirthCamperStationNativeActorPayment.Locks(actor.inventory.ItemGrid.SlotLocks);
                int beltCount=RebirthToolbeltCapacity.GetOwnedSlotCount(actor,beforeBelt.Length);
                var frozen=returns.Select(item=>item.Clone()).ToArray();
                if(!RebirthRecyclerOwnerDelivery.Plan(beforeBag,beforeBelt,bagLocks,beltLocks,beltCount,frozen,out var afterBag,out var afterBelt))return null;
                byte[] before,after;int beforeOffset,afterOffset;
                var projectedQueue=cycle?ProjectNativeCycle(queue):queue;if(projectedQueue==null)return null;
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                    if(decoder==null||!RebirthProductiveEarnedNativeBody.TryCaptureUiProjection(station,projectedQueue,input,output,null,tools,out before,out after,out beforeOffset,out afterOffset))return null;
                var original=new XElement("placedRecyclerOriginal",new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),new XAttribute("purpose",purpose),
                    new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(actor.world)),new XAttribute("owner",identity.StorageKey),new XAttribute("account",identity.CanonicalId),
                    new XAttribute("creation",RebirthCamperCharacterAccess.CurrentCreation(actor)),new XAttribute("deaths",actor.Died),
                    new XAttribute("bagLocks",new string(bagLocks.Select(value=>value?'1':'0').ToArray())),new XAttribute("beltLocks",new string(beltLocks.Select(value=>value?'1':'0').ToArray())),new XAttribute("beltCount",beltCount),
                    new XAttribute("x",station.ToWorldPos().x),new XAttribute("y",station.ToWorldPos().y),new XAttribute("z",station.ToWorldPos().z),new XAttribute("block",station.block.GetBlockName()),
                    new XAttribute("beforeOffset",beforeOffset),new XAttribute("afterOffset",afterOffset),new XAttribute("cycle",cycle?1:0),new XAttribute("workSeconds",workSeconds),
                    Bytes("beforeNative",before),Bytes("afterNative",after),Grid("beforeBag",beforeBag),Grid("beforeBelt",beforeBelt),Grid("afterBag",afterBag),Grid("afterBelt",afterBelt),Grid("returns",frozen));
                if(nativeViewer!=null)original.Add(new XElement(nativeViewer));
                byte[] image=Encoding.UTF8.GetBytes(original.ToString(SaveOptions.DisableFormatting));
                long measured=checked(16L*(image.Length+before.Length+after.Length)+65536);
                if(image.Length>8*1024*1024||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(allocation,measured)||current?.Invoke()!=true||
                    !RebirthProductiveEarnedNativeBody.MatchesCurrent(station,before,beforeOffset))return null;
                var result=issued=new Original(operation,purpose,actor,station,identity,beforeBag,beforeBelt,afterBag,afterBelt,frozen,before,after,beforeOffset,afterOffset,
                    ItemStack.Clone(input),(RecipeQueueItem[])queue.Clone(),ItemStack.Clone(output),ItemStack.Clone(tools),image,allocation);allocation=null;
                result.Cycle=cycle;
                if(!result.Current()||!ReferenceEquals(result.World,sourceWorld)||result.Deaths!=sourceDeaths||result.Creation!=sourceCreation||
                    result.Directory!=sourceDirectory||result.WorldKey!=sourceWorldKey||RecyclerOwnerConflict(actor)||
                    !result.MatchesOwnerCheckpoint(actor.bag.ItemGrid.items,actor.inventory.ItemGrid.items,0)||
                    !result.BagLocks.SequenceEqual(bagLocks)||!result.BeltLocks.SequenceEqual(beltLocks)||result.BeltCount!=beltCount){result.Dispose();return null;}issued=null;return result;
            }
            catch{return null;}
            finally{issued?.Dispose();allocation?.Dispose();}
        }
        internal static Original FromActualCold(LoadedOwner owner,LoadedStation station)
        {
            if(owner?.Current()!=true||station?.Current()!=true||station.MeasureOriginal()<1||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*station.MeasureOriginal()+64L*1024*1024),out var memory))return null;
            Original issued=null;
            try
            {
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                {
                if(decoder==null)return null;
                byte[] image=station.CopyOriginal();if(image==null)return null;
                var node=RebirthCookingRemoteAdmission.ReadProtocolXml(Encoding.UTF8.GetString(image),8*1024*1024);
                if(node.Name!="placedRecyclerOriginal"||(int?)node.Attribute("version")!=1||
                    (string)node.Attribute("operation")!=station.Operation.ToString("N")||
                    !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||(string)node.Attribute("owner")!=identity.StorageKey||
                    (string)node.Attribute("account")!=identity.CanonicalId||(string)node.Attribute("creation")!=owner.creation||
                    (int?)node.Attribute("deaths")!=owner.deaths||!ReferenceEquals(station.Station.GetChunk(),station.Chunk)||
                    (string)node.Attribute("block")!=station.Station.block.GetBlockName()||
                    station.Station.ToWorldPos()!=new Vector3i((int)node.Attribute("x"),(int)node.Attribute("y"),(int)node.Attribute("z")))return null;
                byte[] before=ColdBytes(node.Element("beforeNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes),after=ColdBytes(node.Element("afterNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes);
                int beforeOffset=(int)node.Attribute("beforeOffset"),afterOffset=(int)node.Attribute("afterOffset");
                if(!RebirthProductiveEarnedNativeBody.TryRead(before,beforeOffset,out var prior,out _)||
                    !RebirthProductiveEarnedNativeBody.TryRead(after,afterOffset,out var projected,out _))return null;
                string purpose=(string)node.Attribute("purpose");if(purpose!="enqueue"&&purpose!="cancel"&&purpose!="complete")return null;
                var beforeBag=RebirthCamperInventorySnapshot.Restore(ColdBytes(node.Element("beforeBag"),4*1024*1024));
                var beforeBelt=RebirthCamperInventorySnapshot.Restore(ColdBytes(node.Element("beforeBelt"),4*1024*1024));
                var afterBag=RebirthCamperInventorySnapshot.Restore(ColdBytes(node.Element("afterBag"),4*1024*1024));
                var afterBelt=RebirthCamperInventorySnapshot.Restore(ColdBytes(node.Element("afterBelt"),4*1024*1024));
                var returns=RebirthCamperInventorySnapshot.Restore(ColdBytes(node.Element("returns"),4*1024*1024));
                string bagBits=(string)node.Attribute("bagLocks"),beltBits=(string)node.Attribute("beltLocks");
                if(beforeBag==null||beforeBelt==null||afterBag==null||afterBelt==null||returns==null||bagBits==null||beltBits==null||
                    bagBits.Length!=beforeBag.Length||beltBits.Length!=beforeBelt.Length||bagBits.Any(value=>value!='0'&&value!='1')||beltBits.Any(value=>value!='0'&&value!='1'))return null;
                var bagLocks=bagBits.Select(value=>value=='1').ToArray();var beltLocks=beltBits.Select(value=>value=='1').ToArray();int beltCount=(int)node.Attribute("beltCount");
                if(!RebirthRecyclerOwnerDelivery.Plan(beforeBag,beforeBelt,bagLocks,beltLocks,beltCount,returns,out var expectedBag,out var expectedBelt)||
                    !SameOwnerGrid(expectedBag,afterBag)||!SameOwnerGrid(expectedBelt,afterBelt))return null;
                var result=issued=new Original(station.Operation,purpose,owner.actor,station.Station,identity,beforeBag,beforeBelt,afterBag,afterBelt,returns,
                    before,after,beforeOffset,afterOffset,projected.Input,projected.Queue,projected.Output,projected.Tools,image,memory);memory=null;
                int cycle=(int?)node.Attribute("cycle")??-1;if(cycle<0||cycle>1||cycle==1&&purpose!="complete"){result.Dispose();return null;}
                result.Cycle=cycle==1;
                if(result.Cycle){result.Queue=prior.Queue;result.Queue[result.Queue.Length-1]=new RecipeQueueItem{StartingEntityId=-1};}
                if(!result.Current()||!result.BagLocks.SequenceEqual(bagLocks)||!result.BeltLocks.SequenceEqual(beltLocks)||result.BeltCount!=beltCount||
                    !owner.Current()||!station.Current()){result.Dispose();return null;}issued=null;return result;
                }
            }
            catch{return null;}
            finally{issued?.Dispose();memory?.Dispose();}
        }
        public void Dispose(){if(closed)return;closed=true;ClosedOwner?.Forget(this);ClosedOwner=null;BeforeBag=BeforeBelt=AfterBag=AfterBelt=Returns=Input=Output=Tools=null;BagLocks=BeltLocks=null;BeforeNative=AfterNative=Image=null;Queue=null;Slots=null;allocation?.Dispose();allocation=null;}
    }
    internal sealed class Slot
    {internal readonly int Area,Index;internal Slot(int area,int index){Area=area;Index=index;}}
    private static bool Same(ItemStack a,ItemStack b)=>RebirthStationGridIngredients.IsSameStackSnapshot(a,b);
    private static XElement Bytes(string name,byte[] bytes)=>new XElement(name,Convert.ToBase64String(bytes));
    private static XElement Grid(string name,ItemStack[] items)=>Bytes(name,RebirthCamperInventorySnapshot.Capture(items));
}
