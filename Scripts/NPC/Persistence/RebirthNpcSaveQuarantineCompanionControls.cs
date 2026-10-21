using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public static partial class RebirthCompanionBehaviorService
{
    private static World behaviorWorld;private static ulong behaviorGeneration;
    private static bool BehaviorCurrent(bool mutate)
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return false;
        lock(Sync)
        {
            if(behaviorWorld!=null&&!ReferenceEquals(behaviorWorld,world))return false;
            if(!mutate&&NpcStates.Count==0)return true;
            ulong generation;
            if(mutate)generation=RebirthNpcRecoveryGeneration.RequireCurrent();else if(!RebirthNpcRecoveryGeneration.TryReadObservedGeneration(world,out generation))return false;
            if(behaviorWorld!=null&&behaviorGeneration!=generation){if(NpcStates.Count!=0)return false;if(mutate)behaviorGeneration=generation;}
            if(behaviorWorld==null){if(NpcStates.Count!=0)return false;if(mutate){behaviorWorld=world;behaviorGeneration=generation;}}
            return true;
        }
    }
    internal static bool CanRetireEmptyBehavior{get{lock(Sync)return ThreadManager.IsMainThread()&&NpcStates.Count==0;}}
    internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids)
    {lock(Sync)foreach(var id in NpcStates.Keys)RebirthNpcSaveQuarantineIds.Add(ids,id);}
    internal static RebirthNpcSaveQuarantineDomain PrepareBehaviorQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||scope==null||(behaviorWorld!=null&&(!ReferenceEquals(behaviorWorld,scope.World)||(NpcStates.Count!=0&&behaviorGeneration!=scope.Generation)))||(behaviorWorld==null&&NpcStates.Count!=0))throw new InvalidOperationException("Original companion behavior binding unavailable");
            var records=NpcStates;var world=behaviorWorld;ulong generation=behaviorGeneration;var next=new Dictionary<RebirthNpcStableId,State>();var doc=BehaviorImage();
            return new RebirthNpcSaveQuarantineDomain("companion-behavior",doc,
                ()=>{lock(Sync)return ReferenceEquals(NpcStates,records)&&ReferenceEquals(behaviorWorld,world)&&behaviorGeneration==generation&&BehaviorImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){NpcStates=next;behaviorWorld=null;behaviorGeneration=0;}},
                ()=>{lock(Sync){NpcStates=records;behaviorWorld=world;behaviorGeneration=generation;}});
        }
    }
    private static XmlDocument BehaviorImage()
    {
        if(NpcStates.Count>8192)throw new InvalidOperationException("Companion behavior retention bound exceeded");var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("companionBehavior");doc.AppendChild(root);root.SetAttribute("generation",behaviorGeneration.ToString(CultureInfo.InvariantCulture));var ids=new List<RebirthNpcStableId>(NpcStates.Keys);ids.Sort((a,b)=>string.CompareOrdinal(a.ToString(),b.ToString()));
        foreach(var id in ids){var state=NpcStates[id];if(id.IsEmpty||state==null||(byte)state.Mode>1)throw new InvalidOperationException("Companion behavior record invalid");var row=doc.CreateElement("state");root.AppendChild(row);row.SetAttribute("stable",id.ToString());row.SetAttribute("mode",((byte)state.Mode).ToString(CultureInfo.InvariantCulture));row.SetAttribute("stopped",state.StopAttacking?"1":"0");}return doc;
    }
}

public static partial class RebirthCompanionInventoryLockService
{
    private static World itemLockWorld;private static ulong itemLockGeneration;
    private static bool LockScopeCurrent(bool mutate)
    {
        var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return false;
        lock(Sync)
        {
            if(itemLockWorld!=null&&!ReferenceEquals(itemLockWorld,world))return false;
            if(!mutate&&NpcLocks.Count==0)return true;
            ulong generation;
            if(mutate)generation=RebirthNpcRecoveryGeneration.RequireCurrent();else if(!RebirthNpcRecoveryGeneration.TryReadObservedGeneration(world,out generation))return false;
            if(itemLockWorld!=null&&itemLockGeneration!=generation){if(NpcLocks.Count!=0)return false;if(mutate)itemLockGeneration=generation;}
            if(itemLockWorld==null){if(NpcLocks.Count!=0)return false;if(mutate){itemLockWorld=world;itemLockGeneration=generation;}}
            return true;
        }
    }
    internal static bool CanRetireEmptyItemLocks{get{lock(Sync)return ThreadManager.IsMainThread()&&NpcLocks.Count==0;}}
    internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids)
    {
        lock(Sync)foreach(string key in NpcLocks)
        {int split=key?.IndexOf('|')??-1;if(split<=0||!RebirthNpcStableId.TryParse(key.Substring(0,split),out var id))throw new InvalidOperationException("Companion lock original identity unknown");RebirthNpcSaveQuarantineIds.Add(ids,id);}
    }
    internal static RebirthNpcSaveQuarantineDomain PrepareItemLocksQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||scope==null||(itemLockWorld!=null&&(!ReferenceEquals(itemLockWorld,scope.World)||(NpcLocks.Count!=0&&itemLockGeneration!=scope.Generation)))||(itemLockWorld==null&&NpcLocks.Count!=0))throw new InvalidOperationException("Original companion item lock scope unavailable");
            var records=NpcLocks;var world=itemLockWorld;ulong generation=itemLockGeneration;var next=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var doc=ItemLocksImage();
            return new RebirthNpcSaveQuarantineDomain("companion-item-locks",doc,
                ()=>{lock(Sync)return ReferenceEquals(NpcLocks,records)&&ReferenceEquals(itemLockWorld,world)&&itemLockGeneration==generation&&ItemLocksImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){NpcLocks=next;itemLockWorld=null;itemLockGeneration=0;}},
                ()=>{lock(Sync){NpcLocks=records;itemLockWorld=world;itemLockGeneration=generation;}});
        }
    }
    private static XmlDocument ItemLocksImage()
    {
        if(NpcLocks.Count>8192)throw new InvalidOperationException("Companion item lock retention bound exceeded");var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("companionItemLocks");doc.AppendChild(root);root.SetAttribute("generation",itemLockGeneration.ToString(CultureInfo.InvariantCulture));var keys=new List<string>(NpcLocks);keys.Sort(StringComparer.OrdinalIgnoreCase);long characters=0;
        foreach(string key in keys){characters+=key?.Length??0;if(string.IsNullOrWhiteSpace(key)||key.Length>65536||characters>4L*1024*1024)throw new InvalidOperationException("Companion item lock key invalid");var row=doc.CreateElement("lock");root.AppendChild(row);row.SetAttribute("key",key);}return doc;
    }
}
