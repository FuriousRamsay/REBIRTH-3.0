using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
public class PooledBinaryReader : BinaryReader { public PooledBinaryReader(Stream s):base(s){} }
public class PooledBinaryWriter : BinaryWriter { public PooledBinaryWriter(Stream s):base(s){} }
public struct Vector3i { public int x,y,z; public Vector3i(int a,int b,int c){x=a;y=b;z=c;} }
public struct PropRef { public static PropRef Read(PooledBinaryReader r){throw new Exception("Prop reader forbidden");} }
public struct BlockValueRef { public bool TryGetBlockPos(out Vector3i value){value=pos;return true;} public static BlockValueRef None; public Vector3i pos; public BlockValueRef(Vector3i v){pos=v;} public BlockValueRef(PropRef v){pos=default(Vector3i);} public static BlockValueRef Read(PooledBinaryReader br)
	{
		return br.ReadByte() switch
		{
			0 => None, 
			1 => new BlockValueRef(StreamUtils.ReadVector3i(br)), 
			2 => new BlockValueRef(PropRef.Read(br)), 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	} }
public class Shape {public bool terrain;public bool IsTerrain(){return terrain;}}
public class Block {public Shape shape=new Shape();public bool IsTerrainDecoration;}
public class BlockPlantGrowing:Block {}
public static class MarchingCubes {public const sbyte DensityAir=-127;}
public struct BlockValue {public Block Block {get{return new BlockPlantGrowing();}} public uint rawData; public int damage; public int type {get{return (int)(rawData&65535);}} public BlockValue(uint r,int d){rawData=r;damage=d;} public static BlockValue Read(PooledBinaryReader br)
	{
		BlockValue result = default(BlockValue);
		result.rawData = br.ReadUInt32();
		result.damage = br.ReadUInt16();
		return result;
	} }
public struct TextureFullArray { public long[] values; public bool IsDefault {get{return values==null||values[0]==0;}} public long this[int index]{get{return values==null?0:values[index];}} public void Read(PooledBinaryReader _br, int count = 1)
	{
		int i;
		for (i = 0; i < count; i++)
		{
			long num = _br.ReadInt64();
			if (i < 1)
			{
				values[i] = num;
			}
		}
		for (; i < 1; i++)
		{
			values[i] = values[0];
		}
	} }
public class BlockChangeInfo {
 [Flags] public enum Flags:byte {ChangeBlockValue=1,ChangeDamage=2,ChangeDensity=4,ForceDensity=8,UpdateLight=16,ChangeTexture=32}
 public BlockValueRef blockValueRef; public int changedByEntityId; public bool bChangeBlockValue,bChangeDensity,bForceDensity,bUpdateLight,bChangeDamage,bChangeTexture; public BlockValue blockValue; public sbyte density;
 public TextureFullArray textureFull = new TextureFullArray{values=new long[1]};
 public void Read(PooledBinaryReader _br)
	{
		blockValueRef = BlockValueRef.Read(_br);
		changedByEntityId = _br.ReadInt32();
		Flags flags = (Flags)_br.ReadByte();
		bChangeBlockValue = (flags & Flags.ChangeBlockValue) != 0;
		bChangeDensity = (flags & Flags.ChangeDensity) != 0;
		bForceDensity = (flags & Flags.ForceDensity) != 0;
		bUpdateLight = (flags & Flags.UpdateLight) != 0;
		bChangeDamage = (flags & Flags.ChangeDamage) != 0;
		bChangeTexture = (flags & Flags.ChangeTexture) != 0;
		if (bChangeBlockValue)
		{
			blockValue = BlockValue.Read(_br);
		}
		if (bChangeDensity)
		{
			density = _br.ReadSByte();
		}
		if (bChangeTexture)
		{
			textureFull.Read(_br);
		}
	}
}
public class PlatformUserIdentifierAbs { public byte[] canonical; public void ToStream(PooledBinaryWriter w){w.Write(canonical);} }
public class ClientInfo {public int entityId;public PlatformUserIdentifierAbs PlatformId,CrossplatformId;}
public class ConnectionManager {public bool IsServer=true;}
public class SingletonMonoBehaviour<T> where T:new(){public static T Instance=new T();}
public class NetPackage {public ClientInfo Sender;public virtual void read(PooledBinaryReader r){}public virtual void write(PooledBinaryWriter w){w.Write((ushort)124);}public virtual void ProcessPackage(World w,GameManager gm){}public bool ValidUserIdForSender(PlatformUserIdentifierAbs id){return object.ReferenceEquals(id,Sender.PlatformId)||object.ReferenceEquals(id,Sender.CrossplatformId);}public bool ValidEntityIdForSender(int id){return id==Sender.entityId;}}
public class NetPackageSetBlock {public static int Writes,Executions;public static bool ThrowWrite;public void ProcessPackage(World w,GameManager gm){Executions++;}public void write(PooledBinaryWriter w){Writes++;if(ThrowWrite)throw new InvalidDataException("serialize fixture");w.Write((ushort)123);persistentPlayerId.ToStream(w);w.Write((short)blockChanges.Count);foreach(var c in blockChanges){w.Write((byte)1);w.Write(c.blockValueRef.pos.x);w.Write(c.blockValueRef.pos.y);w.Write(c.blockValueRef.pos.z);w.Write(c.changedByEntityId);byte flags=(byte)((c.bChangeBlockValue?1:0)|(c.bChangeDamage?2:0)|(c.bChangeDensity?4:0)|(c.bForceDensity?8:0)|(c.bUpdateLight?16:0)|(c.bChangeTexture?32:0));w.Write(flags);if(c.bChangeBlockValue){w.Write(c.blockValue.rawData);w.Write((ushort)c.blockValue.damage);}if(c.bChangeDensity)w.Write(c.density);if(c.bChangeTexture)w.Write(c.textureFull[0]);}w.Write(localPlayerThatChanged);} public PlatformUserIdentifierAbs persistentPlayerId;public int localPlayerThatChanged;public List<BlockChangeInfo> blockChanges;public ClientInfo Sender; }
public static class NetPackageManager {public static int Factory;public static int GetPackageId(Type t){return 123;}public static T GetPackage<T>() where T:new(){Factory++;return new T();}}
public static class StreamUtils {
 public static int Readers;
 public static Vector3i ReadVector3i(PooledBinaryReader r){return new Vector3i(r.ReadInt32(),r.ReadInt32(),r.ReadInt32());}
 public static MemoryStream ToBlob(Action<PooledBinaryWriter> a){var m=new MemoryStream();var w=new PooledBinaryWriter(m);a(w);w.Flush();return m;}
 public static void FromBlob(MemoryStream m,Action<PooledBinaryReader> a){Readers++;var r=new PooledBinaryReader(m);a(r);}
}
public class TypedMetadataValue {public enum TypeTag {None,Float,Integer,String} public TypeTag tag;public object value;public TypeTag GetTypeTag(){return tag;}public object GetValue(){return value;}}
public class ItemValue {public struct Stat {public int type;public bool isBoosted;public short value;}public int type,Meta;public float UseTimes;public ushort Quality,Seed;public byte Flags,SelectedAmmoTypeIndex;public TextureFullArray TextureFullArray;public Stat[] Stats;public ItemValue[] modifications,cosmeticMods;public Dictionary<string,TypedMetadataValue> Metadata;public BlockValue ToBlockValue(){return new BlockValue((uint)type,0);}}
public class Inventory {public ItemInventoryData holdingItemData;public int SlotCount=10,SelectedSlot;public ItemStack holdingItemStack;public ItemValue holdingItemItemValue;}
public class ItemStack {public int count;}
public class EntityPlayerLocal:EntityPlayer {public int entityId=7;}
public class EntityPlayer {public Inventory inventory;}
public class World {public object GetTileEntity(Vector3i pos){return null;}public bool remote;public EntityPlayer player;public EntityPlayer GetPrimaryPlayer(){return player;}public BlockValue block;public bool IsRemote(){return remote;}public object GetEntity(int id){return player;}public BlockValue GetBlock(Vector3i pos){return block;}}
public class GameManager {public static GameManager Instance;public World World;public bool IsEditMode(){return false;}}
public class ItemInventoryData {public EntityPlayer holdingEntity;public World world;public bool IsBlock=true;public ItemValue itemValue;}
public class BlockPlacement {public class Result {public Vector3i blockPos;}}
// TOOLS REVIEW ONLY. Registration, bounded seed codec and admission implementations are NOT supplied.
// Native ABI: installed Assembly-CSharp FCEEC273...234C705 / MVID 1a9a4203-3d95-4c90-b094-8926dec1ee9c.



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




public class PacketAdmissionDouble:ISeedPlacementReviewAdmission {public bool ThrowDispose;public int Completed,Disposed;public void CompleteObservedCommit(){Completed++;}public void Dispose(){Disposed++;if(ThrowDispose)throw new InvalidOperationException("dispose fixture");}}
public class PacketOwnerDouble:ISeedPlacementReviewOwner {public int Terminal,Admits;public bool ReplaceOwner;public PacketAdmissionDouble Admission=new PacketAdmissionDouble();public PacketOwnerDouble Replacement;public void ReleaseOrRetainNativeAndReconcile(ClientInfo s,World w,ulong e,ulong n,SeedPlacementIntentCodecReview.Intent? i,NetPackageSetBlock p,ISeedPlacementReviewAdmission a){Terminal++;}public bool TryAdmit(ClientInfo s,World w,GameManager gm,ulong e,ulong n,SeedPlacementIntentCodecReview.Intent i,NetPackageSetBlock p,out ISeedPlacementReviewAdmission a){Admits++;a=Admission;if(ReplaceOwner)NetPackageRebirthSeedPlacementReview.Owner=Replacement;return true;}}
public class ClientOwnerDouble:ISeedPlacementClientReviewOwner,ISeedPlacementRetainedCommandReview {
 public int Retired,Marks,Binds,SendReturns;public bool Sent,Prediction,AllowRetain=true,AllowBind=true;public SeedPlacementOriginalCommandReview Original;public SeedPlacementConsumedEnvelopeReview Capture;
 public bool IsAuthoredHumanSeedCommand(ItemInventoryData d,BlockPlacement.Result p,BlockValue t){return true;}
 public bool TryGetBoundSessionCommand(World w,EntityPlayerLocal p,out ulong e,out ulong n){e=1;n=2;return true;}
 public bool TryRetainOriginalCommand(World w,EntityPlayerLocal p,SeedPlacementOriginalCommandReview original,out ISeedPlacementRetainedCommandReview retained){Original=original;retained=AllowRetain?this:null;return AllowRetain;}
 public void MarkNativeInvocationStarted(){Marks++;}
 public bool TryBindConsumedEnvelope(SeedPlacementConsumedEnvelopeReview capture){Binds++;Capture=capture;return AllowBind&&object.ReferenceEquals(Original,capture.Original);}
 public void ObserveNativeSendReturn(SeedPlacementConsumedEnvelopeReview capture){if(!object.ReferenceEquals(capture,Capture))throw new Exception("capture identity");SendReturns++;}
 public void Retire(bool envelopeConsumed,bool predictionMayHaveStarted){Retired++;Sent=envelopeConsumed;Prediction=predictionMayHaveStarted;}
}
public class HostPolicyDouble:ISeedPlacementHostPolicyReview {public bool Validate(ClientInfo s,World w,GameManager gm,SeedPlacementSessionAuthority.Reservation r,SeedPlacementIntentCodecReview.Intent i,NetPackageSetBlock p){return true;}}
// TOOLS ONLY. Evidence image; never session authorization, transport acknowledgement or debit proof.

public sealed class SeedPlacementConsumedEnvelopeReview
{
    public readonly SeedPlacementOriginalCommandReview Original;
    private readonly byte[] nativeBytes, intentBytes;
    private SeedPlacementConsumedEnvelopeReview(SeedPlacementOriginalCommandReview original,byte[] native,byte[] intent)
    {Original=original??throw new ArgumentNullException(nameof(original));nativeBytes=(byte[])native.Clone();intentBytes=(byte[])intent.Clone();}
    // Internal producer factory only; root registry must verify EXACT previously retained Original reference.
    internal static SeedPlacementConsumedEnvelopeReview Capture(SeedPlacementOriginalCommandReview original,byte[] native,byte[] intent)
    {if(native==null||intent==null)throw new ArgumentNullException();return new SeedPlacementConsumedEnvelopeReview(original,native,intent);}
    public byte[] CopyNativeBytes(){return (byte[])nativeBytes.Clone();}
    public byte[] CopyIntentBytes(){return (byte[])intentBytes.Clone();}
}
public interface ISeedPlacementRetainedCommandReview
{
    void MarkNativeInvocationStarted();
    bool TryBindConsumedEnvelope(SeedPlacementConsumedEnvelopeReview capture);
    // Nominal return ONLY: native SendToServer may return without queue acceptance.
    void ObserveNativeSendReturn(SeedPlacementConsumedEnvelopeReview capture);
    void Retire(bool envelopeConsumed,bool predictionMayHaveStarted);
}


public enum AdvancedFarmingPlantOrigin {Unknown,Player,System}
public class TileEntityPlantGrowingRebirth {public AdvancedFarmingPlantOrigin PlantOrigin;public ulong PlantIncarnation,StateRevision;}
public static class SeedPlacementSessionAuthority {public class Reservation{public int Actor;}public static int Reservations;private static HashSet<string> consumed=new HashSet<string>();public static bool TryReserve(ClientInfo sender,World w,ulong e,ulong n,out Reservation result){result=null;if(!consumed.Add(e+":"+n))return false;Reservations++;result=new Reservation{Actor=sender.entityId};return true;}public static bool IsCurrent(Reservation r){return true;}public static bool IsBoundToSender(Reservation r,ClientInfo s,World w,int actor){return r.Actor==actor&&s.entityId==actor;}}
public static class AdvancedFarmingRemotePlantingAdmissionReview {public class RemoteScope:IDisposable{public bool BeforeNativeExecution(){return true;}public bool Complete(){return false;}public void Dispose(){}}public static RemoteScope Begin(SeedPlacementSessionAuthority.Reservation r,SeedPlacementOriginalCommandReview c,World w,int actor){return new RemoteScope();}}
internal class TerminalDouble:ISeedPlacementTerminalBoundaryReview {public int Count;public void ObserveTerminal(ClientInfo s,World w,ulong e,ulong n,SeedPlacementIntentCodecReview.Intent? i,NetPackageSetBlock p,SeedPlacementHostOutcomeReview o,bool may,SeedPlacementHostOwnerReview.TerminalReceipt receipt){Count++;}}
// TOOLS ONLY. No registration, debit, prediction repair, queue release or session issue transport.

internal interface ISeedPlacementTerminalBoundaryReview
{
    void ObserveTerminal(ClientInfo sender,World world,ulong epoch,ulong nonce,
        SeedPlacementIntentCodecReview.Intent? parsedIntent,NetPackageSetBlock native,
        SeedPlacementHostOutcomeReview outcome,bool nativeExecutionMayHaveStarted,
        SeedPlacementHostOwnerReview.TerminalReceipt receipt);
}
internal interface ISeedPlacementHostPolicyReview
{
    bool Validate(ClientInfo sender,World world,GameManager callbacks,
        SeedPlacementSessionAuthority.Reservation reservation,SeedPlacementIntentCodecReview.Intent intent,NetPackageSetBlock native);
}
internal sealed class SeedPlacementHostOwnerReview : ISeedPlacementReviewOwner
{
    private readonly ISeedPlacementTerminalBoundaryReview terminal;
    private readonly ISeedPlacementHostPolicyReview policy;
    internal SeedPlacementHostOwnerReview(ISeedPlacementTerminalBoundaryReview terminal,ISeedPlacementHostPolicyReview policy)
    {this.terminal=terminal??throw new ArgumentNullException(nameof(terminal));this.policy=policy??throw new ArgumentNullException(nameof(policy));}
    public bool TryAdmit(ClientInfo sender,World world,GameManager callbacks,ulong epoch,ulong nonce,
        SeedPlacementIntentCodecReview.Intent intent,NetPackageSetBlock original,out ISeedPlacementReviewAdmission admission)
    {
        admission=null;
        if(callbacks==null||!ReferenceEquals(GameManager.Instance,callbacks))return false;
        SeedPlacementSessionAuthority.Reservation reservation;
        if(!SeedPlacementSessionAuthority.TryReserve(sender,world,epoch,nonce,out reservation))return false;
        SeedPlacementOriginalCommandReview command;
        try{command=SeedPlacementOriginalCommandReview.Capture(epoch,nonce,sender.entityId,intent.OriginalCount,intent);}
        catch(System.IO.InvalidDataException){return false;}
        // Keep the genuine reservation/image even when subsequent validation rejects the command.
        var owned=new Admission(this,reservation,command,world);admission=owned;
        if(!SeedPlacementIntentCodecReview.ValidateSenderSnapshot(sender,world,callbacks,intent,original)||
            !policy.Validate(sender,world,callbacks,reservation,intent,original))return false;
        var scope=AdvancedFarmingRemotePlantingAdmissionReview.Begin(reservation,command,world,reservation.Actor);
        if(scope==null)return false;
        owned.SetScope(scope);
        if(!scope.BeforeNativeExecution())return false;
        owned.MarkExecutionMayStart();return true; // Envelope alone invokes the native executor next.
    }
    public void ReleaseOrRetainNativeAndReconcile(ClientInfo sender,World world,ulong epoch,ulong nonce,
        SeedPlacementIntentCodecReview.Intent? intent,NetPackageSetBlock original,ISeedPlacementReviewAdmission admission)
    {
        TerminalReceipt receipt;
        TerminalReceipt.TryMint(admission,this,sender,world,epoch,nonce,out receipt);
        // Outcome/bool are diagnostics. Only the sealed same-reservation receipt permits authenticated ACK export.
        terminal.ObserveTerminal(sender,world,epoch,nonce,intent,original,receipt?.Outcome,
            receipt!=null&&receipt.NativeExecutionMayHaveStarted,receipt);
    }
    internal sealed class TerminalReceipt
    {
        private readonly Admission source;
        private readonly ClientInfo sender;
        private readonly World world;
        private readonly int thread;
        private bool consumed;
        internal readonly SeedPlacementHostOutcomeReview Outcome;
        internal readonly bool NativeExecutionMayHaveStarted;
        private TerminalReceipt(Admission source,ClientInfo sender,World world)
        {this.source=source;this.sender=sender;this.world=world;thread=System.Threading.Thread.CurrentThread.ManagedThreadId;
         Outcome=source.SnapshotOutcome();NativeExecutionMayHaveStarted=source.NativeExecutionMayHaveStarted;}
        internal static bool TryMint(object opaqueAdmission,SeedPlacementHostOwnerReview issuer,ClientInfo sender,
            World world,ulong epoch,ulong nonce,out TerminalReceipt receipt)
        {
            receipt=null;var source=opaqueAdmission as Admission;
            if(source==null||!source.TryTakeTerminal(issuer,sender,world,epoch,nonce))return false;
            receipt=new TerminalReceipt(source,sender,world);return true;
        }
        internal bool TryConsumeCurrent(ClientInfo currentSender,World currentWorld,out SeedPlacementHostOutcomeReview outcome)
        {
            outcome=null;
            if(consumed||thread!=System.Threading.Thread.CurrentThread.ManagedThreadId||
                !ReferenceEquals(sender,currentSender)||!ReferenceEquals(world,currentWorld)||
                !source.BoundCurrent(currentSender,currentWorld))return false;
            consumed=true;outcome=Outcome;return true; // Never reserves/releases a second nonce.
        }
    }
    private sealed class Admission : ISeedPlacementReviewAdmission
    {
        private readonly SeedPlacementHostOwnerReview issuer;
        private readonly SeedPlacementSessionAuthority.Reservation reservation;
        private readonly SeedPlacementOriginalCommandReview command;
        private readonly World world;
        private readonly int thread;
        private AdvancedFarmingRemotePlantingAdmissionReview.RemoteScope scope;
        private TileEntityPlantGrowingRebirth committed;
        private ulong incarnation,revision;
        private bool completed,disposed,terminalTaken;
        internal bool NativeExecutionMayHaveStarted {get;private set;}
        internal Admission(SeedPlacementHostOwnerReview issuer,SeedPlacementSessionAuthority.Reservation reservation,
            SeedPlacementOriginalCommandReview command,World world)
        {this.issuer=issuer;this.reservation=reservation;this.command=command;this.world=world;thread=System.Threading.Thread.CurrentThread.ManagedThreadId;}
        internal void SetScope(AdvancedFarmingRemotePlantingAdmissionReview.RemoteScope scope){this.scope=scope;}
        internal void MarkExecutionMayStart(){NativeExecutionMayHaveStarted=true;}
        internal bool BoundCurrent(ClientInfo sender,World suppliedWorld)
        {return ReferenceEquals(world,suppliedWorld)&&SeedPlacementSessionAuthority.IsBoundToSender(reservation,sender,world,reservation.Actor);}
        internal bool TryTakeTerminal(SeedPlacementHostOwnerReview owner,ClientInfo sender,World suppliedWorld,ulong epoch,ulong nonce)
        {
            if(terminalTaken||!disposed||thread!=System.Threading.Thread.CurrentThread.ManagedThreadId||
                !ReferenceEquals(owner,issuer)||epoch!=command.Epoch||nonce!=command.Nonce||!BoundCurrent(sender,suppliedWorld))return false;
            terminalTaken=true;return true;
        }
        public void CompleteObservedCommit()
        {
            if(disposed||completed||!NativeExecutionMayHaveStarted||scope==null||!scope.Complete())return;
            var te=world.GetTileEntity(command.Position) as TileEntityPlantGrowingRebirth;var observed=world.GetBlock(command.Position);
            if(!SeedPlacementSessionAuthority.IsCurrent(reservation)||te==null||te.PlantOrigin!=AdvancedFarmingPlantOrigin.Player||
                te.PlantIncarnation==0||te.StateRevision==0||observed.rawData!=command.Target.rawData||observed.damage!=command.Target.damage)return;
            committed=te;incarnation=te.PlantIncarnation;revision=te.StateRevision;completed=true;
        }
        internal SeedPlacementHostOutcomeReview SnapshotOutcome()
        {
            var observed=world.GetBlock(command.Position);
            bool exact=completed&&SeedPlacementSessionAuthority.IsCurrent(reservation)&&ReferenceEquals(world.GetTileEntity(command.Position),committed)&&
                committed.PlantOrigin==AdvancedFarmingPlantOrigin.Player&&committed.PlantIncarnation==incarnation&&committed.StateRevision==revision&&
                observed.rawData==command.Target.rawData&&observed.damage==command.Target.damage;
            var status=exact?SeedPlacementHostDispositionReview.Committed:NativeExecutionMayHaveStarted?
                SeedPlacementHostDispositionReview.NativeExecutionUncertain:SeedPlacementHostDispositionReview.RejectedBeforeExecution;
            return new SeedPlacementHostOutcomeReview(command,status,observed,exact?incarnation:0,exact?revision:0);
        }
        public void Dispose(){if(disposed)return;try{scope?.Dispose();}finally{disposed=true;}}
    }
}
// TOOLS ONLY. Original command image; never an authorization or inventory mutation receipt.


public sealed class SeedPlacementOriginalCommandReview
{
    public readonly ulong Epoch, Nonce;
    public readonly int Actor, Slot, OriginalCount;
    public readonly byte Flags;
    public readonly sbyte Density;
    public readonly long Texture;
    public readonly Vector3i Position;
    public readonly BlockValue Target, ExpectedOld;
    private readonly byte[] seed;
    private SeedPlacementOriginalCommandReview(ulong epoch,ulong nonce,int actor,int count,
        SeedPlacementIntentCodecReview.Intent intent)
    {
        Epoch=epoch;Nonce=nonce;Actor=actor;Slot=intent.Slot;OriginalCount=count;
        Position=intent.Position;Target=intent.Target;ExpectedOld=intent.ExpectedOld;
        Flags=intent.Flags;Density=(Flags&4)!=0?intent.Density:(sbyte)0;Texture=(Flags&32)!=0?intent.Texture:0;
        seed=(byte[])intent.Seed.Clone();
    }
    public byte[] CopySeedIdentity(){return (byte[])seed.Clone();}
    public bool MatchesSeedIdentity(byte[] candidate)
    {
        if(candidate==null||candidate.Length!=seed.Length)return false;
        for(int i=0;i<seed.Length;i++)if(candidate[i]!=seed[i])return false;
        return true;
    }
    // A capture factory validates shape only. Callers MUST establish session/sender/slot custody separately.
    public static SeedPlacementOriginalCommandReview Capture(ulong epoch,ulong nonce,int actor,int originalCount,
        SeedPlacementIntentCodecReview.Intent intent)
    {
        if(epoch==0||nonce==0||actor<=0||originalCount<=0||intent.Slot<0||intent.Seed==null||
            intent.Seed.Length==0||intent.Seed.Length>SeedPlacementIntentCodecReview.MaxSeedBytes)
            throw new InvalidDataException("Invalid original seed command image.");
        return new SeedPlacementOriginalCommandReview(epoch,nonce,actor,originalCount,intent);
    }
}
// Proposed outcome ABI; no public outcome factory and no inventory debit claim.
// Wire transport must authenticate host/session and correlate the entire original command.
public enum SeedPlacementHostDispositionReview : byte
{
    RejectedBeforeExecution=0,
    Committed=1,
    NativeExecutionUncertain=2
}
public sealed class SeedPlacementHostOutcomeReview
{
    public readonly SeedPlacementOriginalCommandReview Original;
    public readonly SeedPlacementHostDispositionReview Disposition;
    public readonly BlockValue ObservedBlock;
    public readonly ulong PlantIncarnation, StateRevision;
    internal SeedPlacementHostOutcomeReview(SeedPlacementOriginalCommandReview original,
        SeedPlacementHostDispositionReview disposition,BlockValue observed,ulong incarnation,ulong revision)
    {
        if(original==null)throw new ArgumentNullException(nameof(original));
        if(disposition<SeedPlacementHostDispositionReview.RejectedBeforeExecution||
            disposition>SeedPlacementHostDispositionReview.NativeExecutionUncertain)
            throw new ArgumentOutOfRangeException(nameof(disposition));
        if(disposition==SeedPlacementHostDispositionReview.Committed&&
            (incarnation==0||revision==0||observed.rawData!=original.Target.rawData||observed.damage!=original.Target.damage))
            throw new InvalidDataException("Committed origin requires an exact observed target identity.");
        Original=original;Disposition=disposition;ObservedBlock=observed;
        PlantIncarnation=incarnation;StateRevision=revision;
    }
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













// TOOLS ONLY. Versioned command intent and bounded FULL seed identity; never native ItemValue.Write/Clone.




public static class SeedPlacementIntentCodecReview
{
    public const int MaxSeedBytes = 8192;
    public const int MaxOriginalCount = 1000000; // Proposed command-image admission ceiling; NOT native authored max-stack.
    public struct Intent { public int Slot, OriginalCount; public Vector3i Position; public BlockValue Target, ExpectedOld; public byte Flags; public sbyte Density; public long Texture; public byte[] Seed; }
    public static bool TryRead(byte[] bytes, out Intent intent)
    {
        intent = default(Intent);
        if (bytes == null || bytes.Length < 1+8+12+16+10+4 || bytes.Length > MaxSeedBytes+51) return false;
        try { using (var stream=new MemoryStream(bytes,false)) using(var reader=new BinaryReader(stream)) {
            if(reader.ReadByte()!=3)return false;
            var result=new Intent();result.Slot=reader.ReadInt32();result.OriginalCount=reader.ReadInt32();
            if(result.OriginalCount<=0||result.OriginalCount>MaxOriginalCount)return false;
            result.Position=new Vector3i(reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32());
            result.Target=new BlockValue(reader.ReadUInt32(),reader.ReadInt32());
            result.ExpectedOld=new BlockValue(reader.ReadUInt32(),reader.ReadInt32());
            result.Flags=reader.ReadByte();result.Density=reader.ReadSByte();result.Texture=reader.ReadInt64();
            if(result.Flags!=0x15&&result.Flags!=0x11&&result.Flags!=0x21)return false;
            if((result.Flags&4)==0&&result.Density!=0)return false;
            if((result.Flags&32)==0&&result.Texture!=0)return false;
            int count=reader.ReadInt32();if(count<=0||count>MaxSeedBytes||count!=stream.Length-stream.Position)return false;
            result.Seed=reader.ReadBytes(count);intent=result;return true;
        }} catch(IOException){return false;} catch(ArgumentException){return false;}
    }
    public static byte[] Write(int slot, int originalCount, Vector3i pos, BlockValue target, BlockValue old, ItemValue seed)
    {
        if(originalCount<=0||originalCount>MaxOriginalCount)throw new InvalidDataException("Invalid original client count.");
        byte flags; sbyte density; long texture;
        if(!TryExpectedChange(seed,target,out flags,out density,out texture))throw new InvalidDataException("Unsupported native placement route.");
        byte[] identity; if(!TrySeedIdentity(seed,out identity))throw new InvalidDataException("Unsupported seed payload.");
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)) {
            writer.Write((byte)3);writer.Write(slot);writer.Write(originalCount);writer.Write(pos.x);writer.Write(pos.y);writer.Write(pos.z);
            writer.Write(target.rawData);writer.Write(target.damage);writer.Write(old.rawData);writer.Write(old.damage);
            writer.Write(flags);writer.Write(density);writer.Write(texture);
            writer.Write(identity.Length);writer.Write(identity);return stream.ToArray();
        }
    }
    public static bool TryExpectedChange(ItemValue seed,BlockValue target,out byte flags,out sbyte density,out long texture)
    {
        flags=0;density=0;texture=0;if(seed==null)return false;
        if(!seed.TextureFullArray.IsDefault){flags=0x21;texture=seed.TextureFullArray[0];return true;}
        Block block=target.Block;
        // Only exact ordinary plant inheritance qualified. Terrain/other override routes stay closed.
        if(!(block is BlockPlantGrowing)||block.shape.IsTerrain())return false;
        if(block.IsTerrainDecoration){flags=0x11;return true;}
        flags=0x15;density=MarchingCubes.DensityAir;return true;
    }
    public static bool MatchesChange(Intent intent,BlockChangeInfo change)
    {
        if(change==null)return false;
        byte flags=(byte)((change.bChangeBlockValue?1:0)|(change.bChangeDamage?2:0)|(change.bChangeDensity?4:0)|
            (change.bForceDensity?8:0)|(change.bUpdateLight?16:0)|(change.bChangeTexture?32:0));
        return flags==intent.Flags&&change.blockValue.rawData==intent.Target.rawData&&change.blockValue.damage==intent.Target.damage&&
            ((flags&4)==0||change.density==intent.Density)&&((flags&32)==0||change.textureFull[0]==intent.Texture);
    }
    public static bool TrySeedIdentity(ItemValue source,out byte[] bytes)
    {
        bytes=null;if(source==null)return false;
        try {
            // Fixed-capacity stream refuses before expanding beyond the proposed byte budget.
            var buffer=new byte[MaxSeedBytes];using(var stream=new MemoryStream(buffer,0,buffer.Length,true,true)) {
                stream.SetLength(0);using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)) {
                    int nodes=256;WriteValue(writer,source,new HashSet<ItemValue>(ReferenceComparer.Instance),0,ref nodes);writer.Flush();
                    bytes=new byte[stream.Length];Array.Copy(buffer,bytes,bytes.Length);return true;
                }
            }
        }catch(InvalidDataException){return false;}catch(IOException){return false;}catch(ArgumentException){return false;}catch(NotSupportedException){return false;}
    }
    private sealed class ReferenceComparer : IEqualityComparer<ItemValue>
    {
        internal static readonly ReferenceComparer Instance = new ReferenceComparer();
        public bool Equals(ItemValue a,ItemValue b){return object.ReferenceEquals(a,b);}
        public int GetHashCode(ItemValue value){return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);}
    }
    private static void WriteValue(BinaryWriter writer,ItemValue value,HashSet<ItemValue> path,int depth,ref int nodes)
    {
        if(value==null){writer.Write((byte)0);return;}
        if(depth>16||--nodes<0||!path.Add(value))throw new InvalidDataException("Seed graph exceeds bounds.");
        try {
            writer.Write((byte)1);writer.Write(value.type);writer.Write(value.Meta);writer.Write(value.UseTimes);
            writer.Write(value.Quality);writer.Write(value.Seed);writer.Write(value.Flags);writer.Write(value.SelectedAmmoTypeIndex);
            writer.Write(value.TextureFullArray[0]);
            int count=value.Stats==null?-1:value.Stats.Length;BoundCount(count);writer.Write((short)count);
            if(value.Stats!=null)foreach(var stat in value.Stats){writer.Write((int)stat.type);writer.Write(stat.isBoosted);writer.Write(stat.value);}
            WriteArray(writer,value.modifications,path,depth,ref nodes);WriteArray(writer,value.cosmeticMods,path,depth,ref nodes);
            count=value.Metadata==null?-1:value.Metadata.Count;BoundCount(count);writer.Write((short)count);
            if(value.Metadata!=null) {
                byte comparer;
                if(value.Metadata.Comparer.Equals(StringComparer.Ordinal))comparer=1;
                else if(value.Metadata.Comparer.Equals(StringComparer.OrdinalIgnoreCase))comparer=2;
                else if(value.Metadata.Comparer.Equals(EqualityComparer<string>.Default))comparer=0;
                else throw new InvalidDataException("Unsupported metadata comparer.");
                writer.Write(comparer);var keys=new List<string>(value.Metadata.Keys);keys.Sort(StringComparer.Ordinal);
                foreach(var key in keys) {
                    WriteString(writer,key);var metadata=value.Metadata[key];
                    writer.Write(metadata!=null);if(metadata==null)continue;
                    var tag=metadata.GetTypeTag();writer.Write((byte)tag);object item=metadata.GetValue();
                    writer.Write(item!=null);if(item==null)continue;
                    if(tag==TypedMetadataValue.TypeTag.Float&&item is float)writer.Write((float)item);
                    else if(tag==TypedMetadataValue.TypeTag.Integer&&item is int)writer.Write((int)item);
                    else if(tag==TypedMetadataValue.TypeTag.String&&item is string)WriteString(writer,(string)item);
                    else throw new InvalidDataException("Unsupported metadata value.");
                }
            }
        }finally{path.Remove(value);}
    }
    private static void WriteArray(BinaryWriter writer,ItemValue[] values,HashSet<ItemValue> path,int depth,ref int nodes)
    {int count=values==null?-1:values.Length;BoundCount(count);writer.Write((short)count);if(values!=null)foreach(var value in values)WriteValue(writer,value,path,depth+1,ref nodes);}
    private static void BoundCount(int count){if(count>255)throw new InvalidDataException("Seed container exceeds native count.");}
    private static void WriteString(BinaryWriter writer,string value)
    {if(value==null||value.Length>1024)throw new InvalidDataException("Seed string exceeds bound.");int length=new UTF8Encoding(false,true).GetByteCount(value);if(length>2048)throw new InvalidDataException("Seed UTF8 exceeds bound.");writer.Write((ushort)length);writer.Write(new UTF8Encoding(false,true).GetBytes(value));}
    public static bool ValidateSenderSnapshot(ClientInfo sender,World world,GameManager callbacks,Intent intent,NetPackageSetBlock packet)
    {
        if(sender==null||world==null||callbacks==null||callbacks.IsEditMode()||world.IsRemote()||
            !object.ReferenceEquals(callbacks.World,world)||packet==null||packet.blockChanges==null||packet.blockChanges.Count!=1)return false;
        var player=world.GetEntity(sender.entityId) as EntityPlayer;
        if(player==null||player.inventory==null||intent.Slot<0||intent.Slot>=player.inventory.SlotCount||
            player.inventory.SelectedSlot!=intent.Slot||player.inventory.holdingItemStack.count<=0)return false;
        Vector3i position;var change=packet.blockChanges[0];
        if(!change.blockValueRef.TryGetBlockPos(out position)||position.x!=intent.Position.x||position.y!=intent.Position.y||position.z!=intent.Position.z||
            change.changedByEntityId!=sender.entityId||change.blockValue.rawData!=intent.Target.rawData||change.blockValue.damage!=intent.Target.damage)return false;
        if(!MatchesChange(intent,change))return false;
        BlockValue observed=world.GetBlock(position);
        if(observed.rawData!=intent.ExpectedOld.rawData||observed.damage!=intent.ExpectedOld.damage)return false;
        var held=player.inventory.holdingItemItemValue;
        if(held==null||held.ToBlockValue().type!=intent.Target.type)return false;
        byte flags;sbyte density;long texture;
        if(!TryExpectedChange(held,intent.Target,out flags,out density,out texture)||
            flags!=intent.Flags||density!=intent.Density||texture!=intent.Texture)return false;
        byte[] actual;if(!TrySeedIdentity(held,out actual)||intent.Seed==null||actual.Length!=intent.Seed.Length)return false;
        for(int i=0;i<actual.Length;i++)if(actual[i]!=intent.Seed[i])return false;
        return true; // Snapshot consistency ONLY; delayed-debit ordering and authored seed policy remain owner admission.
    }
}






