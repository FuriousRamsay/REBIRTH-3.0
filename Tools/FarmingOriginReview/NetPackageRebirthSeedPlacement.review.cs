// TOOLS REVIEW ONLY. Registration, bounded seed codec and admission implementations are NOT supplied.
// Native ABI: installed Assembly-CSharp FCEEC273...234C705 / MVID 1a9a4203-3d95-4c90-b094-8926dec1ee9c.
using System;
using System.IO;

public sealed class NetPackageRebirthSeedPlacementReview : NetPackage
{
    // Proposed resource ceilings, NOT native packet maxima or decoded-graph bounds.
    private const int MaxNativeBytes = 65535, MaxIntentBytes = 16384;
    private byte[] nativeBytes, intentBytes;
    private ulong sessionEpoch, nonce;
    private bool validFrame;
    public static ISeedPlacementReviewOwner Owner; // Original owner's integration seam; default closed.

    public NetPackageRebirthSeedPlacementReview Setup(NetPackageSetBlock original,
        ulong epoch, ulong oneUseNonce, byte[] qualifiedIntent)
    {
        validFrame = false;
        using (MemoryStream blob = StreamUtils.ToBlob(original.write))
            nativeBytes = blob.ToArray(); // Includes original native ushort PackageId.
        if (nativeBytes.Length < 2 || nativeBytes.Length > MaxNativeBytes ||
            qualifiedIntent == null || qualifiedIntent.Length == 0 || qualifiedIntent.Length > MaxIntentBytes)
            throw new InvalidDataException("Seed placement review admission frame exceeds proposed bounds.");
        intentBytes = (byte[])qualifiedIntent.Clone();
        sessionEpoch = epoch; nonce = oneUseNonce;
        validFrame = epoch != 0 && nonce != 0;
        return this;
    }

    internal SeedPlacementConsumedEnvelopeReview CaptureConsumedEnvelope(SeedPlacementOriginalCommandReview original)
    {
        if(!validFrame)throw new InvalidDataException("Unprepared consumed envelope.");
        return SeedPlacementConsumedEnvelopeReview.Capture(original,nativeBytes,intentBytes);
    }
    public override void write(PooledBinaryWriter writer)
    {
        if (!validFrame) throw new InvalidDataException("Unprepared seed placement frame.");
        base.write(writer);
        writer.Write((byte)1); writer.Write(sessionEpoch); writer.Write(nonce);
        writer.Write(intentBytes.Length); writer.Write(intentBytes);
        writer.Write(nativeBytes.Length); writer.Write(nativeBytes);
    }

    public override void read(PooledBinaryReader reader)
    {
        validFrame = false; nativeBytes = intentBytes = null;
        if (reader.ReadByte() != 1) throw new InvalidDataException("Unknown seed placement frame.");
        sessionEpoch = reader.ReadUInt64(); nonce = reader.ReadUInt64();
        intentBytes = ReadBounded(reader, MaxIntentBytes);
        nativeBytes = ReadBounded(reader, MaxNativeBytes);
        validFrame = sessionEpoch != 0 && nonce != 0 && nativeBytes.Length >= 2;
    }

