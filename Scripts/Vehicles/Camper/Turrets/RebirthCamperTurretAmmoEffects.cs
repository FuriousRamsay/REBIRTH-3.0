using System.Runtime.CompilerServices;
using HarmonyLib;

// Native ammunition defines some calibre effects under weapon-family tags.
// Supply that context only for camper adapters; ordinary turrets are unchanged.
internal static class RebirthCamperTurretAmmoEffects
{
    [System.ThreadStatic]internal static ItemValue ActiveAmmo;
    private sealed class Adapter {internal System.Func<RebirthCamperTurretSlot> Slot;}
    private static readonly ConditionalWeakTable<AutoTurretFireController,Adapter> adapters=new ConditionalWeakTable<AutoTurretFireController,Adapter>();
    private static readonly FastTags<TagGroup.Global> rifle=FastTags<TagGroup.Global>.Parse("ammo762mm");
    private static readonly FastTags<TagGroup.Global> magnum=FastTags<TagGroup.Global>.Parse("ammo44Mag");
    private static readonly FastTags<TagGroup.Global> rifleContext=FastTags<TagGroup.Global>.Parse("perkDeadEye,perkMachineGunner");
    private static readonly FastTags<TagGroup.Global> magnumContext=FastTags<TagGroup.Global>.Parse("perkGunslinger,revolver");
    private static readonly FastTags<TagGroup.Global> roboticsContext=FastTags<TagGroup.Global>.Parse("perkTurrets,turretRanged");
    internal static void Register(AutoTurretFireController fire,System.Func<RebirthCamperTurretSlot> slot=null)=>adapters.Add(fire,new Adapter{Slot=slot});
    internal static bool Owns(AutoTurretFireController fire)=>adapters.TryGetValue(fire,out _);
    internal static RebirthCamperTurretSlot Slot(AutoTurretFireController fire)=>adapters.TryGetValue(fire,out var adapter)?adapter.Slot?.Invoke():null;
    internal static FastTags<TagGroup.Global> Context(ItemValue ammo)
    {
        if(ammo?.ItemClass==null)return FastTags<TagGroup.Global>.none;
        var type=ammo.ItemClass;type.Properties.Values.TryGetValue("DisplayType",out var display);
        if(display=="ammoJunkTurretRegular"||display=="ammoJunkTurretAP"||type.GetItemName()=="ammoJunkTurretShell")return roboticsContext;
        var tags=ammo.ItemClass.ItemTags;
        if(tags.Test_AnySet(rifle))return rifleContext;
        if(tags.Test_AnySet(magnum))return magnumContext;
        return FastTags<TagGroup.Global>.none;
    }
    internal static float Damage(ItemValue ammo)=>EffectManager.GetValue(PassiveEffects.EntityDamage,ammo,0,null,null,Context(ammo));
    internal static float Range(ItemValue ammo,RebirthCamperTurretSlot slot)
    {
        if(ammo?.ItemClass==null)return 0;
        var weapon=RebirthCamperTurretFiringProfiles.Resolve(ammo,slot?.Silencer,slot?.SniperMode==true);
        float range=EffectManager.GetValue(PassiveEffects.MaxRange,ammo,weapon.Range,tags:Context(ammo));
        return float.IsNaN(range)||float.IsInfinity(range)?0:System.Math.Max(0,range);
    }
}
[HarmonyPatch(typeof(AutoTurretFireController),nameof(AutoTurretFireController.GetRange))]
internal static class RebirthCamperTurretAmmoRange
{
    static bool Prefix(AutoTurretFireController __instance,ItemValue _itemValue,ref float __result)
    {
        if(!RebirthCamperTurretAmmoEffects.Owns(__instance))return true;
        __result=RebirthCamperTurretAmmoEffects.Range(_itemValue,RebirthCamperTurretAmmoEffects.Slot(__instance));return false;
    }
}
[HarmonyPatch]
internal static class RebirthCamperTurretNativeAmmoContext
{
    static System.Reflection.MethodBase TargetMethod()=>AccessTools.Method(typeof(EffectManager),nameof(EffectManager.GetValue),new[]{
        typeof(PassiveEffects),typeof(ItemValue),typeof(float),typeof(EntityAlive),typeof(Recipe),typeof(FastTags<TagGroup.Global>),
        typeof(bool),typeof(bool),typeof(bool),typeof(bool),typeof(bool),typeof(int),typeof(bool),typeof(bool)});
    static void Prefix(ItemValue __1,ref FastTags<TagGroup.Global> __5)
    {
        var active=RebirthCamperTurretAmmoEffects.ActiveAmmo;
        if(active!=null&&__1!=null&&active.type==__1.type&&__5.Equals(FastTags<TagGroup.Global>.none))__5=RebirthCamperTurretAmmoEffects.Context(__1);
    }
}
[HarmonyPatch(typeof(AutoTurretFireController),nameof(AutoTurretFireController.GetDamageEntity))]
internal static class RebirthCamperTurretEntityDamage
{
    static bool Prefix(AutoTurretFireController __instance,ItemValue _itemValue,ref float __result)
    {if(!RebirthCamperTurretAmmoEffects.Owns(__instance))return true;__result=RebirthCamperTurretAmmoEffects.Damage(_itemValue);return false;}
}
[HarmonyPatch(typeof(AutoTurretFireController),nameof(AutoTurretFireController.GetDamageBlock))]
internal static class RebirthCamperTurretBlockDamage
{
    static bool Prefix(AutoTurretFireController __instance,ItemValue _itemValue,BlockValue _blockValue,ref float __result)
    {
        if(!RebirthCamperTurretAmmoEffects.Owns(__instance))return true;
        var tags=RebirthCamperTurretAmmoEffects.Context(_itemValue)|_blockValue.Block.Tags;
        __result=Utils.FastMin(_blockValue.Block.blockMaterial.MaxIncomingDamage,EffectManager.GetValue(PassiveEffects.BlockDamage,_itemValue,0,null,null,tags));return false;
    }
}