// TOOLS ONLY. Exact installed voxel-only native payload decoder; no PlatformUserIdentifierAbs.FromStream.



public static class SeedPlacementNativeDecoderReview
{
    public static bool TryDecode(byte[] bytes, ClientInfo sender, out NetPackageSetBlock original)
    {
        original = null;
        if (bytes == null || sender == null || bytes.Length > 65535 || bytes.Length < 2) return false;
        try
        {
            int id = bytes[0] | bytes[1] << 8;
            if (id != NetPackageManager.GetPackageId(typeof(NetPackageSetBlock))) return false;
            PlatformUserIdentifierAbs identity = null;
            int offset = 2;
            if (!MatchIdentity(bytes, sender.PlatformId, ref offset))
            {
                offset = 2;
                if (!MatchIdentity(bytes, sender.CrossplatformId, ref offset)) return false;
                identity = sender.CrossplatformId;
            }
            else identity = sender.PlatformId;
            // Count is native signed Int16. Exactly ONE voxel placement; no partial multi-change support.
            if (bytes.Length - offset < 2 || bytes[offset] != 1 || bytes[offset + 1] != 0) return false;
            offset += 2;
            // BlockValueRef tag1 + XYZ Int32 (13), actor Int32 (4), flags1.
            if (bytes.Length - offset < 18 + 4 || bytes[offset] != 1) return false;
            byte flags = bytes[offset + 17];
            // Require block replacement, forbid damage-only/reserved flags; density/texture/light preserved.
            if ((flags & 1) == 0 || (flags & 0xC2) != 0 || ((flags & 8) != 0 && (flags & 4) == 0)) return false;
            int changeLength = 18 + 6 + ((flags & 4) != 0 ? 1 : 0) + ((flags & 32) != 0 ? 8 : 0);
            if (bytes.Length - offset != changeLength + 4) return false; // Exact EOF BEFORE native Read.
            if (ReadInt32(bytes, offset + 13) != sender.entityId ||
                ReadInt32(bytes, offset + changeLength) != sender.entityId) return false;
            BlockChangeInfo change = null;
            int localActor = -1;
            using (MemoryStream blob = new MemoryStream(bytes, writable: false))
                StreamUtils.FromBlob(blob, reader =>
                {
                    reader.BaseStream.Position = offset;
                    change = new BlockChangeInfo();
                    change.Read(reader); // Proven tag1 path and fixed length; never PropRef/string/array decoder.
                    localActor = reader.ReadInt32();
                    if (reader.BaseStream.Position != bytes.Length) throw new InvalidDataException();
                });
            if (change == null || change.changedByEntityId != sender.entityId || localActor != sender.entityId) return false;
            // Native NetPackageSetBlock is NOT IMemoryPoolableObject; exact manager has null pool,
            // constructs parameterless via ctor.Invoke, and FreePackage is a no-op for this class.
            original = NetPackageManager.GetPackage<NetPackageSetBlock>();
            original.persistentPlayerId = identity;
            original.localPlayerThatChanged = localActor;
            original.blockChanges = new List<BlockChangeInfo>(1) { change };
            original.Sender = sender;
            return true;
        }
        catch (IOException) { return false; }
        catch (ArgumentException) { return false; }
        catch (OverflowException) { return false; }
    }
    private static int ReadInt32(byte[] bytes, int offset)
    {
        return bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16 | bytes[offset + 3] << 24;
    }
    private static bool MatchIdentity(byte[] packet, PlatformUserIdentifierAbs identity, ref int offset)
    {
        if (identity == null) return false;
        // Only authenticated SERVER-owned identity is serialized; attacker strings are never decoded.
        using (MemoryStream blob = StreamUtils.ToBlob(writer => identity.ToStream(writer)))
        {
            byte[] canonical = blob.ToArray();
            if (canonical.Length == 0 || canonical.Length > packet.Length - offset) return false;
            for (int i = 0; i < canonical.Length; i++) if (packet[offset + i] != canonical[i]) return false;
            offset += canonical.Length;
            return true;
        }
    }
}


