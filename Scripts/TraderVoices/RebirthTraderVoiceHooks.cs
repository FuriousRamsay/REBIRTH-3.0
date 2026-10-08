using Audio;
using HarmonyLib;
using UnityEngine;

[HarmonyPatch(typeof(Manager),nameof(Manager.PlayInsidePlayerHead),new[]{typeof(string),typeof(int),typeof(float),typeof(bool),typeof(bool)})]
internal static class RebirthTraderVoiceHeadPatch
{
    private static void Prefix(ref string soundGroupName, ref bool isUnique)
    {
        string original=soundGroupName;
        RebirthTraderVoices.Route(ref soundGroupName);
        if(soundGroupName!=original) isUnique=true;
    }
}
[HarmonyPatch(typeof(Manager),nameof(Manager.Play),new[]{typeof(Vector3),typeof(string),typeof(int),typeof(bool),typeof(float)})]
internal static class RebirthTraderVoicePositionPatch
{
    private static void Prefix(ref string soundGroupName) => RebirthTraderVoices.Route(ref soundGroupName);
}
[HarmonyPatch(typeof(Manager),nameof(Manager.Play),new[]{typeof(Entity),typeof(string),typeof(float),typeof(bool)})]
internal static class RebirthTraderVoiceEntityPatch
{
    private static void Prefix(Entity _entity,ref string soundGroupName)
    {
        if(_entity is EntityNPC npc && RebirthTraderVoices.Trader(npc)==null)return;
        RebirthTraderVoices.Route(ref soundGroupName);
    }
}

internal struct RebirthVoiceTradeState
{
    public XUi Ui;
    public EntityTrader Trader;
    public int Currency;
    public static RebirthVoiceTradeState Capture(BaseItemActionEntry entry)
    {
        var ui=entry?.ItemController?.xui;
        return new RebirthVoiceTradeState {Ui=ui,Trader=ui?.Trader?.Trader as EntityTrader,
            Currency=ui==null?0:RebirthSkillWaveABuyPatch.CurrencyCount(ui)};
    }
}
[HarmonyPatch(typeof(ItemActionEntryPurchase),nameof(ItemActionEntryPurchase.OnActivated))]
internal static class RebirthTraderVoiceBuyPatch
{
    private static void Prefix(ItemActionEntryPurchase __instance,out RebirthVoiceTradeState __state) => __state=RebirthVoiceTradeState.Capture(__instance);
    private static void Postfix(RebirthVoiceTradeState __state)
    {
        if(__state.Trader!=null && RebirthSkillWaveABuyPatch.CurrencyCount(__state.Ui)<__state.Currency)
            RebirthTraderVoices.Play(__state.Trader,"purchase");
    }
}
[HarmonyPatch(typeof(ItemActionEntrySell),nameof(ItemActionEntrySell.OnActivated))]
internal static class RebirthTraderVoiceSellPatch
{
    private static void Prefix(ItemActionEntrySell __instance,out RebirthVoiceTradeState __state) => __state=RebirthVoiceTradeState.Capture(__instance);
    private static void Postfix(RebirthVoiceTradeState __state)
    {
        if(__state.Trader!=null && RebirthSkillWaveABuyPatch.CurrencyCount(__state.Ui)>__state.Currency)
            RebirthTraderVoices.Play(__state.Trader,"sale");
    }
}
[HarmonyPatch(typeof(XUiC_TraderWindow),nameof(XUiC_TraderWindow.OnOpen))]
internal static class RebirthTraderVoiceBrowsePatch
{
    private static void Postfix(XUiC_TraderWindow __instance) => RebirthTraderVoices.Play(__instance.CurrentTraderEntity,"browse");
}

// The native accepted/complete voices precede AddQuest/reward confirmation. Defer custom voices
// to the actual journal transitions. Original behavior is left entirely intact for Original users.
[HarmonyPatch(typeof(EntityTrader),nameof(EntityTrader.PlayVoiceSetEntry))]
internal static class RebirthTraderVoicePrematurePatch
{
    private static bool Prefix(EntityTrader __instance,string name,EntityPlayer player)
    {
        if(name!="quest_accepted" && name!="quest_complete") return true;
        return !(player is EntityPlayerLocal) || RebirthTraderVoicePreferences.Get(RebirthTraderVoices.Trader(__instance))==0;
    }
}
[HarmonyPatch(typeof(QuestJournal),nameof(QuestJournal.AddQuest))]
internal static class RebirthTraderVoiceQuestAcceptPatch
{
    private static void Prefix(QuestJournal __instance,Quest q,out bool __state) => __state=q!=null&&__instance.FindActiveQuest(q.QuestCode)==null;
    private static void Postfix(QuestJournal __instance,Quest q,bool __state)
    {
        if(!__state || q==null || __instance.FindActiveQuest(q.QuestCode)!=q ||
            __instance.OwnerPlayer!=LocalPlayerUI.GetUIForPrimaryPlayer()?.entityPlayer) return;
        var npc=__instance.OwnerPlayer?.world?.GetEntity(q.QuestGiverID) as EntityTrader;
        RebirthTraderVoices.PlayConfirmed(npc,"job_accept","quest_accepted");
    }
}
[HarmonyPatch(typeof(QuestJournal),nameof(QuestJournal.CompleteQuest))]
internal static class RebirthTraderVoiceQuestCompletePatch
{
    private static void Prefix(Quest q,out bool __state) => __state=q!=null&&q.CurrentState!=Quest.QuestState.Completed;
    private static void Postfix(QuestJournal __instance,Quest q,bool __state)
    {
        if(!__state || q==null || q.CurrentState!=Quest.QuestState.Completed ||
            __instance.OwnerPlayer!=LocalPlayerUI.GetUIForPrimaryPlayer()?.entityPlayer) return;
        var npc=__instance.OwnerPlayer?.world?.GetEntity(q.QuestGiverID) as EntityTrader;
        RebirthTraderVoices.PlayConfirmed(npc,"job_complete","quest_complete");
    }
}
// Dialog OnClose also fires when entering trade. Farewells are routed from the
// native sale_accepted/sale_declined events when the trade window really closes.
[HarmonyPatch(typeof(XUiC_DialogWindowGroup),nameof(XUiC_DialogWindowGroup.OnOpen))]
internal static class RebirthTraderVoiceDialogPatch
{
    private static void Postfix(XUiC_DialogWindowGroup __instance)
    {
        var npc=__instance.xui.Dialog.Respondent as EntityTrader;
        if(npc==null)return;
        // Only an actually unfinished job from THIS trader merits the incomplete category.
        var journal=__instance.xui.playerUI.entityPlayer.QuestJournal;
        foreach(var quest in journal.quests)
            if(quest.QuestGiverID==npc.entityId && quest.CurrentState==Quest.QuestState.InProgress)
            { RebirthTraderVoices.Play(npc,"job_incomplete");return; }
    }
}
