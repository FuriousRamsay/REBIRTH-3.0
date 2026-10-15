using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

// Callable original-session lifecycle only. Default off; no bootstrap or reconnect rebinding.
internal static class RebirthConsumptionDurableDelivery
{
    internal static bool Enabled;
    private const int MaximumEntries=256,MaximumAttempts=30,MaximumPerPump=4;
    private sealed class Entry
    {
        internal World World;internal object WorldState;internal GameManager Game;internal ConnectionManager Connection;
        internal ClientInfo Sender;internal EntityPlayer Player;internal RebirthWorldCharacterRecord Character;
        internal string Root,NativeWorldGuid,Directory;
        internal RebirthConsumptionDurableReceipt Receipt;
        internal bool PendingSaved;internal int Attempts;internal double NextAttempt;internal string Error;
        internal bool Current()
        {
            try
            {
                RebirthWorldCharacterRecord current;RebirthStablePlayerIdentity identity;
                return ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance,Game)&&ReferenceEquals(Game.World,World)
                    &&!World.IsRemote()&&ReferenceEquals(World.worldState,WorldState)&&World.worldState.Guid==NativeWorldGuid
                    &&ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,Connection)&&Connection.IsServer
                    &&Connection.Clients!=null&&ReferenceEquals(Connection.Clients.ForEntityId(Receipt.OriginalEntityId),Sender)
                    &&Sender.entityId==Receipt.OriginalEntityId&&Player.entityId==Receipt.OriginalEntityId
                    &&ReferenceEquals(Player.world,World)&&ReferenceEquals(World.GetEntity(Player.entityId),Player)
                    &&RebirthWorldCharacterRepository.IsServerAuthority&&RebirthWorldCharacterRepository.IsCurrentCachedRecord(Character)
                    &&RebirthWorldCharacterService.TryGet(Player,out current)&&ReferenceEquals(current,Character)&&current.IsComplete
                    &&RebirthSurvivorRequestScope.Matches(Receipt.Creation,current.Origin?.CreationId)
                    &&RebirthWorldCharacterService.TryGetIdentity(Player,out identity)&&identity!=null
                    &&identity.CanonicalId==Receipt.Owner&&identity.StorageKey==Receipt.StorageKey
                    &&current.StablePlayerId==Receipt.Owner&&current.StablePlayerKey==Receipt.StorageKey
                    &&string.Equals(Path.GetFullPath(GameIO.GetSaveGameDir()),Root,
                        Path.DirectorySeparatorChar=='\\'?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal);
            }
            catch{return false;}
        }
    }
    private static readonly Dictionary<Guid,Entry> entries=new Dictionary<Guid,Entry>();

    // Caller has performed the sole ConsumeAt already. This method only captures/persists/sends its reply.
    internal static bool TryRetainAndSend(World world,EntityPlayer player,ClientInfo sender,
        NetPackageRebirthMetabolismConsumeResult packet,out string error)
    {
        error=null;
        if(!Enabled||!RebirthConsumptionPublicationRetry.AcknowledgementProtocolEnabled||!ThreadManager.IsMainThread())
        {error="durable delivery disabled";return false;}
        try
        {
            int owner;Guid id;RebirthWorldCharacterRecord character;RebirthStablePlayerIdentity identity;
            var game=GameManager.Instance;var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(world==null||world.IsRemote()||!ReferenceEquals(game?.World,world)||world.worldState==null||player==null||sender==null
                ||connection==null||!connection.IsServer||connection.Clients==null||!ReferenceEquals(connection.Clients.ForEntityId(player.entityId),sender)
                ||sender.entityId!=player.entityId||!ReferenceEquals(player.world,world)||!ReferenceEquals(world.GetEntity(player.entityId),player)
                ||packet==null||!packet.TryGetPublicationIdentity(out owner,out id)||owner!=player.entityId
                ||!RebirthWorldCharacterService.TryGet(player,out character)||character==null||!character.IsComplete
                ||!RebirthWorldCharacterService.TryGetIdentity(player,out identity)||identity==null)throw new InvalidDataException("Original delivery authority unavailable.");
            string root=Path.GetFullPath(GameIO.GetSaveGameDir()),creation;Guid worldGuid;
            if(!RebirthSurvivorRequestScope.TryNormalize(character.Origin?.CreationId,out creation)
                ||!Guid.TryParse(world.worldState.Guid,out worldGuid)||worldGuid==Guid.Empty)throw new InvalidDataException("World/character scope unavailable.");
            RebirthConsumptionDurableReceipt receipt;
            if(!RebirthConsumptionDurableReceipt.TryCreate(id,identity.CanonicalId,identity.StorageKey,creation,worldGuid.ToString("N"),
                RebirthStablePlayerIdentity.ComputeStorageKey(root),player.entityId,RebirthConsumptionPublicationRetry.Write(packet),false,out receipt))
                throw new InvalidDataException("Invalid original reply receipt.");
            Entry retained;
            if(entries.TryGetValue(id,out retained))
            {
                if(!retained.Current()||!retained.Receipt.SameOriginal(receipt))throw new InvalidDataException("Conflicting retained reply.");
                error=retained.Error;return retained.PendingSaved||retained.Receipt.Acknowledged;
            }
            if(entries.Count>=MaximumEntries)throw new InvalidDataException("Unresolved delivery capacity retained.");
            retained=new Entry{World=world,WorldState=world.worldState,Game=game,Connection=connection,Sender=sender,Player=player,Character=character,
                Root=root,NativeWorldGuid=world.worldState.Guid,Receipt=receipt,
                Directory=Path.Combine(root,"RebirthData","Survivor","ConsumptionPublications",receipt.StorageKey,receipt.Creation,receipt.WorldGuid,receipt.SaveKey)};
            if(!retained.Current()||!ValidateOriginal(receipt))throw new InvalidDataException("Original receipt witness failed.");
            entries.Add(id,retained); // Retain even if persistence/send fails; never rerun consumption.
            Attempt(retained,Now());error=retained.Error;return retained.PendingSaved;
        }
        catch(Exception failure){error=failure.GetType().Name+": "+failure.Message;return false;}
    }
    internal static void Pump()
    {
        if(!Enabled||!RebirthConsumptionPublicationRetry.AcknowledgementProtocolEnabled||!ThreadManager.IsMainThread())return;
        double now=Now();int visited=0;
        foreach(var entry in entries.Values)
        {
            if(entry.Receipt.Acknowledged||entry.Attempts>=MaximumAttempts||entry.NextAttempt>now||!entry.Current())continue;
            Attempt(entry,now);if(++visited>=MaximumPerPump)break;
        }
    }
    private static void Attempt(Entry entry,double now)
    {
        entry.Attempts++;entry.NextAttempt=now+2;
        try
        {
            if(!entry.Current())throw new InvalidDataException("Original session changed; retained without rebinding.");
            string path=Path.Combine(entry.Directory,entry.Receipt.Publication.ToString("N")+".xml");
            RebirthConsumptionDurableReceipt saved;
            if(RebirthConsumptionDurableReceiptStore.TryReadFinal(path,out saved)&&saved.SameOriginal(entry.Receipt)&&saved.Acknowledged)
            {entry.Receipt=saved;entry.PendingSaved=true;entry.Error=null;return;}
            string error;
            if(!RebirthConsumptionDurableReceiptStore.TryPersist(entry.Directory,entry.Receipt,entry.Current,ValidateOriginal,out error))
                throw new InvalidDataException(error);
            entry.PendingSaved=true;
            if(!entry.Current())throw new InvalidDataException("Session changed after durable pending receipt.");
            entry.Connection.SendPackage(RebirthConsumptionPublicationRetry.Read(entry.Receipt.OriginalWire),_attachedToEntityId:entry.Receipt.OriginalEntityId);
            entry.Error=null;
        }
        catch(Exception failure){entry.Error=failure.GetType().Name+": "+failure.Message;}
    }
    // Bind to retry.DurableAcknowledgement only after parent lifecycle review; native handler authenticates sender first.
    internal static bool AcknowledgeOriginalSession(World world,EntityPlayer player,Guid id,string creation)
    {
        Entry entry;
        if(!Enabled||!RebirthConsumptionPublicationRetry.AcknowledgementProtocolEnabled||!ThreadManager.IsMainThread()
            ||!entries.TryGetValue(id,out entry)||!ReferenceEquals(entry.World,world)||!ReferenceEquals(entry.Player,player)
            ||!entry.Current()||!entry.PendingSaved||!RebirthSurvivorRequestScope.Matches(creation,entry.Receipt.Creation))return false;
        if(entry.Receipt.Acknowledged)return true;
        var terminal=entry.Receipt.WithAcknowledgement();string error;
        if(!RebirthConsumptionDurableReceiptStore.TryPersist(entry.Directory,terminal,entry.Current,ValidateOriginal,out error))
        {entry.Error=error;return false;}
        entry.Receipt=terminal;entry.Error=null;return true;
    }
    private static bool ValidateOriginal(RebirthConsumptionDurableReceipt receipt)
    {
        try
        {
            var packet=RebirthConsumptionPublicationRetry.Read(receipt.OriginalWire);int owner;Guid id;
            return packet.TryGetPublicationIdentity(out owner,out id)&&owner==receipt.OriginalEntityId&&id==receipt.Publication
                &&RebirthConsumptionPublicationRetry.Write(packet).SequenceEqual(receipt.OriginalWire);
        }
        catch{return false;}
    }
    internal static JObject Observe()
    {
        if(!ThreadManager.IsMainThread())throw new InvalidOperationException("Main-thread observation required.");
        var rows=new JArray();foreach(var entry in entries.Values)rows.Add(new JObject{["publication"]=entry.Receipt.Publication.ToString("N"),
            ["pendingSaved"]=entry.PendingSaved,["acknowledged"]=entry.Receipt.Acknowledged,["attempts"]=entry.Attempts,
            ["originalSessionCurrent"]=entry.Current(),["error"]=entry.Error});
        return new JObject{["schema"]=1,["enabled"]=Enabled,["records"]=rows,["maximumAttempts"]=MaximumAttempts,["maximumPerPump"]=MaximumPerPump};
    }
    private static double Now()=>System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
}
