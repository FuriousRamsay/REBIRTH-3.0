using System;
using System.Collections.Generic;
using System.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    private sealed class Removal:IDisposable
    {
        internal RecipientScope Scope;internal byte[] Body;internal int Offset;
        internal OwnerPds.SavedOwnerFile Owner;
        internal RebirthRemoteResourceNativePublication.Watch Watch;
        internal IDisposable Allocation;
        internal readonly List<IDisposable> GroupRetentions=new List<IDisposable>();
        internal HostTerminalPublication GroupTerminal;
        internal bool HasReadFence;internal long ReadFence;
        internal bool SourceCurrent()=>Scope.Current()&&Owner.Current()&&Body!=null;
        internal bool Current()
        {
            if(!SourceCurrent()||HasReadFence&&(!TryReadNativeObservationFence(Scope.World,Scope.Station,out var fence)||fence!=ReadFence))return false;
            using(var callback=Scope.Source.Memory.TryEnterCallbacks())
                return callback!=null&&SourceCurrent()&&RebirthProductiveEarnedNativeBody.MatchesCurrent(Scope.Station,Body,Offset);
        }
        public void Dispose(){Watch?.Dispose();Watch=null;Body=null;GroupTerminal=null;foreach(var lease in GroupRetentions)lease.Dispose();GroupRetentions.Clear();Allocation?.Dispose();Allocation=null;}
    }
    private sealed class RemovalRead
    {
        internal Removal Original;internal NetPackageTileEntity Packet;internal World World;
        internal ulong Elapsed;internal int Reads;internal bool Completed;internal long ReadFence;internal IDisposable Allocation;
    }
    private static readonly Dictionary<Guid,Removal> removals=new Dictionary<Guid,Removal>();
    [ThreadStatic]private static RemovalRead readingRemoval;

    // Called only after the original recipient's actual native Check/GiveExp
    // frame has been bound and its actual native owner file has been saved.
    // The expected body is derived from the saved station original; callers
    // cannot supply a replacement body or authorize another queue/grid change.
    private static bool ArmRemoval(RecipientScope scope)
    {
        if(scope?.Current()!=true||scope.ExpectedAfter==null||!OwnerPds.TryReadAfter(scope,out var owner)||!owner.Current())return false;
        if(removals.TryGetValue(scope.Operation,out var old))return ReferenceEquals(old.Scope,scope)&&old.SourceCurrent();
        var original=scope.Source;
        byte[] body;int offset;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null||removals.Count>=4||!RebirthProductiveEarnedNativeBody.TryRead(scope.Hydration.NativeBefore,scope.Hydration.BeforeOffset,out var image,out ulong elapsed))return false;
            var selected=image.Completions.Where(value=>scope.Hydration.Sources.Any(source=>Row(value).SequenceEqual(source.Aggregate))).ToArray();
            if(selected.Length!=scope.Hydration.Sources.Length||selected.Any(value=>value.CrafterEntityID!=scope.Actor.entityId))return false;
            image.Completions=image.Completions.Where(value=>!selected.Any(old=>ReferenceEquals(value,old))).ToArray();
            body=RebirthProductiveEarnedNativeBody.Encode(image,elapsed,out offset);
        }
        var allocation=original.Memory.TryObserveNative(body,offset);if(allocation==null)return false;
        removals.Add(scope.Operation,new Removal{Scope=scope,Owner=owner,Body=body,Offset=offset,Allocation=allocation});return true;
    }
    internal static bool TryArmKnownRemoval(RecipientScope scope)=>ArmRemoval(scope);
    internal static bool EnterNativeUpload(NetPackageTileEntity packet,World world,bool run,out object state)
    {
        state=null;if(!run||world?.IsRemote()!=false)return true;
        try
        {
            var matches=removals.Values.Where(value=>ReferenceEquals(value.Scope.World,world)&&value.Scope.Station.ToWorldPos().Equals(packet.teWorldPos)).ToArray();
            if(matches.Length==0)return true;
            if(matches.Length!=1||readingRemoval!=null)return false;
            var source=matches[0];
            if(!source.SourceCurrent()||!ReferenceEquals(source.Scope.Peer,packet.Sender)||packet.ms==null||packet.ms.Length!=source.Body.Length||
                world.GetBlock(packet.teWorldPos).type!=packet.teBlockId)return false;
            var allocation=source.Scope.Source.Memory.TryEnterCallbacks();if(allocation==null)return false;
            try
            {
                var raw=new byte[(int)packet.ms.Length];
                lock(packet.ms){long prior=packet.ms.Position;try{packet.ms.Position=0;int read=0;while(read<raw.Length){int n=packet.ms.Read(raw,read,raw.Length-read);if(n<1)return false;read+=n;}}finally{packet.ms.Position=prior;}}
                if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(raw,source.Offset,source.Body,source.Offset))return false;
                if(!RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Scope.Station,source.Scope.Hydration.NativeBefore,source.Scope.Hydration.BeforeOffset)&&
                    !RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Scope.Station,source.Body,source.Offset))return false;
                var captured=new RemovalRead{Original=source,Packet=packet,World=world,Elapsed=BitConverter.ToUInt64(raw,source.Offset),Allocation=allocation};
                readingRemoval=captured;state=captured;allocation=null;return true;
            }
            finally{allocation?.Dispose();}
        }
        catch{return false;}
    }
    internal static void EnterNativeRead(TileEntityWorkstation station,PooledBinaryReader reader,StreamModeRead mode,bool run)
    {
        EnterHydrationNativeRead(station,reader,mode,run);
        var value=readingRemoval;
        if(run&&value!=null&&mode==StreamModeRead.FromClient&&ReferenceEquals(station,value.Original.Scope.Station)&&ReferenceEquals(reader.BaseStream,value.Packet.ms))value.Reads++;
    }
    internal static void ObserveNativeRead(PooledBinaryReader reader,bool run)
    {
        ObserveHydrationNativeRead(reader,run);
        var value=readingRemoval;
        if(run&&value!=null&&value.Reads==1&&ReferenceEquals(reader.BaseStream,value.Packet.ms)&&reader.BaseStream.Position==value.Packet.ms.Length&&
            TryGetObservedNativeReadFence(value.Original.Scope.Station,out value.ReadFence))value.Completed=true;
    }
    internal static void ObserveNativeUpload(NetPackageTileEntity packet,World world,bool run,object state)
    {
        if(state is not RemovalRead value)return;
        value.Allocation?.Dispose();value.Allocation=null;
        try
        {
            var original=value.Original;
            if(!run||!ReferenceEquals(readingRemoval,value)||!ReferenceEquals(value.Packet,packet)||!ReferenceEquals(world,value.World)||
                !original.SourceCurrent()||value.Reads!=1||!value.Completed||!ReferenceEquals(world.GetTileEntity(packet.teWorldPos),original.Scope.Station)||
                GameTimer.Instance.ticks<value.Elapsed||original.Scope.Station.lastTickTime!=GameTimer.Instance.ticks-value.Elapsed)return;
            if(!TryReadNativeObservationFence(world,original.Scope.Station,out var observed)||observed!=value.ReadFence)return;
            original.ReadFence=value.ReadFence;original.HasReadFence=true;
            if(!original.Current())return;
            if(original.Watch!=null)return;
            if(!RebirthRemoteResourceNativeBinding.TryResolve(world,RemoteResourceIdentity.Workstation(packet.teWorldPos),RemoteResourceSourceKind.WorkstationOutput,
                original.Scope.CustodyBirth.ToString("N"),out var binding))return;
            original.Watch=RebirthRemoteResourceNativePublication.BeginWithBudget(binding,original.Current,original.Scope.Source.Memory);
        }
        catch{/* An uncertain native callback cannot publish a terminal. */}
    }
    internal static void EndNativeUpload(object state)
    {
        if(state is not RemovalRead value)return;
        value.Allocation?.Dispose();value.Allocation=null;if(ReferenceEquals(readingRemoval,value))readingRemoval=null;
    }
}
