using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

// Native World predicate integration. No LPBlocks insertion, fake keystone or save writer.
// Human-confirmed Camp-only scope. Never manufacture physical persistent LPBlocks.
public enum RebirthCamperClaimScope { Unconfigured, CampOnly }
public static class RebirthCamperClaimIntegration
{
    public static RebirthCamperClaimScope Scope {get;private set;}=RebirthCamperClaimScope.CampOnly;
    [ThreadStatic] private static int nativeOnly;
    private static bool installed;
    public static void Configure(RebirthCamperClaimScope scope)
    {if(!ThreadManager.IsMainThread()||!Enum.IsDefined(typeof(RebirthCamperClaimScope),scope))throw new ArgumentException("Current game thread and explicit claim scope required.");Scope=scope;}
    public static void Install()
    {
        if(installed)return;
        var harmony=new Harmony("rebirth.camper.energy.claims");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperClaimOwnership));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperClaimChunkOwnership));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperClaimPermissions));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperClaimChunkChecks));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperClaimHardness));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperClaimParty));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperClaimSpacing));
        RebirthCamperClaimProjection.Install();
        installed=true;
    }
    public static bool Active(EntityVehicle vehicle)
    {
        if(Scope==RebirthCamperClaimScope.Unconfigured||!ThreadManager.IsMainThread()||vehicle?.world==null||
            !ReferenceEquals(GameManager.Instance?.World,vehicle.world)||!ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle)||
            !RebirthCamperRadial.IsCamper(vehicle))return false;
        return RebirthCamperEnergyRuntime.TryObserveMode(vehicle,out var mode,out _,out _)&&mode==RebirthCamperEnergyMode.Camp&&
            (vehicle.world.IsRemote()?RebirthCamperClaimProjection.Ready(vehicle):RebirthCamperEnergyImmobility.CampClaimReady(vehicle));
    }
    public static bool Contains(EntityVehicle vehicle,Vector3i position)
    {
        if(!ThreadManager.IsMainThread()||vehicle?.world==null||
            !RebirthCamperEnergyRuntime.TryObserveMode(vehicle,out var mode,out _,out float range)||mode!=RebirthCamperEnergyMode.Camp)return false;
        if(vehicle.world.IsRemote()&&!RebirthCamperClaimProjection.TryRange(vehicle,out range))return false;
        // Most POI lamps are outside the mobile claim. Reject those before
        // crossing into native Rigidbody pose/constraint checks for every lamp.
        return RebirthCamperEnergyBounds.Contains(RebirthCamperEnergyBounds.CurrentCenter(vehicle),position.ToVector3Center(),range)&&Active(vehicle);
    }
    // Native bypass is recursion-scoped only; no persistent flags or altered claim state.
    private static EnumLandClaimOwner Physical(World world,Vector3i position,PersistentPlayerData relative)
    {nativeOnly++;try{return world.GetLandClaimOwner(position,relative);}finally{nativeOnly--;}}
    internal static EnumLandClaimOwner Resolve(World world,Vector3i position,PersistentPlayerData relative)
    {
        if(nativeOnly!=0||Scope==RebirthCamperClaimScope.Unconfigured||!ThreadManager.IsMainThread()||
            !ReferenceEquals(GameManager.Instance?.World,world)||world.IsWithinTraderArea(position)||GameStats.GetInt(EnumGameStats.GameModeId)!=1)
            return EnumLandClaimOwner.None;
        PersistentPlayerData chosen=null;
        foreach(var entity in world.Entities.list)
        {
            if(!(entity is EntityVehicle vehicle)||!Contains(vehicle,position))continue;
            var owner=OwnerOf(vehicle);
            if(owner==null||!world.IsLandProtectionValidForPlayer(owner))continue;
            // A physical foreign/ally claim is never overridden by mobile proximity.
            var physical=Physical(world,position,owner);
            if(physical!=EnumLandClaimOwner.None&&physical!=EnumLandClaimOwner.Self)continue;
            // Overlapping different mobile owners fail closed rather than select entity order.
            if(chosen!=null&&!ReferenceEquals(chosen,owner))return EnumLandClaimOwner.Other;
            chosen=owner;
        }
        return chosen==null?EnumLandClaimOwner.None:relative==null?EnumLandClaimOwner.Other:
            ReferenceEquals(chosen,relative)?EnumLandClaimOwner.Self:chosen.IsAlly(relative)?EnumLandClaimOwner.Ally:EnumLandClaimOwner.Other;
    }
    internal static PersistentPlayerData OwnerAt(World world,Vector3i position)
    {
        if(Resolve(world,position,null)==EnumLandClaimOwner.None)return null;
        PersistentPlayerData chosen=null;
        foreach(var entity in world.Entities.list)
        {
            if(!(entity is EntityVehicle vehicle)||!Contains(vehicle,position))continue;
            var owner=OwnerOf(vehicle);
            if(owner==null||!world.IsLandProtectionValidForPlayer(owner))continue;
            var physical=Physical(world,position,owner);
            if(physical!=EnumLandClaimOwner.None&&physical!=EnumLandClaimOwner.Self)continue;
            if(chosen!=null&&!ReferenceEquals(chosen,owner))return null;
            chosen=owner;
        }
        return chosen;
    }
    internal static void Arguments(object[] args,out Vector3i position,out PersistentPlayerData relative)
    {
        position=default(Vector3i);relative=null;
        foreach(var arg in args){if(arg is Vector3i p)position=p;if(arg is PersistentPlayerData player)relative=player;}
    }
    // Honest separate mobile count, never masquerades as a physical LPBlocks entry.
    public static int CountActive(World world,PersistentPlayerData owner)
    {
        if(owner==null||world==null||!ThreadManager.IsMainThread()||!ReferenceEquals(GameManager.Instance?.World,world)||!world.IsLandProtectionValidForPlayer(owner))return 0;int count=0;
        foreach(var vehicle in EnumerateActive(world,owner))count++;
        return count;
    }
    public static IEnumerable<EntityVehicle> EnumerateActive(World world,PersistentPlayerData owner)
    {
        if(owner==null||world==null||!ThreadManager.IsMainThread()||!ReferenceEquals(GameManager.Instance?.World,world)||!world.IsLandProtectionValidForPlayer(owner))yield break;
        foreach(var entity in world.Entities.list)if(entity is EntityVehicle vehicle&&Active(vehicle)&&
            ReferenceEquals(OwnerOf(vehicle),owner))yield return vehicle;
    }
    public static int CombinedCount(World world,PersistentPlayerData owner)
        =>owner==null?0:owner.LPBlocks.Count+CountActive(world,owner);
    internal static PersistentPlayerData OwnerOf(EntityVehicle vehicle)
    {var identity=vehicle?.GetOwner();return identity==null?null:GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerData(identity);}
}

