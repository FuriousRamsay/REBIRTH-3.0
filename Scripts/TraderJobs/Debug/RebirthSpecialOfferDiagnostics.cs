#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using UnityEngine;

// Opt-in observations only. Finalizers return the original exception unchanged.
internal static class RebirthSpecialOfferDiagnostics
{
    internal sealed class Call { internal long Id; internal Quest Quest; }
    private static long sequence;
    internal static bool Special(Quest q) => q?.QuestClass != null && !string.IsNullOrEmpty(q.QuestClass.QuestType);
    internal static string Ref(object value) => value == null ? "null" : RuntimeHelpers.GetHashCode(value).ToString("X");
    internal static string Id(Quest q)
    {
        string id = q?.QuestClass?.ID ?? "null";
        var chars = id.Substring(0, Math.Min(80, id.Length)).ToCharArray();
        for (int i=0;i<chars.Length;i++) if (!char.IsLetterOrDigit(chars[i]) && chars[i]!='_' && chars[i]!='-') chars[i]='?';
        return new string(chars);
    }
    internal static void Observe(Action observation)
    {
        if (!RebirthTraderDebug.Enabled) return;
        try { observation(); }
        catch (Exception) { /* Only diagnostic collection failure; never the intercepted exception. */ }
    }
    internal static Call Begin(Quest quest, string stage, XUi ui = null)
{ if(!RebirthTraderDebug.Enabled)return null; return BeginEnabled(quest,stage,ui); }
internal static Call BeginEnabled(Quest quest, string stage, XUi ui = null)
{
        if (!RebirthTraderDebug.Enabled) return null;
        Call call = null;
        Observe(() => { if (!Special(quest)) return; call=new Call{Id=Interlocked.Increment(ref sequence),Quest=quest}; Emit(call,stage,"entry",ui,null); });
        return call;
    }
    internal static void Emit(Call call,string stage,string phase,XUi ui,Exception exception,string extra="")
{ if(!RebirthTraderDebug.Enabled||call==null)return; EmitEnabled(call,stage,phase,ui,exception,extra); }
internal static void EmitEnabled(Call call,string stage,string phase,XUi ui,Exception exception,string extra="")
{
        if (!RebirthTraderDebug.Enabled || call == null) return;
        Observe(() => {
            var journal=ui?.playerUI?.entityPlayer?.QuestJournal;
            var tracked=journal?.TrackedQuest;
            var uiTracked=ui?.QuestTracker?.TrackedQuest;
            var group=ui?.FindWindowGroupByName("questOffer");
            var offer=group?.GetChildByType<XUiC_QuestOfferWindow>();
            var pointer=UICamera.hoveredObject?.transform;
            var focus=ui?.playerUI?.CursorController?.navigationTarget;
            var a=offer?.btnAccept_Background;var d=offer?.btnDecline_Background;
            bool Under(Transform target)=>pointer!=null&&target!=null&&(pointer==target||pointer.IsChildOf(target));
            RebirthTraderDebug.Trace("SPECIAL_TRACE call="+call.Id+" stage="+stage+" phase="+phase+
                " quest="+Id(call.Quest)+" qref="+Ref(call.Quest)+" state="+call.Quest.CurrentState+
                " tracked="+call.Quest.Tracked+" journalContains="+(journal?.quests?.Contains(call.Quest)??false)+" savedPoi="+(call.Quest.PositionData?.ContainsKey(Quest.PositionDataTypes.POIPosition)??false)+" journal="+Ref(journal)+" journalTracked="+Id(tracked)+" trackedRef="+Ref(tracked)+" uiTracked="+Id(uiTracked)+" uiTrackedRef="+Ref(uiTracked)+
                " offer="+Ref(offer)+" offerQuest="+Ref(offer?.Quest)+" accepted="+(offer?.questAccepted??false)+
                " dialogOpen="+(ui?.playerUI?.windowManager?.IsWindowOpen("dialog")??false)+
                " offerOpen="+(ui?.playerUI?.windowManager?.IsWindowOpen("questOffer")??false)+
                " acceptEnabled="+(a?.Enabled??false)+" acceptActive="+(a?.IsActiveInHierarchy??false)+
                " declineEnabled="+(d?.Enabled??false)+" declineActive="+(d?.IsActiveInHierarchy??false)+
                " hoverAccept="+Under(a?.UiTransform)+" hoverDecline="+Under(d?.UiTransform)+
                " focusAccept="+ReferenceEquals(focus,a)+" focusDecline="+ReferenceEquals(focus,d)+
                " exception="+(exception==null?"none":exception.GetType().Name)+extra);
        });
    }
    internal static MethodBase Required(Type type,string name,params Type[] args)
        => AccessTools.Method(type,name,args) ?? throw new MissingMethodException(type?.FullName,name);
    internal static void Install(Harmony harmony)
    {
        foreach(var type in new[]{typeof(RebirthSpecialOfferPreviewTracePatch),typeof(RebirthSpecialOfferLifecycleTracePatch),typeof(RebirthSpecialOfferInputTracePatch),typeof(RebirthSpecialOfferAddTracePatch),typeof(RebirthSpecialOfferTrackingTracePatch)})
        {
            try { RebirthHarmonyBootstrap.PatchClassOnce(harmony,type); }
            catch(Exception ex) { try { Log.Warning("[REBIRTH TraderDebug] special trace unavailable patch="+type.Name+" exception="+ex.GetType().Name); } catch(Exception) { /* Diagnostic installer logging cannot stop ordinary installation. */ } }
        }
    }
    internal static string Stage(MethodBase method)=>method?.Name??"unknown";
    internal static Call BeginJournal(QuestJournal journal,Quest quest,string stage)
{ if(!RebirthTraderDebug.Enabled)return null;return BeginJournalEnabled(journal,quest,stage); }
internal static Call BeginJournalEnabled(QuestJournal journal,Quest quest,string stage)
{
        if(!RebirthTraderDebug.Enabled)return null;
        Call call=null;Observe(()=>call=Begin(quest,stage,journal?.OwnerPlayer?.PlayerUI?.xui));return call;
    }
    internal static void EndJournal(QuestJournal journal,Call call,string stage,string phase,Exception exception)
{ if(!RebirthTraderDebug.Enabled||call==null)return;EndJournalEnabled(journal,call,stage,phase,exception); }
internal static void EndJournalEnabled(QuestJournal journal,Call call,string stage,string phase,Exception exception)
{
        if(!RebirthTraderDebug.Enabled||call==null)return;
        Observe(()=>Emit(call,stage,phase,journal?.OwnerPlayer?.PlayerUI?.xui,exception));
    }
}
[HarmonyPatch]
internal static class RebirthSpecialOfferPreviewTracePatch
{
    static MethodBase TargetMethod()=>RebirthSpecialOfferDiagnostics.Required(AccessTools.TypeByName("RebirthTraderSpecialJobPreviewAcceptance"),"TryOpen",typeof(DialogResponseQuest),typeof(EntityPlayerLocal),typeof(LocalPlayerUI),typeof(EntityTrader),typeof(Func<bool>),typeof(Action<DialogStatement>));
    static void Prefix(DialogResponseQuest response,LocalPlayerUI ui,out RebirthSpecialOfferDiagnostics.Call __state)
{ __state=null;if(!RebirthTraderDebug.Enabled)return;PreviewPrefixEnabled(response,ui,out __state); }
static void PreviewPrefixEnabled(DialogResponseQuest response,LocalPlayerUI ui,out RebirthSpecialOfferDiagnostics.Call __state)
{
        __state=null;if(!RebirthTraderDebug.Enabled)return;
        RebirthSpecialOfferDiagnostics.Call state=null;
        RebirthSpecialOfferDiagnostics.Observe(()=>state=RebirthSpecialOfferDiagnostics.Begin(response?.Quest,"preview.open",ui?.xui));__state=state;
    }
    static void Postfix(LocalPlayerUI ui,bool __result,RebirthSpecialOfferDiagnostics.Call __state)
{ if(!RebirthTraderDebug.Enabled||__state==null)return;PreviewPostfixEnabled(ui,__result,__state); }
static void PreviewPostfixEnabled(LocalPlayerUI ui,bool __result,RebirthSpecialOfferDiagnostics.Call __state)
{if(!RebirthTraderDebug.Enabled||__state==null)return;RebirthSpecialOfferDiagnostics.Observe(()=>RebirthSpecialOfferDiagnostics.Emit(__state,"preview.open","exit",ui?.xui,null," handled="+__result));}
    static Exception Finalizer(LocalPlayerUI ui,Exception __exception,RebirthSpecialOfferDiagnostics.Call __state)
{ if(!RebirthTraderDebug.Enabled||__state==null||__exception==null)return __exception;return PreviewFinalizerEnabled(ui,__exception,__state); }
static Exception PreviewFinalizerEnabled(LocalPlayerUI ui,Exception __exception,RebirthSpecialOfferDiagnostics.Call __state)
{if(!RebirthTraderDebug.Enabled||__state==null||__exception==null)return __exception;RebirthSpecialOfferDiagnostics.Observe(()=>RebirthSpecialOfferDiagnostics.Emit(__state,"preview.open","exception",ui?.xui,__exception));return __exception;}
}

