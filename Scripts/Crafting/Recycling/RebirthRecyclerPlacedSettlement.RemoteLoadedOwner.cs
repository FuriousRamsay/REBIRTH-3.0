using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    // Host-side original native primary Load/Read -> actual remote actor
    // ToPlayer. Client join hydration is a different producer and never uses
    // this token as a substitute for its own owning body event.
    internal sealed class RemoteLoadedOwner
    {
        internal readonly LoadedOwner File;internal readonly EntityPlayer Actor;internal string Creation;internal readonly int Deaths;
        internal bool Returned,Entering;
        private readonly Dictionary<LoadedStation,Guid> generations=new Dictionary<LoadedStation,Guid>();
        private readonly Dictionary<Guid,Guid> reconnectGenerations=new Dictionary<Guid,Guid>();
        internal Guid CaptureReconnectGeneration(Guid operation)
        {if(!Current()||operation==Guid.Empty)return Guid.Empty;if(reconnectGenerations.TryGetValue(operation,out var nonce))return nonce;
            if(reconnectGenerations.Count>=4)return Guid.Empty;nonce=Guid.NewGuid();reconnectGenerations.Add(operation,nonce);return nonce;}
        internal Guid CaptureGeneration(LoadedStation station)
        {if(!Current()||station?.Current()!=true)return Guid.Empty;if(generations.TryGetValue(station,out var value))return value;
            if(generations.Count>=4)return Guid.Empty;value=Guid.NewGuid();generations.Add(station,value);return value;}
        private RemoteLoadedOwner(LoadedOwner file,EntityPlayer actor)
        {File=file;Actor=actor;Deaths=file.data.deaths;Entering=true;}
        internal ClientInfo CurrentPeer()
        {
            if(!Returned||File.closed||!File.loaded||!File.readReturned||!ThreadManager.IsMainThread()||
                !ReferenceEquals(GameManager.Instance?.World,File.world)||File.world.IsRemote()||!ReferenceEquals(Actor.world,File.world)||
                !ReferenceEquals(File.world.GetEntity(Actor.entityId),Actor)||Actor.IsDead()||Actor.Died!=Deaths||
                Path.GetFullPath(GameIO.GetSaveGameDir())!=File.save||
                !SameOwnerGrid(Actor.bag.ItemGrid.items,File.bag)||!SameOwnerGrid(Actor.inventory.ItemGrid.items,File.belt))return null;
            var peer=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(Actor.entityId);
            if(peer?.loginDone!=true||peer.disconnecting||peer.netConnection?[0]==null||peer.netConnection[0].IsDisconnected()||
                !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity)||identity.CanonicalId!=File.account)return null;
            string creation=RebirthCamperCharacterAccess.CurrentCreation(Actor);
            if(string.IsNullOrEmpty(creation)||Creation!=null&&Creation!=creation)return null;
            // Native ToPlayer precedes native SpawnEntity/SetClientEntityId.
            // Bind once only after those real operations expose this SAME
            // retained actor/data/account; no ID lookup can replace the body.
            if(Creation==null)Creation=creation;
            // This remains an historical native Load/ToPlayer source, never
            // a current receive-head grant. Cold continuation requires a NEW
            // actual owner snapshot/upload/save with its own expected head.
            return peer;
        }
        internal bool Current()=>CurrentPeer()!=null;
        internal static RemoteLoadedOwner Enter(PlayerDataFile data,EntityPlayer actor,global::StreamModeRead mode,bool run)
        {
            if(!run||mode!=global::StreamModeRead.Persistency||actor==null||actor is EntityPlayerLocal||!actor.isEntityRemote||
                !loadedOwnerFiles.TryGetValue(data,out var file)||file.closed||file.entering||file.hydrated||!file.loaded||
                !ReferenceEquals(actor.world,file.world)||file.world.IsRemote()||remoteLoadedRecyclerOwners.Any(value=>ReferenceEquals(value.File,file)))return null;
            if(!NativeOwnerImage(data).SequenceEqual(file.native)||!MatchesOwnerFile(file.path,file.native))return null;
            var source=new RemoteLoadedOwner(file,actor);file.entering=true;remoteLoadedRecyclerOwners.Add(source);return source;
        }
    }
    private static readonly List<RemoteLoadedOwner> remoteLoadedRecyclerOwners=new List<RemoteLoadedOwner>();
    private static void ObserveRemoteLoadedRecyclerOwner(PlayerDataFile data,EntityPlayer actor,bool run,RemoteLoadedOwner source)
    {
        if(!run||source?.Entering!=true||!ReferenceEquals(source.File.data,data)||!ReferenceEquals(source.Actor,actor)||source.File.closed||
            !ReferenceEquals(GameManager.Instance?.World,source.File.world)||!ReferenceEquals(actor.world,source.File.world)||actor.Died!=source.Deaths||
            actor.IsDead()||!SameOwnerGrid(actor.bag.ItemGrid.items,source.File.bag)||!SameOwnerGrid(actor.inventory.ItemGrid.items,source.File.belt)||
            !MatchesOwnerFile(source.File.path,source.File.native))return;
        source.Returned=true;
    }
    private static void EndRemoteLoadedRecyclerOwner(RemoteLoadedOwner source)
    {
        source.Entering=false;source.File.entering=false;
        if(!source.Returned)remoteLoadedRecyclerOwners.Remove(source);
    }
    private static void SweepRemoteLoadedRecyclerOwners(World world)
    {
        remoteLoadedRecyclerOwners.RemoveAll(value=>value.File.closed||!ReferenceEquals(value.File.world,world));
    }
}
