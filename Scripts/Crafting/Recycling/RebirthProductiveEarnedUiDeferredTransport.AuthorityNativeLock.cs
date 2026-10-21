using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    // Hold the original unprocessed request, not its TE response: native sends
    // that response before the separate success packet which opens the window.
    private sealed class AuthorityLockRequest:IDisposable
    {
        internal NetPackageLockTarget Packet;internal World World;internal ClientInfo Peer;internal INetConnection Connection;
        internal EntityPlayer Actor;internal string Owner,Creation;internal int Deaths;internal byte[] Payload;
        internal AuthorityNativePublication[] Sources;internal RebirthRecyclerPlacedSettlement.AccessHold[] Recycler;internal IDisposable Allocation;
        internal bool Current()=>ReferenceEquals(GameManager.Instance?.World,World)&&ReferenceEquals(Packet?.Sender,Peer)&&
            Peer.loginDone&&!Peer.disconnecting&&ReferenceEquals(Peer.netConnection?[0],Connection)&&!Connection.IsDisconnected()&&
            ReferenceEquals(World.GetEntity(Peer.entityId),Actor)&&Actor.Died==Deaths&&
            RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&
            RebirthStablePlayerIdentity.TryFromClientInfo(Peer,out var identity)&&identity.StorageKey==Owner&&
            Packet.payload!=null&&Packet.payload.Length==Payload.Length&&Packet.payload.ToArray().SequenceEqual(Payload);
        internal bool PeerRetired()=>Connection?.IsDisconnected()==true||Peer?.disconnecting==true||
            Peer?.loginDone==true&&!ReferenceEquals(Peer.netConnection?[0],Connection);
        public void Dispose(){var packet=Packet;Packet=null;Payload=null;Sources=null;Recycler=null;Actor=null;Peer=null;Connection=null;World=null;
            packet?.SendQueueHandled();Allocation?.Dispose();Allocation=null;}
    }
    private static readonly List<AuthorityLockRequest> authorityLockRequests=new List<AuthorityLockRequest>();
    // ProcessPackages explicitly frees incoming packages irrespective of the
    // send reference count. Its first cleanup is intercepted only for this
    // exact retained incoming request; Dispose later balances the sole pin.
    private static bool IsHeldAuthorityNativeRequest(NetPackage packet)=>authorityLockRequests.Any(value=>ReferenceEquals(value.Packet,packet));
    [ThreadStatic]private static NetPackageLockTarget resumingAuthorityLock;
    [HarmonyPatch(typeof(NetPackageLockTarget),nameof(NetPackageLockTarget.ProcessPackage))]
    private static class AuthorityNativeLockRequest
    {
        private static bool Prefix(NetPackageLockTarget __instance,World _world,bool __runOriginal)
        {
            if(!__runOriginal||ReferenceEquals(resumingAuthorityLock,__instance)||!ThreadManager.IsMainThread()||_world?.IsRemote()!=false||
                __instance.Sender==null||__instance.payload==null||__instance.payload.Length>65536)return true;
            AuthorityNativePublication[] sources;RebirthRecyclerPlacedSettlement.AccessHold[] recycler;
            try
            {
                lock(__instance.payload)
                {
                    long position=__instance.payload.Position;
                    try
                    {
                        using(var reader=MemoryPools.poolBinaryReader.AllocSync(false))
                        {
                            reader.SetBaseStream(__instance.payload);__instance.payload.Position=0;
                            if(!reader.ReadBoolean())return true;reader.ReadUInt16();int count=reader.ReadInt32();if(count<1||count>5)return true;
                            var targets=new List<TileEntityWorkstation>();
                            for(int i=0;i<count;i++)if(WorldAddress.Read(reader).Resolve<ILockTarget>() is TileEntityWorkstation station)targets.Add(station);
                            sources=authorityNative.Values.Where(value=>targets.Any(target=>ReferenceEquals(target,value.Station))&&!value.HasObservedNativeSnapshot).ToArray();
                            recycler=RebirthRecyclerPlacedSettlement.CaptureAccessHolds(_world,targets);
                        }
                    }
                    finally{__instance.payload.Position=position;}
                }
            }
            catch{return true;}
            if(sources.Length==0&&recycler.Length==0)return true;
            if(authorityLockRequests.Any(value=>ReferenceEquals(value.Packet,__instance)))return false;
            var peer=__instance.Sender;var connection=peer.netConnection?[0];
            if(authorityLockRequests.Count>=4||connection==null||connection.IsDisconnected()||_world.GetEntity(peer.entityId) is not EntityPlayer actor||
                !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*__instance.payload.Length+65536),out var allocation))return false;
            try
            {
                var request=new AuthorityLockRequest{Packet=__instance,World=_world,Peer=peer,Connection=connection,Actor=actor,Owner=identity.StorageKey,
                    Creation=RebirthCamperCharacterAccess.CurrentCreation(actor),Deaths=actor.Died,Payload=__instance.payload.ToArray(),Sources=sources,Recycler=recycler,Allocation=allocation};
                authorityLockRequests.Add(request);__instance.RegisterSendQueue();allocation=null;
            }
            catch{/* Exact known request remains unprocessed; no invented success. */}
            finally{allocation?.Dispose();}
            return false;
        }
    }
    private static void PumpAuthorityNativeRequests(World world)
    {
        foreach(var request in authorityLockRequests.ToArray())
        {
            if(!ReferenceEquals(request.World,world)){authorityLockRequests.Remove(request);request.Dispose();continue;}
            // Prefix positively retained this request before original Process.
            // A retired transport can drop that unused packet lease only; its
            // financial source/markers are not released or restored here.
            if(request.PeerRetired()){authorityLockRequests.Remove(request);request.Dispose();continue;}
            bool ready=false;
            try{ready=request.Current()&&request.Sources.All(source=>source.SourceCurrent()&&source.Saved)&&
                (request.Recycler==null||request.Recycler.All(source=>source.ReadyFor(request.Peer,request.Connection)));}
            catch{/* Uncertain custody retains the original request. */}
            if(!ready)continue;
            // Remove before entry: one actual native invocation, even if it
            // throws. Unknown native outcomes are never retried.
            authorityLockRequests.Remove(request);var previous=resumingAuthorityLock;resumingAuthorityLock=request.Packet;
            try{request.Packet.ProcessPackage(world,GameManager.Instance);}
            catch{/* Native outcome remains unknown; no fabricated response. */}
            finally{resumingAuthorityLock=previous;request.Dispose();}
        }
    }
    internal static void PumpSelectedNativeAccessRequests(World world)=>PumpAuthorityNativeRequests(world);
}
