using System;
using System.Collections.Generic;
using System.Linq;

// Exact placed target claims, separate from source endpoint leases. No owner
// setters or queue publication are authorized by a claim. Production remains
// gated until all native/client guards and durable recovery are installed.
internal static class RebirthRemoteResourcePlacedStationCustody
{
    private const int MaximumClaims=32;
    private const long MaximumRetainedBeforeBytes=32L*1024*1024;
    private static readonly Dictionary<Guid,Claim> claims=new Dictionary<Guid,Claim>();
    private static long retained;
    private static World world;private static string directory;
    internal sealed class Claim
    {
        internal readonly Guid Source,Job;internal readonly string Owner,Binding,Birth,Creation;
        internal readonly int Deaths;internal readonly World World;internal readonly TileEntityWorkstation Station;
        internal readonly uint OriginalBlockRaw;
        private readonly Chunk chunk;private readonly Vector3i position;private readonly string save;
        private readonly byte[] before;
        internal Claim(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceClientOriginal.Intent original,
            TileEntityWorkstation station,string birth,uint originalBlock,byte[] image)
        {Source=root.Operation;Job=original.Operation;Owner=root.Owner;Binding=root.Binding;Creation=original.Creation;Deaths=original.Deaths;
            World=actor.world;Station=station;Birth=birth;OriginalBlockRaw=originalBlock;chunk=station.GetChunk();position=station.ToWorldPos();save=GameIO.GetSaveGameDir();before=image;}
        internal int RetainedBytes=>before.Length;
        // Guard scope deliberately survives loss of transient owner refs. A
        // disconnected actor never turns a paid exclusive target into public
        // mutable inventory. Native unload itself is not blocked.
        internal bool TargetCurrent()=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&World.IsRemote()==false&&
            GameIO.GetSaveGameDir()==save&&chunk!=null&&!chunk.InProgressUnloading&&ReferenceEquals(World.GetChunkSync(chunk.X,chunk.Z),chunk)&&
            ReferenceEquals(World.GetTileEntity(position),Station)&&ReferenceEquals(Station.GetChunk(),chunk)&&
            RebirthLogisticsEndpointIdentity.TryGetIncarnation(World,Station,out string birth)&&birth==Birth;
        internal bool Matches(RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceClientOriginal.Intent original)=>root!=null&&original!=null&&
            root.Operation==Source&&root.Owner==Owner&&root.Binding==Binding&&original.Operation==Job&&original.Creation==Creation&&original.Deaths==Deaths;
        internal bool ExactBefore()=>TargetCurrent()&&World.GetBlock(position).rawData==OriginalBlockRaw&&RebirthRemoteResourcePlacedStationCandidate.TrySerialize(Station,Station.Input,Station.Queue,Station.IsBurning,out var image,out _)&&image.SequenceEqual(before);
        internal byte[] CopyBefore()=>(byte[])before.Clone();
        internal long BeforeBytes=>before.LongLength;
        internal bool AllowsNativeLockActor(int entityId)
        {
            if(!TargetCurrent()||!(World.GetEntity(entityId) is EntityPlayer actor)||!ReferenceEquals(actor.world,World)||actor.IsDead()||!actor.IsSpawned()||actor.Died!=Deaths||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||owner.StorageKey!=Owner||!RebirthOriginalActionScope.Matches(actor,owner.StorageKey,Creation))return false;
            // This only narrows access; the native lock/security pipeline
            // still runs. It grants no original item or queue permission.
            return true;
        }
    }
    private static bool Scope()
    {
        if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false)return false;
        var current=GameManager.Instance.World;string save=GameIO.GetSaveGameDir();if(string.IsNullOrEmpty(save))return false;
        if(!ReferenceEquals(world,current)||directory!=save)
        {
            // Transient references only. No original artifact/journal/claim is
            // deleted or declared settled; cold recovery must rebind positively.
            claims.Clear();retained=0;world=current;directory=save;
        }
        return true;
    }
    internal static bool Blocks(TileEntityWorkstation station)
    {return Scope()&&station!=null&&(RebirthRemoteResourcePlacedStationOriginal.CancellationBlocks(station)||claims.Values.Any(claim=>ReferenceEquals(claim.Station,station)&&claim.TargetCurrent()));}
    internal static bool TryReadGuardClaim(TileEntityWorkstation station,out Claim claim)
    {claim=null;if(!Scope()||station==null)return false;claim=claims.Values.SingleOrDefault(value=>ReferenceEquals(value.Station,station)&&value.TargetCurrent());return claim!=null;}
    internal static bool ReleaseUsed(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceClientOriginal.Intent original,string proof)
    {
        if(!Scope()||!RebirthRemoteResourcePlacedStationOriginal.HasSavedRetirement(actor,root,original,proof))return false;
        if(!claims.TryGetValue(root.Operation,out var claim))return true;
        if(!claim.Matches(root,original))return false;
        claims.Remove(root.Operation);retained-=claim.BeforeBytes;return true;
    }
    internal static bool ReleaseNeverEntered(RebirthRemoteResourcePlacedStationOriginal.NeverEnteredTerminal terminal)
    {
        if(!Scope()||terminal?.Current!=true)return false;
        if(!claims.TryGetValue(terminal.Root.Operation,out var claim))return true;
        if(!claim.Matches(terminal.Root,terminal.Original))return false;
        claims.Remove(terminal.Root.Operation);retained-=claim.BeforeBytes;return true;
    }
    internal static bool TryReserveBeforeSources(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,
        RebirthRemoteResourceClientOriginal.Intent original,out Claim claim)
    {
        claim=null;
        try
        {
            // No caller uses this until the complete guard/recovery installer
            // explicitly qualifies the domain. Merely creating a claim would
            // not establish native exclusivity without those guards.
            if(!Scope()||!RebirthRemoteResourcePlacedStationGuards.IsInstalled||root==null||original==null||root.Phase>1||root.HasOwnerAction||!root.SourcesUnattempted||
                !RebirthRemoteResourcePlacedStationOriginal.AllowsReviewedPlacedMode||world.worldState.Guid!=original.World||!RemoteResourceWorldIdentity.TryGet(world,out string sourceWorld)||sourceWorld!=root.World||
                !RebirthRemoteResourceDurableJournal.NeverPreparedOwnerAction(root)||
                !RebirthRemoteResourcePlacedStationOriginal.TryReadAdmission(root,out var admitted)||admitted.Digest!=original.Digest||
                !RebirthRemoteResourcePlacedStationOriginal.ValidateInput(original,out var position,out uint block,out _,out _,out _,out _,out _,out _,out _,out _,out _)||
                actor?.world!=world||actor.IsDead()||!actor.IsSpawned()||actor.Died!=original.Deaths||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||owner.StorageKey!=root.Owner||owner.CanonicalId!=original.Owner||
                !RebirthOriginalActionScope.Matches(actor,owner.StorageKey,original.Creation)||
                !RebirthRemoteResourcePlacedStationScope.TryResolveLayout(actor,position,original.Creation,out var station,out _,out _,out _)||
                world.GetBlock(position).rawData!=block||!RebirthRemoteResourcePlacedStationIdentity.TryRead(actor,original,root.Operation,station,out string birth))return false;
            if(claims.TryGetValue(root.Operation,out var old))
            {if(!old.Matches(root,original)||!ReferenceEquals(old.Station,station)||old.Birth!=birth||!old.ExactBefore())return false;claim=old;return true;}
            if(claims.Count>=MaximumClaims||Blocks(station)||RebirthRemoteResourceSourceCustody.HasForeignPlacedStationHold(station,root.Owner,root.Operation,root.Binding)||
                RebirthCookingRemoteAdmissionBackend.Held(station)||
                !RebirthCamperEnergySavedOwner.TryRead(owner,out var actual,out var saved)||saved.id!=actor.entityId||saved.deaths!=original.Deaths||
                !actual.Matches(RebirthRemoteResourceClientOriginal.ReadActor(original.Copy().Element("actorBefore").Value))||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,RebirthRemoteResourceOwnerAction.Receipt(original.Operation),out float phase,out bool present)||!present||phase!=10f||
                !RebirthRemoteResourcePlacedStationCandidate.TrySerialize(station,station.Input,station.Queue,station.IsBurning,out var before,out _ )||retained>MaximumRetainedBeforeBytes-before.LongLength)return false;
            var value=new Claim(actor,root,original,station,birth,block,before);
            if(!value.TargetCurrent())return false;
            claims.Add(root.Operation,value);retained+=before.LongLength;claim=value;return true;
        }
        catch{claim=null;return false;}
    }
}
