using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class EAIRebirthBreakObstruction : EAIBase
{
    private EntityRebirthNPC actor;private int delay,previousSlot=-1,bareSlot=-1;private float boost;
    private readonly List<Entity> allies=new List<Entity>();
    private ItemActionAttackData data;private ItemActionAttackData.HitDelegate previous,installed;
    public override void Init(EntityAlive entity){base.Init(entity);actor=entity as EntityRebirthNPC;MutexBits=8;executeDelay=.15f;}
    private bool Ready()=>RebirthHumanAiPolicy.Ready(actor)&&(!RebirthHumanAiPolicy.Owned(actor)||actor.RebirthRuntimeState.Order==RebirthNpcOrderState.Stay||RebirthHumanAiPolicy.Offline(actor));
    public override bool CanExecute()
    {
        if(!Ready()||actor.moveHelper==null||actor.moveHelper.HitInfo==null||!actor.moveHelper.CanBreakBlocks||actor.moveHelper.BlockedTime<=.35f||
            (actor.Jumping&&!actor.moveHelper.IsDestroyArea))return false;
        if(RebirthHumanDoorRuntime.Check(actor,true))return false;
        float radius=actor.m_characterController.GetRadius()+.6f;
        return actor.moveHelper.CalcBlockedDistanceSq()<=radius*radius;
    }
    public override void Start()
    {
        delay=1;previousSlot=bareSlot=-1;actor.IsBreakingBlocks=true;
        var actions=actor.inventory.holdingItem?.Actions;
        // Legacy NPC guns had a melee action alongside their ranged action.
        // Playerlike guns do not: select an existing empty slot for natural
        // bare hands, without replacing/copying the owned firearm or its ammo.
        if(actions==null||actions.Length==0||!(actions[0] is ItemActionMelee||actions[0] is ItemActionDynamicMelee))
        {
            var slots=actor.inventory.ItemGrid.items;
            for(int i=0;i<slots.Length;i++)if(slots[i]!=null&&slots[i].IsEmpty())
            {previousSlot=actor.inventory.holdingItemIdx;bareSlot=i;actor.inventory.SetHoldingItemIdxNoHolsterTime(i);break;}
        }
        var selectedActions=actor.inventory.holdingItem?.Actions;
        if(selectedActions==null||selectedActions.Length==0||!(selectedActions[0] is ItemActionMelee||selectedActions[0] is ItemActionDynamicMelee)){data=null;return;}
        var native=actor.inventory.holdingItemData?.actionData;
        data=native!=null&&native.Count>0?native[0] as ItemActionAttackData:null;
        if(data!=null){previous=data.hitDelegate;installed=GetHitInfo;data.hitDelegate=installed;}
        var block=actor.world.GetBlock(actor.moveHelper.HitInfo.hit.blockPos).Block;
        actor.IsBreakingDoors=block.HasTag(BlockTags.Door)||block.HasTag(BlockTags.ClosetDoor);
    }
    public override bool Continue()=>data!=null&&Ready()&&actor.onGround&&CanExecute();
    public override void Update()
    {
        if(!Continue()||--delay>0)return;
        actor.SetLookPosition(Vector3.zero);boost=0f;allies.Clear();
        actor.world.GetEntitiesInBounds(typeof(EntityZombie),new Bounds(actor.position,new Vector3(1.7f,1.5f,1.7f)),allies);
        foreach(var ally in allies)if(ally!=actor)boost+=.2f;allies.Clear();
        if(!actor.Attack(false))return;actor.IsBreakingBlocks=true;
        float jitter=.25f+RandomFloat*.8f;if(actor.moveHelper.IsUnreachableAbove)jitter*=.5f;
        delay=(int)((jitter+.75f)*20f);actor.Attack(true);
    }
    private WorldRayHitInfo GetHitInfo(out float scale){scale=actor.moveHelper.DamageScale+boost;return actor.moveHelper.HitInfo;}
    public override void Reset()
    {
        if(data!=null&&data.hitDelegate==installed)data.hitDelegate=previous;data=null;
        if(actor==null)return;actor.IsBreakingBlocks=actor.IsBreakingDoors=false;
        if(bareSlot>=0&&actor.inventory.holdingItemIdx==bareSlot&&actor.inventory.GetItem(bareSlot).IsEmpty())
            actor.inventory.SetHoldingItemIdxNoHolsterTime(previousSlot);
        previousSlot=bareSlot=-1;
    }
}


