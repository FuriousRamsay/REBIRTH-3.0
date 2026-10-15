using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthHordeNightStatisticsPanel:XUiController
{
    private readonly XUiV_Label[] cards=new XUiV_Label[6];
    private const int HistoryPoolSize=12;
    private readonly XUiController[] historyRows=new XUiController[HistoryPoolSize];
    private readonly XUiV_Label[] historyLabels=new XUiV_Label[HistoryPoolSize];
    private readonly Dictionary<int,RebirthHordeNightStatistics> historyCache=new Dictionary<int,RebirthHordeNightStatistics>();
    private XUiC_RebirthCharacterOverviewList historyScroll;
    private int cachedHistoryCount=-1,requestedPage=-1;
    private readonly XUiController[,] rows=new XUiController[3,RebirthHordeNightStatisticsProjection.RankingLimit];
    private readonly XUiV_Label[,] names=new XUiV_Label[3,RebirthHordeNightStatisticsProjection.RankingLimit];
    private readonly XUiV_Label[,] counts=new XUiV_Label[3,RebirthHordeNightStatisticsProjection.RankingLimit];
    private readonly XUiV_Sprite[,] fills=new XUiV_Sprite[3,RebirthHordeNightStatisticsProjection.RankingLimit];
    private readonly XUiV_Sprite[,] icons=new XUiV_Sprite[3,RebirthHordeNightStatisticsProjection.RankingLimit];
    private readonly XUiV_Sprite[] historyBackgrounds=new XUiV_Sprite[HistoryPoolSize];
    private long generation=long.MinValue,textRevision=long.MinValue;
    private bool wasVisible;
    private RebirthStatisticsSnapshot snapshot;
    private int displayedDay=int.MinValue;
    private bool resetRankScroll;
    private XUiV_Label selectionLabel,pageLabel,emptyLabel;
    private XUiController previous,next;
    public override void Init()
    {
        base.Init();
        historyScroll=GetChildById("hordeHistoryScroll") as XUiC_RebirthCharacterOverviewList;
        if(historyScroll!=null)historyScroll.DataRangeChanged+=Render;
        for(int i=0;i<cards.Length;i++)cards[i]=Label("hordeCardValue"+i);
        for(int i=0;i<historyRows.Length;i++)
        {
            int row=i;historyRows[i]=GetChildById("hordeHistoryRow"+i);historyLabels[i]=Label("hordeHistoryLabel"+i);
            historyBackgrounds[i]=GetChildById("hordeHistoryBg"+i)?.ViewComponent as XUiV_Sprite;
            Wire("hordeHistorySelect"+i,()=>
            {if(historyCache.TryGetValue((historyScroll?.FirstDataIndex??0)+row,out var entry))Request(snapshot?.HordeHistoryPage??0,entry.WorldDay);});
        }
        previous=GetChildById("hordePrevious");next=GetChildById("hordeNext");
        Wire("hordePrevious",()=>{if(snapshot?.HordeHistoryPage>0)Request(snapshot.HordeHistoryPage-1,snapshot.SelectedHordeNight?.WorldDay??0);});
        Wire("hordeNext",()=>{if(snapshot!=null&&(long)(snapshot.HordeHistoryPage+1)*historyRows.Length<snapshot.HordeHistoryCount)Request(snapshot.HordeHistoryPage+1,snapshot.SelectedHordeNight?.WorldDay??0);});
        Wire("hordeLifetime",()=>Request(snapshot?.HordeHistoryPage??0,0));
        selectionLabel=Label("hordeSelection");pageLabel=Label("hordePage");emptyLabel=Label("hordeEmpty");
        for(int column=0;column<3;column++)for(int row=0;row<RebirthHordeNightStatisticsProjection.RankingLimit;row++)
        {
            string id="hordeRank"+column+"_"+row;
            rows[column,row]=GetChildById(id);names[column,row]=Label(id+"Name");counts[column,row]=Label(id+"Count");
            fills[column,row]=GetChildById(id+"Fill")?.ViewComponent as XUiV_Sprite;
            icons[column,row]=GetChildById(id+"Icon")?.ViewComponent as XUiV_Sprite;
        }
    }
    private void Wire(string id,Action action)
    {var control=GetChildById(id);if(control!=null)control.OnPress+=(sender,button)=>{if(button==0||button==-1)action();};}
    private void Request(int page,int day)
    {var player=xui?.playerUI?.entityPlayer as EntityPlayerLocal;if(player!=null)RebirthStatisticsService.RequestHordeHistory(player,page,day);}
    public override void OnClose(){wasVisible=false;historyCache.Clear();cachedHistoryCount=-1;requestedPage=-1;base.OnClose();}
    public override void Update(float dt)
    {
        base.Update(dt);
        bool visible=ViewComponent!=null&&ViewComponent.IsVisible&&ViewComponent.UiTransform!=null&&ViewComponent.UiTransform.gameObject.activeInHierarchy;
        if(!visible){wasVisible=false;return;}
        if(!wasVisible){wasVisible=true;generation=long.MinValue;Request(snapshot?.HordeHistoryPage??0,snapshot?.SelectedHordeNight?.WorldDay??0);}
        long words=RebirthUiProjectionTextCache.Revision;
        if(generation==RebirthStatisticsClientState.Generation&&textRevision==words)return;
        snapshot=RebirthStatisticsClientState.Get(out generation);textRevision=words;Render();
    }
    private void Render()
    {
        var totals=snapshot?.HordeNightTotals;var night=snapshot?.SelectedHordeNight;
        int day=night?.WorldDay??0;resetRankScroll=day!=displayedDay;displayedDay=day;
        string pending=L("xuiRebirthCharacterDataPending");
        long[] values=totals==null?null:new[]{totals.NightsParticipated,totals.Kills,totals.BestNightKills,totals.HeadshotKills,totals.HeadDecapitations,totals.Deaths};
        for(int i=0;i<cards.Length;i++)Set(cards[i],values==null?pending:values[i].ToString("N0",CultureInfo.InvariantCulture));
        Set(selectionLabel,night==null?L("xuiRebirthHordeLifetime"):string.Format(CultureInfo.InvariantCulture,L("xuiRebirthHordeSelectedCounts"),night.WorldDay,Count(night.Kills,"Kill"),Count(night.HeadshotKills,"Headshot"),Count(night.HeadDecapitations,"Decapitation"),Count(night.Deaths,"Death")));
        int page=snapshot?.HordeHistoryPage??0,total=snapshot?.HordeHistoryCount??0;
        if(cachedHistoryCount!=total){historyCache.Clear();cachedHistoryCount=total;requestedPage=-1;}
        if(snapshot!=null)
        {
            for(int i=0;i<snapshot.HordeHistory.Count;i++)historyCache[page*RebirthStatisticsProtocol.HordeHistoryPageSize+i]=snapshot.HordeHistory[i];
            if(requestedPage==page)requestedPage=-1;
        }
        historyScroll?.SetItemCount(total,string.Empty);
        int missingPage=-1;
        Set(pageLabel,string.Format(CultureInfo.InvariantCulture,L("xuiRebirthHordeHistoryPage"),total==0?0:page+1,total==0?0:(total-1)/historyRows.Length+1));
        Set(emptyLabel,snapshot==null?pending:total==0?L("xuiRebirthHordeNoHistory"):string.Empty);
        if(previous?.ViewComponent!=null)previous.ViewComponent.Enabled=page>0;
        if(next?.ViewComponent!=null)next.ViewComponent.Enabled=(long)(page+1)*historyRows.Length<total;
        for(int i=0;i<historyRows.Length;i++)
        {
            int index=(historyScroll?.FirstDataIndex??0)+i;
            RebirthHordeNightStatistics entry;
            bool present=historyCache.TryGetValue(index,out entry);
            if(!present&&index<total&&missingPage<0)missingPage=index/RebirthStatisticsProtocol.HordeHistoryPageSize;
            if(historyRows[i]?.ViewComponent!=null)historyRows[i].ViewComponent.IsVisible=present;
            if(present)
            {
                Set(historyLabels[i],string.Format(CultureInfo.InvariantCulture,L("xuiRebirthHordeHistoryCounts").Replace("\\n","\n"),entry.WorldDay,Count(entry.Kills,"Kill"),Count(entry.HeadshotKills,"Headshot"),Count(entry.Deaths,"Death"))+(entry.Completed?string.Empty:"  "+L("xuiRebirthHordeInProgress")));
                if(historyBackgrounds[i]!=null)historyBackgrounds[i].Color=entry.WorldDay==day?new Color32(65,65,72,240):new Color32(24,24,29,220);
            }
        }
        if(missingPage>=0&&requestedPage<0){requestedPage=missingPage;Request(missingPage,day);}
        RenderRank(0,night!=null?night.WeaponCategories:totals?.WeaponCategories);
        RenderRank(1,night!=null?night.ZombieTypes:totals?.ZombieTypes);
        RenderRank(2,night!=null?night.ZombieVariants:totals?.ZombieVariants);
    }
    private void RenderRank(int column,Dictionary<string,long> map)
    {
        var entries=map?.OrderByDescending(x=>x.Value).ThenBy(x=>Display(column,x.Key),StringComparer.CurrentCultureIgnoreCase).ToArray();
        long best=entries!=null&&entries.Length>0?entries[0].Value:0;
        for(int row=0;row<RebirthHordeNightStatisticsProjection.RankingLimit;row++)
        {
            bool present=entries!=null&&row<entries.Length;
            if(rows[column,row]?.ViewComponent!=null)rows[column,row].ViewComponent.IsVisible=present;
            if(!present)continue;
            Set(names[column,row],(row+1)+". "+Display(column,entries[row].Key));Set(counts[column,row],entries[row].Value.ToString("N0",CultureInfo.InvariantCulture));
            var tint=RankColor(column,entries[row].Key);
            if(counts[column,row]!=null)counts[column,row].Color=tint;
            if(icons[column,row]!=null)
            {
                RebirthSkillDefinition skill=null;
                bool known=column==0&&(RebirthSurvivorDefinitionRegistry.TryGetSkill(entries[row].Key,out skill)||RebirthSurvivorDefinitionRegistry.TryGetSkill("skill."+entries[row].Key,out skill));
                icons[column,row].UIAtlas=known?"RebirthSurvivorIcons":"UIAtlas";
                icons[column,row].SpriteName=known?RebirthSkillAptitudeTraitFactory.SkillIconKey(skill.Id):column==0?"ui_game_symbol_fire":column==1?"ui_game_symbol_zombie":"ui_game_symbol_skull";
                icons[column,row].Color=tint;
            }
            if(fills[column,row]!=null){fills[column,row].Color=tint;fills[column,row].Fill=best>0?Mathf.Clamp01((float)((double)entries[row].Value/best)):0f;}
        }
        RebirthStatisticsScrollLayout.Refresh(GetChildById("hordeScroll"+column),GetChildById("hordeScroll"+column+"Content"),350,54*(entries?.Length??0),resetRankScroll);
    }
    private static string Count(long value,string noun)=>string.Format(CultureInfo.InvariantCulture,L("xuiRebirthHordeCount"+noun+(value==1?string.Empty:"s")),value);
    private static Color32 RankColor(int column,string id)
    {
        if(column==0)return new Color32(55,197,176,255);
        if(column==1)return new Color32(231,184,74,255);
        switch(id){case "feral":return new Color32(240,75,75,255);case "radiated":return new Color32(119,211,115,255);case "charged":return new Color32(95,176,238,255);case "infernal":return new Color32(245,215,65,255);default:return new Color32(245,245,245,255);}
    }
    private static string Display(int column,string id)
    {
        RebirthSkillDefinition definition;
        if(column==0&&RebirthSurvivorDefinitionRegistry.TryGetSkill(id,out definition))return L(definition.NameKey);
        string key=column==0?"xuiRebirthHordeWeapon_"+id:column==2?"xuiRebirthHordeVariant_"+id:id;
        string text=L(key);return text==key?id:text;
    }
    private XUiV_Label Label(string id)=>GetChildById(id)?.ViewComponent as XUiV_Label;
    private static void Set(XUiV_Label label,string text){if(label!=null)label.Text=text??string.Empty;}
    private static string L(string key)=>RebirthUiProjectionTextCache.L(key,key);
}
