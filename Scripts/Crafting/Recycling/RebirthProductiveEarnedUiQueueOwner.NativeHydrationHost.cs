using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    // A selected native packet's supplemental earlier-owner source. This is
    // not a payment, native custody, generic incarnation or saved reentry token.
    internal sealed class NativeHydrationPublication:IDisposable
    {
        private readonly World world;private readonly TileEntityWorkstation station;
        private readonly ClientInfo peer;private readonly INetConnection connection;private readonly EntityPlayer actor;
        private readonly string owner,creation;private readonly int deaths;private readonly XElement image;
        private readonly Witness[] sources;private readonly RecipeQueueItem[] rows;private readonly int[] indices,counts;
        private IDisposable[] leases;private IDisposable memory;
        private NativeHydrationPublication(World w,TileEntityWorkstation s,ClientInfo p,INetConnection c,EntityPlayer a,string key,string birth,
            Witness[] originals,RecipeQueueItem[] nativeRows,int[] slots,XElement envelope,IDisposable[] holds,IDisposable allocation)
        {world=w;station=s;peer=p;connection=c;actor=a;owner=key;creation=birth;deaths=a.Died;sources=originals;rows=nativeRows;indices=slots;
            counts=nativeRows.Select(row=>(int)row.Multiplier).ToArray();image=envelope;leases=holds;memory=allocation;}
        internal XElement Image=>new XElement(image);
        // After the proven actual enqueue this retained identity is metadata
        // only. A later purpose-owned native removal may replace row objects.
        internal bool SourceCurrent()
        {
            try{return memory!=null&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,world)&&!world.IsRemote()&&
                ReferenceEquals(world.GetTileEntity(station.ToWorldPos()),station)&&ReferenceEquals(peer.netConnection?[0],connection)&&!connection.IsDisconnected()&&
                actor.Died==deaths&&RebirthProductiveEarnedArtifact.RemoteAuthority(actor,peer,station,creation)&&
                RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity)&&identity.StorageKey==owner&&sources.All(source=>source.SourceCurrent());}
            catch{return false;}
        }
        internal bool Current()
        {
            try
            {
                if(!SourceCurrent())return false;
                for(int i=0;i<sources.Length;i++)if(indices[i]>=station.Queue.Length||!ReferenceEquals(station.Queue[indices[i]],rows[i])||
                    rows[i].Multiplier!=counts[i]||!sources[i].SourcePublished()||!sources[i].Matches(rows[i])||Binding(rows[i])!=sources[i].RecipeBinding)return false;
                return true;
            }
            catch{return false;}
        }
        public void Dispose(){if(memory==null)return;memory.Dispose();memory=null;foreach(var lease in leases)lease?.Dispose();leases=null;}
        internal static bool Capture(World world,TileEntityWorkstation station,ClientInfo peer,INetConnection connection,out NativeHydrationPublication result,RebirthProductiveEarnedObservationBudget.Decoder borrowed=null)
        {
            result=null;IDisposable allocation=null;var holds=new System.Collections.Generic.List<IDisposable>();
            try
            {
                if(!installed||!ThreadManager.IsMainThread()||world?.IsRemote()!=false||station?.Queue==null||station.Queue.Length>byte.MaxValue||
                    !ReferenceEquals(world,GameManager.Instance?.World)||!ReferenceEquals(peer?.netConnection?[0],connection)||connection==null||connection.IsDisconnected()||
                    world.GetEntity(peer.entityId) is not EntityPlayer actor||!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity))return false;
                string creation=RebirthCamperCharacterAccess.CurrentCreation(actor);
                if(!RebirthProductiveEarnedArtifact.RemoteAuthority(actor,peer,station,creation))return false;
                ObserveWorld();var node=new XElement("queueOwners",new XAttribute("version",1));
                var witnesses=new System.Collections.Generic.List<Witness>();var rows=new System.Collections.Generic.List<RecipeQueueItem>();var slots=new System.Collections.Generic.List<int>();
                using(var decoder=borrowed==null?RebirthProductiveEarnedObservationBudget.TryEnterDecoder():null)
                {
                    if(borrowed?.IsCurrent!=true&&decoder==null)return false;
                    for(int i=0;i<station.Queue.Length;i++)
                    {
                        var row=station.Queue[i];if(row?.Recipe==null||row.Multiplier<1)continue;
                        if(!TryObserveQueueOwner(station,i,row,out var source)||!MatchesSourceDefinition(source,row))continue; // unknown old rows stay unknown
                        var lease=source.RetainForCompletion();if(lease==null)return false;holds.Add(lease);
                        witnesses.Add(source);rows.Add(row);slots.Add(i);
                        node.Add(new XElement("row",new XAttribute("index",i),new XAttribute("count",row.Multiplier),new XAttribute("originalCount",source.OriginalCount),
                            new XElement("purpose",Export(source)),new XElement("binding",source.RecipeBinding)));
                    }
                }
                if(witnesses.Count==0)return false;int bytes=Encoding.UTF8.GetByteCount(node.ToString(SaveOptions.DisableFormatting));
                if(bytes>48*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*bytes+65536),out allocation))return false;
                var value=new NativeHydrationPublication(world,station,peer,connection,actor,identity.StorageKey,creation,witnesses.ToArray(),rows.ToArray(),slots.ToArray(),node,holds.ToArray(),allocation);
                if(!value.Current())return false;allocation=null;holds.Clear();result=value;return true;
            }
            catch{return false;}
            finally{allocation?.Dispose();foreach(var lease in holds)lease.Dispose();}
        }
    }
    // Routing only: a known original whose full observation is unavailable
    // must be held rather than silently delivered without its attestation.
    internal static bool HasNativeHydrationCandidate(World world,TileEntityWorkstation station)
        =>installed&&ThreadManager.IsMainThread()&&originals.Any(value=>!value.Closed&&ReferenceEquals(value.World,world)&&
            ReferenceEquals(value.Station,station)&&value.Published&&!value.RowRetired&&station.Queue!=null&&
            station.Queue.Any(row=>row?.Recipe!=null&&ReferenceEquals(row.Recipe,value.Recipe)&&row.StartingEntityId==value.StartingEntityId&&row.Multiplier>0&&row.Multiplier<=value.OriginalCount));
    internal static bool TryCaptureNativeHydration(World world,TileEntityWorkstation station,ClientInfo peer,INetConnection connection,out NativeHydrationPublication proof,RebirthProductiveEarnedObservationBudget.Decoder borrowed=null)
        =>NativeHydrationPublication.Capture(world,station,peer,connection,out proof,borrowed);
}

