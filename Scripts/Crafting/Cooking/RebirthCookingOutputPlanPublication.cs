using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using UnityEngine.Scripting;

// Original native queue/owner publication only. It never applies player slots.
internal static class RebirthCookingOutputPlanPublication
{
    private const int Limit=4;
    private sealed class Watch:IDisposable
    {
        internal XUiC_RecipeStack Card;internal Func<bool> Current;internal string Key;
        internal RebirthStationSnapshotEvidence.Request Placed;
        internal RebirthCamperStationSaveEvidence.Request Mobile;
        internal bool Published;internal float Expires;
        public void Dispose(){Placed?.Dispose();Mobile?.Dispose();Placed=null;Mobile=null;}
    }
    private sealed class Query
    {
        internal Recipe Recipe;internal XUiC_RecipeStack Card;internal World World;internal object Peer;
        internal EntityPlayerLocal Player;internal Guid Batch;
        internal string Key,Location,Creation;internal Guid Id;internal byte Stage;internal bool Accepted;internal float Next,Expires;
    }
    private static readonly List<Watch> local=new List<Watch>();
    private static readonly Dictionary<int,Watch> server=new Dictionary<int,Watch>();
    private static readonly List<Query> queries=new List<Query>();
    private static readonly Dictionary<int,float> requestAfter=new Dictionary<int,float>();
    private static bool installed;private static World activeWorld;private static float nextWatchSweep;
    internal static void Install()
    {
        if(installed)return;
        ModEvents.GameUpdate.RegisterHandler(Tick);installed=true;
    }
    internal static string QueueKey(TileEntityWorkstation station)
    {
        var queue=station?.Queue;if(queue==null||queue.Length<1||queue.Length>byte.MaxValue)return null;
        using(var stream=new MemoryStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {
            writer.SetBaseStream(stream);writer.Write((byte)queue.Length);
            foreach(var row in queue){if(row==null)return null;row.Write(writer);if(stream.Length>256*1024)return null;}
            writer.Flush();using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","");
        }
    }
    private static bool Scope(XUiC_RecipeStack card,Recipe recipe)
    {
        var player=card?.xui?.playerUI?.entityPlayer;var window=card?.GetParentByType<XUiC_WorkstationWindowGroup>();
        if(!ThreadManager.IsMainThread()||player==null||player.IsDead()||!ReferenceEquals(card.recipe,recipe)||window?.windowGroup?.isShowing!=true||
            !ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player))return false;
        var station=window.WorkstationData?.TileEntity;if(station==null)return false;
        if(station is RebirthCamperStationState mobile)return RebirthCamperCookingQueueAdmission.CanObserveCurrentCard(card,mobile);
        return ReferenceEquals(player.world.GetTileEntity(station.ToWorldPos()),station)&&LockManager.Instance?.IsLockedByLocalPlayer(station,0)==true;
    }
    // Shared native evidence only. The caller owns bounded lifecycle/disposal;
    // this grants neither admission nor player/station mutation authority.
    internal static IDisposable BeginAdmissionPublication(EntityPlayer actor,TileEntityWorkstation station,Recipe recipe,Func<bool> current,out Func<bool> published)
    {
        published=null;
        if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||station==null||recipe==null||current==null)return null;
        Install();
        string key=QueueKey(station);if(key==null)return null;
        var watch=Start(actor,station,key,()=>current()&&QueueKey(station)==key);
        if(watch==null)return null;
        published=()=>UnityEngine.Time.realtimeSinceStartup<watch.Expires&&StillCurrent(watch)&&Published(watch);
        return watch;
    }
    internal static IDisposable BeginOutputPublication(EntityPlayer actor,TileEntityWorkstation station,Func<bool> current,out Func<bool> published)
    {
        published=null;
        if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||station?.Output==null||current==null)return null;
        Install();
        string key=QueueKey(station);if(key==null)return null;var output=ItemStack.Clone(station.Output);
        bool Current()
        {
            if(!current()||QueueKey(station)!=key||station.Output?.Length!=output.Length)return false;
            for(int i=0;i<output.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(output[i],station.Output[i]))return false;return true;
        }
        var watch=Start(actor,station,key,Current,output);if(watch==null)return null;
        published=()=>UnityEngine.Time.realtimeSinceStartup<watch.Expires&&StillCurrent(watch)&&Published(watch);return watch;
    }
    internal static bool Observe(XUiC_RecipeStack card,Recipe recipe,byte stage)
    {
        if(!Scope(card,recipe))return false;
        var player=card.xui.playerUI.entityPlayer;var station=card.GetParentByType<XUiC_WorkstationWindowGroup>().WorkstationData.TileEntity;
        if(player.world.IsRemote())return Remote(card,recipe,station,stage);
        if(stage!=0)return RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)&&RebirthCookingOutputDelivery.HasSavedOwner(identity,recipe,stage);
        string key=QueueKey(station);if(key==null)return false;
        var existing=local.FirstOrDefault(item=>ReferenceEquals(item.Card,card));
        if(existing!=null&&(!existing.Current()||existing.Key!=key||UnityEngine.Time.realtimeSinceStartup>=existing.Expires))
        {existing.Dispose();local.Remove(existing);existing=null;}
        if(existing==null)
        {
            if(local.Count+server.Count>=Limit)return false;
            Func<bool> current=()=>Scope(card,recipe)&&QueueKey(station)==key;
            existing=Start(player,station,key,current);if(existing==null)return false;existing.Card=card;local.Add(existing);
        }
        return Published(existing);
    }
    private static Watch Start(EntityPlayer player,TileEntityWorkstation station,string key,Func<bool> current,ItemStack[] output=null)
    {
        if(player?.world==null||player.world.IsRemote()||!current())return null;
        var watch=new Watch{Key=key,Current=current,Expires=UnityEngine.Time.realtimeSinceStartup+30f};
        try
        {
            if(station is RebirthCamperStationState mobile)
            {
                if(player is EntityPlayerLocal)watch.Mobile=RebirthCamperStationSaveEvidence.Begin(player,current);
#if !REBIRTH_CAMPER_DIAGNOSTICS
                else if(RebirthCamperRemoteStationTransport.TryGetCurrent(player,out var live,out var address,out _,out _)&&ReferenceEquals(live,mobile))
                    watch.Mobile=RebirthCamperStationSaveEvidence.BeginProcessingRecovery(mobile,address,current);
#endif
                return watch.Mobile==null?null:watch;
            }
            var world=player.world;var position=station.ToWorldPos();var chunk=station.GetChunk();
            var provider=world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;var regions=provider?.m_RegionFileManager;
            if(chunk==null||regions==null||!(regions.regionFileAccess is RegionFileAccessSectorBased)||string.IsNullOrEmpty(regions.saveDirectory)||
                !RebirthStationNativeQueueSnapshot.TryCapture(station.Queue,out var expected))return null;
            var access=regions.regionFileAccess;string directory=Path.GetFullPath(regions.saveDirectory);
            Func<bool> scope=()=>current()&&ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(world.GetTileEntity(position),station)&&
                ReferenceEquals(world.GetChunkSync(position.x>>4,position.z>>4),chunk)&&ReferenceEquals(station.GetChunk(),chunk)&&
                ReferenceEquals(world.ChunkCache.ChunkProvider,provider)&&ReferenceEquals(provider.m_RegionFileManager,regions)&&
                ReferenceEquals(regions.regionFileAccess,access)&&Path.GetFullPath(regions.saveDirectory)==directory&&expected.Matches(station.Queue);
            watch.Current=scope;
            Func<byte[],byte[],bool> terminal=null;
            if(output!=null)terminal=(bytes,completions)=>
            {
                if(!RebirthStationTerminalContents.TryReadOutput(bytes,output.Length,out var saved))return false;
                for(int i=0;i<output.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(saved[i],output[i]))return false;return true;
            };
            watch.Placed=RebirthStationSnapshotEvidence.Watch(world,chunk,position.x>>4,position.z>>4,scope,station,(inputs,queue)=>expected.MatchesSerializedQueue(queue),terminal);
            if(watch.Placed==null)return null;
            lock(chunk)
            {
                if(!scope()||chunk.IsLocked||chunk.InProgressSaving||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting)
                {watch.Dispose();return null;}
                chunk.InProgressSaving=true;
            }
            try{if(!scope()){watch.Dispose();return null;}regions.SaveChunkSnapshot(chunk,true);}
            finally{lock(chunk)chunk.InProgressSaving=false;}
            return watch;
        }
        catch{watch.Dispose();return null;}
    }
    private static bool Published(Watch watch)
    {
        if(!watch.Current())return false;if(watch.Published)return true;
        bool observed=watch.Mobile!=null?RebirthCamperStationSaveEvidence.TryObserve(watch.Mobile,out _):
            watch.Placed!=null&&RebirthStationSnapshotEvidence.TryGetPublished(watch.Placed,out _);
        if(observed){watch.Published=true;watch.Dispose();}return observed&&watch.Current();
    }
    private static bool Remote(XUiC_RecipeStack card,Recipe recipe,TileEntityWorkstation station,byte stage)
    {
        var player=card.xui.playerUI.entityPlayer;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        var peers=manager?.connectionToServer;if(peers==null||peers.Length==0||peers[0]==null||peers[0].IsDisconnected())return false;
        string key=QueueKey(station),location=RebirthCookingStationLocator.Encode(station),creation=RebirthCamperCharacterAccess.CurrentCreation(player);
        if(!RebirthCookingOutputDelivery.TryGetBatch(recipe,out var batch))return false;
        if(key==null||!RebirthSurvivorRequestScope.TryNormalize(creation,out creation))return false;
        var query=queries.FirstOrDefault(item=>ReferenceEquals(item.Card,card));
        if(query!=null&&(!ReferenceEquals(query.World,player.world)||!ReferenceEquals(query.Peer,peers[0])||query.Key!=key||query.Location!=location||query.Creation!=creation||query.Stage!=stage))
        {queries.Remove(query);query=null;}
        if(query==null)
        {
            if(queries.Count>=Limit)return false;
            query=new Query{Card=card,Recipe=recipe,Player=player,Batch=batch,World=player.world,Peer=peers[0],Key=key,Location=location,Creation=creation,Stage=stage,Id=Guid.NewGuid(),Expires=UnityEngine.Time.realtimeSinceStartup+30f};queries.Add(query);
        }
        if(query.Accepted)return true;
        if(UnityEngine.Time.realtimeSinceStartup<query.Next)return false;query.Next=UnityEngine.Time.realtimeSinceStartup+1f;
        try
        {
            if(stage>0||station is RebirthCamperStationState)
            {
                // Normal native owner upload; the host verifies its final file.
                manager.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(player),false);
            }
            manager.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCookingOutputWitnessRequest>()
                .Setup(player.entityId,query.Id,query.Batch,stage,location,creation,key),false);
        }
        catch(Exception error){Log.Warning("[Cooking] Original publication request retained: "+error.Message);}
        return false;
    }
    internal static void Receive(World world,Guid id,Guid batch,byte stage,string key,bool accepted,string marker)
    {
        if(world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,world))return;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var peer=manager?.connectionToServer;
        var query=queries.FirstOrDefault(item=>item.Id==id&&item.Batch==batch&&item.Stage==stage&&item.Key==key&&ReferenceEquals(item.World,world));
        if(query==null||peer==null||peer.Length==0||!ReferenceEquals(query.Peer,peer[0])||RebirthCamperCharacterAccess.CurrentCreation(query.Player)!=query.Creation)return;
        if(stage==3&&accepted)
        {query.Accepted=true;RebirthCookingOutputDelivery.ObserveRetiredOwner(query.Recipe,query.Player);return;}
        if(!Scope(query.Card,query.Recipe))return;
        if(stage==0&&!string.IsNullOrEmpty(marker))
        {
            if(RebirthCookingOutputDelivery.AdoptMarker(query.Card,batch,marker))
            {query.Key=QueueKey(query.Card.GetParentByType<XUiC_WorkstationWindowGroup>().WorkstationData.TileEntity);query.Next=0f;}
            return;
        }
        if(QueueKey(query.Card.GetParentByType<XUiC_WorkstationWindowGroup>().WorkstationData.TileEntity)!=key)return;
        query.Accepted=accepted;
    }
    private static bool OriginalTicket(World world,EntityPlayer recipient,TileEntityWorkstation station,Recipe recipe,string location)
    {
        if(!RebirthCookingHeat.Managed(recipe)||!recipe.ingredients[0].itemValue.TryGetMetadata("rebirth.cooking.queue.token",out string token)||!Guid.TryParse(token,out _))return false;
        bool Match(RebirthWorldCharacterRecord record)=>record?.Progression?.Cooking!=null&&record.Progression.Cooking.Tickets.TryGetValue(token,out var ticket)&&
            ticket!=null&&ticket.Recipe==recipe.GetName()&&ticket.OutputCount==recipe.count&&ticket.StationPosition==location&&
            station.Queue.Any(row=>ReferenceEquals(row?.Recipe,recipe)&&row.Multiplier==ticket.Portions);
        if(RebirthWorldCharacterService.TryGet(recipient,out var own)&&Match(own))return true;
        if(recipe.ingredients[0].itemValue.TryGetMetadata("rebirth.cooking.registeredOwner",out string canonical))
        {
            var players=GameManager.Instance.GetPersistentPlayerList()?.Players;
            if(players!=null)foreach(var entry in players)
                if(entry.Value?.PrimaryId?.CombinedString==canonical&&RebirthStablePlayerIdentity.TryFromPersistentPlayer(entry.Value,out var identity)&&
                    RebirthWorldCharacterRepository.TryGet(identity,out var record)&&Match(record))return true;
        }
        // Legacy native rows retain no stable cook identity. Existing online
        // owner records can still prove their original ticket without new rights.
        if(world.Players?.list!=null)foreach(var player in world.Players.list)
            if(RebirthWorldCharacterService.TryGet(player,out var record)&&Match(record))return true;
        return false;
    }
    internal static void Request(World world,int playerId,Guid id,Guid batch,byte stage,string location,string creation,string key)
    {
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||id==Guid.Empty||stage>3)return;
        Install();
        var player=world.GetEntity(playerId) as EntityPlayer;var sender=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(playerId);
        if(sender==null)return;
        float now=UnityEngine.Time.realtimeSinceStartup;
        if(requestAfter.TryGetValue(playerId,out float after)&&now<after)return;
        if(requestAfter.Count>=128&&!requestAfter.ContainsKey(playerId))
            foreach(var expired in requestAfter.Where(pair=>pair.Value<=now).Select(pair=>pair.Key).ToArray())requestAfter.Remove(expired);
        if(requestAfter.Count>=128&&!requestAfter.ContainsKey(playerId))return;
        requestAfter[playerId]=now+.25f;
        bool accepted=false;string projected=null;
        try
        {
            if(player==null||sender==null||!RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var identity)||
                RebirthCamperCharacterAccess.CurrentCreation(player)!=creation)throw new InvalidOperationException("Original authenticated cooking owner required.");
            // Terminal acknowledgement has no new item/queue effects. It can
            // finish its original owner hold after native UI close/projection.
            if(stage==3&&RebirthCookingOutputDelivery.HasSavedRetirement(identity,batch))
                accepted=true;
            TileEntityWorkstation station;object lease=null;
            if(RebirthCamperStationAddress.TryParse(location,out var mobileAddress))
            {if(!RebirthCamperRemoteStationTransport.TryResolve(player,mobileAddress,creation,out var mobile,out lease))throw new InvalidOperationException("Original mobile lease unavailable.");station=mobile;}
            else
            {
                var xyz=location.Split(',');if(xyz.Length!=3||!int.TryParse(xyz[0],out int x)||!int.TryParse(xyz[1],out int y)||!int.TryParse(xyz[2],out int z))throw new InvalidOperationException("Invalid original station address.");
                station=world.GetTileEntity(new Vector3i(x,y,z)) as TileEntityWorkstation;
            }
            var recipe=station?.Queue?.Select(row=>row?.Recipe).FirstOrDefault(value=>RebirthCookingOutputDelivery.TryGetBatch(value,out var token)&&token==batch);
            if(recipe==null)
            {if(accepted)throw new InvalidOperationException("Original delivery is already terminal; no queue mutation.");throw new InvalidOperationException("Original native batch unavailable.");}
            if(stage==0)
            {
                if(!RebirthCookingOutputDelivery.HasPrepared(recipe)&&RebirthRemoteResourceSourceCustody.BlocksStation(station))throw new InvalidOperationException("Original native station source is already held.");
                if(!OriginalTicket(world,player,station,recipe,location)||!RebirthCookingOutputDelivery.PrepareSavedOwner(player,identity,recipe))throw new InvalidOperationException("Original native owner plan unavailable.");
                station.setModified();
                if(QueueKey(station)!=key)projected=RebirthNativeItemCodec.Encode(recipe.ingredients[0].itemValue);
            }
            if(QueueKey(station)!=key||!OriginalTicket(world,player,station,recipe,location))throw new InvalidOperationException("Original native batch unavailable.");
            bool Current()
            {
                if(!ReferenceEquals(GameManager.Instance?.World,world)||!ReferenceEquals(world.GetEntity(playerId),player)||player.IsDead()||
                    !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(playerId),sender)||RebirthCamperCharacterAccess.CurrentCreation(player)!=creation||QueueKey(station)!=key)return false;
                if(station is RebirthCamperStationState mobile)return RebirthCamperRemoteStationTransport.IsCurrent(player,mobileAddress,creation,lease)&&ReferenceEquals(mobile.BoundVehicle?.world,world)&&
                    !RebirthCamperStationGridCustody.IsHeld(mobile)&&!RebirthCamperModuleRuntime.For(mobile.BoundVehicle).TransferPending;
                var nativeIdentity=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(playerId)?.PrimaryId;
                return nativeIdentity!=null&&ReferenceEquals(world.GetTileEntity(station.ToWorldPos()),station)&&(player.position-station.ToWorldPos().ToVector3()).sqrMagnitude<=64f&&
                    LockManager.Instance!=null&&!station.IsSharedLock(0)&&LockManager.Instance.singleLocks.TryGetByValue(new LockManager.LockEntry(station,0),out var ownerId)&&ownerId==playerId&&
                    RebirthWorkstationSecurityService.CanAccessWorkstation(world,station.ToWorldPos(),player,nativeIdentity,false,RebirthSecureAccessPurpose.WorkstationAccess,out _);
            }
            if(!Current())throw new InvalidOperationException("Original native station scope expired.");
            if(stage>0)
            {
                if(stage==3&&station is RebirthCamperStationState mobile)
                    accepted=RebirthCamperCookingQueueAdmission.TryRetireObservedOutput(player,mobile,recipe,()=>Current()&&RebirthCookingOutputDelivery.HasSavedOwner(identity,recipe,3));
                else accepted=RebirthCookingOutputDelivery.HasSavedOwner(identity,recipe,stage)&&Current();
            }
            else
            {
                if(server.TryGetValue(playerId,out var watch)&&(!watch.Current()||watch.Key!=key||UnityEngine.Time.realtimeSinceStartup>=watch.Expires))
                {watch.Dispose();server.Remove(playerId);watch=null;}
                if(!server.TryGetValue(playerId,out watch)&&server.Count+local.Count<Limit)
                {watch=Start(player,station,key,Current);if(watch!=null)server.Add(playerId,watch);}
                accepted=watch!=null&&Published(watch)&&Current();
            }
        }
        catch(Exception error){if(RebirthLogSettings.CraftingUiLoggingEnabled)Log.Warning("[Cooking] Original publication pending/refused: "+error.Message);}
        if(sender!=null)SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCookingOutputWitnessResult>().Setup(id,batch,stage,key,accepted,projected),_attachedToEntityId:playerId);
    }
    private static void Tick(ref ModEvents.SGameUpdateData data)
    {
        var world=GameManager.Instance?.World;
        if(ReferenceEquals(world,activeWorld)&&local.Count==0&&server.Count==0&&queries.Count==0)return;
        if(!ReferenceEquals(world,activeWorld))
        {foreach(var entry in local)entry.Dispose();foreach(var entry in server.Values)entry.Dispose();local.Clear();server.Clear();queries.Clear();requestAfter.Clear();nextWatchSweep=0f;activeWorld=world;}
        // Cleanup performs full queue serialization/hashing through Current.
        // Output attempts already validate synchronously before every effect;
        // idle cleanup needs only the same four-Hz cadence as output retries.
        float now=UnityEngine.Time.realtimeSinceStartup;
        if(now>=nextWatchSweep)
        {
            nextWatchSweep=now+.25f;
            for(int i=local.Count-1;i>=0;i--)if(now>=local[i].Expires||!StillCurrent(local[i])){local[i].Dispose();local.RemoveAt(i);}
            foreach(var id in server.Where(pair=>now>=pair.Value.Expires||!StillCurrent(pair.Value)).Select(pair=>pair.Key).ToArray()){server[id].Dispose();server.Remove(id);}
        }
        for(int i=queries.Count-1;i>=0;i--)
        {
            var query=queries[i];
            if(query.Stage==3&&!query.Accepted&&ReferenceEquals(query.World,world)&&RebirthCamperCharacterAccess.CurrentCreation(query.Player)==query.Creation)
            {
                // Retain only original terminal acknowledgement retry across UI
                // close. No paid command or owner setter is replayed here.
                if(UnityEngine.Time.realtimeSinceStartup>=query.Next)
                {
                    query.Next=UnityEngine.Time.realtimeSinceStartup+1f;
                    var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;var peers=connection?.connectionToServer;
                    try{if(peers!=null&&peers.Length>0&&peers[0]!=null&&!peers[0].IsDisconnected())
                        {
                            // Terminal queries have no owner setters or new award.
                            // Rebind only their correlation after reconnect.
                            if(!ReferenceEquals(peers[0],query.Peer)){query.Peer=peers[0];query.Id=Guid.NewGuid();}
                            connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCookingOutputWitnessRequest>().Setup(query.Player.entityId,query.Id,query.Batch,3,query.Location,query.Creation,query.Key),false);
                        }}catch{}
                }
                continue;
            }
            if(UnityEngine.Time.realtimeSinceStartup>=query.Expires||!Scope(query.Card,query.Recipe))queries.RemoveAt(i);
        }
    }
    private static bool StillCurrent(Watch watch)
    {try{return watch?.Current?.Invoke()==true;}catch{return false;}}
}