public static class DecoderFixture {
 static int checks;
 static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
 static byte[] Frame(byte[] identity,byte flags=1,short count=1,byte tag=1,int actor=7,int local=7){using(var m=new MemoryStream()){var w=new BinaryWriter(m);w.Write((ushort)123);w.Write(identity);w.Write(count);w.Write(tag);w.Write(1);w.Write(2);w.Write(3);w.Write(actor);w.Write(flags);if((flags&1)!=0){w.Write((uint)42);w.Write((ushort)12);}if((flags&4)!=0)w.Write((sbyte)-12);if((flags&32)!=0)w.Write((long)987654321);w.Write(local);return m.ToArray();}}
 static void Refuse(byte[] b,ClientInfo s,string name,bool beforeRead=true){int f=NetPackageManager.Factory,r=StreamUtils.Readers;NetPackageSetBlock p;Check(!SeedPlacementNativeDecoderReview.TryDecode(b,s,out p)&&p==null,name);Check(NetPackageManager.Factory==f,name+" factory");if(beforeRead)Check(StreamUtils.Readers==r,name+" reader");}
 public static void Run(){var platform=new byte[]{1,1,3,65,66,67,2,68,69};var cross=new byte[]{1,1,1,88,1,89};var sender=new ClientInfo{entityId=7,PlatformId=new PlatformUserIdentifierAbs{canonical=platform},CrossplatformId=new PlatformUserIdentifierAbs{canonical=cross}};
 foreach(var identity in new[]{platform,cross})foreach(byte flags in new byte[]{1,17,5,13,33,53,61}){NetPackageSetBlock p;Check(SeedPlacementNativeDecoderReview.TryDecode(Frame(identity,flags),sender,out p),"valid");Check(p.blockChanges.Count==1&&p.Sender==sender&&p.blockChanges[0].blockValue.rawData==42,"payload");if((flags&32)!=0)Check(p.blockChanges[0].textureFull.values[0]==987654321,"texture");}
 var good=Frame(platform);for(int n=0;n<good.Length;n++){var shortFrame=new byte[n];Array.Copy(good,shortFrame,n);Refuse(shortFrame,sender,"trunc"+n);}
 var trailing=new byte[good.Length+1];Array.Copy(good,trailing,good.Length);Refuse(trailing,sender,"trailing");
 var badId=(byte[])good.Clone();badId[0]=124;Refuse(badId,sender,"id");
 foreach(short count in new short[]{-1,0,2,32767})Refuse(Frame(platform,1,count),sender,"count");
 foreach(byte tag in new byte[]{0,2,255})Refuse(Frame(platform,1,1,tag),sender,"reference");
 foreach(byte flags in new byte[]{0,2,3,9,65,129,255})Refuse(Frame(platform,flags),sender,"flags");
 Refuse(Frame(new byte[]{0}),sender,"identity");Refuse(good,null,"sender");
 Refuse(Frame(platform,1,1,1,8),sender,"embeddedactor");Refuse(Frame(platform,1,1,1,7,8),sender,"actor");
  var seed=new ItemValue{type=42,Meta=13,Seed=123,Quality=5,UseTimes=float.NaN,Stats=new ItemValue.Stat[]{new ItemValue.Stat{type=8,isBoosted=true,value=-12}},Metadata=new Dictionary<string,TypedMetadataValue>(StringComparer.OrdinalIgnoreCase){["Color"]=new TypedMetadataValue{tag=TypedMetadataValue.TypeTag.String,value="green"}}};
 var empty=new ItemValue{Seed=456};seed.modifications=new[]{empty};byte[] identityBytes;Check(SeedPlacementIntentCodecReview.TrySeedIdentity(seed,out identityBytes),"full seed");Check(empty.Seed==456&&seed.Seed==123,"unchanged empty seed");
 var encoded=SeedPlacementIntentCodecReview.Write(2,3,new Vector3i(1,2,3),new BlockValue(42,12),new BlockValue(9,0),seed);SeedPlacementIntentCodecReview.Intent intent;Check(SeedPlacementIntentCodecReview.TryRead(encoded,out intent),"intent");Check(intent.Slot==2&&intent.Seed.Length==identityBytes.Length,"intent fields");
 for(int n=0;n<encoded.Length;n++){var truncated=new byte[n];Array.Copy(encoded,truncated,n);Check(!SeedPlacementIntentCodecReview.TryRead(truncated,out intent),"intent trunc");}
 seed.modifications=new[]{seed};Check(!SeedPlacementIntentCodecReview.TrySeedIdentity(seed,out identityBytes)&&identityBytes==null,"cycle");seed.modifications=new ItemValue[256];Check(!SeedPlacementIntentCodecReview.TrySeedIdentity(seed,out identityBytes),"count");seed.modifications=null;
 seed.Metadata["Color"].value=new string('x',1025);Check(!SeedPlacementIntentCodecReview.TrySeedIdentity(seed,out identityBytes),"string");seed.Metadata["Color"].value="\uD800";Check(!SeedPlacementIntentCodecReview.TrySeedIdentity(seed,out identityBytes),"surrogate");seed.Metadata["Color"].value="green";
 var held=new Inventory{SelectedSlot=2,holdingItemStack=new ItemStack{count=1},holdingItemItemValue=seed};var world=new World{player=new EntityPlayer{inventory=held},block=new BlockValue(9,0)};var gm=new GameManager{World=world};NetPackageSetBlock packet;Check(SeedPlacementNativeDecoderReview.TryDecode(Frame(platform,21),sender,out packet),"snapshot packet");packet.blockChanges[0].density=MarchingCubes.DensityAir;Check(SeedPlacementIntentCodecReview.TryRead(SeedPlacementIntentCodecReview.Write(2,3,new Vector3i(1,2,3),new BlockValue(42,12),new BlockValue(9,0),seed),out intent),"snapshot intent");Check(SeedPlacementIntentCodecReview.ValidateSenderSnapshot(sender,world,gm,intent,packet),"snapshot valid");held.SelectedSlot=3;Check(!SeedPlacementIntentCodecReview.ValidateSenderSnapshot(sender,world,gm,intent,packet),"snapshot slot");held.SelectedSlot=2;seed.Seed++;Check(!SeedPlacementIntentCodecReview.ValidateSenderSnapshot(sender,world,gm,intent,packet),"snapshot fullseed");seed.Seed--;world.block=new BlockValue(10,0);Check(!SeedPlacementIntentCodecReview.ValidateSenderSnapshot(sender,world,gm,intent,packet),"stale old");
 var local=new EntityPlayerLocal{inventory=held};world.player=local;world.remote=true;world.block=new BlockValue(9,0);GameManager.Instance=gm;var data=new ItemInventoryData{world=world,holdingEntity=local,itemValue=seed};held.holdingItemData=data;var owner=new ClientOwnerDouble();SeedPlacementClientScopeReview.ClientOwner=owner;
 NetPackageRebirthSeedPlacementReview consumedEnvelope=null;using(var scope=SeedPlacementClientScopeReview.Begin(data,new BlockPlacement.Result{blockPos=new Vector3i(1,2,3)},new BlockValue(42,12))){Check(scope!=null,"client begin preprediction");NetPackageRebirthSeedPlacementReview envelope;Check(!SeedPlacementClientScopeReview.TryConsumeMatchingSend(packet,out envelope),"beforeprediction refuses");Check(scope.BeforeNativeInvocation()&&owner.Marks==1,"retained mark native once");world.block=new BlockValue(42,12);Check(SeedPlacementClientScopeReview.TryConsumeMatchingSend(packet,out envelope)&&envelope!=null,"one client send");consumedEnvelope=envelope;bool duplicate=false;try{SeedPlacementClientScopeReview.TryConsumeMatchingSend(packet,out envelope);}catch(InvalidDataException){duplicate=true;}Check(duplicate&&NetPackageSetBlock.Writes==1,"duplicate suppressed");Check(!SeedPlacementClientScopeReview.ObserveNativeSendReturned(new NetPackageRebirthSeedPlacementReview()),"wrong envelope return closed");Check(SeedPlacementClientScopeReview.ObserveNativeSendReturned(consumedEnvelope)&&owner.SendReturns==1,"exact native nominal return capture");Check(!SeedPlacementClientScopeReview.ObserveNativeSendReturned(consumedEnvelope)&&owner.SendReturns==1,"duplicate native return closed");}
 Check(owner.Retired==1&&owner.Sent,"client retirement");NetPackageRebirthSeedPlacementReview after;Check(!SeedPlacementClientScopeReview.TryConsumeMatchingSend(packet,out after),"disposed scope");Check(held.holdingItemStack.count==1,"no second debit");Check(!SeedPlacementClientScopeReview.ObserveNativeSendReturned(consumedEnvelope)&&owner.SendReturns==1,"disposed nominal return closed");
 var receiptIntent=new SeedPlacementIntentCodecReview.Intent{Slot=2,Position=new Vector3i(1,2,3),Target=new BlockValue(42,12),ExpectedOld=new BlockValue(9,0),Seed=new byte[]{4,5,6}};
 var original=SeedPlacementOriginalCommandReview.Capture(1,2,7,3,receiptIntent);receiptIntent.Seed[0]=99;Check(original.MatchesSeedIdentity(new byte[]{4,5,6}),"receipt input detached");var copy=original.CopySeedIdentity();copy[1]=88;Check(original.MatchesSeedIdentity(new byte[]{4,5,6}),"receipt output detached");Check(!original.MatchesSeedIdentity(null)&&!original.MatchesSeedIdentity(new byte[]{4,5})&&!original.MatchesSeedIdentity(new byte[]{4,5,7}),"receipt mismatch");Check(original.Actor==7&&original.OriginalCount==3&&original.Slot==2&&original.Position.x==1&&original.Epoch==1&&original.Nonce==2,"receipt primitive fields");
 for(int kind=0;kind<8;kind++){var invalid=receiptIntent;invalid.Seed=new byte[]{1};ulong e=1,n=2;int actor=7,count=1;if(kind==0)e=0;if(kind==1)n=0;if(kind==2)actor=0;if(kind==3)count=0;if(kind==4)invalid.Slot=-1;if(kind==5)invalid.Seed=null;if(kind==6)invalid.Seed=new byte[0];if(kind==7)invalid.Seed=new byte[8193];bool refused=false;try{SeedPlacementOriginalCommandReview.Capture(e,n,actor,count,invalid);}catch(InvalidDataException){refused=true;}Check(refused,"receipt invalid shape"+kind);}
 var uncertain=new SeedPlacementHostOutcomeReview(original,SeedPlacementHostDispositionReview.NativeExecutionUncertain,new BlockValue(999,3),0,0);Check(uncertain.ObservedBlock.rawData==999&&uncertain.PlantIncarnation==0,"uncertain partial target allowed no commit");
 var committed=new SeedPlacementHostOutcomeReview(original,SeedPlacementHostDispositionReview.Committed,new BlockValue(42,12),5,6);Check(committed.PlantIncarnation==5&&committed.StateRevision==6&&object.ReferenceEquals(committed.Original,original),"valid committed identity");
 for(int kind=0;kind<4;kind++){bool refused=false;try{new SeedPlacementHostOutcomeReview(original,SeedPlacementHostDispositionReview.Committed,new BlockValue(kind==2?43u:42u,kind==3?13:12),kind==0?0UL:5UL,kind==1?0UL:6UL);}catch(InvalidDataException){refused=true;}Check(refused,"committed invalid"+kind);}
 Check(SeedPlacementIntentCodecReview.MatchesChange(intent,packet.blockChanges[0]),"full native change");packet.blockChanges[0].density++;Check(!SeedPlacementIntentCodecReview.MatchesChange(intent,packet.blockChanges[0]),"density bound");packet.blockChanges[0].density--;packet.blockChanges[0].bUpdateLight=false;Check(!SeedPlacementIntentCodecReview.MatchesChange(intent,packet.blockChanges[0]),"light bound");packet.blockChanges[0].bUpdateLight=true;
 seed.TextureFullArray=new TextureFullArray{values=new long[]{777}};Check(SeedPlacementIntentCodecReview.TryRead(SeedPlacementIntentCodecReview.Write(2,3,new Vector3i(1,2,3),new BlockValue(42,12),new BlockValue(9,0),seed),out intent)&&intent.Flags==33&&intent.Texture==777&&intent.Density==0,"native texture route");var textured=new BlockChangeInfo{bChangeBlockValue=true,bChangeTexture=true,blockValue=new BlockValue(42,12),textureFull=new TextureFullArray{values=new long[]{777}}};Check(SeedPlacementIntentCodecReview.MatchesChange(intent,textured),"texture exact");textured.textureFull.values[0]=778;Check(!SeedPlacementIntentCodecReview.MatchesChange(intent,textured),"texture mismatch");
 seed.TextureFullArray=new TextureFullArray();world.remote=false;world.block=new BlockValue(9,0);var forged=intent;byte[] plainIdentity;Check(SeedPlacementIntentCodecReview.TrySeedIdentity(seed,out plainIdentity),"plain host seed");forged.Seed=plainIdentity;textured.textureFull.values[0]=777;textured.changedByEntityId=7;textured.blockValueRef=new BlockValueRef(new Vector3i(1,2,3));var forgedPacket=new NetPackageSetBlock{blockChanges=new List<BlockChangeInfo>{textured}};Check(!SeedPlacementIntentCodecReview.ValidateSenderSnapshot(sender,world,gm,forged,forgedPacket),"both intent+packet forged texture versus native seed route");
 var nativeIntent=receiptIntent;nativeIntent.Seed=new byte[]{4,5,6};nativeIntent.Flags=0x15;nativeIntent.Density=-127;nativeIntent.Texture=999;var densityReceipt=SeedPlacementOriginalCommandReview.Capture(1,2,7,3,nativeIntent);Check(densityReceipt.Flags==0x15&&densityReceipt.Density==-127&&densityReceipt.Texture==0,"receipt native density normalized texture");nativeIntent.Flags=0x21;nativeIntent.Density=123;nativeIntent.Texture=987654321;var textureReceipt=SeedPlacementOriginalCommandReview.Capture(1,2,7,3,nativeIntent);Check(textureReceipt.Flags==0x21&&textureReceipt.Density==0&&textureReceipt.Texture==987654321,"receipt native texture normalized density");nativeIntent.Flags=0x11;nativeIntent.Density=123;nativeIntent.Texture=987654321;var lightReceipt=SeedPlacementOriginalCommandReview.Capture(1,2,7,3,nativeIntent);Check(lightReceipt.Flags==0x11&&lightReceipt.Density==0&&lightReceipt.Texture==0,"receipt native light absent fields");nativeIntent.Flags=0;nativeIntent.Seed[0]=88;Check(textureReceipt.Flags==0x21&&textureReceipt.Texture==987654321&&textureReceipt.MatchesSeedIdentity(new byte[]{4,5,6}),"receipt input mutation primitives+seed");var textureCopy=textureReceipt.CopySeedIdentity();textureCopy[2]=44;Check(textureReceipt.MatchesSeedIdentity(new byte[]{4,5,6}),"receipt output copy v2");
 seed.TextureFullArray=new TextureFullArray();var countFrame=SeedPlacementIntentCodecReview.Write(2,17,new Vector3i(1,2,3),new BlockValue(42,12),new BlockValue(9,0),seed);Check(SeedPlacementIntentCodecReview.TryRead(countFrame,out intent)&&intent.OriginalCount==17,"original client count preserved");world.remote=false;world.block=new BlockValue(9,0);Check(SeedPlacementIntentCodecReview.ValidateSenderSnapshot(sender,world,gm,intent,packet)&&held.holdingItemStack.count==1,"server snapshot count differs allowed");
 foreach(int count in new[]{0,-1,1000001,int.MaxValue}){bool refused=false;try{SeedPlacementIntentCodecReview.Write(2,count,new Vector3i(1,2,3),new BlockValue(42,12),new BlockValue(9,0),seed);}catch(InvalidDataException){refused=true;}Check(refused,"original count write refusal");var badCount=(byte[])countFrame.Clone();var countBytes=BitConverter.GetBytes(count);Array.Copy(countBytes,0,badCount,5,4);Check(!SeedPlacementIntentCodecReview.TryRead(badCount,out intent),"original count read refusal");}
 var maxCount=SeedPlacementIntentCodecReview.Write(2,1000000,new Vector3i(1,2,3),new BlockValue(42,12),new BlockValue(9,0),seed);Check(SeedPlacementIntentCodecReview.TryRead(maxCount,out intent)&&intent.OriginalCount==1000000,"count proposed ceiling accepted");
 world.remote=false;world.block=new BlockValue(9,0);packet.persistentPlayerId=sender.PlatformId;packet.localPlayerThatChanged=7;var terminalOwner=new PacketOwnerDouble();terminalOwner.Admission.ThrowDispose=true;NetPackageRebirthSeedPlacementReview.Owner=terminalOwner;var hostEnvelope=new NetPackageRebirthSeedPlacementReview().Setup(packet,1,2,SeedPlacementIntentCodecReview.Write(2,3,new Vector3i(1,2,3),new BlockValue(42,12),new BlockValue(9,0),seed));hostEnvelope.Sender=sender;int executeBefore=NetPackageSetBlock.Executions;bool disposeThrown=false;try{hostEnvelope.ProcessPackage(world,gm);}catch(InvalidOperationException){disposeThrown=true;}Check(disposeThrown&&terminalOwner.Terminal==1&&terminalOwner.Admission.Disposed==1&&terminalOwner.Admission.Completed==1&&NetPackageSetBlock.Executions==executeBefore+1,"dispose throws still terminal exactlyonce nativeonce");
 var originalOwner=new PacketOwnerDouble{ReplaceOwner=true,Replacement=new PacketOwnerDouble()};NetPackageRebirthSeedPlacementReview.Owner=originalOwner;hostEnvelope.ProcessPackage(world,gm);Check(originalOwner.Terminal==1&&originalOwner.Replacement.Terminal==0&&originalOwner.Admits==1,"static owner replacement uses captured original");Check(NetPackageSetBlock.Executions==executeBefore+2,"replacement no extra executor");
 var terminal=new TerminalDouble();var actualOwner=new SeedPlacementHostOwnerReview(terminal,new HostPolicyDouble());NetPackageRebirthSeedPlacementReview.Owner=actualOwner;world.remote=false;world.block=new BlockValue(9,0);held.holdingItemItemValue=seed;var actualIntent=SeedPlacementIntentCodecReview.Write(2,3,new Vector3i(1,2,3),new BlockValue(42,12),new BlockValue(9,0),seed);var actualEnvelope=new NetPackageRebirthSeedPlacementReview().Setup(packet,55,66,actualIntent);actualEnvelope.Sender=sender;held.SelectedSlot=3;int executions=NetPackageSetBlock.Executions,reserved=SeedPlacementSessionAuthority.Reservations;actualEnvelope.ProcessPackage(world,gm);Check(SeedPlacementSessionAuthority.Reservations==reserved+1&&NetPackageSetBlock.Executions==executions&&terminal.Count==1,"actual owner bad snapshot reserves no execution terminal");held.SelectedSlot=2;actualEnvelope.ProcessPackage(world,gm);Check(SeedPlacementSessionAuthority.Reservations==reserved+1&&NetPackageSetBlock.Executions==executions&&terminal.Count==2,"corrected snapshot same nonce rejected no executor");
 Check(owner.Capture!=null&&object.ReferenceEquals(owner.Capture.Original,owner.Original)&&owner.Original.OriginalCount==1&&owner.Prediction,"same retained private original+count");var nativeCopy=owner.Capture.CopyNativeBytes();var expectedNative=StreamUtils.ToBlob(packet.write).ToArray();Check(nativeCopy.SequenceEqual(expectedNative),"captured exact native Setup bytes");nativeCopy[0]++;Check(owner.Capture.CopyNativeBytes().SequenceEqual(expectedNative),"native byte output alias detached");var commandCopy=owner.Capture.CopyIntentBytes();commandCopy[0]++;Check(owner.Capture.CopyIntentBytes()[0]==3,"intent byte output alias detached");
 world.remote=true;world.block=new BlockValue(9,0);var refusedRetain=new ClientOwnerDouble{AllowRetain=false};SeedPlacementClientScopeReview.ClientOwner=refusedRetain;using(var noScope=SeedPlacementClientScopeReview.Begin(data,new BlockPlacement.Result{blockPos=new Vector3i(1,2,3)},new BlockValue(42,12))){Check(noScope==null&&refusedRetain.Marks==0&&refusedRetain.Binds==0,"retention false no specialized scope");}
 var refusedBind=new ClientOwnerDouble{AllowBind=false};SeedPlacementClientScopeReview.ClientOwner=refusedBind;using(var noBind=SeedPlacementClientScopeReview.Begin(data,new BlockPlacement.Result{blockPos=new Vector3i(1,2,3)},new BlockValue(42,12))){Check(noBind!=null&&noBind.BeforeNativeInvocation(),"binding setup mark");world.block=new BlockValue(42,12);NetPackageRebirthSeedPlacementReview rejectedEnvelope;bool rejected=false;try{SeedPlacementClientScopeReview.TryConsumeMatchingSend(packet,out rejectedEnvelope);}catch(InvalidDataException){rejected=true;}Check(rejected&&refusedBind.Binds==1,"bind false refuses specialized send");bool retryRejected=false;try{SeedPlacementClientScopeReview.TryConsumeMatchingSend(packet,out rejectedEnvelope);}catch(InvalidDataException){retryRejected=true;}Check(retryRejected&&refusedBind.Binds==1,"failed binding never native fallback/rebind");}Check(refusedBind.Retired==1&&!refusedBind.Sent&&refusedBind.Prediction,"bind refusal preserves terminal prediction state");
 world.block=new BlockValue(9,0);var duplicateMark=new ClientOwnerDouble();SeedPlacementClientScopeReview.ClientOwner=duplicateMark;using(var doubleScope=SeedPlacementClientScopeReview.Begin(data,new BlockPlacement.Result{blockPos=new Vector3i(1,2,3)},new BlockValue(42,12))){Check(doubleScope.BeforeNativeInvocation()&&!doubleScope.BeforeNativeInvocation()&&duplicateMark.Marks==1,"duplicate invocation cannot run second native");}
 var inputNative=new byte[]{1,2,3};var inputIntent=new byte[]{3,4,5};var copiedCapture=SeedPlacementConsumedEnvelopeReview.Capture(original,inputNative,inputIntent);inputNative[0]=99;inputIntent[0]=99;Check(copiedCapture.CopyNativeBytes()[0]==1&&copiedCapture.CopyIntentBytes()[0]==3,"capture both input aliases detached");
 world.remote=true;world.block=new BlockValue(9,0);var serializeOwner=new ClientOwnerDouble();SeedPlacementClientScopeReview.ClientOwner=serializeOwner;using(var serializeScope=SeedPlacementClientScopeReview.Begin(data,new BlockPlacement.Result{blockPos=new Vector3i(1,2,3)},new BlockValue(42,12))){Check(serializeScope.BeforeNativeInvocation(),"serialization mark");world.block=new BlockValue(42,12);NetPackageSetBlock.ThrowWrite=true;NetPackageRebirthSeedPlacementReview unavailable;bool failed=false;try{SeedPlacementClientScopeReview.TryConsumeMatchingSend(packet,out unavailable);}catch(InvalidDataException){failed=true;}NetPackageSetBlock.ThrowWrite=false;bool retry=false;try{SeedPlacementClientScopeReview.TryConsumeMatchingSend(packet,out unavailable);}catch(InvalidDataException){retry=true;}Check(failed&&retry&&serializeOwner.Binds==0,"serialize failure no second fallback/bind");}Check(serializeOwner.Retired==1&&!serializeOwner.Sent&&serializeOwner.Prediction,"serialize failure terminal flags");
 Console.WriteLine("PASS"+checks+" exact decoder; native Read bodies extracted; identity canonical bytes/adapters are explicit test doubles, not platform codec proof.");
 }
}

















