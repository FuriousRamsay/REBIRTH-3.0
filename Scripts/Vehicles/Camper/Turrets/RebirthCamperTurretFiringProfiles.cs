using System;
using System.Collections.Generic;
using System.Globalization;

internal static class RebirthCamperTurretFiringProfiles
{
    internal sealed class Profile {internal string Weapon,Sound;internal float Interval,Range;}
    private static readonly Dictionary<string,Profile> cache=new Dictionary<string,Profile>();
    private static readonly FastTags<TagGroup.Global> rifle=FastTags<TagGroup.Global>.Parse("ammo762mm");
    private static readonly FastTags<TagGroup.Global> magnum=FastTags<TagGroup.Global>.Parse("ammo44Mag");
    private static readonly FastTags<TagGroup.Global> shells=FastTags<TagGroup.Global>.Parse("ammoShells");
    internal static bool IsRifle(ItemValue ammo)=>ammo?.ItemClass!=null&&ammo.ItemClass.ItemTags.Test_AnySet(rifle);
    internal static float Zoom(ItemValue ammo,bool sniper)
    {
        if(ammo?.ItemClass==null)return 1;
        var type=ammo.ItemClass;type.Properties.Values.TryGetValue("DisplayType",out var display);
        if(type.ItemTags.Test_AnySet(shells)||display=="ammoJunkTurretRegular"||display=="ammoJunkTurretAP"||type.GetItemName()=="ammoJunkTurretShell")return 2;
        return IsRifle(ammo)&&sniper?8:4;
    }
    internal static Profile Resolve(ItemValue ammo,ItemStack silencer,bool sniper=false)
    {
        var type=ammo.ItemClass;type.Properties.Values.TryGetValue("DisplayType",out var display);
        bool junk=display=="ammoJunkTurretRegular"||display=="ammoJunkTurretAP"||type.GetItemName()=="ammoJunkTurretShell";
        string weapon=junk?"gunBotT2JunkTurret":type.ItemTags.Test_AnySet(shells)?"gunShotgunT2PumpShotgun":
            type.ItemTags.Test_AnySet(rifle)?(sniper?"gunRifleT3SniperRifle":"gunMGT2TacticalAR"):type.ItemTags.Test_AnySet(magnum)?"gunHandgunT2Magnum44":"gunHandgunT3SMG5";
        bool suppressed=silencer!=null&&!silencer.IsEmpty();string key=weapon+(suppressed?":suppressed":":normal")+(junk?":"+ammo.type:"");
        if(cache.TryGetValue(key,out var cached))return cached;
        var value=ItemClass.GetItem(weapon,false);var item=value?.ItemClass;
        if(item==null||!item.Properties.Classes.TryGetValue("Action0",out var action)||!action.Values.TryGetValue("Sound_start",out var sound))
            throw new InvalidOperationException("Camper firing profile weapon unavailable: "+weapon);
        if(suppressed){value.modifications=new[]{silencer.itemValue.Clone()};sound=value.GetPropertyOverride("Sound_start",sound);}
        float fallback=60;
        if(action.Values.TryGetValue("Delay",out var delay)&&float.TryParse(delay,NumberStyles.Float,CultureInfo.InvariantCulture,out float seconds)&&seconds>0)fallback=60/seconds;
        float rpm=EffectManager.GetValue(PassiveEffects.RoundsPerMinute,value,fallback,tags:item.ItemTags);
        // Junk ammunition itself carries its family-tagged rate (shell versus scrap/AP).
        if(junk)rpm=EffectManager.GetValue(PassiveEffects.RoundsPerMinute,ammo,rpm,tags:RebirthCamperTurretAmmoEffects.Context(ammo));
        if(float.IsNaN(rpm)||float.IsInfinity(rpm)||rpm<=0)throw new InvalidOperationException("Invalid camper firing cadence: "+weapon);
        float range=EffectManager.GetValue(PassiveEffects.MaxRange,value,16,tags:item.ItemTags);
        if(float.IsNaN(range)||float.IsInfinity(range)||range<=0)throw new InvalidOperationException("Invalid camper native weapon range: "+weapon);
        var profile=new Profile{Weapon=weapon,Sound=sound,Interval=60/rpm,Range=range};
        // Junk shell has its own cache key because it supplies a separate rate.
        cache[key]=profile;return profile;
    }
}
