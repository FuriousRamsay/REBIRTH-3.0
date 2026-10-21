using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

// Exact native door features; no background threads and no dog callbacks.
internal static class RebirthHumanDoorRuntime
{
    private sealed class Restore
    {internal World World;internal Vector3i Position;internal TileEntityComposite Tile;internal TEFeatureDoor Door;internal bool Before,Applied;internal float Due;}
    private static readonly List<Restore> Restores=new List<Restore>();
    internal static void Tick()
    {
        var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world==null||world.IsRemote())return;
        for(int i=Restores.Count-1;i>=0;i--)
        {
            var r=Restores[i];
            if(!ReferenceEquals(r.World,world)){Restores.RemoveAt(i);continue;}
            if(Time.time<r.Due)continue;Restores.RemoveAt(i);
            if(ReferenceEquals(world.GetTileEntity(r.Position),r.Tile)&&r.Door.IsOpen()==r.Applied)r.Door.SetOpen(r.Before,true);
        }
    }
    internal static bool Check(EntityRebirthNPC actor,bool blockedAttack=false)
    {
        if(!RebirthHumanAiPolicy.Ready(actor)||actor.moveHelper?.HitInfo==null||actor.moveHelper.BlockedTime<=.35f||
            actor.moveHelper.BlockedFlags==0)return false;
        var position=actor.moveHelper.HitInfo.hit.blockPos;
        var tile=actor.world.GetTileEntity(position) as TileEntityComposite;
        var door=tile?.GetFeature<TEFeatureDoor>();if(door==null)return false;
        var security=tile.GetFeature<TEFeatureLockable>();
        if(security!=null&&security.IsLocked())
        {
            // Preserve companion access through its actual owner's native door
            // permission. Merely finding a player record is not lock authority.
            var owner=RebirthHumanAiPolicy.Owner(actor);
            var data=owner!=null?GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(owner.entityId):null;
            if(data?.PrimaryId==null||!security.IsUserAllowed(data.PrimaryId))return false;
        }
        bool hatch=actor.world.GetBlock(position).Block.GetBlockName().IndexOf("Hatch",StringComparison.OrdinalIgnoreCase)>=0;
        bool before=door.IsOpen();if(before&&!hatch||!before&&hatch)return false;
        if(blockedAttack&&(security?.IsLocked()==true||actor.Buffs.GetCustomVar("$varFuriousRamsayOpenDoors")!=1f))return false;
        foreach(var r in Restores)if(ReferenceEquals(r.World,actor.world)&&r.Position==position)return true;
        if(Restores.Count>=256)return false;
        door.SetOpen(!before,true);
        Restores.Add(new Restore {World=actor.world,Position=position,Tile=tile,Door=door,Before=before,Applied=!before,Due=Time.time+2f});
        actor.moveHelper.ClearBlocked();return true;
    }
}

[Preserve]
public sealed class EAIRebirthHumanEnvironment : EAIBase
{
    private EntityRebirthNPC actor;
    public override void Init(EntityAlive entity){base.Init(entity);actor=entity as EntityRebirthNPC;MutexBits=0;executeDelay=.5f;}
    public override bool CanExecute()=>RebirthHumanAiPolicy.Ready(actor);
    public override bool Continue()=>CanExecute();
    public override void Update(){RebirthHumanDoorRuntime.Check(actor);RebirthHumanWeaponSelection.Tick(actor);}
}

