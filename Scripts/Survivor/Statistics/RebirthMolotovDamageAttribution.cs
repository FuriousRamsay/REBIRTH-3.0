using System;
using System.Runtime.CompilerServices;
using HarmonyLib;

// Keep provenance on the actual accepted native buff, not the entity ID or held weapon.
internal static class RebirthMolotovDamageAttribution
{
    private sealed class Origin { internal EntityAlive Instigator; internal EntityPlayer Player; internal World World; }
    private static ConditionalWeakTable<BuffValue,Origin> origins=new ConditionalWeakTable<BuffValue,Origin>();
    private static ConditionalWeakTable<DamageSource,Origin> damageOrigins=new ConditionalWeakTable<DamageSource,Origin>();
    internal static void Reset(){origins=new ConditionalWeakTable<BuffValue,Origin>();damageOrigins=new ConditionalWeakTable<DamageSource,Origin>();}
    internal static void Applied(EntityBuffs buffs,string name,int instigator)
    {
        if(buffs?.parent==null||string.IsNullOrEmpty(name))return;
        var world=buffs.parent.world as World;
        if(world==null||world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world))return;
        var buff=buffs.GetBuff(name);
        if(buff==null||buff.InstigatorId!=instigator||buff.BuffClass.DamageType==EnumDamageTypes.None)return;
        origins.Remove(buff);
        var entity=world.GetEntity(instigator) as EntityAlive;
        var player=RebirthStatisticsService.ResolveHordeOwner(entity);
        if(player!=null)origins.Add(buff,new Origin{Instigator=entity,Player=player,World=world});
    }
    internal static void CaptureDamage(EntityAlive victim,DamageSource source)
    {
        if(source==null)return;
        damageOrigins.Remove(source);
        if(source.BuffClass==null||victim?.Buffs==null)return;
        var buff=victim.Buffs.GetBuff(source.BuffClass.Name);Origin origin;
        if(buff==null||!ReferenceEquals(buff.BuffClass,source.BuffClass)||!origins.TryGetValue(buff,out origin)||
            !ReferenceEquals(victim.world,origin.World)||origin.World.IsRemote()||
            !ReferenceEquals(GameManager.Instance?.World,origin.World)||
            !ReferenceEquals(origin.World.GetEntity(origin.Player.entityId),origin.Player)||
            !ReferenceEquals(origin.World.GetEntity(origin.Instigator.entityId),origin.Instigator)||
            !ReferenceEquals(RebirthStatisticsService.ResolveHordeOwner(origin.Instigator),origin.Player)||
            buff.InstigatorId!=origin.Instigator.entityId||
            source.getEntityId()>=0&&source.getEntityId()!=origin.Instigator.entityId)return;
        damageOrigins.Add(source,origin);
    }
    internal static EntityPlayer StatisticsOwner(EntityAlive victim,DamageSource source)
    {
        Origin origin;
        if(source==null||!damageOrigins.TryGetValue(source,out origin)||
            !ReferenceEquals(victim?.world,origin.World)||origin.World.IsRemote()||
            !ReferenceEquals(GameManager.Instance?.World,origin.World)||
            !ReferenceEquals(origin.World.GetEntity(origin.Player.entityId),origin.Player)||
            !ReferenceEquals(origin.World.GetEntity(origin.Instigator.entityId),origin.Instigator)||
            !ReferenceEquals(RebirthStatisticsService.ResolveHordeOwner(origin.Instigator),origin.Player))return null;
        return origin.Player;
    }
    internal static bool IsCompanionDamage(DamageSource source)
    {Origin origin;return source!=null&&damageOrigins.TryGetValue(source,out origin)&&origin.Instigator is EntityRebirthNPC;}
    internal static void Resolve(EntityAlive victim,DamageSource source)
    {
        if(!string.Equals(source?.BuffClass?.Name,"buffBurningMolotov",StringComparison.OrdinalIgnoreCase)||victim?.Buffs==null||source.AttackingItem!=null)return;
        var buff=victim.Buffs.GetBuff("buffBurningMolotov");Origin origin;
        if(buff==null||!ReferenceEquals(buff.BuffClass,source.BuffClass)||!origins.TryGetValue(buff,out origin)
            ||!ReferenceEquals(origin.Instigator,origin.Player)
            ||origin.World.IsRemote()||!ReferenceEquals(victim.world,origin.World)
            ||!ReferenceEquals(GameManager.Instance?.World,origin.World)
            ||!ReferenceEquals(origin.World.GetEntity(origin.Player.entityId),origin.Player)
            ||buff.InstigatorId!=origin.Player.entityId)return;
        // Native NPC overtime damage omits these fields; retain all damage values/flags.
        if(source.getEntityId()>=0&&source.getEntityId()!=origin.Player.entityId)return;
        var item=ItemClass.GetItem("thrownAmmoMolotovCocktail",false);
        if(item?.ItemClass==null)return;
        source.ownerEntityId=origin.Player.entityId;source.AttackingItem=item;
    }
}

// The short native overload forwards directly to this position overload, so one
// hook covers both calls without recording the same application twice.
[HarmonyPatch(typeof(EntityBuffs),nameof(EntityBuffs.AddBuff),new Type[]{typeof(string),typeof(Vector3i),typeof(int),typeof(bool),typeof(bool),typeof(float)})]
internal static class RebirthMolotovPositionBuffAttributionPatch
{
    static void Postfix(EntityBuffs __instance,string __0,int __2,bool __runOriginal,EntityBuffs.BuffStatus __result)
    {if(__runOriginal&&__result==EntityBuffs.BuffStatus.Added)RebirthMolotovDamageAttribution.Applied(__instance,__0,__2);}
}
[HarmonyPatch(typeof(EntityAlive),nameof(EntityAlive.DamageEntity),new Type[]{typeof(DamageSource),typeof(int),typeof(bool),typeof(float)})]
internal static class RebirthMolotovDamageAttributionPatch
{
    [HarmonyPriority(Priority.First)]
    static void Prefix(EntityAlive __instance,DamageSource __0)
    {RebirthMolotovDamageAttribution.CaptureDamage(__instance,__0);RebirthMolotovDamageAttribution.Resolve(__instance,__0);}
}
