using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public static partial class RebirthNpcDecisionEngine
{
    private static World decisionWorld;
    private static bool DecisionScope()
    {
        var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return false;
        lock(Sync){if(decisionWorld!=null&&!ReferenceEquals(decisionWorld,world))return false;if(decisionWorld==null)decisionWorld=world;return true;}
    }
    internal static bool CanRetireEmptyDecision{get{lock(Sync)return ThreadManager.IsMainThread()&&States.Count==0;}}
    internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids){lock(Sync)foreach(var id in States.Keys)RebirthNpcSaveQuarantineIds.Add(ids,id);}
    internal static RebirthNpcSaveQuarantineDomain PrepareDecisionQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||scope==null||(decisionWorld!=null&&!ReferenceEquals(decisionWorld,scope.World))||(decisionWorld==null&&States.Count!=0))throw new InvalidOperationException("Original decision binding unavailable");
            var states=States;var world=decisionWorld;var snapshot=npcSnapshot;int revision=npcSnapshotRevision,cursor=decisionCursor;var next=new Dictionary<RebirthNpcStableId,Runtime>();var empty=Array.Empty<RebirthNpcStableId>();var doc=DecisionImage();
            return new RebirthNpcSaveQuarantineDomain("decision-runtime",doc,
                ()=>{lock(Sync)return ReferenceEquals(States,states)&&ReferenceEquals(decisionWorld,world)&&ReferenceEquals(npcSnapshot,snapshot)&&npcSnapshotRevision==revision&&decisionCursor==cursor&&DecisionImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){States=next;decisionWorld=null;npcSnapshot=empty;npcSnapshotRevision=-1;decisionCursor=0;}},
                ()=>{lock(Sync){States=states;decisionWorld=world;npcSnapshot=snapshot;npcSnapshotRevision=revision;decisionCursor=cursor;}});
        }
    }
    private static XmlDocument DecisionImage()
    {
        if(States.Count>8192||npcSnapshot.Length>8192)throw new InvalidOperationException("Decision retention bound exceeded");var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("decision");doc.AppendChild(root);root.SetAttribute("revision",npcSnapshotRevision.ToString(CultureInfo.InvariantCulture));root.SetAttribute("cursor",decisionCursor.ToString(CultureInfo.InvariantCulture));
        var ids=new List<RebirthNpcStableId>(States.Keys);ids.Sort((a,b)=>string.CompareOrdinal(a.ToString(),b.ToString()));long characters=0;
        foreach(var id in ids)
        {
            var state=States[id];if(state==null||state.Active!=null)throw new InvalidOperationException("Opaque active decision task requires its own typed custody adapter; no Cancel performed");
            if(float.IsNaN(state.LastScore)||float.IsInfinity(state.LastScore)||state.RecentGoals.Count>16)throw new InvalidOperationException("Decision scalar/history unavailable");
            var row=doc.CreateElement("npc");root.AppendChild(row);row.SetAttribute("id",id.ToString());Text(row,"goal",state.GoalId,ref characters);row.SetAttribute("score",state.LastScore.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("next",state.NextEvaluation.ToString(CultureInfo.InvariantCulture));row.SetAttribute("cooldown",state.CooldownUntil.ToString(CultureInfo.InvariantCulture));foreach(string goal in state.RecentGoals){var e=doc.CreateElement("recent");row.AppendChild(e);Text(e,"goal",goal,ref characters);}
        }
        foreach(var id in npcSnapshot){var row=doc.CreateElement("snapshot");root.AppendChild(row);row.SetAttribute("id",id.ToString());}return doc;
    }
    private static void Text(XmlElement row,string name,string value,ref long characters)
    {characters+=value?.Length??0;if((value?.Length??0)>65536||characters>4L*1024*1024)throw new InvalidOperationException("Decision text retention bound exceeded");row.SetAttribute(name+"Null",value==null?"1":"0");row.SetAttribute(name,value??string.Empty);}
}

public static partial class RebirthNpcDecisionSignalStore
{
    private static World signalWorld;
    private static bool SignalScope()
    {
        var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return false;
        lock(Sync){if(signalWorld!=null&&!ReferenceEquals(signalWorld,world))return false;if(signalWorld==null)signalWorld=world;return true;}
    }
    internal static bool CanRetireEmptySignals{get{lock(Sync)return ThreadManager.IsMainThread()&&Signals.Count==0;}}
    internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids){lock(Sync)foreach(var id in Signals.Keys)RebirthNpcSaveQuarantineIds.Add(ids,id);}
    internal static RebirthNpcSaveQuarantineDomain PrepareSignalsQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||scope==null||(signalWorld!=null&&!ReferenceEquals(signalWorld,scope.World))||(signalWorld==null&&Signals.Count!=0))throw new InvalidOperationException("Original signal binding unavailable");
            var signals=Signals;var world=signalWorld;var snapshot=KnownSnapshot;int membership=membershipRevision,revision=snapshotRevision;var next=new Dictionary<RebirthNpcStableId,Dictionary<string,float>>();var empty=Array.Empty<RebirthNpcStableId>();var doc=SignalsImage();
            return new RebirthNpcSaveQuarantineDomain("decision-signals",doc,
                ()=>{lock(Sync)return ReferenceEquals(Signals,signals)&&ReferenceEquals(signalWorld,world)&&ReferenceEquals(KnownSnapshot,snapshot)&&membershipRevision==membership&&snapshotRevision==revision&&SignalsImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){Signals=next;signalWorld=null;KnownSnapshot=empty;membershipRevision=0;snapshotRevision=-1;}},
                ()=>{lock(Sync){Signals=signals;signalWorld=world;KnownSnapshot=snapshot;membershipRevision=membership;snapshotRevision=revision;}});
        }
    }
    private static XmlDocument SignalsImage()
    {
        if(Signals.Count>8192||KnownSnapshot.Length>8192)throw new InvalidOperationException("Signal retention bound exceeded");var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("signals");doc.AppendChild(root);root.SetAttribute("membership",membershipRevision.ToString(CultureInfo.InvariantCulture));root.SetAttribute("revision",snapshotRevision.ToString(CultureInfo.InvariantCulture));long count=0,characters=0;
        var ids=new List<RebirthNpcStableId>(Signals.Keys);ids.Sort((a,b)=>string.CompareOrdinal(a.ToString(),b.ToString()));foreach(var id in ids)
        {
            var map=Signals[id];if(map==null||map.Count>4096)throw new InvalidOperationException("Signal row unavailable");count+=map.Count;if(count>65536)throw new InvalidOperationException("Signal whole retention bound exceeded");var row=doc.CreateElement("npc");root.AppendChild(row);row.SetAttribute("id",id.ToString());var keys=new List<string>(map.Keys);keys.Sort(StringComparer.OrdinalIgnoreCase);foreach(string key in keys){float value=map[key];characters+=key?.Length??0;if(key==null||key.Length>65536||characters>4L*1024*1024||float.IsNaN(value)||float.IsInfinity(value))throw new InvalidOperationException("Signal key/value unavailable");var e=doc.CreateElement("signal");row.AppendChild(e);e.SetAttribute("key",key);e.SetAttribute("value",value.ToString("R",CultureInfo.InvariantCulture));}
        }
        foreach(var id in KnownSnapshot){var e=doc.CreateElement("snapshot");root.AppendChild(e);e.SetAttribute("id",id.ToString());}return doc;
    }
}
