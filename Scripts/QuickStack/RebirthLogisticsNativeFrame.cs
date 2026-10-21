using System;
using System.IO;

// Exact existing journal spawn text, not a second source of inventory truth.
// The XML/base64 parse graph is bounded by the existing journal codec. Binary
// ECD arrays are materialized one frame at a time under the original account.
internal sealed class RebirthLogisticsNativeFrame
{
    internal readonly string Encoded;
    internal int Length=>length;
    private readonly string owner,world,binding;private readonly Guid operation;private readonly int length;
    private RebirthLogisticsNativeFrame(string owner,Guid operation,string world,string binding,string encoded)
    {
        this.owner=owner;this.operation=operation;this.world=world;this.binding=binding;Encoded=encoded;
        if(string.IsNullOrEmpty(encoded)||encoded.Length>174764||(encoded.Length&3)!=0)throw new InvalidDataException();
        int padding=encoded.EndsWith("==",StringComparison.Ordinal)?2:encoded.EndsWith("=",StringComparison.Ordinal)?1:0;
        for(int i=0;i<encoded.Length-padding;i++){char c=encoded[i];if(!(c>='A'&&c<='Z'||c>='a'&&c<='z'||c>='0'&&c<='9'||c=='+'||c=='/'))throw new InvalidDataException();}
        length=encoded.Length/4*3-padding;if(length<1||length>131072)throw new InvalidDataException();
    }
    internal bool Matches(string owner,Guid operation,string world,string binding)
        =>this.owner==owner&&this.operation==operation&&this.world==world&&this.binding==binding;
    internal static RebirthLogisticsNativeFrame Read(string owner,Guid operation,string world,string binding,string encoded)
        =>encoded==null?null:new RebirthLogisticsNativeFrame(owner,operation,world,binding,encoded);
    internal static RebirthLogisticsNativeFrame Capture(RebirthLogisticsTransferJournal.Record original,byte[] bytes)
    {if(bytes==null)return null;if(bytes.Length<1||bytes.Length>131072||original==null)throw new InvalidDataException();return Read(original.Owner,original.Operation,original.World,RebirthLogisticsTransferJournal.ImmutablePlanDigest(original),Convert.ToBase64String(bytes));}
    internal sealed class Materialization:IDisposable
    {
        internal byte[] Bytes;private IDisposable allocation,decoder;private RebirthLogisticsSnapshotReservation account;
        internal Materialization(RebirthLogisticsSnapshotReservation account,IDisposable allocation,IDisposable decoder,byte[] bytes){this.account=account;this.allocation=allocation;this.decoder=decoder;Bytes=bytes;}
        // Call only after native decode/validation temporaries are no longer
        // retained. The original bytes remain charged until this lease ends.
        internal void FinishDecode(){decoder?.Dispose();decoder=null;}
        public void Dispose(){Bytes=null;decoder?.Dispose();decoder=null;allocation?.Dispose();allocation=null;account?.Dispose();account=null;}
    }
    [ThreadStatic]private static ReadScope active;
    internal sealed class ReadScope:IDisposable
    {
        private readonly ReadScope previous;private readonly bool owns;private bool closed;
        internal readonly RebirthLogisticsSnapshotReservation Account;internal readonly string Owner,World,Binding;internal readonly Guid Operation;private readonly global::World nativeWorld;
        internal ReadScope(RebirthLogisticsSnapshotReservation account,bool owns,string owner,Guid operation,string world,string binding,global::World nativeWorld)
        {previous=active;this.owns=owns;Account=account;Owner=owner;Operation=operation;World=world;Binding=binding;this.nativeWorld=nativeWorld;active=this;}
        internal bool Matches(string owner,Guid operation,string world,string binding)=>!closed&&ReferenceEquals(GameManager.Instance?.World,nativeWorld)&&Owner==owner&&Operation==operation&&World==world&&Binding==binding;
        public void Dispose(){if(closed)return;closed=true;if(ReferenceEquals(active,this))active=previous;if(owns)Account.Dispose();}
    }
    // One synchronous codec action rederives the saved original once, then
    // streams frames under that same readonly accounting context. No yielding,
    // actor permission, inventory setter or file-publication grant exists here.
    internal static ReadScope BeginRead(string owner,Guid operation,string world,string binding)
    {
        RebirthLogisticsSnapshotReservation account=null;
        try
        {
            var nativeWorld=GameManager.Instance?.World;
            if(active?.Matches(owner,operation,world,binding)==true)return new ReadScope(active.Account,false,owner,operation,world,binding,nativeWorld);
            if(nativeWorld?.IsRemote()!=false||RebirthLogisticsTransferJournal.WorldKey(nativeWorld)!=world||!RebirthLogisticsTransferJournal.TryRead(owner,operation,out var original)||
                original.World!=world||RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)!=binding)return null;
            account=RebirthLogisticsSnapshotReservation.TryOpenRetainedRead(original,()=>ReferenceEquals(GameManager.Instance?.World,nativeWorld));
            if(account==null)return null;var result=new ReadScope(account,true,owner,operation,world,binding,nativeWorld);account=null;return result;
        }
        catch{return null;}
        finally{account?.Dispose();}
    }
    internal Materialization Open()
    {
        RebirthLogisticsSnapshotReservation account=null;bool owns=false;IDisposable allocation=null,decoder=null;
        try
        {
            var currentWorld=GameManager.Instance?.World;
            if(active?.Matches(owner,operation,world,binding)==true)account=active.Account;
            else
            {
                if(currentWorld?.IsRemote()!=false||world!=RebirthLogisticsTransferJournal.WorldKey(currentWorld)||
                    !RebirthLogisticsTransferJournal.TryRead(owner,operation,out var original)||original.World!=world||RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)!=binding)return null;
                account=RebirthLogisticsSnapshotReservation.TryOpenRetainedRead(original,()=>ReferenceEquals(GameManager.Instance?.World,currentWorld)&&RebirthLogisticsTransferJournal.WorldKey(currentWorld)==world);owns=true;
            }
            if(account==null||!account.TryReserveAllocation(length,out allocation)||!account.TryReserveAllocation(8L*length,out decoder))return null;
            // Native strict validation performs bounded nested read/copy passes;
            // reserve their serialized byte workspace dynamically from this frame.
            // Length is derived from this actual canonical retained text, not
            // a network header; reserve BEFORE the binary decoder allocation.
            var bytes=Convert.FromBase64String(Encoded);if(bytes.Length!=length||Convert.ToBase64String(bytes)!=Encoded)return null;
            var result=new Materialization(owns?account:null,allocation,decoder,bytes);account=null;allocation=null;decoder=null;return result;
        }
        catch{return null;}
        finally{decoder?.Dispose();allocation?.Dispose();if(owns)account?.Dispose();}
    }
}