    private static byte[] ReadBounded(PooledBinaryReader reader, int maximum)
    {
        int length = reader.ReadInt32();
        if (length <= 0 || length > maximum) throw new InvalidDataException("Invalid admission frame length.");
        byte[] bytes = reader.ReadBytes(length);
        if (bytes.Length != length) throw new EndOfStreamException();
        return bytes;
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        ISeedPlacementReviewOwner owner = Owner;
        if (!validFrame || Sender == null || world == null || callbacks == null || owner == null ||
            !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;
        NetPackageSetBlock original = null;
        ISeedPlacementReviewAdmission admission = null;
        SeedPlacementIntentCodecReview.Intent? decodedIntent = null;
        try
        {
            // MUST bound native nested allocations BEFORE decoding; bytes alone do not provide this.
            if (!SeedPlacementNativeDecoderReview.TryDecode(nativeBytes, Sender, out original) || original == null) return;
            original.Sender = Sender; // Preserve native validation/rebroadcast sender context.
            if (!ValidUserIdForSender(original.persistentPlayerId) ||
                !ValidEntityIdForSender(original.localPlayerThatChanged) ||
                original.blockChanges == null || original.blockChanges.Count == 0) return;
            foreach (BlockChangeInfo change in original.blockChanges)
                if (!ValidEntityIdForSender(change.changedByEntityId)) return;
            SeedPlacementIntentCodecReview.Intent intent;
            if (!SeedPlacementIntentCodecReview.TryRead(intentBytes, out intent)) return;
            decodedIntent = intent;
            // Owner MUST reserve authenticated session nonce, then validate full snapshot/native seed policy before execution.
            // Authenticated specialized COMMAND intent, not a generic block packet or held-seed inference.
            // Reserve nonce BEFORE execution; rejection/reentrancy also consumes it until session expiry.
            if (!owner.TryAdmit(Sender, world, callbacks, sessionEpoch, nonce, intent,
                    original, out admission) || admission == null) return;
            original.ProcessPackage(world, callbacks); // Exactly ONE native executor; no extra debit.
            admission.CompleteObservedCommit(); // No origin publication without matching native callback/current TE.
        }
        finally
        {
            try
            {
                admission?.Dispose();
            }
            finally
            {
            // Native server rebroadcast may own queues. Original owner must account for queue ownership;
            // NEVER blindly FreePackage here. Rejection includes prediction reconciliation, not a second executor.
                owner.ReleaseOrRetainNativeAndReconcile(Sender, world, sessionEpoch, nonce, decodedIntent, original, admission);
            }
        }
    }
}

// These are explicitly PROPOSED interfaces, not claims of installed/root APIs.
public interface ISeedPlacementReviewAdmission : IDisposable
{
    void CompleteObservedCommit();
}
public interface ISeedPlacementReviewOwner
{
    bool TryAdmit(ClientInfo sender, World world, GameManager callbacks, ulong epoch, ulong nonce,
        SeedPlacementIntentCodecReview.Intent intent, NetPackageSetBlock original, out ISeedPlacementReviewAdmission admission);
    void ReleaseOrRetainNativeAndReconcile(ClientInfo sender, World world, ulong epoch, ulong nonce,
        SeedPlacementIntentCodecReview.Intent? intent, NetPackageSetBlock original, ISeedPlacementReviewAdmission admission);
}



// Client synchronous native voxel action scope. Root supplies registered session + authored seed policy.
public sealed class SeedPlacementClientScopeReview : IDisposable
{
    [ThreadStatic] private static SeedPlacementClientScopeReview current;
    public static ISeedPlacementClientReviewOwner ClientOwner;
    private readonly World world;
    private readonly EntityPlayerLocal player;
    private readonly ItemInventoryData data;
    private readonly int slot, thread;
    private readonly Vector3i position;
    private readonly BlockValue target, old;
    private readonly byte[] identity, intent;
    private readonly ulong epoch, nonce;
    private bool consumed, disposed, invalid, invocationStarted, bindingFailed, nativeSendReturned;
    private NetPackageRebirthSeedPlacementReview boundEnvelope;
    private SeedPlacementConsumedEnvelopeReview boundCapture;
    private readonly SeedPlacementOriginalCommandReview original;
    private readonly ISeedPlacementRetainedCommandReview retained;
    private SeedPlacementClientScopeReview(World w,EntityPlayerLocal p,ItemInventoryData d,int s,
        Vector3i pos,BlockValue actualTarget,BlockValue expectedOld,byte[] seed,byte[] command,ulong e,ulong n,SeedPlacementOriginalCommandReview original,ISeedPlacementRetainedCommandReview retained)
    {world=w;player=p;data=d;slot=s;position=pos;target=actualTarget;old=expectedOld;identity=seed;intent=command;epoch=e;nonce=n;thread=System.Threading.Thread.CurrentThread.ManagedThreadId;this.original=original;this.retained=retained;}
    public static SeedPlacementClientScopeReview Begin(ItemInventoryData data,BlockPlacement.Result placement,BlockValue actualTarget)
    {
        ISeedPlacementClientReviewOwner owner=ClientOwner;
        var gm=GameManager.Instance;var player=data==null?null:data.holdingEntity as EntityPlayerLocal;
        World world=gm==null?null:gm.World;
        if(owner==null||gm==null||gm.IsEditMode()||world==null||!world.IsRemote()||player==null||
            !object.ReferenceEquals(world.GetPrimaryPlayer(),player)||!object.ReferenceEquals(data.world,world)||
            !object.ReferenceEquals(player.inventory.holdingItemData,data)||!data.IsBlock||
            player.inventory.holdingItemStack.count<=0||data.itemValue.ToBlockValue().type!=actualTarget.type||
            !owner.IsAuthoredHumanSeedCommand(data,placement,actualTarget))return null;
        if(current!=null){current.invalid=true;return null;}
        byte[] seed;if(!SeedPlacementIntentCodecReview.TrySeedIdentity(data.itemValue,out seed))return null;
        int originalCount=player.inventory.holdingItemStack.count;
        int slot=player.inventory.SelectedSlot;Vector3i pos=placement.blockPos;BlockValue old=world.GetBlock(pos);
        ulong epoch,nonce;
        if(!owner.TryGetBoundSessionCommand(world,player,out epoch,out nonce)||epoch==0||nonce==0)return null;
        // Capture old block BEFORE native client ChangeBlocks prediction. No ItemValue.Clone/Write mutation.
        byte[] command=SeedPlacementIntentCodecReview.Write(slot,originalCount,pos,actualTarget,old,data.itemValue);
        SeedPlacementIntentCodecReview.Intent parsed;
        if(!SeedPlacementIntentCodecReview.TryRead(command,out parsed))return null;
        var original=SeedPlacementOriginalCommandReview.Capture(epoch,nonce,player.entityId,originalCount,parsed);
        ISeedPlacementRetainedCommandReview retained;
        if(!owner.TryRetainOriginalCommand(world,player,original,out retained)||retained==null)return null;
        current=new SeedPlacementClientScopeReview(world,player,data,slot,pos,actualTarget,old,seed,command,epoch,nonce,original,retained);
        return current;
    }
    public bool BeforeNativeInvocation()
    {
        if(disposed||invalid||invocationStarted||!object.ReferenceEquals(current,this)||
            thread!=System.Threading.Thread.CurrentThread.ManagedThreadId){invalid=true;return false;}
        invocationStarted=true; // Conservative before the original native call; NOT a successful prediction receipt.
        retained.MarkNativeInvocationStarted();
        return true;
    }
    public static bool TryConsumeMatchingSend(NetPackageSetBlock package,out NetPackageRebirthSeedPlacementReview envelope)
    {
        envelope=null;var scope=current;
        if(scope!=null&&scope.bindingFailed)throw new InvalidDataException("Consumed binding failed; native fallback is prohibited.");
        if(scope==null||scope.disposed||scope.invalid||!scope.invocationStarted||package==null||package.blockChanges==null||package.blockChanges.Count!=1)return false;
        var gm=GameManager.Instance;var change=package.blockChanges[0];Vector3i pos;
        if(gm==null||!object.ReferenceEquals(gm.World,scope.world)||!scope.world.IsRemote()||gm.IsEditMode()||
            scope.thread!=System.Threading.Thread.CurrentThread.ManagedThreadId||
            !object.ReferenceEquals(scope.world.GetPrimaryPlayer(),scope.player)||
            !object.ReferenceEquals(scope.player.inventory.holdingItemData,scope.data)||
            scope.player.inventory.SelectedSlot!=scope.slot||scope.player.inventory.holdingItemStack.count<=0||
            package.localPlayerThatChanged!=scope.player.entityId||change.changedByEntityId!=scope.player.entityId||
            !change.blockValueRef.TryGetBlockPos(out pos)||pos.x!=scope.position.x||pos.y!=scope.position.y||pos.z!=scope.position.z||
            !change.bChangeBlockValue||change.bChangeDamage||change.blockValue.rawData!=scope.target.rawData||change.blockValue.damage!=scope.target.damage)return false;
        SeedPlacementIntentCodecReview.Intent expected;
        if(!SeedPlacementIntentCodecReview.TryRead(scope.intent,out expected)||!SeedPlacementIntentCodecReview.MatchesChange(expected,change))return false;
        var observed=scope.world.GetBlock(pos);if(observed.rawData!=scope.target.rawData||observed.damage!=scope.target.damage)return false;
        byte[] held;if(!SeedPlacementIntentCodecReview.TrySeedIdentity(scope.player.inventory.holdingItemItemValue,out held)||held.Length!=scope.identity.Length)return false;
        for(int i=0;i<held.Length;i++)if(held[i]!=scope.identity[i])return false;
        if(scope.consumed)throw new InvalidDataException("Matching seed command already sent; prediction recovery required.");
        // Serialization failure propagates: original owner MUST reconcile prediction. Never send both.
        scope.bindingFailed=true; // Serialization/binding failure cannot fall back to a second native send.
        envelope=new NetPackageRebirthSeedPlacementReview().Setup(package,scope.epoch,scope.nonce,scope.intent);
        var capture=envelope.CaptureConsumedEnvelope(scope.original);
        if(!scope.retained.TryBindConsumedEnvelope(capture))throw new InvalidDataException("Retained command rejected exact envelope binding.");
        scope.boundEnvelope=envelope;scope.boundCapture=capture;
        scope.bindingFailed=false;
        scope.consumed=true; // Envelope selected, NOT successful send or acknowledgement.
        return true;
    }
    public static bool ObserveNativeSendReturned(NetPackageRebirthSeedPlacementReview envelope)
    {
        var scope=current;
        if(scope==null||scope.disposed||scope.invalid||scope.bindingFailed||!scope.consumed||scope.nativeSendReturned||
            scope.thread!=System.Threading.Thread.CurrentThread.ManagedThreadId||
            !object.ReferenceEquals(scope.boundEnvelope,envelope)||scope.boundCapture==null||
            !object.ReferenceEquals(scope.boundCapture.Original,scope.original))return false;
        scope.nativeSendReturned=true; // Nominal return only; no queue/ACK/network delivery assertion.
        scope.retained.ObserveNativeSendReturn(scope.boundCapture);
        return true;
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;if(object.ReferenceEquals(current,this))current=null;
        // Owner handles unsent reservation retirement and predicted state recovery; no extra native executor/debit.
        retained.Retire(consumed,invocationStarted);
    }
}
public interface ISeedPlacementClientReviewOwner
{
    bool IsAuthoredHumanSeedCommand(ItemInventoryData data,BlockPlacement.Result placement,BlockValue actualTarget);
    bool TryGetBoundSessionCommand(World world,EntityPlayerLocal player,out ulong epoch,out ulong nonce);
    bool TryRetainOriginalCommand(World world,EntityPlayerLocal player,SeedPlacementOriginalCommandReview original,
        out ISeedPlacementRetainedCommandReview retained);
}












