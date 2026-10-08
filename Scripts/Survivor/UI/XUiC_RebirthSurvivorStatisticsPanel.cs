using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Read-only presentation controller for the Character / Statistics page. All counters are supplied
/// by the server-authoritative Statistics subsystem; this controller performs no gameplay mutation.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthSurvivorStatisticsPanel : XUiController
{
    private const int ActivityRows=5, TrendRows=7, TrendMetrics=5, CategoryRows=8, WeaponRows=16, MilestoneRows=16;
    private RebirthStatisticsSnapshot snapshot;
    private long lastRevision=long.MinValue;
    private float refresh;
    private bool subscribed, dirty = true, wasVisible, presentationActive;
    private long projectionGeneration = long.MinValue, textRevision = long.MinValue;
    private readonly XUiV_Label[] kpis=new XUiV_Label[8];
    private readonly XUiV_Label[] activityValues=new XUiV_Label[ActivityRows];
    private readonly XUiV_Sprite[] activityFills=new XUiV_Sprite[ActivityRows];
    private readonly XUiV_Label[] trendDays=new XUiV_Label[TrendRows];
    private readonly XUiV_Sprite[,] trendFills=new XUiV_Sprite[TrendRows,TrendMetrics];
    private readonly XUiV_Label[] categoryValues=new XUiV_Label[CategoryRows];
    private readonly XUiV_Label[] weaponNames=new XUiV_Label[WeaponRows];
    private readonly XUiV_Label[] weaponValues=new XUiV_Label[WeaponRows];
    private readonly XUiController[] weaponRowControllers=new XUiController[WeaponRows];
    private readonly XUiV_Label[] milestoneNames=new XUiV_Label[MilestoneRows];
    private readonly XUiV_Label[] milestoneDays=new XUiV_Label[MilestoneRows];
    private readonly XUiController[] milestoneRowControllers=new XUiController[MilestoneRows];
    private XUiV_Label combatSummary,damageSummary,bestSummary;

    public override void Init()
    {
        base.Init(); presentationActive = true;
        for(int i=0;i<kpis.Length;i++)kpis[i]=Label("survivorStatisticsKpiValue"+i);
        for(int i=0;i<ActivityRows;i++){activityValues[i]=Label("survivorStatisticsActivityValue"+i);activityFills[i]=Sprite("survivorStatisticsActivityFill"+i);}
        for(int r=0;r<TrendRows;r++){trendDays[r]=Label("survivorStatisticsTrendDay"+r);for(int m=0;m<TrendMetrics;m++)trendFills[r,m]=Sprite("survivorStatisticsTrendFill"+r+"_"+m);}
        for(int i=0;i<CategoryRows;i++)categoryValues[i]=Label("survivorStatisticsCategoryValue"+i);
        for(int i=0;i<WeaponRows;i++){weaponRowControllers[i]=GetChildById("survivorStatisticsWeaponRow"+i);weaponNames[i]=Label("survivorStatisticsWeaponName"+i);weaponValues[i]=Label("survivorStatisticsWeaponValue"+i);}
        for(int i=0;i<MilestoneRows;i++){milestoneRowControllers[i]=GetChildById("survivorStatisticsMilestoneRow"+i);milestoneNames[i]=Label("survivorStatisticsMilestoneName"+i);milestoneDays[i]=Label("survivorStatisticsMilestoneDay"+i);}
        combatSummary=Label("survivorStatisticsCombatSummary");damageSummary=Label("survivorStatisticsDamageSummary");bestSummary=Label("survivorStatisticsBestSummary");
        Subscribe();
        // A snapshot is requested when the Statistics page actually becomes visible.
    }
    private void Subscribe() { if (subscribed) return; RebirthStatisticsClientState.ProjectionChanged += OnStatisticsChanged; subscribed = true; }
    private void Unsubscribe() { if (!subscribed) return; RebirthStatisticsClientState.ProjectionChanged -= OnStatisticsChanged; subscribed = false; }
    public override void OnOpen() { base.OnOpen(); presentationActive = true; Subscribe(); dirty = true; wasVisible = false; }
    public override void OnClose() { presentationActive = false; Unsubscribe(); wasVisible = false; base.OnClose(); }
    public override void Cleanup() { presentationActive = false; Unsubscribe(); snapshot = null; wasVisible = false; base.Cleanup(); }
    public override void Update(float dt)
    {
        base.Update(dt);
        if (!presentationActive) return;
        bool visible = PageVisible;
        if (!visible) { wasVisible = false; return; }
        if (!wasVisible) { Subscribe(); dirty = true; Request(true); wasVisible = true; }
        refresh += Math.Max(0f, dt);
        if (refresh >= 0.5f) { refresh = 0f; if (snapshot == null) Request(false); }
        long text = RebirthUiProjectionTextCache.Revision;
        if (dirty || projectionGeneration != RebirthStatisticsClientState.Generation || textRevision != text)
        { dirty = false; textRevision = text; Refresh(); }
    }
    private bool PageVisible => ViewComponent != null && ViewComponent.IsVisible && ViewComponent.UiTransform != null && ViewComponent.UiTransform.gameObject.activeInHierarchy;
    public void RefreshVisiblePage() { if (!presentationActive || !PageVisible) return; Subscribe(); Request(true); dirty = true; }
    private void OnStatisticsChanged() { dirty = true; }
    private void Request(bool force) { EntityPlayerLocal p=xui!=null&&xui.playerUI!=null?xui.playerUI.entityPlayer as EntityPlayerLocal:null; if(p!=null)RebirthStatisticsService.RequestSnapshot(p,force); }
    private void Refresh()
    {
        if (projectionGeneration != RebirthStatisticsClientState.Generation)
            snapshot = RebirthStatisticsClientState.Get(out projectionGeneration);
        lastRevision = snapshot != null ? snapshot.Revision : long.MinValue;
        Render();
    }

    private void Render()
    {
        if (!PageVisible) return;
        RebirthStatisticsSnapshot s=snapshot??new RebirthStatisticsSnapshot();
        string[] values={FormatTime(s.CurrentLifeSeconds),FormatDistance(s.DistanceMeters),s.ZombiesKilled.ToString("N0",CultureInfo.InvariantCulture),s.Deaths.ToString("N0",CultureInfo.InvariantCulture),s.DaysSurvived.ToString("N0",CultureInfo.InvariantCulture),s.KnowledgeDiscovered.ToString("N0",CultureInfo.InvariantCulture),s.SkillProgressGained.ToString("0.0",CultureInfo.InvariantCulture),s.PlayerLevel.ToString(CultureInfo.InvariantCulture)};
        for(int i=0;i<kpis.Length;i++)Set(kpis[i],values[i]);

        // Slot 1 is intentionally absent from the view. Keep historical save fields,
        // but exclude idle/unclassified "Survival" time from activity percentages.
        double[] activity={s.CombatSeconds,0d,s.CraftingSeconds,s.ExplorationSeconds,s.ManagementSeconds};
        double activityTotal=0d;for(int i=0;i<activity.Length;i++)activityTotal+=activity[i];
        for(int i=0;i<ActivityRows;i++){float f=activityTotal>0d?(float)(activity[i]/activityTotal):0f;SetFill(activityFills[i],f);Set(activityValues[i],(f*100f).ToString("0",CultureInfo.InvariantCulture)+"%  "+FormatTime(activity[i]));}

        for(int r=0;r<TrendRows;r++)
        {
            bool has=r<s.Trend.Count;RebirthStatisticsTrendSnapshot t=has?s.Trend[r]:null;Set(trendDays[r],has?"Day "+t.WorldDay.ToString(CultureInfo.InvariantCulture):"—");
            float[] fs=has?new[]{t.HealthPercent,t.StaminaPercent,t.NutritionPercent,t.HydrationPercent,t.EnergyPercent}:new[]{0f,0f,0f,0f,0f};
            for(int m=0;m<TrendMetrics;m++)SetFill(trendFills[r,m],fs[m]);
        }

        string[] cats={s.ZombiesKilled.ToString("N0",CultureInfo.InvariantCulture),s.ItemsCrafted.ToString("N0",CultureInfo.InvariantCulture),s.ResourcesGathered.ToString("N0",CultureInfo.InvariantCulture),FormatDistance(s.DistanceMeters),s.PoisCleared.ToString("N0",CultureInfo.InvariantCulture),s.LocationsDiscovered.ToString("N0",CultureInfo.InvariantCulture),s.TradersVisited.ToString("N0",CultureInfo.InvariantCulture),s.BiomesVisited.ToString("N0",CultureInfo.InvariantCulture)};
        for(int i=0;i<CategoryRows;i++)Set(categoryValues[i],cats[i]);

        Set(combatSummary,
            L("xuiRebirthStatisticsMeleeKills","Melee Kills")+": "+s.MeleeKills.ToString("N0",CultureInfo.InvariantCulture)+"    "+
            L("xuiRebirthStatisticsRangedKills","Ranged Kills")+": "+s.RangedKills.ToString("N0",CultureInfo.InvariantCulture)+"\n"+
            L("xuiRebirthStatisticsHeadshots","Headshots")+": "+s.HeadshotKills.ToString("N0",CultureInfo.InvariantCulture)+"    "+
            L("xuiRebirthStatisticsAnimalsKilled","Animals Killed")+": "+s.AnimalsKilled.ToString("N0",CultureInfo.InvariantCulture)+"    "+
            L("xuiPlayerKills","Player Kills")+": "+s.PlayerKills.ToString("N0",CultureInfo.InvariantCulture));
        string damage=L("xuiRebirthStatisticsDamageDealt","Damage Dealt")+": "+s.DamageDealt.ToString("N0",CultureInfo.InvariantCulture)+"    "+L("xuiRebirthStatisticsDamageTaken","Damage Taken")+": "+s.DamageTaken.ToString("N0",CultureInfo.InvariantCulture);
        if(s.HasAuthoritativeBlockedDamage)damage+="    "+L("xuiRebirthStatisticsDamageBlocked","Damage Blocked")+": "+s.DamageBlocked.ToString("N0",CultureInfo.InvariantCulture);
        Set(damageSummary,damage);
        SetVisible(GetChildById("pc134WeaponsEmpty"), s.Weapons.Count == 0);
        SetVisible(GetChildById("survivorStatisticsWeaponScroll"), s.Weapons.Count > 0);
        var weaponScroll=GetChildById("survivorStatisticsWeaponScroll");
        if(weaponScroll!=null)foreach(var child in weaponScroll.GetChildrenByType<XUiController>())
            if(child.ViewComponent is XUiV_ScrollBar bar){bar.IsVisible=s.Weapons.Count>6;if(bar.ScrollBar!=null)bar.ScrollBar.alpha=s.Weapons.Count>6?1f:0f;}

        SetVisible(GetChildById("pc134MilestonesEmpty"), s.Milestones.Count == 0);
        for(int i=0;i<WeaponRows;i++){bool has=i<s.Weapons.Count;SetVisible(weaponRowControllers[i],has);if(has){Set(weaponNames[i],LocalizeItem(s.Weapons[i].ItemName));Set(weaponValues[i],s.Weapons[i].Kills.ToString("N0",CultureInfo.InvariantCulture));}}

        Set(bestSummary,
            L("xuiRebirthStatisticsMostZombiesDay","Most Zombies Killed in a Day")+": "+s.BestZombiesInDay.ToString("N0",CultureInfo.InvariantCulture)+"\n"+
            L("xuiRebirthStatisticsLongestLife","Longest Life")+": "+FormatTime(s.LongestLifeSeconds)+"\n"+
            L("xuiRebirthStatisticsFarthestOnFoot","Farthest on Foot in a Day")+": "+FormatDistance(s.BestOnFootDistanceInDay)+"\n"+
            L("xuiRebirthStatisticsHighestHit","Highest Damage in a Hit")+": "+s.HighestDamageHit.ToString("N0",CultureInfo.InvariantCulture)+"\n"+
            L("xuiRebirthStatisticsMostCraftedDay","Most Items Crafted in a Day")+": "+s.BestItemsCraftedInDay.ToString("N0",CultureInfo.InvariantCulture)+"\n"+
            L("xuiRebirthStatisticsMostResourcesDay","Most Resources Gathered in a Day")+": "+s.BestResourcesInDay.ToString("N0",CultureInfo.InvariantCulture)+(s.HighestFallSurvived>0d?"\n"+L("xuiRebirthStatisticsHighestFallSurvived","Highest Fall Survived")+": "+s.HighestFallSurvived.ToString("N0",CultureInfo.InvariantCulture):string.Empty));

        for(int i=0;i<MilestoneRows;i++){bool has=i<s.Milestones.Count;SetVisible(milestoneRowControllers[i],has);if(has){Set(milestoneNames[i],s.Milestones[i].Name);Set(milestoneDays[i],"Day "+s.Milestones[i].WorldDay.ToString(CultureInfo.InvariantCulture));}}
    }

    private XUiV_Label Label(string id){XUiController c=GetChildById(id);return c!=null?c.ViewComponent as XUiV_Label:null;}
    private XUiV_Sprite Sprite(string id){XUiController c=GetChildById(id);return c!=null?c.ViewComponent as XUiV_Sprite:null;}
    private static void Set(XUiV_Label l,string v){if(l!=null)l.Text=v??string.Empty;}
    private static void SetFill(XUiV_Sprite s,float f){if(s!=null)s.Fill=Mathf.Clamp01(f);}
    private static void SetVisible(XUiController c,bool v){if(c!=null&&c.ViewComponent!=null)c.ViewComponent.IsVisible=v;}
    private static string FormatTime(double seconds){if(seconds<0d)seconds=0d;TimeSpan t=TimeSpan.FromSeconds(seconds);if(t.TotalDays>=1d)return ((int)t.TotalDays).ToString(CultureInfo.InvariantCulture)+"d "+t.Hours.ToString("00",CultureInfo.InvariantCulture)+"h";return ((int)t.TotalHours).ToString("00",CultureInfo.InvariantCulture)+":"+t.Minutes.ToString("00",CultureInfo.InvariantCulture)+":"+t.Seconds.ToString("00",CultureInfo.InvariantCulture);}
    private static string FormatDistance(double meters){return meters>=1000d?(meters/1000d).ToString("0.0",CultureInfo.InvariantCulture)+" km":meters.ToString("0",CultureInfo.InvariantCulture)+" m";}
    private static string L(string key,string fallback){return RebirthUiProjectionTextCache.L(key,fallback);}
    private static string LocalizeItem(string itemName){if(string.IsNullOrEmpty(itemName))return string.Empty;ItemValue v=ItemClass.GetItem(itemName,false);if(v!=null&&v.ItemClass!=null){string key=v.ItemClass.GetLocalizedItemName();if(!string.IsNullOrEmpty(key))return key;}return itemName;}
}