[HarmonyPatch(typeof(World),nameof(World.CanPlaceLandProtectionBlockAt))]
internal static class RebirthCamperClaimSpacing
{
    private static void Postfix(World __instance,Vector3i blockPos,PersistentPlayerData lpRelative,ref bool __result)
    {
        if(!__result||GameStats.GetInt(EnumGameStats.GameModeId)!=1||RebirthCamperClaimIntegration.Scope==RebirthCamperClaimScope.Unconfigured)return;
        // Native placed claims extend vertically without limit. Intersect the
        // sphere's x/z disk with their square column, preserving native padding.
        float nativeHalf=Math.Max(0,(GameStats.GetInt(EnumGameStats.LandClaimSize)-1)/2f);
        float deadZone=Math.Max(0,GameStats.GetInt(EnumGameStats.LandClaimDeadZone));
        foreach(var entity in __instance.Entities.list)
        {
            if(!(entity is EntityVehicle vehicle)||!RebirthCamperClaimIntegration.Active(vehicle))continue;
            var owner=RebirthCamperClaimIntegration.OwnerOf(vehicle);
            if(owner==null||ReferenceEquals(owner,lpRelative)||!__instance.IsLandProtectionValidForPlayer(owner))continue;
            if(!RebirthCamperEnergyRuntime.TryObserveMode(vehicle,out _,out _,out float range))continue;
            if(vehicle.world.IsRemote()&&!RebirthCamperClaimProjection.TryRange(vehicle,out range))continue;
            float padding=lpRelative!=null&&owner.IsAlly(lpRelative)?0:deadZone;
            var center=RebirthCamperEnergyBounds.CurrentCenter(vehicle);
            float dx=Math.Max(0,Mathf.Abs(center.x-(blockPos.x+.5f))-nativeHalf-padding);
            float dz=Math.Max(0,Mathf.Abs(center.z-(blockPos.z+.5f))-nativeHalf-padding);
            if(dx*dx+dz*dz<=range*range)
            {__result=false;return;}
        }
    }
}

