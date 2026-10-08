using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;

// Exact 21-key medical scalar lane. Never reconstructs Medicine or Trauma.
internal static class RebirthSelfMedicalReplicaLane
{
    internal static readonly string[] Names = {
        "healAbrasionMult", "$legTreatedCritHealingBase", "$armTreatedCritHealingBase",
        "rbParamedicAbrasionTreatment", "rbParamedicLegTreatment", "rbParamedicArmTreatment",
        "$rbParamedicNewSelf_buffInjuryAbrasionTreated", "$rbParamedicNewSelf_buffLegSplinted",
        "$rbParamedicNewSelf_buffLegCast", "$rbParamedicNewSelf_buffArmSplinted", "$rbParamedicNewSelf_buffArmCast",
        "$rbSelfQualityAbrasion", "$rbSelfQualityLegSplint", "$rbSelfQualityLegCast", "$rbSelfQualityArmSplint", "$rbSelfQualityArmCast",
        "$rbSelfTraumaAbrasion", "$rbSelfTraumaLegSplint", "$rbSelfTraumaLegCast", "$rbSelfTraumaArmSplint", "$rbSelfTraumaArmCast"
    };
    internal sealed class Record
    {
        internal World World;
        internal ConnectionManager Connection;
        internal ClientInfo Owner;
        internal INetConnection Peer;
        internal readonly Dictionary<string,float> Values = new Dictionary<string,float>(StringComparer.Ordinal);
    }
    internal sealed class ReadScope
    {
        internal EntityPlayer Player;
        internal Record Record;
        internal ReadScope Previous;
    }
    static ConditionalWeakTable<EntityPlayer,Record> records = new ConditionalWeakTable<EntityPlayer,Record>();
    [ThreadStatic] static ReadScope reading;
    internal sealed class StarterScope
    {
        internal EntityPlayer Player;
        internal Record Record;
        internal BuffValue Buff;
        internal string Name, Key;
        internal StarterScope Previous;
    }
    [ThreadStatic] static StarterScope starter;
    [ThreadStatic] static int authoritativeWriteDepth;
    internal static StarterScope EnterStarter(EntityBuffs buffs, MinEventTypes type, BuffClass definition, MinEventParams context)
    {
        StarterScope next=null;
        var player=buffs?.parent as EntityPlayer;
        string name=definition?.Name;
        string key=name=="buffLegSplinted" || name=="buffLegCast" ? "$legTreatedCritHealingBase"
            : name=="buffArmSplinted" || name=="buffArmCast" ? "$armTreatedCritHealingBase" : null;
        var buff=context?.Buff;
        if(type==MinEventTypes.onSelfBuffStart && key!=null && player!=null
            && ReferenceEquals(context.Self,player) && buff!=null
            && ReferenceEquals(buffs.GetBuff(name),buff) && ReferenceEquals(buff.BuffClass,definition)
            && !buff.Started && !buff.Remove && !buff.Invalid
            && records.TryGetValue(player,out Record record) && Current(player,record)
            && record.Values.ContainsKey(key))
            next=new StarterScope{Player=player,Record=record,Buff=buff,Name=name,Key=key};
        // A nested unrelated event masks the outer starter; its own scalar actions remain native.
        if(next==null && starter==null) return null;
        if(next==null) next=new StarterScope();
        next.Previous=starter;starter=next;return next;
    }
    internal static void ExitStarter(StarterScope scope)
    {
        if(scope!=null) starter=scope.Previous;
    }
    static bool ProtectedStarterWrite(EntityBuffs buffs,string name)
    {
        var scope=starter;
        return scope?.Player!=null && name==scope.Key && ReferenceEquals(scope.Player.Buffs,buffs)
            && Current(scope.Player,scope.Record)
            && ReferenceEquals(buffs.GetBuff(scope.Name),scope.Buff)
            && !scope.Buff.Started && !scope.Buff.Remove && !scope.Buff.Invalid;
    }
    static readonly HashSet<string> medicalNames=new HashSet<string>(Names,StringComparer.Ordinal);
    internal static bool Medical(string name) => medicalNames.Contains(name);
    static bool Finite(float value) => value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);

    internal static bool Bound(World world, EntityPlayer player, ClientInfo sender, out Record bound)
    {
        bound = null;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        GameManager game = GameManager.Instance;
        if (world == null || player == null || connection == null || game == null
            || !ReferenceEquals(game.World, world) || !ReferenceEquals(player.world, world)
            || !ReferenceEquals(world.GetEntity(player.entityId), player) || !player.isEntityRemote
            || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return false;
        if (connection.IsServer)
        {
            if (world.IsRemote() || sender == null || !sender.loginDone || sender.entityId != player.entityId
                || connection.Clients == null || !ReferenceEquals(connection.Clients.ForEntityId(player.entityId), sender)
                || sender.netConnection == null || sender.netConnection.Length == 0
                || sender.netConnection[0] == null || sender.netConnection[0].IsDisconnected()) return false;
            bool registered=false;
            for(int i=0;i<connection.Clients.List.Count;i++)
                if(ReferenceEquals(connection.Clients.List[i],sender)) registered=true;
            if(!registered) return false;
            bound = new Record { World=world, Connection=connection, Owner=sender, Peer=sender.netConnection[0] };
        }
        else
        {
            // Native parsing assigns null Sender for the server connection on clients.
            if (!world.IsRemote() || sender != null || connection.connectionToServer == null
                || connection.connectionToServer.Length == 0 || connection.connectionToServer[0] == null
                || connection.connectionToServer[0].IsDisconnected()) return false;
            bound = new Record { World=world, Connection=connection, Peer=connection.connectionToServer[0] };
        }
        return true;
    }
    static bool Same(Record a, Record b) => ReferenceEquals(a.World,b.World)
        && ReferenceEquals(a.Connection,b.Connection) && ReferenceEquals(a.Owner,b.Owner) && ReferenceEquals(a.Peer,b.Peer);
    internal static Record Get(EntityPlayer player, Record bound, bool create)
    {
        if (records.TryGetValue(player, out Record record))
        {
            if (Same(record,bound)) return record;
            records.Remove(player); // Peer/session substitution cannot inherit scalar authority.
        }
        if (!create) return null;
        records.Add(player,bound);
        return bound;
    }
    internal static bool Current(EntityPlayer player, Record record)
        => records.TryGetValue(player,out Record active) && ReferenceEquals(active,record)
            && Bound(record.World,player,record.Owner,out Record bound) && Same(record,bound);

    internal static bool Receive(NetPackageModifyCVar packet, World world)
    {
        if (world == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) { Clear(); return true; }
        if (!Medical(packet.cvarName)) return true;
        ConnectionManager connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        Entity entity=world.GetEntity(packet.m_entityId);
        EntityPlayer player=entity as EntityPlayer;
        if (entity!=null && player==null) return true;
        if (player!=null && !player.isEntityRemote && connection!=null && !connection.IsServer) return true;
        if ((packet.operation == CVarOperation.set && !Finite(packet.value))
            || !Bound(world,player,packet.Sender,out Record bound)) return false;
        if (connection.IsServer && !packet.ValidEntityIdForSender(packet.m_entityId)) return false;
        if (packet.operation != CVarOperation.set) return true; // Run authentic native operation exactly once.
        Record record=Get(player,bound,true);
        bool changed=!record.Values.TryGetValue(packet.cvarName,out float previous) || previous!=packet.value;
        record.Values[packet.cvarName]=packet.value;
        authoritativeWriteDepth++;
        try { player.Buffs.SetCustomVar(packet.cvarName,packet.value,false,CVarOperation.set); }
        finally { authoritativeWriteDepth--; }
        if (connection.IsServer && changed)
            player.Buffs.SetCustomVarNetwork(packet.cvarName,packet.value,CVarOperation.set);
        return false;
    }
    internal static bool EnterStats(NetPackageEntityStatsBuff packet, World world, out ReadScope scope)
    {
        scope=null;
        if (world == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) { Clear(); return true; }
        EntityPlayer player=world?.GetEntity(packet.m_entityId) as EntityPlayer;
        ConnectionManager connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (player == null || connection == null) return true;
        if (!Bound(world,player,packet.Sender,out Record bound))
        {
            // Keep native non-player/local-player traffic. Remote-player receipt must own its actor.
            return !player.isEntityRemote || !RebirthSurvivorMode.IsEnabledForCurrentWorld();
        }
        if (connection.IsServer && !packet.ValidEntityIdForSender(packet.m_entityId)) return false;
        Record record=Get(player,bound,false);
        if (record == null) return true; // Unseeded keys use native state until reliable admission/bootstrap.
        scope=new ReadScope { Player=player,Record=record,Previous=reading };
        reading=scope;
        return true;
    }
    internal static bool AllowWrite(EntityBuffs buffs,string name)
    {
        if(authoritativeWriteDepth>0) return true;
        if(ProtectedStarterWrite(buffs,name)) return false;
        ReadScope scope=reading;
        return scope == null || !ReferenceEquals(scope.Player.Buffs,buffs)
            || !scope.Record.Values.ContainsKey(name) || !Current(scope.Player,scope.Record);
    }
    internal static void ExitStats(ReadScope scope,Exception error)
    {
        if (scope == null) return;
        reading=scope.Previous;
        if (error != null || !Current(scope.Player,scope.Record)) return;
    }
    internal static void Bootstrap(ClientInfo owner)
    {
        World world=GameManager.Instance?.World;
        EntityPlayer player=world?.GetEntity(owner?.entityId ?? -1) as EntityPlayer;
        if (!Bound(world,player,owner,out Record bound) || !bound.Connection.IsServer) return;
        // Runs after native server RequestToSpawnPlayer restores saved Buffs and registers entity/owner.
        Record record=Get(player,bound,true);
        foreach(string name in Names)
        {
            if (record.Values.ContainsKey(name)) continue;
            float value=player.Buffs.GetCustomVar(name);
            if (Finite(value)) record.Values[name]=value;
        }
    }
    internal static void ObserveScalarWrite(EntityBuffs buffs,string name,bool ranOriginal)
    {
        if(!ranOriginal || reading!=null || !Medical(name) || !(buffs.parent is EntityPlayer player)
            || !records.TryGetValue(player,out Record record) || !Current(player,record)) return;
        float value=buffs.GetCustomVar(name);
        if(!float.IsNaN(value) && !float.IsInfinity(value)) record.Values[name]=value;
        // The native write already owns any network delivery. This observes its result only.
    }
    internal static void ObserveNativeOperation(NetPackageModifyCVar packet,World world,bool ranOriginal)
    {
        if (!ranOriginal || packet.operation==CVarOperation.set || !Medical(packet.cvarName)
            || world==null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;
        EntityPlayer player=world.GetEntity(packet.m_entityId) as EntityPlayer;
        if (!Bound(world,player,packet.Sender,out Record bound)) return;
        float value=player.Buffs.GetCustomVar(packet.cvarName);
        if(float.IsNaN(value)||float.IsInfinity(value)) return;
        Record record=Get(player,bound,true);
        bool changed=!record.Values.TryGetValue(packet.cvarName,out float previous)||previous!=value;
        record.Values[packet.cvarName]=value; // Observe the already executed native result; never apply the operation again.
        if(bound.Connection.IsServer && changed)
            player.Buffs.SetCustomVarNetwork(packet.cvarName,value,CVarOperation.set);
    }
    internal static void BootstrapSpawn(INetConnection recipientConnection,NetPackage package)
    {
        var spawn=package as NetPackageEntitySpawn;
        ConnectionManager connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        World world=GameManager.Instance?.World;
        if(spawn?.es==null || connection==null || !connection.IsServer || world==null
            || !RebirthSurvivorMode.IsEnabledForCurrentWorld() || connection.Clients==null) return;
        EntityPlayer player=world.GetEntity(spawn.es.id) as EntityPlayer;
        ClientInfo owner=connection.Clients.ForEntityId(spawn.es.id);
        if(!Bound(world,player,owner,out Record bound)) return;
        ClientInfo recipient=null;
        foreach(ClientInfo client in connection.Clients.List)
            if(client.loginDone && client.netConnection!=null && client.netConnection.Length>package.Channel
                && ReferenceEquals(client.netConnection[package.Channel],recipientConnection)
                && !recipientConnection.IsDisconnected()) recipient=client;
        if(recipient==null || ReferenceEquals(recipient,owner)) return;
        Bootstrap(owner); // Seed only native restored state if not admitted yet; preserve latest reliable values.
        Record record=Get(player,bound,false);
        if(record==null) return;
        foreach(var pair in record.Values)
            recipient.SendPackage(NetPackageManager.GetPackage<NetPackageModifyCVar>()
                .Setup(player,pair.Key,pair.Value,CVarOperation.set));
    }
    internal static bool QueueScope(NetPackage package,World world,out int entityId)
    {
        entityId=-1;
        if(world==null || !world.IsRemote() || !ReferenceEquals(GameManager.Instance?.World,world)
            || !RebirthSurvivorMode.IsEnabledForCurrentWorld() || package.Sender!=null
            || world.netEntityPackageQueue==null || world.entityAsyncManager==null) return false;
        ConnectionManager connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(connection==null || connection.IsServer || connection.connectionToServer==null
            || connection.connectionToServer.Length==0 || connection.connectionToServer[0]==null
            || connection.connectionToServer[0].IsDisconnected()) return false;
        if(package is NetPackageModifyCVar medical && Medical(medical.cvarName)) entityId=medical.m_entityId;
        else if(package is NetPackageEntityStatsBuff stats
            && world.netEntityPackageQueue.entityPackageQueues.TryGetValue(stats.m_entityId,out Queue<NetPackage> queued))
            foreach(NetPackage waiting in queued)
                if(waiting is NetPackageModifyCVar scalar && Medical(scalar.cvarName)) entityId=stats.m_entityId;
        return entityId>=0;
    }
    internal static void Forget(EntityPlayer player) { if(player!=null) records.Remove(player); }
    internal static void Clear() { records=new ConditionalWeakTable<EntityPlayer,Record>(); reading=null; starter=null; }
}

[HarmonyPatch(typeof(NetPackageModifyCVar),nameof(NetPackageModifyCVar.ProcessPackage))]
internal static class RebirthSelfMedicalReliableReceive
{
    static bool Prefix(NetPackageModifyCVar __instance,World _world) => RebirthSelfMedicalReplicaLane.Receive(__instance,_world);
    static void Postfix(NetPackageModifyCVar __instance,World _world,bool __runOriginal)
        => RebirthSelfMedicalReplicaLane.ObserveNativeOperation(__instance,_world,__runOriginal);
}
[HarmonyPatch(typeof(NetPackageEntityStatsBuff),nameof(NetPackageEntityStatsBuff.ProcessPackage))]
internal static class RebirthSelfMedicalStatsReceive
{
    static bool Prefix(NetPackageEntityStatsBuff __instance,World _world,out RebirthSelfMedicalReplicaLane.ReadScope __state)
        => RebirthSelfMedicalReplicaLane.EnterStats(__instance,_world,out __state);
    static Exception Finalizer(Exception __exception,RebirthSelfMedicalReplicaLane.ReadScope __state)
    { RebirthSelfMedicalReplicaLane.ExitStats(__state,__exception); return __exception; }
}
[HarmonyPatch(typeof(EntityBuffs),nameof(EntityBuffs.SetCustomVar))]
internal static class RebirthSelfMedicalStatsScalarWrite
{
    static bool Prefix(EntityBuffs __instance,string _name) => RebirthSelfMedicalReplicaLane.AllowWrite(__instance,_name);
    static void Postfix(EntityBuffs __instance,string _name,bool __runOriginal)
        => RebirthSelfMedicalReplicaLane.ObserveScalarWrite(__instance,_name,__runOriginal);
}
[HarmonyPatch(typeof(GameManager),nameof(GameManager.RequestToSpawnPlayer))]
internal static class RebirthSelfMedicalNativeRestoreBootstrap
{
    static void Postfix(ClientInfo _cInfo) => RebirthSelfMedicalReplicaLane.Bootstrap(_cInfo);
}
[HarmonyPatch(typeof(World),nameof(World.RemoveEntity))]
internal static class RebirthSelfMedicalReplicaEntityTeardown
{
    static void Prefix(World __instance,int _entityId) => RebirthSelfMedicalReplicaLane.Forget(__instance.GetEntity(_entityId) as EntityPlayer);
}
[HarmonyPatch(typeof(ClientInfoCollection),nameof(ClientInfoCollection.Remove))]
internal static class RebirthSelfMedicalReplicaOwnerTeardown
{
    static void Prefix(ClientInfo _cInfo) => RebirthSelfMedicalReplicaLane.Forget(GameManager.Instance?.World?.GetEntity(_cInfo.entityId) as EntityPlayer);
}
[HarmonyPatch(typeof(World),nameof(World.Cleanup))]
internal static class RebirthSelfMedicalReplicaWorldTeardown { static void Prefix()=>RebirthSelfMedicalReplicaLane.Clear(); }
[HarmonyPatch(typeof(ConnectionManager),nameof(ConnectionManager.Disconnect))]
internal static class RebirthSelfMedicalReplicaDisconnect { static void Prefix()=>RebirthSelfMedicalReplicaLane.Clear(); }
[HarmonyPatch(typeof(ConnectionManager),nameof(ConnectionManager.DisconnectFromServer))]
internal static class RebirthSelfMedicalReplicaServerDisconnect { static void Prefix()=>RebirthSelfMedicalReplicaLane.Clear(); }
[HarmonyPatch(typeof(ClientInfoCollection),nameof(ClientInfoCollection.Clear))]
internal static class RebirthSelfMedicalReplicaClientsClear { static void Prefix()=>RebirthSelfMedicalReplicaLane.Clear(); }

[HarmonyPatch(typeof(World),nameof(World.RemoveEntityFromMap))]
internal static class RebirthSelfMedicalReplicaMapTeardown
{
    static void Prefix(Entity _entity) => RebirthSelfMedicalReplicaLane.Forget(_entity as EntityPlayer);
}

[HarmonyPatch(typeof(NetConnectionSimple),nameof(NetConnectionSimple.AddToSendQueue),new Type[]{typeof(NetPackage)})]
internal static class RebirthSelfMedicalSimpleSpawnBootstrap
{
    static void Postfix(NetConnectionSimple __instance,NetPackage _package) => RebirthSelfMedicalReplicaLane.BootstrapSpawn(__instance,_package);
}
[HarmonyPatch(typeof(NetConnectionSteam),nameof(NetConnectionSteam.AddToSendQueue),new Type[]{typeof(NetPackage)})]
internal static class RebirthSelfMedicalSteamSpawnBootstrap
{
    static void Postfix(NetConnectionSteam __instance,NetPackage _package) => RebirthSelfMedicalReplicaLane.BootstrapSpawn(__instance,_package);
}
[HarmonyPatch(typeof(NetPackage),nameof(NetPackage.ShouldProcess))]
internal static class RebirthSelfMedicalPendingShouldProcess
{
    static bool Prefix(NetPackage __instance,World _world,ref bool __result)
    {
        if(!RebirthSelfMedicalReplicaLane.QueueScope(__instance,_world,out int id)
            || (!_world.netEntityPackageQueue.HasPackagesForEntity(id) && !_world.entityAsyncManager.IsEntityPending(id))) return true;
        __result=false;
        return false;
    }
}
[HarmonyPatch(typeof(NetPackage),nameof(NetPackage.HandleSkipped))]
internal static class RebirthSelfMedicalPendingHandleSkipped
{
    static bool Prefix(NetPackage __instance,World _world)
    {
        if(!RebirthSelfMedicalReplicaLane.QueueScope(__instance,_world,out int id)
            || (!_world.netEntityPackageQueue.HasPackagesForEntity(id) && !_world.entityAsyncManager.IsEntityPending(id))) return true;
        _world.netEntityPackageQueue.EnqueueNetPackageForEntity(id,__instance);
        return false;
    }
}

// Preserve admitted owner scalar only during exact late native replica fracture initialization.
[HarmonyPatch(typeof(EntityBuffs),nameof(EntityBuffs.FireEvent),new Type[]{typeof(MinEventTypes),typeof(BuffClass),typeof(MinEventParams)})]
internal static class RebirthSelfMedicalReplicaBuffStarter
{
    static void Prefix(EntityBuffs __instance,MinEventTypes _eventType,BuffClass _buffClass,MinEventParams _params,out RebirthSelfMedicalReplicaLane.StarterScope __state)
        => __state=RebirthSelfMedicalReplicaLane.EnterStarter(__instance,_eventType,_buffClass,_params);
    static Exception Finalizer(Exception __exception,RebirthSelfMedicalReplicaLane.StarterScope __state)
    {RebirthSelfMedicalReplicaLane.ExitStarter(__state);return __exception;}
}

