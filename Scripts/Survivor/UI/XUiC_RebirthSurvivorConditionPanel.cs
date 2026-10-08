using System;
using System.Globalization;
using UnityEngine.Scripting;

#nullable disable

/// <summary>Functional Chunk 09 condition projection. Final Character-window visual polish is Chunk 15.</summary>
[Preserve]
public sealed class XUiC_RebirthSurvivorConditionPanel : XUiController
{
    private RebirthSurvivorOwnerStateSnapshot snapshot;
    private RebirthSurvivorOwnerHeader lastHeader;
    private long lastTextRevision = long.MinValue;
    private float refreshAccumulator;
    private string cachedMood = string.Empty;
    private string cachedDiet = string.Empty;
    private string cachedVariety = string.Empty;
    private string cachedHealth = string.Empty;
    private string cachedCauses = string.Empty;


    public override void Init() { base.Init(); RefreshSnapshot(true); }
    public override void Update(float _dt)
    {
        base.Update(_dt); refreshAccumulator += Math.Max(0f,_dt);
        if (refreshAccumulator < 0.25f) return; refreshAccumulator=0f; RefreshSnapshot(false);
    }

    private void RefreshSnapshot(bool force)
    {
        RebirthSurvivorOwnerHeader header = RebirthSurvivorClientState.GetOwnerHeader();
        long textRevision = RebirthUiProjectionTextCache.Revision;
        bool changed = !header.Equals(lastHeader);
        if (!force && !changed && textRevision == lastTextRevision) return;
        if (force || changed) snapshot = RebirthSurvivorClientState.GetOwnerStateSnapshot(out lastHeader);
        lastTextRevision = textRevision;
        cachedMood = FormatMood();
        cachedDiet = FormatDiet();
        cachedVariety = FormatVariety();
        cachedHealth = FormatHealth();
        cachedCauses = FormatCauses();
        IsDirty = true; RefreshBindings();
    }

    public override bool GetBindingValueInternal(ref string value,string bindingName)
    {
        switch(bindingName)
        {
            case "rbcond_visible": value=(snapshot!=null&&snapshot.RebirthModeEnabled&&snapshot.HasCharacter).ToString();return true;
            case "rbcond_mood": value=cachedMood;return true;
            case "rbcond_diet": value=cachedDiet;return true;
            case "rbcond_variety": value=cachedVariety;return true;
            case "rbcond_health": value=cachedHealth;return true;
            case "rbcond_causes": value=cachedCauses;return true;
            default:return base.GetBindingValueInternal(ref value,bindingName);
        }
    }

    private string FormatMood()
    {
        if(snapshot==null)return string.Empty;
        return snapshot.MoodCurrent.ToString("0.0",CultureInfo.InvariantCulture)+" / 100  (target "+snapshot.MoodTarget.ToString("0.0",CultureInfo.InvariantCulture)+")";
    }
    private string FormatDiet(){return snapshot==null?string.Empty:snapshot.DietSatisfaction.ToString("0.0",CultureInfo.InvariantCulture)+" / 100";}
    private string FormatVariety()
    {
        if(snapshot==null||snapshot.RecentMealCount<=0)return Localize("xuiRebirthConditionNoMeals","No meaningful meals recorded yet.");
        return snapshot.RecentVarietyCount.ToString(CultureInfo.InvariantCulture)+" "+Localize("xuiRebirthConditionVarieties","compatible varieties")+" / "+snapshot.RecentMealCount.ToString(CultureInfo.InvariantCulture)+" "+Localize("xuiRebirthConditionRecentMeals","recent meals");
    }
    private string FormatHealth()
    {
        if(snapshot==null)return string.Empty;
        return snapshot.HealthCapacity.ToString("0.0",CultureInfo.InvariantCulture)+" / "+snapshot.HealthPotential.ToString("0.0",CultureInfo.InvariantCulture);
    }
    private string FormatCauses()
    {
        if(snapshot==null)return string.Empty;
        string positive=FormatCause(snapshot.MoodPositiveCauseId,snapshot.MoodPositiveCauseDelta);
        string negative=FormatCause(snapshot.MoodNegativeCauseId,snapshot.MoodNegativeCauseDelta);
        if(positive.Length==0&&negative.Length==0)return Localize("xuiRebirthConditionNoDominantCause","No dominant Mood contributor.");
        if(positive.Length==0)return negative;if(negative.Length==0)return positive;return positive+"   |   "+negative;
    }
    private static string FormatCause(string id,float delta)
    {
        if(string.IsNullOrEmpty(id)||Math.Abs(delta)<0.001f)return string.Empty;
        string label=id=="diet_satisfaction"?Localize("xuiRebirthConditionDietCause","Diet Satisfaction"):id;
        return label+" "+(delta>0f?"+":"")+delta.ToString("0.0",CultureInfo.InvariantCulture);
    }
    private static string Localize(string key,string fallback)
    {string v=RebirthUiProjectionTextCache.L(key,fallback);return string.IsNullOrEmpty(v)||string.Equals(v,key,StringComparison.Ordinal)?fallback:v;}
}
