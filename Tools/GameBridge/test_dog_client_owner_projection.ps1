$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/NPC/Dog/RebirthDogLifecycleService.cs')
$start=$source.IndexOf('    public static void SynchronizeClientOwnerProjection')
$end=$source.IndexOf('    public static void SynchronizeLegacyOwnerProjection',$start)
if($start -lt 0 -or $end -le $start){throw 'Production helper not found'}
$helper=$source.Substring($start,$end-$start)
$template=@'
using System;
using System.Collections.Generic;
public enum RebirthNpcOwnershipKind { None, Player }
public class Entity { public int entityId; }
public class EntityAlive : Entity { public HashSet<int> owned=new HashSet<int>(); public bool HasOwnedEntity(int id){return owned.Contains(id);} public void RemoveOwnedEntity(int id){owned.Remove(id);} public void AddOwnedEntity(Entity e){owned.Add(e.entityId);} }
public class EntityPlayer : EntityAlive {}
public class World { public bool remote=true; public Dictionary<int,Entity> entities=new Dictionary<int,Entity>(); public bool IsRemote(){return remote;} public Entity GetEntity(int id){Entity e;return entities.TryGetValue(id,out e)?e:null;} }
public class Buffs { public float leader; public void SetCustomVar(string k,float v,bool sync){if(sync)throw new Exception("client sent CVar");leader=v;} }
public class RebirthNpcRuntimeState { public RebirthNpcOwnershipKind OwnershipKind;public string OwnerId; }
public class EntityRebirthDogCompanion : Entity { public World world;public RebirthNpcRuntimeState RebirthRuntimeState;public int belongsPlayerId; public Buffs Buffs=new Buffs(); }
public static class RebirthDogRuntimeService { public static EntityPlayer ResolveOwnerPublic(World w,string id){int n;return int.TryParse(id,out n)?w.GetEntity(n) as EntityPlayer:null;} }
public static class Projection {
static int ResolveLegacyOwnerEntityId(EntityRebirthDogCompanion d){return d.belongsPlayerId>0?d.belongsPlayerId:(int)d.Buffs.leader;}
__PRODUCTION_HELPER__
}
public static class Checks {
static void Check(bool ok,string m){if(!ok)throw new Exception(m);}
public static void Main(){var w=new World();var old=new EntityPlayer{entityId=1};w.entities[1]=old;var d=new EntityRebirthDogCompanion{entityId=5,world=w,belongsPlayerId=1,RebirthRuntimeState=new RebirthNpcRuntimeState{OwnershipKind=RebirthNpcOwnershipKind.Player,OwnerId="2"}};old.owned.Add(5);
Projection.SynchronizeClientOwnerProjection(d);Check(d.belongsPlayerId==1&&old.owned.Contains(5),"unloaded owner cleared projection");
var next=new EntityPlayer{entityId=2};w.entities[2]=next;Projection.SynchronizeClientOwnerProjection(d);Check(d.belongsPlayerId==2&&d.Buffs.leader==2&&next.owned.Contains(5)&&!old.owned.Contains(5),"owner transfer projection");
Projection.SynchronizeClientOwnerProjection(d);Check(next.owned.Count==1,"duplicate projection");
d.RebirthRuntimeState.OwnershipKind=RebirthNpcOwnershipKind.None;Projection.SynchronizeClientOwnerProjection(d);Check(d.belongsPlayerId==0&&d.Buffs.leader==0&&!next.owned.Contains(5),"dismiss projection");
w.remote=false;d.belongsPlayerId=8;Projection.SynchronizeClientOwnerProjection(d);Check(d.belongsPlayerId==8,"server changed");Console.WriteLine("PASS: delayed owner, owner change, idempotence, dismissal, server isolation, no CVar send");}}

'@
Add-Type -TypeDefinition ($template.Replace('__PRODUCTION_HELPER__',$helper))
[Checks]::Main()
