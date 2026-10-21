using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

// Observe the existing native replicated projectile. Never replay native Hit,
// spend ammunition, or accept a separate client-selected ignition coordinate.
internal static class RebirthFlamingProjectileNative
{
    private sealed class Launch
    {
        internal World World;internal EntityPlayer Actor;internal ClientInfo Peer;internal ItemInventoryData Held;
        internal ItemActionLauncher Action;internal ItemActionLauncher.ItemActionDataLauncher Data;
        internal int Slot,Index,Deaths;internal Launch Previous;internal readonly List<Flight> Flights=new List<Flight>();
        internal bool Current()=>Authority(World)&&ReferenceEquals(World.GetEntity(Actor.entityId),Actor)&&!Actor.IsDead()&&
            ReferenceEquals(Actor.world,World)&&Actor.Died==Deaths&&
            ReferenceEquals(CurrentPeer(Actor),Peer)&&Actor.inventory?.holdingItemIdx==Slot&&
            ReferenceEquals(Actor.inventory.holdingItemData,Held)&&Held.item?.Actions!=null&&Index>=0&&Index<Held.item.Actions.Length&&
            ReferenceEquals(Held.item.Actions[Index],Action)&&ReferenceEquals(Data.invData,Held);
    }
    private sealed class Flight
    {
        internal World World;internal EntityPlayer Actor;internal ClientInfo Peer;internal ProjectileMoveScript Projectile;
        internal ItemActionLauncher.ItemActionDataLauncher Data;internal string Ammo;
        // A held-weapon change is normal during flight; a reused body after
        // respawn is a different native life and cannot inherit this launch.
        internal int Deaths;internal bool FireReturned,LaunchReturned,Consumed;
        internal bool Current()=>!Consumed&&Authority(World)&&Projectile!=null&&Actor!=null&&
            ReferenceEquals(World.GetEntity(Actor.entityId),Actor)&&ReferenceEquals(Actor.world,World)&&Actor.Died==Deaths&&
            ReferenceEquals(CurrentPeer(Actor),Peer)&&ReferenceEquals(Projectile.firingEntity,Actor)&&
            ReferenceEquals(Projectile.actionData,Data)&&Projectile.ProjectileOwnerID==Actor.entityId&&
            Projectile.itemProjectile?.GetItemName()==Ammo&&Projectile.itemValueProjectile?.ItemClass?.GetItemName()==Ammo;
    }
    private sealed class Collision
    {internal Flight Flight;internal Collision Previous;internal WorldRayHitInfo Hit;internal bool DeadReturned;}
    [ThreadStatic]private static Launch launching;
    [ThreadStatic]private static Collision colliding;
    private static readonly ConditionalWeakTable<ProjectileMoveScript,Flight> flights=new ConditionalWeakTable<ProjectileMoveScript,Flight>();
    private static bool Authority(World world)=>ThreadManager.IsMainThread()&&world!=null&&!world.IsRemote()&&
        ReferenceEquals(world,GameManager.Instance?.World)&&SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer==true;
    private static ClientInfo CurrentPeer(EntityPlayer actor)=>SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(actor.entityId);
    private static bool Flaming(string name)=>name=="ammoArrowFlaming"||name=="ammoCrossbowBoltFlaming";
    private static bool Finite(Vector3 value)=>!float.IsNaN(value.x)&&!float.IsInfinity(value.x)&&
        !float.IsNaN(value.y)&&!float.IsInfinity(value.y)&&!float.IsNaN(value.z)&&!float.IsInfinity(value.z);