[HarmonyPatch(typeof(World),nameof(World.GetLandProtectionHardnessModifier),new[]{typeof(Chunk),typeof(Vector3i),typeof(PersistentPlayerData),typeof(int)})]
internal static class RebirthCamperClaimHardness
{
    private static void Postfix(World __instance,Vector3i blockPos,PersistentPlayerData lpRelative,ref float __result)
    {
        // Preserve any native physical protection already computed, including its
        // actor effect; never apply that effect a second time to a native result.
        if(__result!=1f)return;
        var owner=RebirthCamperClaimIntegration.OwnerAt(__instance,blockPos);
        if(owner==null||ReferenceEquals(owner,lpRelative)||lpRelative!=null&&owner.IsAlly(lpRelative))return;
        float modifier=__instance.GetLandProtectionHardnessModifierForPlayer(owner);
        if(modifier<1f){__result=modifier;return;}
        modifier=Mathf.Max(__result,modifier);
        var actor=lpRelative==null?null:__instance.GetEntity(lpRelative.EntityId) as EntityPlayer;
        if(actor!=null)modifier=EffectManager.GetValue(PassiveEffects.LandClaimDamageModifier,actor.inventory.holdingItemItemValue,modifier,actor);
        // Native outer overload still owns block LPHardnessScale and vehicle scaling.
        __result=modifier;
    }
}
[HarmonyPatch]
internal static class RebirthCamperClaimParty
{
    private static IEnumerable<MethodBase> TargetMethods()
    {foreach(var method in typeof(World).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
        if(method.DeclaringType==typeof(World)&&method.Name==nameof(World.GetLandClaimOwnerInParty))yield return method;}
    private static void Postfix(World __instance,object[] __args,ref bool __result)
    {
        if(__result)return;EntityPlayer player=null;
        foreach(var arg in __args)if(arg is EntityPlayer p)player=p;
        if(player==null)return;
        RebirthCamperClaimIntegration.Arguments(__args,out var position,out var relative);
        if(__args.Length==2)position=player.GetBlockPosition();
        relative=relative??GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        var owner=RebirthCamperClaimIntegration.OwnerAt(__instance,position);
        __result=owner!=null&&relative!=null&&(ReferenceEquals(owner,relative)||player.Party!=null&&owner.IsAlly(relative)&&player.Party.ContainsMember(owner.EntityId));
    }
}

[HarmonyPatch]
internal static class RebirthCamperClaimChunkChecks
{
    private static IEnumerable<MethodBase> TargetMethods()
    {foreach(var method in typeof(World).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
        if(method.DeclaringType==typeof(World)&&method.ReturnType==typeof(bool)&&
            (method.Name==nameof(World.IsLandProtectedBlock)||method.Name==nameof(World.IsMyLandClaimInChunk)))yield return method;}
    private static void Postfix(World __instance,MethodBase __originalMethod,object[] __args,ref bool __result)
    {
        if(__result)return;
        RebirthCamperClaimIntegration.Arguments(__args,out var position,out var relative);
        var owner=RebirthCamperClaimIntegration.Resolve(__instance,position,relative);
        bool keystone=__args.Length>0&&__args[__args.Length-1] is bool value&&value;
        if(__originalMethod.Name==nameof(World.IsLandProtectedBlock))
            __result=owner==EnumLandClaimOwner.Other||owner==EnumLandClaimOwner.Ally&&keystone;
        else __result=owner==EnumLandClaimOwner.Self||owner==EnumLandClaimOwner.Ally&&keystone;
    }
}

[HarmonyPatch(typeof(World),nameof(World.GetLandClaimOwner),new[]{typeof(Vector3i),typeof(PersistentPlayerData)})]
internal static class RebirthCamperClaimOwnership
{
    private static void Postfix(World __instance,Vector3i __0,PersistentPlayerData __1,ref EnumLandClaimOwner __result)
    {
        if(__result!=EnumLandClaimOwner.None)return;
        __result=RebirthCamperClaimIntegration.Resolve(__instance,__0,__1);
    }
}
// Both native overloads retain their original claim behavior. Typed arguments
// avoid Harmony allocating and boxing an object[] for every light/claim query.
[HarmonyPatch(typeof(World),nameof(World.GetLandClaimOwner),new[]{typeof(Chunk),typeof(Vector3i),typeof(PersistentPlayerData),typeof(int),typeof(int),typeof(bool)})]
internal static class RebirthCamperClaimChunkOwnership
{
    private static void Postfix(World __instance,Vector3i __1,PersistentPlayerData __2,ref EnumLandClaimOwner __result)
    {
        if(__result!=EnumLandClaimOwner.None)return;
        __result=RebirthCamperClaimIntegration.Resolve(__instance,__1,__2);
    }
}
[HarmonyPatch]
internal static class RebirthCamperClaimPermissions
{
    private static IEnumerable<MethodBase> TargetMethods()
    {foreach(var method in typeof(World).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
        if(method.DeclaringType==typeof(World)&&method.ReturnType==typeof(bool)&&
            (method.Name==nameof(World.CanPlaceBlockAt)||method.Name==nameof(World.CanPickupBlockAt)||
             method.Name==nameof(World.IsMyLandProtectedBlock)||method.Name==nameof(World.IsEmptyPosition)))yield return method;}
    private static void Postfix(World __instance,MethodBase __originalMethod,object[] __args,ref bool __result)
    {
        RebirthCamperClaimIntegration.Arguments(__args,out var position,out var relative);
        var owner=RebirthCamperClaimIntegration.Resolve(__instance,position,relative);
        if(__originalMethod.Name==nameof(World.IsMyLandProtectedBlock))
        {
            // Do not grant owner pickup inside an existing foreign physical claim.
            if(!__result&&owner==EnumLandClaimOwner.Self)__result=true;
        }
        else if(__result&&owner==EnumLandClaimOwner.Other)__result=false;
    }
}
