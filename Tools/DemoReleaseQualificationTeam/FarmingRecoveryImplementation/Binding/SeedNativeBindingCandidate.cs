// TOOLS ONLY. No installer; Owner null leaves every native action unchanged.
// Proposed adapter contract; Third owns authority/session/ACK/terminal custody lifecycle.
using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;

public interface ISeedNativeBindingOwner
{
    bool TryEnroll(SeedNativeCapture capture, out SeedDebitReceipt receipt);
    void PlacementReturned(SeedNativeCapture capture, SeedDebitReceipt receipt, bool returned, Exception error);
    bool TryClaimQueuedDebit(ItemInventoryData data, int nativeQueuedIndex, out SeedNativeCapture capture, out SeedDebitReceipt receipt);
    bool MatchedEnvelopeAndContextCurrent(SeedNativeCapture capture, SeedDebitReceipt receipt);
    SeedPlacementOutcome ProvenOutcome(SeedNativeCapture capture, SeedDebitReceipt receipt);
    void KeepHeld(SeedNativeDeferredDebit continuation, SeedDebitReceipt receipt, string reason);
    void RecordOriginalDebit(SeedNativeCapture capture, SeedDebitEvaluation evaluation);
}

public interface ISeedNativePreparedCaptureOwner : ISeedNativeBindingOwner
{bool TryAttachPrepared(ItemInventoryData data,BlockPlacement.Result placement,BlockValue actualTarget,out SeedNativeCapture capture,out SeedDebitReceipt receipt);}
public interface ISeedNativeRejectionCancellationOwner : ISeedNativeBindingOwner
{ bool CanCancelOriginalDebit(SeedNativeCapture capture,SeedDebitReceipt receipt); }

public sealed class SeedNativeCapture
{
    public readonly World World;
    public readonly EntityPlayerLocal Player;
    public readonly ItemInventoryData Data;
    public readonly object Source = new object();
    public readonly int OriginalSlot, Count;
    readonly int thread=System.Threading.Thread.CurrentThread.ManagedThreadId;
    public readonly Vector3i Position;
    public readonly BlockValue Target, Old;
    private readonly byte[] seed;
    public byte[] Seed => (byte[])seed.Clone();
    internal SeedNativeCapture(World world, EntityPlayerLocal player, ItemInventoryData data, int slot,
        int count, Vector3i position, BlockValue target, BlockValue old, byte[] identity)
    { World=world;Player=player;Data=data;OriginalSlot=slot;Count=count;Position=position;Target=target;Old=old;seed=(byte[])identity.Clone(); }
    internal bool StableContext() => thread==System.Threading.Thread.CurrentThread.ManagedThreadId && GameManager.Instance != null && !GameManager.Instance.IsEditMode() &&
        ReferenceEquals(GameManager.Instance.World,World) && World.IsRemote() &&
        ReferenceEquals(World.GetPrimaryPlayer(),Player) && ReferenceEquals(Data.world,World) &&
        ReferenceEquals(Data.holdingEntity,Player) &&
        !Player.IsDead() && Player.IsSpawned();
    internal bool ContextCurrent() => StableContext() && ReferenceEquals(Player.inventory.holdingItemData,Data);
    internal SeedDebitObservation Observe(SeedDebitReceipt receipt, bool reentrant=false, bool afterNative=false)
    {
        if(!(afterNative?StableContext():ContextCurrent())) return null;
        ItemStack stack=Player.inventory.GetItem(OriginalSlot);
        if(stack==null) return null;
        byte[] identity=null;
        if(stack.count>0 && !SeedPlacementIntentCodecReview.TrySeedIdentity(stack.itemValue,out identity))return null;
        return new SeedDebitObservation(receipt.Epoch,receipt.Nonce,OriginalSlot,stack.count,identity,
            World,Player,Data,Source,reentrant);
    }
}

