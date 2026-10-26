using System;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthSurvivalStatisticsPanel:XUiController
{
    private long generation=long.MinValue,textRevision=long.MinValue;
    private bool wasVisible;
    private readonly XUiV_Label[] values=new XUiV_Label[7];
    private static readonly string[] EpisodeNames={"buffLegBroken","buffArmBroken","buffLegSprained","buffArmSprained","buffInjuryConcussion","buffDysenteryMain"};
    private sealed class RankingControls
    {
        internal XUiController Scroll;
        internal readonly XUiController[] Rows=new XUiController[RebirthStatisticsProtocol.MaxSurvivalEntries];
        internal readonly XUiV_Label[] Names=new XUiV_Label[RebirthStatisticsProtocol.MaxSurvivalEntries];
        internal readonly XUiV_Label[] Counts=new XUiV_Label[RebirthStatisticsProtocol.MaxSurvivalEntries];
        internal readonly XUiV_Sprite[] Fills=new XUiV_Sprite[RebirthStatisticsProtocol.MaxSurvivalEntries];
    }
    private readonly Dictionary<string,RankingControls> rankings=new Dictionary<string,RankingControls>(StringComparer.Ordinal);
    public override void Init()
    {
        base.Init();
        for(int i=0;i<values.Length;i++)values[i]=GetChildById("survivalValue"+i)?.ViewComponent as XUiV_Label;
        foreach(string kind in new[]{"Cooked","Foods","Drinks","Health","Healing"})
        {
            var controls=new RankingControls {Scroll=GetChildById("survival"+kind+"Scroll")};
            for(int i=0;i<controls.Rows.Length;i++)
            {
                string prefix="survival"+kind;
                controls.Rows[i]=GetChildById(prefix+"Row"+i);
                controls.Names[i]=GetChildById(prefix+"Name"+i)?.ViewComponent as XUiV_Label;
                controls.Counts[i]=GetChildById(prefix+"Count"+i)?.ViewComponent as XUiV_Label;
                controls.Fills[i]=GetChildById(prefix+"Fill"+i)?.ViewComponent as XUiV_Sprite;
            }
            rankings[kind]=controls;
        }
    }
    public override void OnClose(){wasVisible=false;base.OnClose();}
    public override void Update(float dt)
    {
        base.Update(dt);
        bool visible=ViewComponent!=null&&ViewComponent.IsVisible&&ViewComponent.UiTransform!=null&&ViewComponent.UiTransform.gameObject.activeInHierarchy;
        if(!visible){wasVisible=false;return;}
        if(!wasVisible)
        {
            wasVisible=true;generation=long.MinValue;
            var player=xui?.playerUI?.entityPlayer as EntityPlayerLocal;
            if(player!=null)RebirthStatisticsService.RequestSnapshot(player,true);
        }
        long current=RebirthStatisticsClientState.Generation,words=RebirthUiProjectionTextCache.Revision;
        if(generation==current&&textRevision==words)return;
        long observed;var snapshot=RebirthStatisticsClientState.Get(out observed);
        generation=observed;textRevision=words;
        string pending=RebirthUiProjectionTextCache.L("xuiRebirthCharacterDataPending","Waiting for character data...");
        Set(values[0],snapshot==null?pending:snapshot.CookedOutputUnits.ToString("N0",CultureInfo.InvariantCulture));
        Set(values[1],snapshot==null?pending:snapshot.MealsEaten.ToString("N0",CultureInfo.InvariantCulture));
        Set(values[2],snapshot==null?pending:snapshot.DrinkServings.ToString("N0",CultureInfo.InvariantCulture));
        Set(values[3],snapshot==null?pending:(snapshot.DrinkVolumeMl/1000d).ToString("N1",CultureInfo.InvariantCulture)+" L");
        long illnesses=0,injuries=0;
        if(snapshot!=null)foreach(var entry in snapshot.HealthEpisodes)
        {
            if(entry.Id=="buffDysenteryMain")illnesses=entry.Count;
            else injuries=entry.Count>long.MaxValue-injuries?long.MaxValue:injuries+entry.Count;
        }
        Set(values[4],snapshot==null?pending:illnesses.ToString("N0",CultureInfo.InvariantCulture));
        Set(values[5],snapshot==null?pending:injuries.ToString("N0",CultureInfo.InvariantCulture));
        Set(values[6],snapshot==null?pending:snapshot.HealingUses.ToString("N0",CultureInfo.InvariantCulture));
        Render("Cooked",snapshot?.FoodsCooked);Render("Foods",snapshot?.FoodsEaten);Render("Drinks",snapshot?.DrinksConsumed);
        List<RebirthStatisticsCountSnapshot> episodes=null;
        if(snapshot!=null)
        {
            episodes=new List<RebirthStatisticsCountSnapshot>(EpisodeNames.Length);
            foreach(string name in EpisodeNames)
            {
                long count=0;foreach(var entry in snapshot.HealthEpisodes)if(entry.Id==name){count=entry.Count;break;}
                if(count>0)episodes.Add(new RebirthStatisticsCountSnapshot{Id=name,Count=count});
            }
            episodes.Sort((a,b)=>{int order=b.Count.CompareTo(a.Count);return order!=0?order:StringComparer.Ordinal.Compare(a.Id,b.Id);});
        }
        Render("Health",episodes);
        Render("Healing",snapshot?.HealingItems);
    }
    private void Render(string kind,List<RebirthStatisticsCountSnapshot> entries)
    {
        RankingControls controls;
        if(!rankings.TryGetValue(kind,out controls))return;
        long best=entries!=null&&entries.Count>0?entries[0].Count:0;
        for(int i=0;i<RebirthStatisticsProtocol.MaxSurvivalEntries;i++)
        {
            var row=controls.Rows[i];
            bool present=entries!=null&&i<entries.Count;
            if(row?.ViewComponent!=null)row.ViewComponent.IsVisible=present;
            if(!present)continue;
            var entry=entries[i];string name;
            if(kind=="Health")name=Localization.Get("xuiRebirthSurvivalEpisode_"+entry.Id);
            else
            {
                name=Localization.Get(entry.Id);
                ItemValue item=ItemClass.GetItem(entry.Id,false);
                if(item?.ItemClass!=null)name=item.ItemClass.GetLocalizedItemName();
            }
            Set(controls.Names[i],(i+1).ToString(CultureInfo.InvariantCulture)+". "+name);
            Set(controls.Counts[i],entry.Count.ToString("N0",CultureInfo.InvariantCulture));
            var fill=controls.Fills[i];
            if(fill!=null)fill.Fill=best<=0?0:Mathf.Clamp01((float)((double)entry.Count/best));
        }
        RebirthStatisticsScrollLayout.Refresh(controls.Scroll,null,kind=="Health"||kind=="Healing"?802:502,38*(entries?.Count??0));
    }
    private static void Set(XUiV_Label label,string value)
    {if(label!=null)label.Text=value??string.Empty;}
}
