using System;
using System.Collections.Generic;
public enum RebirthNpcOwnershipKind { None, Player }
public class Entity { public int entityId; }
public class EntityAlive : Entity { public HashSet<int> owned=new HashSet<int>(); public bool HasOwnedEntity(int id){return owned.Contains(id);} public void RemoveOwnedEntity(int id){owned.Remove(id);} public void AddOwnedEntity(Entity e){owned.Add(e.entityId);} }
public class EntityPlayer : EntityAlive {}
public class World { public EntityPlayer local; public EntityPlayer GetPrimaryPlayer(){return local;} public List<EntityPlayer> GetPlayers(){var p=new List<EntityPlayer>();foreach(var e in entities.Values)if(e is EntityPlayer)p.Add((EntityPlayer)e);return p;} public bool remote=true; public Dictionary<int,Entity> entities=new Dictionary<int,Entity>(); public bool IsRemote(){return remote;} public Entity GetEntity(int id){Entity e;return entities.TryGetValue(id,out e)?e:null;} }
public class Buffs { public float leader; public void SetCustomVar(string k,float v,bool sync){if(sync)throw new Exception("client sent CVar");leader=v;} }
public class RebirthNpcRuntimeState { public RebirthNpcOwnershipKind OwnershipKind;public string OwnerId; }
public class EntityRebirthDogCompanion : Entity { public World world;public RebirthNpcRuntimeState RebirthRuntimeState;public int belongsPlayerId; public Buffs Buffs=new Buffs(); }
public static class RebirthDogRuntimeService { public static EntityPlayer ResolveOwnerPublic(World w,string id){return ResolveOwner(w,id);}
// OWNER_RESOLVER
}
public class PersistentPlayerData { public string PrimaryId; }
public class PlayerList { public PersistentPlayerData GetPlayerDataFromEntityID(int id){return null;} }
public class GameManager { public static GameManager Instance; public World World; public PlayerList GetPersistentPlayerList(){return new PlayerList();} public PersistentPlayerData local; public PersistentPlayerData GetPersistentLocalPlayer(){return local;} }
public class RebirthStablePlayerIdentity { public string CanonicalId; public static bool TryResolveServerEntity(EntityPlayer p,out RebirthStablePlayerIdentity i){i=null;return false;} }
public static class RebirthDogLifecycleService {
// IDENTITY
}
public static class Projection {
static int ResolveLegacyOwnerEntityId(EntityRebirthDogCompanion d){return d.belongsPlayerId>0?d.belongsPlayerId:(int)d.Buffs.leader;}
__PRODUCTION_HELPER__
}
public static class Checks {
static void Check(bool ok,string m){if(!ok)throw new Exception(m);}
public static void Main(){var w=new World();var local=new EntityPlayer{entityId=7};var other=new EntityPlayer{entityId=8};w.local=local;w.entities[7]=local;w.entities[8]=other;GameManager.Instance=new GameManager{World=w,local=new PersistentPlayerData{PrimaryId="Steam_owner"}};var d=new EntityRebirthDogCompanion{entityId=9,world=w,RebirthRuntimeState=new RebirthNpcRuntimeState{OwnershipKind=RebirthNpcOwnershipKind.Player,OwnerId="Steam_owner"}};
Projection.SynchronizeClientOwnerProjection(d);Check(d.belongsPlayerId==7&&local.owned.Contains(9)&&d.Buffs.leader==7,"joining local owner not projected");Check(!other.owned.Contains(9),"other player claimed dog");Projection.SynchronizeClientOwnerProjection(d);Check(local.owned.Count==1,"duplicate roster");d.RebirthRuntimeState.OwnershipKind=RebirthNpcOwnershipKind.None;Projection.SynchronizeClientOwnerProjection(d);Check(!local.owned.Contains(9)&&d.belongsPlayerId==0&&d.Buffs.leader==0,"dismiss not cleared");Console.WriteLine("PASS: real identity lookup + resolver + projection restores joining-client dog roster, isolates other player and clears dismissal");}}