[HarmonyPatch]
internal static class RebirthSpecialOfferLifecycleTracePatch
{
    static IEnumerable<MethodBase> TargetMethods()
    {
        yield return RebirthSpecialOfferDiagnostics.Required(typeof(XUiC_QuestOfferWindow),"OnOpen",Type.EmptyTypes);
        yield return RebirthSpecialOfferDiagnostics.Required(typeof(XUiC_QuestOfferWindow),"OnClose",Type.EmptyTypes);
        yield return RebirthSpecialOfferDiagnostics.Required(typeof(XUiC_QuestOfferWindow),"btnAccept_OnPress",typeof(XUiController),typeof(int));
        yield return RebirthSpecialOfferDiagnostics.Required(typeof(XUiC_QuestOfferWindow),"btnDecline_OnPress",typeof(XUiController),typeof(int));
    }
    [HarmonyPriority(Priority.First)]
    static void Prefix(XUiC_QuestOfferWindow __instance,MethodBase __originalMethod,out RebirthSpecialOfferDiagnostics.Call __state)
{ __state=null;if(!RebirthTraderDebug.Enabled)return;LifecyclePrefixEnabled(__instance,__originalMethod,out __state); }
static void LifecyclePrefixEnabled(XUiC_QuestOfferWindow __instance,MethodBase __originalMethod,out RebirthSpecialOfferDiagnostics.Call __state)
{__state=null;if(!RebirthTraderDebug.Enabled)return;RebirthSpecialOfferDiagnostics.Call state=null;RebirthSpecialOfferDiagnostics.Observe(()=>state=RebirthSpecialOfferDiagnostics.Begin(__instance?.Quest,RebirthSpecialOfferDiagnostics.Stage(__originalMethod),__instance?.xui));__state=state;}
    static void Postfix(XUiC_QuestOfferWindow __instance,MethodBase __originalMethod,bool __runOriginal,RebirthSpecialOfferDiagnostics.Call __state)
{ if(!RebirthTraderDebug.Enabled||__state==null)return;LifecyclePostfixEnabled(__instance,__originalMethod,__runOriginal,__state); }
static void LifecyclePostfixEnabled(XUiC_QuestOfferWindow __instance,MethodBase __originalMethod,bool __runOriginal,RebirthSpecialOfferDiagnostics.Call __state)
{if(!RebirthTraderDebug.Enabled||__state==null)return;RebirthSpecialOfferDiagnostics.Observe(()=>RebirthSpecialOfferDiagnostics.Emit(__state,RebirthSpecialOfferDiagnostics.Stage(__originalMethod),"exit",__instance?.xui,null," ranOriginal="+__runOriginal));}
    static Exception Finalizer(XUiC_QuestOfferWindow __instance,MethodBase __originalMethod,Exception __exception,RebirthSpecialOfferDiagnostics.Call __state)
{ if(!RebirthTraderDebug.Enabled||__state==null||__exception==null)return __exception;return LifecycleFinalizerEnabled(__instance,__originalMethod,__exception,__state); }
static Exception LifecycleFinalizerEnabled(XUiC_QuestOfferWindow __instance,MethodBase __originalMethod,Exception __exception,RebirthSpecialOfferDiagnostics.Call __state)
{if(!RebirthTraderDebug.Enabled||__state==null||__exception==null)return __exception;RebirthSpecialOfferDiagnostics.Observe(()=>RebirthSpecialOfferDiagnostics.Emit(__state,RebirthSpecialOfferDiagnostics.Stage(__originalMethod),"exception",__instance?.xui,__exception));return __exception;}
}