    [HarmonyPatch(typeof(NetPackageItemActionEffects),nameof(NetPackageItemActionEffects.ProcessPackage))]
    internal static class NativeEffects
    {
        private static bool Prefix(NetPackageItemActionEffects __instance,World __0,GameManager __1,bool __runOriginal,out object __state)
        {
            __state=null;if(!__runOriginal||!Authority(__0)||!ReferenceEquals(__1,GameManager.Instance))return true;
            var actor=__0.GetEntity(__instance.entityId) as EntityPlayer;var held=actor?.inventory?.holdingItemData;int index=__instance.actionIdx;
            if(held?.item?.Actions==null||index<0||index>=held.item.Actions.Length||held.item.Actions[index] is not ItemActionLauncher action)return true;
            var peer=__instance.Sender;
            if(actor is EntityPlayerLocal||peer==null||peer.entityId!=actor.entityId||!__instance.ValidEntityIdForSender(actor.entityId)||
                !ReferenceEquals(CurrentPeer(actor),peer)||actor.IsDead()||actor.inventory.holdingItemIdx!=__instance.slotIdx)return false;
            int firing=(int)__instance.firingState;if(firing==0)return true;
            if((firing!=1&&firing!=2)||!Finite(__instance.startPos)||!Finite(__instance.direction)||__instance.direction.sqrMagnitude<0.000001f||
                held.actionData==null||index>=held.actionData.Count||held.actionData[index] is not ItemActionLauncher.ItemActionDataLauncher data||
                !ReferenceEquals(data.invData,held)||data.indexInEntityOfAction!=index)return false;
            var scope=new Launch{World=__0,Actor=actor,Peer=peer,Held=held,Action=action,Data=data,Slot=__instance.slotIdx,Index=index,Deaths=actor.Died,Previous=launching};
            launching=scope;__state=scope;return true;
        }
        private static void Postfix(bool __runOriginal,object __state)
        {
            if(!__runOriginal||__state is not Launch scope||!ReferenceEquals(launching,scope)||!scope.Current())return;
            foreach(var flight in scope.Flights)if(flight.FireReturned&&flight.Current())flight.LaunchReturned=true;
        }
        private static Exception Finalizer(Exception __exception,object __state)
        {
            if(__state is Launch scope)
            {
                if(__exception!=null)foreach(var flight in scope.Flights)flight.Consumed=true;
                scope.Flights.Clear();if(ReferenceEquals(launching,scope))launching=scope.Previous;
            }
            return __exception;
        }
    }
    [HarmonyPatch(typeof(ProjectileMoveScript),nameof(ProjectileMoveScript.Fire))]
    internal static class NativeFire
    {
        private static void Prefix(ProjectileMoveScript __instance,Entity _firingEntity,Vector3 _idealStartPos,Vector3 _dirOrPos,bool _isBallistic,bool __runOriginal,out object __state)
        {
            __state=null;
            if(flights.TryGetValue(__instance,out var previous))
            {
                previous.Consumed=true;
                flights.Remove(__instance);
            }
            var scope=launching;string ammo=__instance.itemProjectile?.GetItemName();
            if(!__runOriginal||scope?.Current()!=true||_isBallistic||!Finite(_idealStartPos)||!Finite(_dirOrPos)||
                !ReferenceEquals(_firingEntity,scope.Actor)||!ReferenceEquals(__instance.actionData,scope.Data)||
                __instance.ProjectileOwnerID!=scope.Actor.entityId||!Flaming(ammo)||__instance.itemValueProjectile?.ItemClass?.GetItemName()!=ammo)return;
            var flight=new Flight{World=scope.World,Actor=scope.Actor,Peer=scope.Peer,Projectile=__instance,Data=scope.Data,Ammo=ammo,Deaths=scope.Deaths};
            flights.Add(__instance,flight);scope.Flights.Add(flight);__state=flight;
        }
        private static void Postfix(bool __runOriginal,object __state)
        {if(__runOriginal&&__state is Flight flight&&flight.Current()&&flight.Projectile.state==ProjectileMoveScript.State.Active)flight.FireReturned=true;}
        private static Exception Finalizer(Exception __exception,object __state)
        {if(__exception!=null&&__state is Flight flight)flight.Consumed=true;return __exception;}
    }
    [HarmonyPatch(typeof(ProjectileMoveScript),nameof(ProjectileMoveScript.checkCollision))]
    internal static class NativeCollision
    {
        private static void Prefix(ProjectileMoveScript __instance,bool __runOriginal,out object __state)
        {
            __state=null;
            if(!__runOriginal||!flights.TryGetValue(__instance,out var flight)||!flight.FireReturned||!flight.LaunchReturned||!flight.Current()||
                __instance.state!=ProjectileMoveScript.State.Active)return;
            var scope=new Collision{Flight=flight,Previous=colliding};colliding=scope;__state=scope;
        }
        private static void Postfix(bool __runOriginal,object __state)
        {
            if(!__runOriginal||__state is not Collision scope||!ReferenceEquals(colliding,scope)||!scope.DeadReturned||scope.Hit==null||
                !scope.Flight.Current()||scope.Flight.Projectile.state!=ProjectileMoveScript.State.Dead)return;
            var flight=scope.Flight;flight.Consumed=true;
            if(!RebirthFireRuntimePolicy.Enabled||GameManager.Instance.IsPaused())return;
            RebirthFireService.Instance.ScheduleIgnition(flight.World,new[]{scope.Hit.hit.blockPos},flight.Actor.entityId,RebirthFireIgnitionCause.Projectile,0f);
        }
        private static Exception Finalizer(Exception __exception,object __state)
        {
            if(__state is Collision scope)
            {if(__exception!=null)scope.Flight.Consumed=true;if(ReferenceEquals(colliding,scope))colliding=scope.Previous;}
            return __exception;
        }
    }
    [HarmonyPatch(typeof(ProjectileMoveScript),nameof(ProjectileMoveScript.SetState))]
    internal static class NativeState
    {
        private static void Prefix(ProjectileMoveScript __instance,ProjectileMoveScript.State _state,bool __runOriginal,out object __state)
        {
            __state=null;var scope=colliding;
            if(!__runOriginal||scope==null||!ReferenceEquals(scope.Flight.Projectile,__instance)||!scope.Flight.Current()||scope.Hit!=null||
                _state!=ProjectileMoveScript.State.Dead||__instance.state!=ProjectileMoveScript.State.Active)return;
            var hit=Voxel.voxelRayHitInfo;
            if(hit==null||!hit.bHitValid||!GameUtils.IsBlockOrTerrain(hit.tag)||!Finite(hit.hit.pos)||ItemActionAttack.GetBlockHit(scope.Flight.World,hit).isair)return;
            scope.Hit=hit.Clone();__state=scope;
        }
        private static void Postfix(ProjectileMoveScript __instance,bool __runOriginal,object __state)
        {if(__runOriginal&&__state is Collision scope&&ReferenceEquals(scope,colliding)&&__instance.state==ProjectileMoveScript.State.Dead)scope.DeadReturned=true;}
    }
}
