using System;
using System.Linq;
using HarmonyLib;

public static class RebirthCookingItemStats
{
    public static readonly string[] Keys={"nutrition","water","energy","comfort","quality","seasoning","tags","family","burnt"};
    public static bool IsLiquid(ItemValue item)
    {
        RebirthConsumableDefinition definition;
        return item != null && !item.IsEmpty() && RebirthConsumableResolver.TryResolve(item, out definition)
            && definition != null && definition.IsDrink;
    }
    public static bool NeedsStateAwareStacking(ItemValue item)
    {
        return item != null && (IsLiquid(item) || item.Metadata?.ContainsKey("rebirth.cooking.nutrition") == true);
    }
    public static bool Compatible(ItemValue a, ItemValue b)
    {
        if (a == null || b == null) return a == b;
        RebirthConsumableDefinition da, db;
        bool liquidA = RebirthConsumableResolver.TryResolve(a, out da) && da != null && da.IsDrink;
        bool liquidB = RebirthConsumableResolver.TryResolve(b, out db) && db != null && db.IsDrink;
        if (liquidA || liquidB)
        {
            if (!liquidA || !liquidB || a.type != b.type || a.Meta != b.Meta ||
                a.Activated != b.Activated || a.SelectedAmmoTypeIndex != b.SelectedAmmoTypeIndex ||
                a.ItemClass.HasQuality && a.Quality != b.Quality ||
                !SameMods(a.modifications,b.modifications) || !SameMods(a.cosmeticMods,b.cosmeticMods)) return false;
            // Compare effective quantities, not metadata-object identity or rounded tooltip text.
            // An explicitly normalized full bottle and an otherwise identical fresh full bottle
            // can merge; a sip, different cooked yield or prepared-drink bonus cannot be erased.
            if (!SameNumber(RebirthLiquidContainerService.GetRemainingMl(a, da), RebirthLiquidContainerService.GetRemainingMl(b, db)) ||
                !SameNumber(da.ContainerCapacityMl, db.ContainerCapacityMl) || !SameNumber(da.InitialVolumeMl, db.InitialVolumeMl) ||
                !SameNumber(da.NutritionUnits, db.NutritionUnits) || !SameNumber(da.EnergyUnits, db.EnergyUnits) ||
                !SameNumber(Value(a, "nutrition"), Value(b, "nutrition")) || !SameNumber(Value(a, "energy"), Value(b, "energy")) ||
                !SameNumber(Value(a, "comfort"), Value(b, "comfort")) ||
                !SameNumber(da.ManualSipMl, db.ManualSipMl) || !SameNumber(da.LiquidHydrationYield, db.LiquidHydrationYield) ||
                !string.Equals(RebirthLiquidContainerService.GetLiquidProfile(a, da), RebirthLiquidContainerService.GetLiquidProfile(b, db), StringComparison.Ordinal)) return false;
            string qualityA = CookingQuality(a), qualityB = CookingQuality(b);
            if (qualityA == null || qualityB == null || !string.Equals(qualityA, qualityB, StringComparison.Ordinal) ||
                !SameOptionalNumber(a, b, "rebirth.cooking.seasoning", 0f) ||
                !SameBurnState(a, b)) return false;
            RebirthFoodMoodDefinition ma, mb;
            bool hasMoodA = RebirthFoodMoodResolver.TryResolve(a, out ma), hasMoodB = RebirthFoodMoodResolver.TryResolve(b, out mb);
            if (hasMoodA != hasMoodB) return false;
            if (hasMoodA && (ma == null || mb == null || !SameNumber(ma.BaseMoodInfluence, mb.BaseMoodInfluence) ||
                !string.Equals(ma.VarietyFamilyId, mb.VarietyFamilyId, StringComparison.Ordinal) ||
                !string.Equals(ma.MoodProfileId, mb.MoodProfileId, StringComparison.Ordinal) || !ma.DietTags.SetEquals(mb.DietTags))) return false;
            // Preserve all prepared-drink/electrical provenance checks on partial transfers too.
            return SameOtherMetadata(a,b) && RebirthItemProvenanceAdapter.AreStackCompatible(a, b) && RebirthElectricalItemProvenance.AreStackCompatible(a, b);
        }
        foreach (string key in Keys)
        {
            string name = "rebirth.cooking." + key;
            bool left = a.HasMetadata(name), right = b.HasMetadata(name);
            if (left != right) return false;
            if (!left) continue;
            if (key == "quality" || key == "tags" || key == "family")
            {
                string av, bv;
                if (!a.TryGetMetadata(name, out av) || !b.TryGetMetadata(name, out bv) || !string.Equals(av, bv, StringComparison.Ordinal)) return false;
            }
            else if (key == "burnt") { if (!SameBurnState(a, b)) return false; }
            else
            {
                float av, bv;
                if (!a.TryGetMetadata(name, out av) || !b.TryGetMetadata(name, out bv) || !SameNumber(av, bv)) return false;
            }
        }
        return true;
    }
    private static bool SameMods(ItemValue[] a,ItemValue[] b)
    {
        int count=Math.Max(a==null?0:a.Length,b==null?0:b.Length);
        for(int i=0;i<count;i++)
        {
            ItemValue x=a!=null&&i<a.Length?a[i]:null, y=b!=null&&i<b.Length?b[i]:null;
            bool xe=x==null||x.IsEmpty(), ye=y==null||y.IsEmpty();
            if(xe!=ye || !xe&&!x.Equals(y))return false;
        }
        return true;
    }
    private static bool ResolvedMetadata(string key)
    {
        if(key==RebirthLiquidContainerService.VolumeKey || key==RebirthLiquidContainerService.ProfileKey ||
            key==RebirthLiquidContainerService.VersionKey)return true;
        if(!key.StartsWith("rebirth.cooking.",StringComparison.Ordinal))return false;
        string suffix=key.Substring("rebirth.cooking.".Length);
        // These fields are already compared through their typed/effective definitions above.
        return suffix=="batch" || suffix=="nutrition" || suffix=="water" || suffix=="energy" ||
            suffix=="comfort" || suffix=="quality" || suffix=="seasoning" || suffix=="tags" ||
            suffix=="family" || suffix=="burnt" || suffix=="skill";
    }
    private static bool SameOtherMetadata(ItemValue a,ItemValue b)
    { return MetadataSubset(a,b) && MetadataSubset(b,a); }
    private static bool MetadataSubset(ItemValue a,ItemValue b)
    {
        if(a.Metadata==null)return true;
        foreach(var pair in a.Metadata)
        {
            if(ResolvedMetadata(pair.Key))continue;
            TypedMetadataValue other;
            if(b.Metadata==null||!b.Metadata.TryGetValue(pair.Key,out other))return false;
            object av=pair.Value==null?null:pair.Value.GetValue(), bv=other==null?null:other.GetValue();
            if(!object.Equals(av,bv))return false;
        }
        return true;
    }
    private static bool SameNumber(float a, float b)
    { return !float.IsNaN(a) && !float.IsInfinity(a) && !float.IsNaN(b) && !float.IsInfinity(b) && a.Equals(b); }
    private static bool SameOptionalNumber(ItemValue a, ItemValue b, string key, float fallback)
    {
        float av = fallback, bv = fallback;
        if (a.HasMetadata(key) && !a.TryGetMetadata(key, out av)) return false;
        if (b.HasMetadata(key) && !b.TryGetMetadata(key, out bv)) return false;
        return SameNumber(av, bv);
    }
    private static bool SameBurnState(ItemValue a, ItemValue b)
    {
        int av = 0, bv = 0;
        if (a.HasMetadata("rebirth.cooking.burnt") && !a.TryGetMetadata("rebirth.cooking.burnt", out av)) return false;
        if (b.HasMetadata("rebirth.cooking.burnt") && !b.TryGetMetadata("rebirth.cooking.burnt", out bv)) return false;
        return av == bv;
    }
    private static string CookingQuality(ItemValue item)
    {
        string quality;
        if (!item.HasMetadata("rebirth.cooking.quality")) return "Standard";
        return item.TryGetMetadata("rebirth.cooking.quality", out quality) ? quality : null;
    }
    public static bool FullIngredient(ItemValue item)
    {
        if(item==null||item.IsEmpty())return false;
        return !RebirthConsumableResolver.TryResolve(item,out var food)||!food.IsDrink||
            RebirthLiquidContainerService.GetRemainingMl(item,food)>=food.ContainerCapacityMl-.01f;
    }
    public static ItemValue BaseItem(ItemValue item)
    {
        if(item.TryGetMetadata("rebirth.cooking.source",out string source)&&!string.IsNullOrEmpty(source))return ItemClass.GetItem(source);
        string name=item.ItemClass.GetItemName();
        return ItemClass.GetItem(name.StartsWith("rebirthBurnt_",StringComparison.Ordinal)?name.Substring("rebirthBurnt_".Length):name);
    }
    public static float Value(ItemValue item,string key)
    {
        if(item.TryGetMetadata("rebirth.cooking."+key,out float value))return value;
        if(key=="comfort")return RebirthFoodMoodResolver.TryResolve(item,out var mood)?mood.BaseMoodInfluence:0;
        if(!RebirthConsumableResolver.TryResolve(item,out var food))return 0;
        return key=="nutrition"?food.NutritionUnits:key=="water"?(food.IsDrink?RebirthLiquidContainerService.GetRemainingMl(item,food):food.FoodWaterMl):food.EnergyUnits;
    }
    public static string Display(float value,float baseline,string unit="")
    {
        string text=value.ToString("0.#")+unit;
        float delta=value-baseline;
        if(Math.Abs(delta)<.05f)return text;
        return text+(delta>0?" [70DD70](+":" [F07070](")+delta.ToString("0.#")+") "+(delta>0?"▲":"▼")+"[-]";
    }
}