[HarmonyPatch(typeof(XUiView),"OnClick",new Type[]{typeof(GameObject)})]
internal static class RebirthSpecialOfferInputTracePatch
{
    static void Prefix(XUiView __instance)
{ if(!RebirthTraderDebug.Enabled)return;InputPrefixEnabled(__instance); }
static void InputPrefixEnabled(XUiView __instance)
{
        if(!RebirthTraderDebug.Enabled)return;
        RebirthSpecialOfferDiagnostics.Observe(()=>{
            var ui=__instance?.xui;var offer=ui?.FindWindowGroupByName("questOffer")?.GetChildByType<XUiC_QuestOfferWindow>();
            if(offer==null||(!ReferenceEquals(__instance,offer.btnAccept_Background)&&!ReferenceEquals(__instance,offer.btnDecline_Background)))return;
            RebirthSpecialOfferDiagnostics.Begin(offer.Quest,ReferenceEquals(__instance,offer.btnAccept_Background)?"input.accept":"input.decline",ui);
        });
    }
}

[HarmonyPatch(typeof(QuestJournal),"AddQuest",new Type[]{typeof(Quest),typeof(Quest.QuestSource),typeof(bool)})]
internal static class RebirthSpecialOfferAddTracePatch
{
    static void Prefix(QuestJournal __instance,Quest q,out RebirthSpecialOfferDiagnostics.Call __state)
    {__state=null;if(!RebirthTraderDebug.Enabled)return;__state=RebirthSpecialOfferDiagnostics.BeginJournal(__instance,q,"AddQuest");}
    static void Postfix(QuestJournal __instance,RebirthSpecialOfferDiagnostics.Call __state)
    {if(!RebirthTraderDebug.Enabled||__state==null)return;RebirthSpecialOfferDiagnostics.EndJournal(__instance,__state,"AddQuest","exit",null);}
    static Exception Finalizer(QuestJournal __instance,Exception __exception,RebirthSpecialOfferDiagnostics.Call __state)
    {if(!RebirthTraderDebug.Enabled||__state==null||__exception==null)return __exception;RebirthSpecialOfferDiagnostics.EndJournal(__instance,__state,"AddQuest","exception",__exception);return __exception;}
}

