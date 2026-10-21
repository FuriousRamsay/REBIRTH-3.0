using System;
using System.Runtime.CompilerServices;
using HarmonyLib;

// One actual authenticated native ranged-effects invocation supplies this
// action's server hit calculation. Legacy client fire-position requests stay
// rejected. This does not provide an ammunition/debit permit for finite guns.
internal static class RebirthExtinguisherNativeShot
{
    private sealed class PeerShots{internal int Sequence;internal long Next;internal WeakReference<World> World;}
    private static readonly ConditionalWeakTable<ClientInfo,PeerShots> peers=new ConditionalWeakTable<ClientInfo,PeerShots>();
    [ThreadStatic]private static Shot entered;
    private sealed class Shot
    {
        internal World World;internal EntityPlayer Actor;internal ClientInfo Peer;
        internal ItemActionRangedRebirthExtinguisher Action;internal ItemActionRanged.ItemActionDataRanged Data;
        internal int Slot,Deaths,Sequence;
        internal Shot Previous;internal bool NativeEffectsReturned;
        internal bool Current()=>ThreadManager.IsMainThread()&&SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer==true&&
            ReferenceEquals(GameManager.Instance?.World,World)&&!World.IsRemote()&&
            ReferenceEquals(Actor.world,World)&&ReferenceEquals(Data?.invData?.world,World)&&
            ReferenceEquals(World.GetEntity(Actor.entityId),Actor)&&!Actor.IsDead()&&Actor.Died==Deaths&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(Actor.entityId),Peer)&&
            Actor.inventory?.holdingItemIdx==Slot&&Data?.invData?.item?.Actions!=null&&ReferenceEquals(Actor.inventory.holdingItemData,Data.invData)&&
            ReferenceEquals(Data.invData.holdingEntity,Actor)&&Data.indexInEntityOfAction>=0&&
            Data.indexInEntityOfAction<Data.invData.item.Actions.Length&&
            ReferenceEquals(Data.invData.item.Actions[Data.indexInEntityOfAction],Action)&&
            peers.TryGetValue(Peer,out var source)&&source.Sequence==Sequence;
    }
    internal static void ObserveActualNativeEffects(ItemActionRangedRebirthExtinguisher action,ItemActionData data,int firingState)
    {
        var shot=entered;
        if(shot!=null&&(firingState==1||firingState==2)&&ReferenceEquals(shot.Action,action)&&ReferenceEquals(shot.Data,data)&&shot.Current())
            shot.NativeEffectsReturned=true;
    }
    [HarmonyPatch(typeof(NetPackageItemActionEffects),nameof(NetPackageItemActionEffects.ProcessPackage))]
    internal static class NativeEffects
    {
        private static bool Prefix(NetPackageItemActionEffects __instance,World __0,GameManager __1,bool __runOriginal,out object __state)
        {
            __state=null;
            if(!__runOriginal||!ThreadManager.IsMainThread()||__0==null||__0.IsRemote()||
                SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||
                !ReferenceEquals(__0,GameManager.Instance?.World)||!ReferenceEquals(__1,GameManager.Instance))return true;
            var actor=__0.GetEntity(__instance.entityId) as EntityPlayer;
            var held=actor?.inventory?.holdingItemData;
            int index=__instance.actionIdx;
            if(held?.item?.Actions==null||index<0||index>=held.item.Actions.Length||
                held.item.Actions[index] is not ItemActionRangedRebirthExtinguisher action)return true;
            // Native packet processing itself does not check the owning sender.
            // A foreign packet cannot use this holder's action or visuals.
            var peer=__instance.Sender;
            if(peer==null||peer.entityId!=actor.entityId||!__instance.ValidEntityIdForSender(actor.entityId)||
                !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(actor.entityId),peer)||
                actor is EntityPlayerLocal||actor.IsDead()||actor.inventory.holdingItemIdx!=__instance.slotIdx)return false;
            int firing=(int)__instance.firingState;
            if(firing==0)return true; // Native release remains native; never a hit.
            if(firing!=1&&firing!=2||!action.UsesNativeInfiniteSpray||__instance.userData<=0||
                held.actionData==null||index>=held.actionData.Count||held.actionData[index] is not ItemActionRanged.ItemActionDataRanged data||
                !ReferenceEquals(data.invData,held)||data.indexInEntityOfAction!=index)return false;
            float delay=action.NativeShotDelay(data);
            if(float.IsNaN(delay)||float.IsInfinity(delay)||delay<=0f)return false;
            var source=peers.GetValue(peer,_=>new PeerShots());
            long now=System.Diagnostics.Stopwatch.GetTimestamp();
            if(__instance.userData<=source.Sequence)return false;
            double wait=System.Diagnostics.Stopwatch.Frequency*Math.Max(.01,(double)delay)*.9;
            if(wait>long.MaxValue-now)return false;
            bool paced=source.World!=null&&source.World.TryGetTarget(out var priorWorld)&&ReferenceEquals(priorWorld,__0)&&now<source.Next;
            source.Sequence=__instance.userData;source.World=new WeakReference<World>(__0);
            if(paced)return false; // A refused ordinal cannot be replayed later.
            source.Next=now+(long)wait;
            var shot=new Shot{World=__0,Actor=actor,Peer=peer,Action=action,Data=data,Slot=__instance.slotIdx,Deaths=actor.Died,Sequence=source.Sequence,Previous=entered};
            entered=shot;__state=shot;
            return true;
        }
        private static void Postfix(bool __runOriginal,object __state)
        {
            if(!__runOriginal||__state is not Shot shot)return;
            try{if(shot.NativeEffectsReturned&&shot.Current()&&!GameManager.Instance.IsPaused())shot.Action.ApplyReplicatedNativeHit(shot.Data);}
            catch(Exception error){Log.Warning("[REBIRTH Fire] Native extinguisher hit failed; shot ordinal consumed, no automatic retry: "+error.Message);}
        }
        private static Exception Finalizer(Exception __exception,object __state)
        {if(__state is Shot shot&&ReferenceEquals(entered,shot))entered=shot.Previous;return __exception;}
    }
}