[Preserve]
public sealed class NetPackageRebirthCookingOutputWitnessRequest:NetPackage
{
    private const byte Protocol=1;
    private int player;private Guid request,batch;private byte stage;private string location,creation,key;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthCookingOutputWitnessRequest Setup(int p,Guid id,Guid original,byte step,string station,string character,string digest)
    {player=p;request=id;batch=original;stage=step;location=station;creation=character;key=digest;return this;}
    public override void read(PooledBinaryReader reader)
    {
        var r=(BinaryReader)reader;if(r.ReadByte()!=Protocol)throw new InvalidDataException("Unsupported cooking publication protocol.");player=r.ReadInt32();var id=r.ReadBytes(16);if(id.Length!=16)throw new InvalidDataException();request=new Guid(id);id=r.ReadBytes(16);if(id.Length!=16)throw new InvalidDataException();batch=new Guid(id);stage=r.ReadByte();
        location=ReadText(r,160);creation=ReadText(r,71);key=ReadText(r,64);
        if(stage>3||request==Guid.Empty||batch==Guid.Empty||!RebirthSurvivorRequestScope.TryNormalize(creation,out creation)||key.Length!=64||key.Any(c=>!Uri.IsHexDigit(c)))throw new InvalidDataException("Invalid original cooking publication request.");
    }
    internal static string ReadText(BinaryReader reader,int max)
    {int length=reader.ReadByte();if(length<1||length>max)throw new InvalidDataException();var bytes=reader.ReadBytes(length);if(bytes.Length!=length||bytes.Any(value=>value>127))throw new InvalidDataException();return Encoding.ASCII.GetString(bytes);}
    internal static void WriteText(BinaryWriter writer,string value,int max)
    {if(string.IsNullOrEmpty(value)||value.Length>max||value.Any(c=>c>127))throw new InvalidDataException();writer.Write((byte)value.Length);writer.Write(Encoding.ASCII.GetBytes(value));}
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);var w=(BinaryWriter)writer;w.Write(Protocol);w.Write(player);w.Write(request.ToByteArray());w.Write(batch.ToByteArray());w.Write(stage);WriteText(w,location,160);WriteText(w,creation,71);WriteText(w,key,64);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(ValidEntityIdForSender(player))RebirthCookingOutputPlanPublication.Request(world,player,request,batch,stage,location,creation,key);}
    public int GetLength()=>41+(location?.Length??0)+(creation?.Length??0)+(key?.Length??0);
}
[Preserve]
public sealed class NetPackageRebirthCookingOutputWitnessResult:NetPackage
{
    private const byte Protocol=1;
    private Guid request,batch;private byte stage;private string key,marker;private bool accepted;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthCookingOutputWitnessResult Setup(Guid id,Guid original,byte step,string digest,bool success,string projection){request=id;batch=original;stage=step;key=digest;accepted=success;marker=projection;return this;}
    public override void read(PooledBinaryReader reader)
    {var r=(BinaryReader)reader;if(r.ReadByte()!=Protocol)throw new InvalidDataException("Unsupported cooking publication protocol.");var id=r.ReadBytes(16);if(id.Length!=16)throw new InvalidDataException();request=new Guid(id);id=r.ReadBytes(16);if(id.Length!=16)throw new InvalidDataException();batch=new Guid(id);stage=r.ReadByte();key=NetPackageRebirthCookingOutputWitnessRequest.ReadText(r,64);accepted=r.ReadBoolean();int length=r.ReadInt32();if(length<0||length>1048576)throw new InvalidDataException();var bytes=r.ReadBytes(length);if(bytes.Length!=length||bytes.Any(value=>value>127))throw new InvalidDataException();marker=length==0?null:Encoding.ASCII.GetString(bytes);if(request==Guid.Empty||batch==Guid.Empty||stage>3||key.Length!=64||key.Any(c=>!Uri.IsHexDigit(c)))throw new InvalidDataException();}
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);var w=(BinaryWriter)writer;w.Write(Protocol);w.Write(request.ToByteArray());w.Write(batch.ToByteArray());w.Write(stage);NetPackageRebirthCookingOutputWitnessRequest.WriteText(w,key,64);w.Write(accepted);if(marker?.Any(value=>value>127)==true)throw new InvalidDataException();var bytes=Encoding.ASCII.GetBytes(marker??string.Empty);if(bytes.Length>1048576)throw new InvalidDataException();w.Write(bytes.Length);w.Write(bytes);}
    public override void ProcessPackage(World world,GameManager callbacks)=>RebirthCookingOutputPlanPublication.Receive(world,request,batch,stage,key,accepted,marker);
    public int GetLength()=>40+(key?.Length??0)+(marker?.Length??0);
}