[HarmonyPatch(typeof(QuestJournal),"TrackedQuest",MethodType.Setter)]
internal static class RebirthSpecialOfferTrackingTracePatch
{
    static void Prefix(QuestJournal __instance,Quest value,out RebirthSpecialOfferDiagnostics.Call __state)
{ __state=null;if(!RebirthTraderDebug.Enabled)return;TrackingPrefixEnabled(__instance,value,out __state); }
static void TrackingPrefixEnabled(QuestJournal __instance,Quest value,out RebirthSpecialOfferDiagnostics.Call __state)
{
        __state=null;if(!RebirthTraderDebug.Enabled)return;RebirthSpecialOfferDiagnostics.Call state=null;
        RebirthSpecialOfferDiagnostics.Observe(()=>{
            state=RebirthSpecialOfferDiagnostics.BeginJournal(__instance,value??__instance?.TrackedQuest,"set_TrackedQuest");
            RebirthSpecialOfferDiagnostics.Emit(state,"set_TrackedQuest","request",__instance?.OwnerPlayer?.PlayerUI?.xui,null," requestedRef="+RebirthSpecialOfferDiagnostics.Ref(value));
        });__state=state;
    }
    static void Postfix(QuestJournal __instance,RebirthSpecialOfferDiagnostics.Call __state)
    {if(!RebirthTraderDebug.Enabled||__state==null)return;RebirthSpecialOfferDiagnostics.EndJournal(__instance,__state,"set_TrackedQuest","exit",null);}
    static Exception Finalizer(QuestJournal __instance,Exception __exception,RebirthSpecialOfferDiagnostics.Call __state)
    {if(!RebirthTraderDebug.Enabled||__state==null||__exception==null)return __exception;RebirthSpecialOfferDiagnostics.EndJournal(__instance,__state,"set_TrackedQuest","exception",__exception);return __exception;}
}
#endif