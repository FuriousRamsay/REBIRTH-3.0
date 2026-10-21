using System;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;

// Nonfinancial native action courier. No client coordinates or fire parameters
// cross this boundary; the server resolves its own current native melee target.
internal static class RebirthTorchNativeHit
{
    private sealed class Original
    {
        internal EntityPlayerLocal Actor;internal World World;internal ItemInventoryData Held;
        internal ItemActionAttack.AttackHitInfo Details;internal Original Previous;internal bool Captured;internal int Deaths;
    }
    [ThreadStatic]private static Original entered;
    private static long nextOrdinal;
    private sealed class Peer {internal long Ordinal,Next;internal WeakReference<World> World;}
    private static readonly ConditionalWeakTable<ClientInfo,Peer> peers=new ConditionalWeakTable<ClientInfo,Peer>();
    private static bool Torch(ItemInventoryData held)=>held?.item?.GetItemName()=="meleeToolTorch"&&
        held.item.Actions!=null&&held.item.Actions.Length>0&&held.item.Actions[0] is ItemActionDynamicMelee;
    // Called only from the original configured min-event, inside native Hit.
    internal static bool Capture(MinEventParams context,RebirthFireIgnitionCause cause,int range,bool positionTarget,float delay)
    {
        var original=entered;
        if(original==null||cause!=RebirthFireIgnitionCause.BlockEvent||range!=0||positionTarget||delay!=0f||
            !ReferenceEquals(context?.Self,original.Actor)||!RebirthFireMinEventHelpers.IsCurrentEventWorld(context,original.World)||
            original.Actor.Died!=original.Deaths||!ReferenceEquals(original.Actor.inventory.holdingItemData,original.Held)||!Torch(original.Held)||
            context.ItemValue?.ItemClass?.GetItemName()!="meleeToolTorch"||!original.Details.bBlockHit||original.Details.bKilled)return false;
        original.Captured=true;return true;
    }
    [HarmonyPatch(typeof(ItemActionAttack),nameof(ItemActionAttack.Hit))]
    internal static class NativeHit
    {
        private static void Prefix(int _attackerEntityId,ItemActionAttack.AttackHitInfo _attackDetails,
            ItemActionAttack.EnumAttackMode _attackMode,bool __runOriginal,out object __state)
        {
            __state=null;
            var world=GameManager.Instance?.World;
            if(!__runOriginal||!ThreadManager.IsMainThread()||world?.IsRemote()!=true||_attackDetails==null||
                _attackMode==ItemActionAttack.EnumAttackMode.Simulate||world.GetEntity(_attackerEntityId) is not EntityPlayerLocal actor||
                !ReferenceEquals(world.GetPrimaryPlayer(),actor)||actor.IsDead()||!Torch(actor.inventory?.holdingItemData))return;
            var original=new Original{Actor=actor,World=world,Held=actor.inventory.holdingItemData,Details=_attackDetails,Previous=entered,Deaths=actor.Died};
            entered=original;__state=original;
        }
        private static void Postfix(bool __runOriginal,object __state)
        {
            if(!__runOriginal||__state is not Original original||!original.Captured||!ReferenceEquals(entered,original)||
                !RebirthFireMinEventHelpers.IsCurrentEventWorld(new MinEventParams{Self=original.Actor},original.World)||
                original.Actor.IsDead()||original.Actor.Died!=original.Deaths||string.IsNullOrEmpty(original.World.worldState?.Guid)||
                original.World.worldState.Guid.Length>128||!ReferenceEquals(original.Actor.inventory.holdingItemData,original.Held)||!Torch(original.Held))return;
            long ordinal=Interlocked.Increment(ref nextOrdinal);if(ordinal<=0)return;
            try{SingletonMonoBehaviour<ConnectionManager>.Instance?.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthTorchNativeHit>()
                .Setup(original.Actor.entityId,original.Actor.inventory.holdingItemIdx,original.Deaths,original.World.worldState.Guid,ordinal),false);}
            catch(Exception error){Log.Warning("[REBIRTH Fire] Torch action courier failed without replay: "+error.Message);}
        }
        private static Exception Finalizer(Exception __exception,object __state)
        {if(__state is Original original&&ReferenceEquals(entered,original))entered=original.Previous;return __exception;}
    }
    internal static void Receive(NetPackageRebirthTorchNativeHit packet,World world,int actorId,int slot,int deaths,string worldGuid,long ordinal)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var sender=packet.Sender;
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!ReferenceEquals(world,GameManager.Instance?.World)||manager?.IsServer!=true||
            sender==null||sender.entityId!=actorId||!packet.ValidEntityIdForSender(actorId)||!ReferenceEquals(manager.Clients?.ForEntityId(actorId),sender)||
            world.GetEntity(actorId) is not EntityPlayer actor||actor is EntityPlayerLocal||!ReferenceEquals(actor.world,world)||actor.IsDead()||actor.Died!=deaths||
            string.IsNullOrEmpty(worldGuid)||worldGuid!=world.worldState?.Guid||actor.inventory?.holdingItemIdx!=slot||ordinal<=0||
            !RebirthFireRuntimePolicy.Enabled||GameManager.Instance.IsPaused())return;
        var held=actor.inventory.holdingItemData;if(!Torch(held))return;
        var action=(ItemActionDynamicMelee)held.item.Actions[0];
        if(action.CreateModifierData(held,0) is not ItemActionDynamic.ItemActionDynamicData data)return;
        var tags=FastTags<TagGroup.Global>.Parse("primary")|held.item.ItemTags;
        float rate=EffectManager.GetValue(PassiveEffects.AttacksPerMinute,held.itemValue,60f,actor,null,tags);
        if(float.IsNaN(rate)||float.IsInfinity(rate)||rate<=0f)return;
        var peer=peers.GetValue(sender,_=>new Peer());if(ordinal<=peer.Ordinal)return;
        long now=System.Diagnostics.Stopwatch.GetTimestamp();double wait=System.Diagnostics.Stopwatch.Frequency*(60d/rate)*.9;
        if(wait>long.MaxValue-now)return;
        bool paced=peer.World!=null&&peer.World.TryGetTarget(out var previous)&&ReferenceEquals(previous,world)&&now<peer.Next;
        peer.Ordinal=ordinal;peer.World=new WeakReference<World>(world);if(paced)return;
        peer.Next=now+(long)wait;
        // Native target calculation uses actual server look/range/sphere/mask.
        // Fresh data prevents adoption of a former action's retained ray.
        WorldRayHitInfo hit;int layer=actor.GetModelLayer();
        try{hit=action.GetExecuteActionTarget(data)?.Clone();}
        finally{actor.SetModelLayer(layer);}
        if(hit==null||!hit.bHitValid||hit.tag!=null&&hit.tag.StartsWith("E_")||ItemActionAttack.GetBlockHit(world,hit).isair)return;
        if(!ReferenceEquals(world,GameManager.Instance?.World)||!ReferenceEquals(world.GetEntity(actorId),actor)||!ReferenceEquals(actor.world,world)||actor.IsDead()||actor.Died!=deaths||worldGuid!=world.worldState?.Guid||
            !ReferenceEquals(manager.Clients?.ForEntityId(actorId),sender)||!ReferenceEquals(actor.inventory.holdingItemData,held)||actor.inventory.holdingItemIdx!=slot||
            !Torch(held)||!ReferenceEquals(held.item.Actions[0],action)||!RebirthFireRuntimePolicy.Enabled)return;
        RebirthFireService.Instance.ScheduleIgnition(world,new[]{hit.hit.blockPos},actorId,RebirthFireIgnitionCause.BlockEvent,0f);
    }
}
