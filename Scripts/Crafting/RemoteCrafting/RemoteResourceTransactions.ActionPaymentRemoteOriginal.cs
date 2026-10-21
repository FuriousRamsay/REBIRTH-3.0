using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

public static partial class RemoteResourceTransactions
{
    // Actual client click originals are a separate purpose. Neither a host
    // described inventory nor a receipt can manufacture this retained source.
    private sealed class ActionRemoteClick
    {
        internal Guid Operation;internal World World;internal EntityPlayerLocal Actor;internal TileEntityPowered Target;
        internal INetConnection Connection;internal Vector3i Position;internal BlockValue Block;
        internal string Account,Creation,Original,NativeWorld,ClientHeadStage;internal int Deaths,NativeEntityId,OriginalNativeEntityId;
        internal ActionClientNativeRead RestartOwnerSource;
        internal ItemStackGrid Bag,Belt;internal ItemStack[] BagBefore,BeltBefore,BagReferences,BeltReferences;
        internal ItemStack[] Price;
        internal byte[] NativeBefore;internal bool BeforeObserved,InitialSaved;
        internal RebirthRemoteResourceFragments.Body RequestBody;
        internal ActionRemoteDebitPlan Plan;internal bool DebitEntered,Debited,RefundEntered,Refunded,DebitRefused,TerminalMetadataOnly;
        internal ActionClientCheckpoint InitialCheckpoint,DebitCheckpoint,RefundCheckpoint;
        internal ActionClientCheckpoint DebitOutcome,RefundOutcome;
        internal ItemStack[] AppliedBagReferences,AppliedBeltReferences;
        internal IDisposable Memory,GrantMemory;
        internal bool NativeLifeEnded;
        internal bool OwnerMetadataCurrent()=>ThreadManager.IsMainThread()&&ReferenceEquals(World,GameManager.Instance?.World)&&World?.IsRemote()==true&&
            ReferenceEquals(Actor,World.GetPrimaryPlayer())&&!Actor.IsDead()&&Actor.Died>=Deaths&&RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&
            RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&owner.CanonicalId==Account&&
            ReferenceEquals(Connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0])&&World.worldState.Guid==NativeWorld;
        internal bool Current()=>!NativeLifeEnded&&OwnerMetadataCurrent()&&Actor.entityId==NativeEntityId&&Actor.Died==Deaths&&(RestartOwnerSource==null||ActionClientLoadedCurrent(RestartOwnerSource))&&
            ReferenceEquals(Target,World.GetTileEntity(Position))&&World.GetBlock(Position).Equals(Block)&&
            ReferenceEquals(Bag,Actor.bag.ItemGrid)&&ReferenceEquals(Belt,Actor.inventory.ItemGrid);
        internal bool CourierCurrent()=>Current()||RestartOwnerSource!=null&&ActionClientLoadedCurrent(RestartOwnerSource)&&OwnerMetadataCurrent()&&(Debited||Refunded||DebitRefused||TerminalMetadataOnly);
    }
    private static ActionRemoteClick actionRemoteClick;
    private static void RetireReplacedActionRemoteObservation(bool nativeJoin=false)
    {
        if(!ThreadManager.IsMainThread())return;
        var pending=actionRemoteClick;if(pending==null)return;
        if(!nativeJoin&&ReferenceEquals(pending.World,GameManager.Instance?.World)&&pending.World?.worldState.Guid==pending.NativeWorld&&ReferenceEquals(pending.Connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0])&&
            ReferenceEquals(pending.Actor,pending.World?.GetPrimaryPlayer()))return;
        lock(actionOwnerGate)
        {
            if(!ReferenceEquals(actionRemoteClick,pending))return;
            actionRequestWire.Clear();actionRequestNext=0;
            actionRemoteClick=null;
            ReleaseActionClientCheckpoint(pending.InitialCheckpoint);ReleaseActionClientCheckpoint(pending.DebitCheckpoint);ReleaseActionClientCheckpoint(pending.RefundCheckpoint);
            ReleaseActionClientCheckpoint(pending.DebitOutcome);ReleaseActionClientCheckpoint(pending.RefundOutcome);
            pending.DebitOutcome=null;pending.RefundOutcome=null;
            pending.InitialCheckpoint=null;pending.DebitCheckpoint=null;pending.RefundCheckpoint=null;
            // Actual lifecycle replacement retires only observation allocation.
            // Persisted Held/operation stamps deliberately remain unresolved;
            // no actor setter, terminal receipt, or financial outcome is issued.
            pending.NativeBefore=null;pending.RequestBody=null;pending.Plan=null;pending.BagBefore=null;pending.BeltBefore=null;pending.Price=null;
            pending.AppliedBagReferences=null;pending.AppliedBeltReferences=null;
            pending.BagReferences=null;pending.BeltReferences=null;pending.Bag=null;pending.Belt=null;
            pending.Actor=null;pending.Target=null;pending.World=null;pending.Connection=null;pending.Original=null;
            pending.GrantMemory?.Dispose();pending.GrantMemory=null;pending.Memory.Dispose();pending.Memory=null;
        }
    }
    private static void RetireActionRemoteObservationForNativeJoin()
    {
        RetireReplacedActionRemoteObservation(true);
        foreach(var receipt in actionRemoteTerminalReceipts.Values.ToArray())
        {
            actionRequestWire.Cancel(ActionRemoteRequestKey(receipt.Operation));actionRemoteTerminalReceipts.Remove(receipt.Operation);
            receipt.Request=null;receipt.Original=null;receipt.Actor=null;receipt.World=null;receipt.Connection=null;receipt.Memory?.Dispose();receipt.Memory=null;
        }
    }
    private static void ObserveRemoteActionFromPlayer(PlayerDataFile data,EntityPlayer actor,bool ran)
    {
        var pending=actionRemoteClick;
        if(!ran||pending==null||!ReferenceEquals(actor,pending.Actor)||!pending.Current()||data==null||data.id!=actor.entityId||data.bDead||data.deaths!=pending.Deaths||pending.BeforeObserved)return;
        if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,ActionOwnerMarker(pending.Operation),out float phase,out bool present)||!present||phase!=1f||
            data.bagData==null||data.inventoryData==null||data.bagData.Length+data.inventoryData.Length>4L*1024*1024)return;
        if(!TryReserveActionOwnerProjection(data,out var scratch))return;
        using(scratch)
        {
            if(!SameActionItems(RebirthGearPlayerDataInventory.ReadSlots(data,true),pending.BagBefore)||!SameActionItems(RebirthGearPlayerDataInventory.ReadSlots(data,false),pending.BeltBefore)||
                !SameActionItems(pending.Bag.items,pending.BagBefore)||!SameActionItems(pending.Belt.items,pending.BeltBefore))return;
            // Actual native FromPlayer image, never a reconstructed host proposal.
            // Receipt of this callback grants no financial setter permission.
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out var nativeTransfer))return;
            using(nativeTransfer)
            {
                pending.NativeBefore=ActionOwnerImage(data);
                if(!NarrowActionRemoteClickMemory(pending)){pending.NativeBefore=null;return;}
                pending.BeforeObserved=true;
            }
        }
    }
    private static bool ActionRemoteBlocksNative(object target)
    {
        if(ActionClientLoadedInventoryHeld(target))return true;
        var pending=actionRemoteClick;
        if(pending==null)return false;
        if(ThreadManager.IsMainThread()&&(pending.Actor.IsDead()||pending.Actor.Died!=pending.Deaths))pending.NativeLifeEnded=true;
        return !pending.NativeLifeEnded&&(ReferenceEquals(target,pending.Bag)||ReferenceEquals(target,pending.Belt));
    }
    internal static bool CaptureActionRemoteClick(EntityPlayerLocal actor,Vector3i position,out Guid operation,out string original)
    {
        operation=Guid.Empty;original=null;
        if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=true||!ReferenceEquals(actor,actor.world.GetPrimaryPlayer())||actor.IsDead()||
            actor.world.GetTileEntity(position) is not TileEntityPowered target||!target.IsPlayerPlaced||
            (actor.position-position.ToVector3()).sqrMagnitude>Constants.cDigAndBuildDistance*Constants.cDigAndBuildDistance||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))return false;
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0];
        string creation=RebirthCamperCharacterAccess.CurrentCreation(actor);
        if(connection==null||string.IsNullOrEmpty(creation)||creation.Length>1024)return false;
        // Repeated actual clicks can only resume this exact retained original.
        if(actionRemoteClick!=null)
        {
            if(!actionRemoteClick.Current()||!ReferenceEquals(actionRemoteClick.Actor,actor)||actionRemoteClick.Position!=position)return false;
            operation=actionRemoteClick.Operation;original=actionRemoteClick.Original;return true;
        }
        if(actionRemoteTerminalReceipts.Count>=8||actor.Buffs.GetCustomVar("rbActionPaymentHeld")!=0f||RebirthBackpackLibraryReservation.BlocksResourceUse(actor)||ActionOtherOwnerHold(actor)||RebirthDogLifecycleService.HasHireInventoryHold(actor)||actor.DragAndDropItem==null||!actor.DragAndDropItem.IsEmpty()||actor.PlayerUI?.xui?.DragAndDropWindow?.IsEmpty()!=true||
            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(128L*1024*1024,out var memory))return false;
        var pending=new ActionRemoteClick{Operation=Guid.NewGuid(),World=actor.world,Actor=actor,Target=target,Connection=connection,Position=position,
            Block=actor.world.GetBlock(position),Account=owner.CanonicalId,Creation=creation,Deaths=actor.Died,NativeEntityId=actor.entityId,OriginalNativeEntityId=actor.entityId,NativeWorld=actor.world.worldState.Guid,Memory=memory,Bag=actor.bag.ItemGrid,Belt=actor.inventory.ItemGrid};
        try
        {
            long remaining=2L*1024*1024;
            if(!RebirthInfrastructureWorkService.TryDescribeActionPrice(pending.World,target,position,out pending.Price))return false;
            byte[] bagFrame=CaptureActionFrame(pending.Bag.items,ref remaining),beltFrame=CaptureActionFrame(pending.Belt.items,ref remaining);
            long graph=checked(MeasureActionRetainedFrame(bagFrame,pending.Bag.items)+MeasureActionRetainedFrame(beltFrame,pending.Belt.items));
            long preparing=checked(graph+16L*(bagFrame.Length+beltFrame.Length)+12L*1024*1024);
            if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(pending.Memory,preparing))return false;
            pending.BagBefore=RebirthCamperInventorySnapshot.Restore(bagFrame);
            pending.BeltBefore=RebirthCamperInventorySnapshot.Restore(beltFrame);
            bagFrame=null;beltFrame=null;
            if(pending.BagBefore.Concat(pending.BeltBefore).Any(stack=>stack.count>ushort.MaxValue))return false;
            pending.BagReferences=pending.Bag.items.ToArray();pending.BeltReferences=pending.Belt.items.ToArray();
            pending.Original=new XElement("electricalOwnerClick",new XAttribute("version",1),new XAttribute("operation",pending.Operation.ToString("N")),
                new XAttribute("account",pending.Account),new XAttribute("creation",pending.Creation),new XAttribute("deaths",pending.Deaths),new XAttribute("nativeId",pending.NativeEntityId),new XAttribute("world",pending.NativeWorld),
                new XAttribute("x",position.x),new XAttribute("y",position.y),new XAttribute("z",position.z),new XAttribute("block",pending.Block.type),
                new XElement("price",ActionFrame(pending.Price)),new XElement("bag",ActionFrame(pending.BagBefore)),new XElement("belt",ActionFrame(pending.BeltBefore))).ToString(SaveOptions.DisableFormatting);
            if(new UTF8Encoding(false,true).GetByteCount(pending.Original)>8*1024*1024)return false;
            if(!pending.Current()||!SameActionItems(pending.Bag.items,pending.BagBefore)||!SameActionItems(pending.Belt.items,pending.BeltBefore)||!NarrowActionRemoteClickMemory(pending))return false;
            // No mutation permit is issued here. Native saved Before capture and
            // host acceptance must precede a separately journaled first grant.
            lock(actionOwnerGate)actionRemoteClick=pending;
            actor.Buffs.SetCustomVar("rbActionPaymentHeld",1f,true);actor.Buffs.SetCustomVar(ActionOwnerMarker(pending.Operation),1f,true);
            operation=pending.Operation;original=pending.Original;return true;
        }
        finally{if(!ReferenceEquals(actionRemoteClick,pending))memory.Dispose();}
    }
}
