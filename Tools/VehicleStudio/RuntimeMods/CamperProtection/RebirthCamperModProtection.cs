using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

// Camper-only native mod policy. Native damage dispatch/ownership and saves remain in charge.
internal static class RebirthCamperModProtection
{
    internal static readonly FastTags<TagGroup.Global> Armor=FastTags<TagGroup.Global>.Parse("armor");
    internal static readonly FastTags<TagGroup.Global> Plow=FastTags<TagGroup.Global>.Parse("plow");
    internal static bool IsCamper(Vehicle v)=>v!=null&&string.Equals(v.GetName(),"rebirthCamper80s",StringComparison.OrdinalIgnoreCase);
    internal static bool Armored(Vehicle v)=>IsCamper(v)&&v.ModTags.Test_AnySet(Armor);
    internal static bool ArmoredItem(ItemValue item)
    {
        if(item?.ItemClass?.GetItemName()!="rebirthCamper80sPlaceable")return false;
        for(int i=0;i<item.ModificationCount;i++)
            if(item.GetModification(i)?.ItemClass is ItemClassModifier mod&&mod.ModifierTags.Test_AnySet(Armor))return true;
        return false;
    }
    internal static void BindPloughHull(Transform physics)
    {
        if(!physics)return;
        var hull=physics.Find("REBIRTHModPlowHull");if(!hull)return;
        // Resolve custom tags in the running game's TagManager, not the authoring project's IDs.
        hull.gameObject.tag="E_VehicleStrong";hull.gameObject.layer=21;
        foreach(var collider in hull.GetComponentsInChildren<MeshCollider>(true))
        {
            collider.gameObject.tag="E_VehicleStrong";collider.gameObject.layer=21;
            if(collider.isTrigger)collider.isTrigger=false;
            if(!collider.convex)collider.convex=true;
        }
    }
    internal sealed class DamageCarry {public DamageCarry(){} internal double Value;}
    internal static readonly ConditionalWeakTable<EntityVehicle,DamageCarry> Carry=new ConditionalWeakTable<EntityVehicle,DamageCarry>();
    [ThreadStatic] internal static EntityVehicle ZombieDamageTarget;
    internal static Entity CollisionEntity(Collision c)
    {
        var forward=c.gameObject.GetComponent<ColliderHitCallForward>();
        if(forward&&forward.Entity)return forward.Entity;
        var entity=CollisionCallForward.FindEntity(c.transform);
        if(!entity&&c.transform.parent)entity=CollisionCallForward.FindEntity(c.transform.parent);
        if(!entity&&c.rigidbody)entity=CollisionCallForward.FindEntity(c.rigidbody.transform);
        return entity;
    }
}

[HarmonyPatch(typeof(ItemValue),"get_MaxUseTimesBase")]
internal static class RebirthCamperArmorDurability
{
    static void Postfix(ItemValue __instance,ref int __result)
    {if(RebirthCamperModProtection.ArmoredItem(__instance))__result=checked(__result*3);}
}

[HarmonyPatch(typeof(Vehicle),nameof(Vehicle.CalcMods))]
internal static class RebirthCamperArmorCapacity
{
    static void Postfix(Vehicle __instance)
    {
        if(!RebirthCamperModProtection.IsCamper(__instance)||!__instance.entity)return;
        var entity=__instance.entity;RebirthCamperModProtection.BindPloughHull(entity.PhysicsTransform);int maximum=__instance.itemValue.MaxUseTimes;
        int before=(int)entity.Stats.Health.BaseMax;
        if(before==maximum||before<=0||maximum<=0)return;
        // Preserve condition across installation/removal; never grant a free repair.
        int health=entity.Health<=1?entity.Health:Mathf.Clamp((int)Math.Floor((double)entity.Health*maximum/before),1,maximum);
        entity.Stats.Health.BaseMax=maximum;entity.Stats.Health.OriginalMax=maximum;entity.Health=health;
        __instance.itemValue.UseTimes=maximum-health;
    }
}

[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.ProcessDamageResponseLocal))]
internal static class RebirthCamperZombieDamageScope
{
    static void Prefix(EntityVehicle __instance,DamageResponse _dmResponse,out EntityVehicle __state)
    {
        __state=RebirthCamperModProtection.ZombieDamageTarget;
        RebirthCamperModProtection.ZombieDamageTarget=RebirthCamperModProtection.Armored(__instance.vehicle)&&
            _dmResponse.Source!=null&&__instance.world?.GetEntity(_dmResponse.Source.getEntityId()) is EntityZombie?__instance:null;
    }
    static Exception Finalizer(Exception __exception,EntityVehicle __state)
    {RebirthCamperModProtection.ZombieDamageTarget=__state;return __exception;}
}

[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.ApplyDamage))]
internal static class RebirthCamperZombieDamageReduction
{
    static void Prefix(EntityVehicle __instance,ref int damage)
    {
        if(damage<=0||!ReferenceEquals(RebirthCamperModProtection.ZombieDamageTarget,__instance))return;
        var carry=RebirthCamperModProtection.Carry.GetOrCreateValue(__instance);
        double reduced=damage*.05d+carry.Value;
        damage=(int)Math.Floor(reduced+1e-9);carry.Value=Math.Max(0,reduced-damage);
    }
}

[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.OnCollisionForward))]
internal static class RebirthCamperZombiePloughImpact
{
    internal struct SavedEffects {internal Vehicle Vehicle;internal float Self,Entity,Strong;}
    static void Prefix(EntityVehicle __instance,Collision collision,out SavedEffects __state)
    {
        __state=default;
        var v=__instance.vehicle;
        if(__instance.isEntityRemote||!RebirthCamperModProtection.IsCamper(v)||(!v.ModTags.Test_AnySet(RebirthCamperModProtection.Plow)&&!RebirthCamperModProtection.Armored(v))||
            !(RebirthCamperModProtection.CollisionEntity(collision) is EntityZombie))return;
        __state=new SavedEffects{Vehicle=v,Self=v.EffectSelfDamagePer,Entity=v.EffectEntityDamagePer,Strong=v.EffectStrongSelfDamagePer};
        // Native strong-contact colliders apply this multiplier to zombie impacts.
        bool plough=v.ModTags.Test_AnySet(RebirthCamperModProtection.Plow);
        v.EffectSelfDamagePer=plough?0:v.EffectSelfDamagePer*.05f;
        if(!plough)v.EffectStrongSelfDamagePer=1;
        if(plough)v.EffectEntityDamagePer=Mathf.Max(2f,v.EffectEntityDamagePer);
    }
    static Exception Finalizer(Exception __exception,SavedEffects __state)
    {
        if(__state.Vehicle!=null){__state.Vehicle.EffectSelfDamagePer=__state.Self;__state.Vehicle.EffectEntityDamagePer=__state.Entity;__state.Vehicle.EffectStrongSelfDamagePer=__state.Strong;}
        return __exception;
    }
}