public static class SeedNativeBindingCandidate
{
    public static ISeedNativeBindingOwner Owner; // CLOSED. This candidate installs no patches.
    public sealed class PlacementState
    { internal ISeedNativeBindingOwner Owner; internal SeedNativeCapture Capture; internal SeedDebitReceipt Receipt; internal bool Reported; }
    internal static PlacementState Begin(ItemInventoryData data, BlockPlacement.Result result, BlockValue blockValue)
    {
        ISeedNativeBindingOwner owner=Owner;
        var world=GameManager.Instance?.World;
        var player=data?.holdingEntity as EntityPlayerLocal;
        if(owner==null || world==null || !world.IsRemote() || GameManager.Instance.IsEditMode() || player==null ||
            !ReferenceEquals(world.GetPrimaryPlayer(),player) || !ReferenceEquals(data.world,world) ||
            !ReferenceEquals(player.inventory.holdingItemData,data) || !data.IsBlock || result.placement!=BlockPlacement.EnumPlacement.Voxel)return null;
        BlockValue actual=data.itemValue.TextureFullArray.IsDefault?result.blockValue:blockValue;
                if(owner is ISeedNativePreparedCaptureOwner prepared)
        {
            SeedNativeCapture existing;SeedDebitReceipt retained;
            if(!prepared.TryAttachPrepared(data,result,actual,out existing,out retained))throw new InvalidOperationException("Prepared original producer binding is absent.");
            if(existing==null || retained==null || !existing.ContextCurrent() || !ReferenceEquals(existing.Data,data) ||
                existing.Target.rawData!=actual.rawData || existing.Target.damage!=actual.damage ||
                !ReferenceEquals(retained.SourceIdentity,existing.Source) || !retained.MatchesOriginal(existing.Observe(retained)))
                throw new InvalidOperationException("Prepared original producer capture changed before native call.");
            return new PlacementState{Owner=owner,Capture=existing,Receipt=retained};
        }
var plant=actual.Block as BlockPlantGrowingRebirth;
        if(plant==null || !plant.IsSeedStage(actual))return null;
        int index=player.inventory.holdingItemIdx;
        ItemStack stack=player.inventory.GetItem(index);
        byte[] identity;
        if(stack==null || stack.count<=0 || !SeedPlacementIntentCodecReview.TrySeedIdentity(stack.itemValue,out identity))return null;
        byte[] heldIdentity;
        if(!SeedPlacementIntentCodecReview.TrySeedIdentity(data.itemValue,out heldIdentity) || !Equal(identity,heldIdentity))return null;
        var capture=new SeedNativeCapture(world,player,data,index,stack.count,result.blockPos,actual,world.GetBlock(result.blockPos),identity);
        SeedDebitReceipt receipt;
        if(!owner.TryEnroll(capture,out receipt))return null;
        if(receipt==null || receipt.OriginalSlot!=index || receipt.OriginalCount!=stack.count ||
            !ReferenceEquals(receipt.WorldIdentity,world) || !ReferenceEquals(receipt.PlayerIdentity,player) ||
            !ReferenceEquals(receipt.ActionIdentity,data) || !ReferenceEquals(receipt.SourceIdentity,capture.Source) ||
            !receipt.MatchesOriginal(capture.Observe(receipt)))
            throw new InvalidOperationException("Owner returned an invalid enrolled receipt; placement must fail closed.");
        return new PlacementState {Owner=owner,Capture=capture,Receipt=receipt};
    }
    static bool Equal(byte[] a,byte[] b)
    {if(a==null || b==null || a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
    internal static void Report(PlacementState state,bool returned,Exception error)
    {
        if(state==null || state.Reported)return;
        state.Reported=true;state.Owner.PlacementReturned(state.Capture,state.Receipt,returned,error);
    }
}

[HarmonyPatch(typeof(BlockToolSelection),nameof(BlockToolSelection.PlaceBlock))]
public static class SeedNativePlaceBlockBindingPatch
{
    // Exact native helper seam, before quest/event/snapping callbacks. No placement executor replacement.
    static void Prefix(ItemInventoryData _data, BlockPlacement.Result result, BlockValue blockValue,
        out SeedNativeBindingCandidate.PlacementState __state)
    { __state=SeedNativeBindingCandidate.Begin(_data,result,blockValue); }
    static void Postfix(bool __result,SeedNativeBindingCandidate.PlacementState __state)
    { SeedNativeBindingCandidate.Report(__state,__result,null); }
    static Exception Finalizer(Exception __exception,SeedNativeBindingCandidate.PlacementState __state)
    { if(__exception!=null)SeedNativeBindingCandidate.Report(__state,false,__exception);return __exception; }
}

[HarmonyPatch(typeof(BlockToolSelection),nameof(BlockToolSelection.decInventoryLater))]
public static class SeedNativeDebitBindingPatch
{
    internal sealed class State {internal ISeedNativeBindingOwner Owner;internal SeedNativeCapture Capture;internal SeedDebitReceipt Receipt;}
    internal static void Prefix(ItemInventoryData data,ref int index,out State __state)
    {
        __state=null;var owner=SeedNativeBindingCandidate.Owner;if(owner==null)return;
        SeedNativeCapture capture;SeedDebitReceipt receipt;
        // Adapter must claim only the exact one-shot enrolled action; unrelated action falls through untouched.
        if(!owner.TryClaimQueuedDebit(data,index,out capture,out receipt))return;
        if(capture==null || receipt==null || !ReferenceEquals(capture.Data,data) || receipt.OriginalSlot!=capture.OriginalSlot)
            throw new InvalidOperationException("Invalid enrolled debit claim.");
        __state=new State{Owner=owner,Capture=capture,Receipt=receipt};
        index=capture.OriginalSlot; // Queue-time slot may have changed in original placement callbacks.
    }
    internal static void Postfix(State __state,ref IEnumerator __result)
    {
        if(__state!=null)__result=Wrap(__result,__state);
    }
    static IEnumerator Wrap(IEnumerator native,State state)
    {
        var continuation=new SeedNativeDeferredDebit(native,state.Owner,state.Capture,state.Receipt);
        if(!continuation.AdvanceInitialWait())yield break;
        yield return native.Current; // Retain original .1 second wait exactly once.
        continuation.AttemptOriginalOnce(); // Held continuations are retained by owner; no coroutine rerun/refund.
    }
}

public sealed class SeedNativeDeferredDebit
{
    readonly IEnumerator native;
    readonly ISeedNativeBindingOwner owner;
    readonly SeedNativeCapture capture;
    SeedDebitReceipt receipt;
    bool initial,ready,executed,cancelled,cancelling;
    public SeedDebitReceipt CurrentReceipt => receipt;
    public bool NativeDebitSettled => executed || cancelled;
    public bool CancelBeforeOriginalOnce()
    {
        if(executed || cancelled || cancelling || SeedNativeSetterWitness.Active!=null)return false;
        var authority=owner as ISeedNativeRejectionCancellationOwner;if(authority==null)return false;
        cancelling=true;bool allowed=false;try {allowed=authority.CanCancelOriginalDebit(capture,receipt);}finally {cancelling=false;}
        if(!allowed || executed || cancelled || SeedNativeSetterWitness.Active!=null)return false;
        cancelled=true; // Irrevocable BEFORE any owner callback. No native setter can run afterward.
        owner.KeepHeld(this,receipt,"Authoritative rejected-before-execution cancelled original delayed debit; no refund.");
        return true;
    }
    internal SeedNativeDeferredDebit(IEnumerator native,ISeedNativeBindingOwner owner,SeedNativeCapture capture,SeedDebitReceipt receipt)
    {this.native=native;this.owner=owner;this.capture=capture;this.receipt=receipt;}
    internal bool AdvanceInitialWait()
    {
        if(initial || cancelled || executed)return false;initial=true;
        try {if(native!=null && native.MoveNext() && native.Current is WaitForSeconds){ready=true;return true;}}
        catch(Exception){owner.KeepHeld(this,receipt,"Original coroutine failed before wait; custody unresolved.");return false;}
        owner.KeepHeld(this,receipt,"Original coroutine missing native wait; custody unresolved.");return false;
    }
    public bool AttemptOriginalOnce()
    {
        if(cancelled || cancelling || executed)return false;
        if(CancelBeforeOriginalOnce())return false;
        if(!ready)return false;
        var before=capture.Observe(receipt);
        if(!owner.MatchedEnvelopeAndContextCurrent(capture,receipt) || !receipt.MatchesOriginal(before))
        {owner.KeepHeld(this,receipt,"Unknown envelope/context or changed original seed slot; no debit/refund.");return false;}
        if(SeedNativeSetterWitness.Active!=null)
        {owner.KeepHeld(this,receipt,"Reentrant debit; retain custody.");return false;}
        executed=true; // Consume BEFORE original callbacks. Never invoke original again after exception.
        var witness=new SeedNativeSetterWitness(capture,receipt);
        Exception failure=null;bool extraYield=false;
        SeedNativeSetterWitness.Active=witness;
        try {extraYield=native.MoveNext();}catch(Exception error){failure=error;}
        finally {SeedNativeSetterWitness.Active=null;}
        var after=capture.Observe(receipt,witness.Reentrant || extraYield,true);
        var evaluation=receipt.ObserveOriginalDebit(before,after,witness.ExactlyOne,
            owner.ProvenOutcome(capture,receipt));
        receipt=evaluation.Receipt;
        owner.RecordOriginalDebit(capture,evaluation);
        if(failure!=null || extraYield || !witness.ExactlyOne || evaluation.Decision==SeedDebitDecision.PreserveCustody)
            owner.KeepHeld(this,receipt,"Original native debit result uncertain; never retry/refund blindly.");
        return evaluation.Decision!=SeedDebitDecision.PreserveCustody;
    }
}

sealed class SeedNativeSetterWitness
{
    [ThreadStatic] internal static SeedNativeSetterWitness Active;
    internal readonly SeedNativeCapture Capture;internal readonly SeedDebitReceipt Receipt;
    internal int Calls,Completed;internal bool Reentrant;
    internal bool ExactlyOne => Calls==1 && Completed==1 && !Reentrant;
    internal SeedNativeSetterWitness(SeedNativeCapture capture,SeedDebitReceipt receipt){Capture=capture;Receipt=receipt;}
}

[HarmonyPatch(typeof(Inventory),nameof(Inventory.SetItem),new Type[]{typeof(int),typeof(ItemStack)})]
public static class SeedNativeSetterWitnessPatch
{
    internal static void Prefix(Inventory __instance,int _idx,ItemStack _itemStack,out object __state)
    {
        __state=null;var w=SeedNativeSetterWitness.Active;if(w==null)return;
        w.Calls++;
        if(w.Calls!=1 || !ReferenceEquals(__instance,w.Capture.Player.inventory) || _idx!=w.Receipt.OriginalSlot ||
            _itemStack==null || _itemStack.count!=w.Receipt.OriginalCount-1){w.Reentrant=true;return;}
        __state=w;
    }
    internal static void Postfix(object __state)
    {var w=__state as SeedNativeSetterWitness;if(w!=null)w.Completed++;}
}





