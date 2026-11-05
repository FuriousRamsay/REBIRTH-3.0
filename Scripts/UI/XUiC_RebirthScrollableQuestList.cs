using UnityEngine;
using UnityEngine.Scripting;
// The native row shows only the quest type, so POI jobs read "Tier 1 - The Purser Residence".
[HarmonyLib.HarmonyPatch(typeof(XUiC_QuestEntry),nameof(XUiC_QuestEntry.GetBindingValueInternal))]
internal static class RebirthQuestEntryPoiName
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Quest,string> Names=new System.Runtime.CompilerServices.ConditionalWeakTable<Quest,string>();
    private static void Postfix(XUiC_QuestEntry __instance,ref string value,string bindingName)
    {
        if(bindingName!="questname")return;
        var quest=__instance.Quest;
        if(quest?.QuestClass==null)return;
        string name;
        if(Names.TryGetValue(quest,out name)){if(name.Length>0)value=name;return;}
        string poi=quest.GetPOIName();
        // POI data can arrive after the row first binds, so only a real name is cached.
        if(string.IsNullOrEmpty(poi))return;
        name=Localization.Get("xuiTier")+" "+quest.QuestClass.DifficultyTier+" - "+poi;
        Names.Add(quest,name);
        value=name;
    }
}
[Preserve]
public sealed class XUiC_RebirthScrollableQuestList : XUiC_QuestList
{
    private readonly RebirthMapRowDoubleClick doubleClick=new RebirthMapRowDoubleClick();
    private XUiC_RebirthCharacterOverviewList scroll;
    private XUiC_QuestEntry reference;
    private Quest selection;
    private System.Collections.Generic.List<Quest> previous;
    private bool rebind=true;
    // Idle entries only need their bindings refreshed a few times per second (each refresh boxes colours/bools).
    private float idleAccumulated=1f;
    public override void Init()
    {
        base.Init();
        RebirthHarmonyBootstrap.PatchClassOnce(new HarmonyLib.Harmony("rebirth.quest.names"),typeof(RebirthQuestEntryPoiName));
        // Native entries keep their quest actions; the clipped pool replaces page navigation.
        scroll=GetParentByType<XUiC_RebirthCharacterOverviewList>();
        reference=Parent.Parent.GetChildById("questSelectionReference") as XUiC_QuestEntry;
        reference.QuestUIHandler=(XUiC_QuestWindowGroup)WindowGroup.Controller;
        foreach(var entry in entryList){entry.OnScroll-=OnScrollQuest;entry.OnPress+=(s,b)=>{if((b!=0&&b!=-1)||RebirthConsoleInputGuardRuntime.BlocksGameplayInput())return;var e=s as XUiC_QuestEntry;if(e?.Quest==null)return;selection=e.Quest;reference.Quest=selection;bool isDouble=doubleClick.Press(selection,b);OnPressQuest(reference,b);rebind=true;if(isDouble&&QuestListWindow!=null){SelectedEntry=reference;QuestListWindow.showOnMapBtn_OnPress(s,b);}};}
        scroll.DataRangeChanged+=()=>rebind=true;
    }
    public override void Update(float dt)
    {
        if(scroll==null)return;
        if(previous!=questList){previous=questList;scroll.ResetPosition();rebind=true;}
        scroll.SetItemCount(questList.Count,"No quests.");
        if(!IsDirty&&!rebind){idleAccumulated+=dt;if(idleAccumulated<0.1f)return;foreach(var e in entryList)e.Update(idleAccumulated);idleAccumulated=0f;return;}
        idleAccumulated=0f;
        if(selection!=null&&!questList.Contains(selection))selection=null;
        if(selection==null&&questList.Count>0)selection=questList[0];
        reference.Quest=selection;SelectedEntry=selection==null?null:reference;
        ((XUiC_QuestWindowGroup)WindowGroup.Controller).SetQuest(SelectedEntry);
        for(int i=0;i<entryList.Count;i++){int n=scroll.FirstDataIndex+i;var e=entryList[i];e.Quest=n<questList.Count?questList[n]:null;e.Selected=e.Quest!=null&&e.Quest==selection;e.ViewComponent.IsVisible=e.Quest!=null;e.Update(dt);}
        IsDirty=false;rebind=false;
    }
}
