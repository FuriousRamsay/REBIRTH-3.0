using System;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;

public static partial class RebirthGameBridgeHandlers
{
    private sealed class FireAuditSourceToken{internal readonly string Value=Guid.NewGuid().ToString("N");}
    private static readonly ConditionalWeakTable<Entity,FireAuditSourceToken> fireAuditSources=new ConditionalWeakTable<Entity,FireAuditSourceToken>();
    // Read only. No diagnostic ignition, clearing, packet or policy mutation.
    private static object CaptureFireAuditState(World world,EntityPlayer actor,BridgeRequest req)
    {
        if(req.QueryInt("owner",0)==1)
        {
            if(!ThreadManager.IsMainThread()||world==null||!ReferenceEquals(world,GameManager.Instance?.World)||
                actor is not EntityPlayerLocal local||!ReferenceEquals(world.GetPrimaryPlayer(),local)||local.IsDead())return new{available=false,owner=true};
            var held=local.inventory?.holdingItemData;var action=held?.item?.Actions?.Length>0?held.item.Actions[0]:null;
            var ranged=action as ItemActionRanged;var magazines=ranged?.MagazineItemNames;int index=held?.itemValue?.SelectedAmmoTypeIndex??-1;
            string ammo=magazines!=null&&index>=0&&index<magazines.Length?magazines[index]:null;
            return new{available=held?.item!=null,owner=true,item=held?.item?.GetItemName(),ammo,
                launcher=action is ItemActionLauncher,torch=action is ItemActionDynamicMelee,
                extinguisher=action is ItemActionRangedRebirthExtinguisher,
                entityId=local.entityId,deaths=local.Died};
        }
        if(ThreadManager.IsMainThread()&&world?.IsRemote()==true&&ReferenceEquals(world,GameManager.Instance?.World)&&
            ReferenceEquals(actor,world.GetPrimaryPlayer())&&ReferenceEquals(world.GetEntity(actor.entityId),actor))
        {
            var localCenter=World.worldToBlockPos(actor.position);
            var position=new Vector3i(req.QueryInt("x",localCenter.x),req.QueryInt("y",localCenter.y),req.QueryInt("z",localCenter.z));
            if(req.QueryInt("radius",0)!=0||!RebirthFireVisualManager.TryObservePosition(world,position,out bool active,out ulong cooldown,out bool visible,out bool pending))
                return new{available=false,authoritative=false,projection=true};
            return new{schema=2,available=true,authoritative=false,projection=true,enabled=RebirthFireRuntimePolicy.Enabled,
                revision=RebirthFireVisualManager.LastRevision,snapshotPending=pending,active,visible,cooldown,
                x=position.x,y=position.y,z=position.z};
        }
        if(!RebirthFirePersistenceScope.IsCurrentWorld(world)||!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!ReferenceEquals(world,GameManager.Instance?.World)||
            SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true)return new{available=false,authoritative=false};
        var center=World.worldToBlockPos(actor.position);
        int x=req.QueryInt("x",center.x),y=req.QueryInt("y",center.y),z=req.QueryInt("z",center.z);
        int radius=Math.Max(0,Math.Min(32,req.QueryInt("radius",8)));
        var fires=new JArray();var cooldowns=new JArray();bool truncated=false;
        foreach(var fire in RebirthFireService.Instance.SnapshotFires())
        {
            var p=fire.Position;if(Math.Abs((long)p.x-x)>radius||Math.Abs((long)p.y-y)>radius||Math.Abs((long)p.z-z)>radius)continue;
            if(fires.Count>=128){truncated=true;continue;}
            fires.Add(new JObject{["x"]=p.x,["y"]=p.y,["z"]=p.z,["igniter"]=fire.IgniterEntityId,
                ["blockType"]=fire.SourceBlockType,["ignitedAt"]=fire.IgnitedWorldTime,["nextProcess"]=fire.NextProcessWorldTime});
        }
        foreach(var cooldown in RebirthFireService.Instance.SnapshotCooldowns())
        {
            var p=cooldown.Key;if(Math.Abs((long)p.x-x)>radius||Math.Abs((long)p.y-y)>radius||Math.Abs((long)p.z-z)>radius)continue;
            if(cooldowns.Count>=128){truncated=true;continue;}
            cooldowns.Add(new JObject{["x"]=p.x,["y"]=p.y,["z"]=p.z,["expires"]=cooldown.Value});
        }
        JObject source=null;int sourceId=req.QueryInt("source",-1);
        if(sourceId>0)
        {
            var entity=world.GetEntity(sourceId) as EntityAlive;
            if(entity!=null&&ReferenceEquals(entity.world,world))
                source=new JObject{["id"]=entity.entityId,["instance"]=fireAuditSources.GetValue(entity,_=>new FireAuditSourceToken()).Value,
                    ["class"]=entity.EntityClass?.entityClassName,["zombie"]=entity is EntityZombie,["alive"]=!entity.IsDead(),
                    ["fireElement"]=entity.Buffs.GetCustomVar("$FuriousRamsayElementFire"),
                    ["fireAoEBuff"]=entity.Buffs.HasBuff("FuriousRamsayFireZombieAoEDamageTimer")};
        }
        JObject saved=null;
        if(req.QueryInt("saved",0)==1)
        {
            if(RebirthFirePersistence.TryObserveSaved(out int savedRevision,out var savedFires,out var savedCooldowns,out string digest))
            {
                var savedRows=new JArray();var savedCooling=new JArray();
                foreach(var fire in savedFires)
                    if(fire.Position.x==x&&fire.Position.y==y&&fire.Position.z==z)
                        savedRows.Add(new JObject{["x"]=x,["y"]=y,["z"]=z,["igniter"]=fire.IgniterEntityId,
                            ["blockType"]=fire.SourceBlockType,["ignitedAt"]=fire.IgnitedWorldTime});
                if(savedCooldowns.TryGetValue(new Vector3i(x,y,z),out ulong expiry))savedCooling.Add(new JObject{["x"]=x,["y"]=y,["z"]=z,["expires"]=expiry});
                saved=new JObject{["available"]=true,["revision"]=savedRevision,["digest"]=digest,["fires"]=savedRows,["cooldowns"]=savedCooling};
            }
            else saved=new JObject{["available"]=false};
        }
        return new{schema=1,available=true,authoritative=true,enabled=RebirthFireRuntimePolicy.Enabled,
            worldTime=world.worldTime,revision=RebirthFireService.Instance.Revision,queued=RebirthFireDelayedIgnitionQueue.Count,
            center=new{x,y,z},radius,truncated,fires,cooldowns,source,saved};
    }
}