[HarmonyPatch(typeof(ItemStack),nameof(ItemStack.CanStackWith),new[]{typeof(ItemStack),typeof(bool)})]
public static class RebirthCookingStackCompatibility
{
    [HarmonyPrefix] public static bool Prefix(ItemStack __instance,ItemStack __0,bool __1,ref bool __result)
    {
        if(__instance==null || __0==null || !RebirthCookingItemStats.IsLiquid(__instance.itemValue) ||
            !RebirthCookingItemStats.IsLiquid(__0.itemValue))return true;
        // This narrow path replaces native equipment/metadata equality only for liquid containers.
        RebirthLiquidStackCapacityPatch.Apply(__instance.itemValue.ItemClass);
        __result=__instance.count>0 && __0.count>0 && RebirthCookingItemStats.Compatible(__instance.itemValue,__0.itemValue) &&
            (__1 ? __instance.count<10 : (long)__instance.count+__0.count<=10);
        return false;
    }
    [HarmonyPostfix] public static void Postfix(ItemStack __instance,ItemStack __0,ref bool __result)
    {if(__result)__result=RebirthCookingItemStats.Compatible(__instance.itemValue,__0.itemValue);}
}
[HarmonyPatch(typeof(ItemValue),nameof(ItemValue.EqualsForMerging))]
public static class RebirthCookingMergeCompatibility
{
    [HarmonyPrefix] public static bool Prefix(ItemValue __instance,ItemValue __0,ref bool __result)
    {
        if(!RebirthCookingItemStats.IsLiquid(__instance)||!RebirthCookingItemStats.IsLiquid(__0))return true;
        __result=RebirthCookingItemStats.Compatible(__instance,__0); return false;
    }
    [HarmonyPostfix] public static void Postfix(ItemValue __instance,ItemValue __0,ref bool __result)
    {if(__result)__result=RebirthCookingItemStats.Compatible(__instance,__0);}
}
[HarmonyPatch(typeof(XUiC_ItemActionList), nameof(XUiC_ItemActionList.AddActionActions))]
public static class RebirthCookingBurntActions
{
    static bool Prefix(ItemValue itemValue) => itemValue?.ItemClass?.GetItemName().StartsWith("rebirthBurnt_",StringComparison.Ordinal)!=true;
}
