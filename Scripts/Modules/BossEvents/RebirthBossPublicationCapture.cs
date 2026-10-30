using System;
using System.Security.Cryptography;
using System.Threading;

// Boss image custody only; main-thread TryPublishedEntity remains mandatory.
// Independent retention quota survives release of the shared request slot.
internal sealed class RebirthBossPublicationCapture : IDisposable
{
    private const int Limit=4;
    private static readonly object quotaGate=new object();
    private static int reservations;
    private sealed class Reservation : IDisposable
    {
        private int disposed;
        public void Dispose(){if(Interlocked.Exchange(ref disposed,1)==0)lock(quotaGate)reservations--;}
    }
    private static IDisposable Reserve()
    {lock(quotaGate){if(reservations>=Limit)return null;reservations++;return new Reservation();}}
    private sealed class Image : IDisposable
    {
        internal readonly byte[] Bytes;internal readonly long Generation;internal readonly string Digest;
        internal readonly WeakReference Carrier;
        private IDisposable allocation,reservation;
        internal Image(byte[] bytes,long generation,string digest,object carrier,IDisposable lease,IDisposable quota)
        {Bytes=bytes;Generation=generation;Digest=digest;Carrier=new WeakReference(carrier);allocation=lease;reservation=quota;}
        public void Dispose(){try{Interlocked.Exchange(ref allocation,null)?.Dispose();}finally{Interlocked.Exchange(ref reservation,null)?.Dispose();}}
    }
    private readonly RebirthStationSnapshotEvidence.Request request;
    private readonly World world;private readonly Chunk chunk;
    private readonly RebirthBossEventInstance encounter;private readonly RebirthBossSpawnOriginal original;
    private readonly Guid operation,eventId;private readonly int id,entityClass,role,x,z,removalX,removalZ;
    private readonly ulong birth;private readonly byte phase;private readonly bool removalInvoking,absent,death;
    private readonly string nativeWorld,directory,owner,nativeProof;
    private readonly Func<bool> scopeCurrent;
    private Image image;private int closed;private bool proofAttempted;private string entityProof;
    internal RebirthBossPublicationCapture(RebirthStationSnapshotEvidence.Request watch,World currentWorld,Chunk currentChunk,
        RebirthBossEventInstance currentEvent,RebirthBossSpawnOriginal currentOriginal,string saveDirectory,Func<bool> currentScope,bool absent=false,bool death=false)
    {
        this.absent=absent;this.death=death;
        scopeCurrent=currentScope??throw new ArgumentNullException(nameof(currentScope));
        request=watch??throw new ArgumentNullException(nameof(watch));world=currentWorld??throw new ArgumentNullException(nameof(currentWorld));
        chunk=currentChunk??throw new ArgumentNullException(nameof(currentChunk));encounter=currentEvent??throw new ArgumentNullException(nameof(currentEvent));
        original=currentOriginal??throw new ArgumentNullException(nameof(currentOriginal));
        operation=original.Operation;eventId=encounter.EventId;id=original.EntityId;entityClass=original.EntityClass;role=original.Role;birth=original.WorldTimeBorn;
        phase=original.Phase;nativeProof=original.NativeProof;removalInvoking=original.RemovalInvoking;removalX=original.RemovalX;removalZ=original.RemovalZ;
        x=chunk.X;z=chunk.Z;nativeWorld=world.worldState?.Guid;directory=saveDirectory;owner=encounter.Progression?.OwnerStableId;
        request.OnPublished=Published;
    }
    private void Published(long generation,string serializedDigest)
    {
        // Shared callback runs under its publication gate, possibly on the disk
        // thread. No live entities, game predicates or domain writes here.
        if(Volatile.Read(ref closed)!=0||Volatile.Read(ref image)!=null||generation<=0||serializedDigest==null||serializedDigest.Length!=64||
           request.Closed||request.Generation!=generation||!ReferenceEquals(request.World,world)||!ReferenceEquals(request.Chunk,chunk)||request.X!=x||request.Z!=z||
           !RebirthStationSnapshotEvidence.HasPublished(request)||
           request.Budget!=null&&request.Budget.MaximumSnapshotBytes>RebirthStationSnapshotEvidence.MaximumSnapshotBytes)return;
        IDisposable quota=Reserve();if(quota==null)return;
        IDisposable allocation=null;Image retained=null;
        try
        {
            if(!RebirthStationSnapshotEvidence.TryCopy(request,out var bytes,out allocation)||bytes==null||bytes.Length>RebirthStationSnapshotEvidence.MaximumSnapshotBytes)return;
            string digest;using(var hash=SHA256.Create())digest=BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
            if(!string.Equals(digest,serializedDigest,StringComparison.OrdinalIgnoreCase)||Volatile.Read(ref closed)!=0)return;
            retained=new Image(bytes,generation,digest,request.Snapshot,allocation,quota);allocation=quota=null;
            if(Interlocked.CompareExchange(ref image,retained,null)==null)retained=null;
        }
        finally{try{allocation?.Dispose();}finally{try{quota?.Dispose();}finally{retained?.Dispose();}}}
    }
    internal bool Superseded
    {get{var retained=Volatile.Read(ref image);return request.Closed||retained!=null&&
        (request.Generation!=retained.Generation||!ReferenceEquals(request.Snapshot,retained.Carrier.Target));}}
    internal bool TryRead(World currentWorld,Chunk currentChunk,RebirthBossEventInstance currentEvent,RebirthBossSpawnOriginal currentOriginal,out byte[] bytes)
    {
        bytes=null;var retained=Volatile.Read(ref image);
        if(retained==null||Superseded||!ScopeValid||!ReferenceEquals(world,currentWorld)||!ReferenceEquals(chunk,currentChunk)||
           !ReferenceEquals(encounter,currentEvent)||!ReferenceEquals(original,currentOriginal))return false;
        bytes=retained.Bytes;return true;
    }
    internal bool ScopeValid
    {
        get
        {
        RebirthBossEventInstance registered;
        if(!ThreadManager.IsMainThread()||Volatile.Read(ref closed)!=0||!ReferenceEquals(GameManager.Instance?.World,world)||world.worldState?.Guid!=nativeWorld||GameIO.GetSaveGameDir()!=directory||
           chunk.X!=x||chunk.Z!=z||encounter.EventId!=eventId||encounter.Progression?.OwnerStableId!=owner||
           !RebirthBossEventRegistry.TryGet(eventId,out registered)||!ReferenceEquals(registered,encounter)||encounter.SpawnOriginals.Count>580||!encounter.SpawnOriginals.Contains(original)||
           original.Operation!=operation||original.EntityId!=id||original.EntityClass!=entityClass||original.Role!=role||original.WorldTimeBorn!=birth||
           original.Phase!=phase||original.NativeProof!=nativeProof||original.Abandoned||original.NativeRemoved||original.RemovalInvoking!=removalInvoking||
           original.RemovalX!=removalX||original.RemovalZ!=removalZ)return false;
        try{return scopeCurrent();}catch{return false;}
        }
    }
    internal bool TryProof(World currentWorld,Chunk currentChunk,RebirthBossEventInstance currentEvent,RebirthBossSpawnOriginal currentOriginal,out string proof)
    {
        proof=null;
        if(!TryRead(currentWorld,currentChunk,currentEvent,currentOriginal,out var bytes)||death&&!original.Killed)return false;
        if(!proofAttempted)
        {
            proofAttempted=true;
            RebirthBossNativePublication.TryPublishedEntity(bytes,x,z,original,RebirthBossNativePublication.Key(original),
                RebirthBossNativePublication.Stamp(world,encounter,original),out entityProof,absent,death);
        }
        proof=entityProof;return proof!=null;
    }
    public void Dispose()
    {
        if(Interlocked.Exchange(ref closed,1)!=0)return;
        try{request.Dispose();}finally{Interlocked.Exchange(ref image,null)?.Dispose();}
    }
}
