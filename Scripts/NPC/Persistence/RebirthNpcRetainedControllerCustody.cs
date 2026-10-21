using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using UnityEngine;

// Retention of actual controller/native path owners. No path serialization,
// cancellation, executor End, actor setter or implicit restart occurs here.
internal sealed class RebirthNpcRetainedControllerCustody
{
    internal sealed class Owner
    {
        internal RebirthNpcControllerOriginalScope Original;
        internal ulong Lease,Activity;
        internal object NativeNavigator;
        internal bool SessionTerminal,PathStopped;
        internal bool Terminal=>SessionTerminal&&(NativeNavigator==null||PathStopped);
    }
    internal sealed class Member
    {
        internal readonly RebirthNpcSaveQuarantineDomain Domain;
        internal readonly Owner[] Owners;
        internal readonly Func<bool> Current,Detached;
        internal Member(RebirthNpcSaveQuarantineDomain domain,Owner[] owners,Func<bool> current,Func<bool> detached)
        {Domain=domain;Owners=owners;Current=current;Detached=detached;}
    }
    private static readonly List<RebirthNpcRetainedControllerCustody> retained=new List<RebirthNpcRetainedControllerCustody>();
    private readonly RebirthNpcSaveQuarantineScope scope;
    private readonly Member[] members;
    private bool registered;
    private RebirthNpcRuntimeNativeAdoption.NativeReentry incomingSource;
    private RebirthDogLifecycleService.NativeOwnerReentry incomingOwner;
    private bool HasNativeOwners{get{foreach(var member in members)if(member.Owners.Length!=0)return true;return false;}}
    internal bool ObserveOwnerScope(RebirthDogLifecycleService.NativeOwnerReentry source)
    {
        if(!registered||HasNativeOwners||source?.Current()!=true||ReferenceEquals(source.World,scope.World)||
            !RebirthNpcSaveQuarantineScope.SameDirectory(scope.Directory,source.Directory))return false;
        if(incomingSource!=null&&!ReferenceEquals(incomingSource.World,source.World)||incomingOwner!=null&&!ReferenceEquals(incomingOwner.World,source.World))return false;
        incomingOwner=source;return true;
    }
    private RebirthNpcRetainedControllerCustody(RebirthNpcSaveQuarantineScope scope,Member[] members){this.scope=scope;this.members=members;}
    internal bool IsOriginalCurrent
    {get{if(!ThreadManager.IsMainThread()||registered)return false;foreach(var item in members)if(!item.Current())return false;return true;}}
    internal bool CanRegister=>IsOriginalCurrent&&retained.Count<8;
    internal bool ObserveIncoming(RebirthNpcRuntimeNativeAdoption.NativeReentry source)
    {
        if(!registered||source?.Current()!=true||ReferenceEquals(source.World,scope.World)||
            !RebirthNpcSaveQuarantineScope.SameDirectory(source.Directory,scope.Directory))return false;
        if(incomingSource!=null&&!ReferenceEquals(incomingSource.World,source.World))return false;
        incomingSource=source;return true;
    }
    internal bool IncomingSafe(World incoming,string directory)
    {
        bool native=incomingSource?.Current()==true&&ReferenceEquals(incomingSource.World,incoming)||
            !HasNativeOwners&&incomingOwner?.Current()==true&&ReferenceEquals(incomingOwner.World,incoming);
        if(!ThreadManager.IsMainThread()||!registered||!retained.Contains(this)||!native||
            !ReferenceEquals(GameManager.Instance?.World,incoming)||incoming?.IsRemote()!=false||
            SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||!RebirthNpcSaveQuarantineScope.SameDirectory(directory,scope.Directory)||
            !RebirthNpcSaveQuarantineScope.SameDirectory(directory,GameIO.GetSaveGameDir()))return false;
        foreach(var member in members)if(!member.Detached())return false;
        return true;
    }
    internal bool RegisterDetached()
    {
        if(!ThreadManager.IsMainThread())return false;if(registered)return retained.Contains(this);
        if(retained.Count>=8)return false;foreach(var item in members)if(!item.Detached())return false;
        retained.Add(this);registered=true;return true;
    }
    internal bool AbortAfterRollback()
    {
        if(!ThreadManager.IsMainThread())return false;foreach(var item in members)if(!item.Current())return false;
        if(registered&&!retained.Remove(this))return false;registered=false;incomingSource=null;incomingOwner=null;return true;
    }
    private bool Relevant=>registered&&!ReferenceEquals(scope.World,GameManager.Instance?.World)&&
        RebirthNpcSaveQuarantineScope.SameDirectory(scope.Directory,GameIO.GetSaveGameDir());
    internal static bool BlocksOwner(RebirthNpcStableId owner)
    {
        if(owner.IsEmpty)return false;
        foreach(var group in retained)if(group.Relevant)foreach(var member in group.members)foreach(var source in member.Owners)
            if(!source.Terminal&&source.Original.StableId==owner)return true;
        return false;
    }
    internal static bool BlocksLease(ulong lease)
    {foreach(var group in retained)if(group.Relevant)foreach(var member in group.members)foreach(var source in member.Owners)if(!source.Terminal&&source.Lease==lease)return true;return false;}
    internal static bool BlocksActivity(ulong activity)
    {foreach(var group in retained)if(group.Relevant)foreach(var member in group.members)foreach(var source in member.Owners)if(!source.Terminal&&source.Activity==activity)return true;return false;}
    internal static void ObserveTerminal(RebirthNpcControllerOriginalScope original,ulong lease,ulong activity)
    {
        if(!ThreadManager.IsMainThread()||original==null||!original.Current())return;
        foreach(var group in retained)if(ReferenceEquals(group.scope.World,original.World))foreach(var member in group.members)foreach(var source in member.Owners)
            if(ReferenceEquals(source.Original,original)&&source.Lease==lease&&source.Activity==activity)source.SessionTerminal=true;
    }
    internal static void ObserveNativePathStopped(EntityRebirthNPC actor,object navigator)
    {
        if(!ThreadManager.IsMainThread()||actor==null||navigator==null)return;
        foreach(var group in retained)if(ReferenceEquals(group.scope.World,GameManager.Instance?.World))foreach(var member in group.members)foreach(var source in member.Owners)
            if(ReferenceEquals(source.Original.Actor,actor)&&ReferenceEquals(source.NativeNavigator,navigator)&&source.Original.Current())
            {source.PathStopped=true;if(source.Lease==0&&source.Activity==0)source.SessionTerminal=true;}
    }
    internal static bool TryCapture(RebirthNpcSaveQuarantineScope scope,out RebirthNpcRetainedControllerCustody receipt)
    {
        receipt=null;if(scope==null||!ThreadManager.IsMainThread()||!RebirthNpcWorkCallbackObservation.IsIdle)return false;
        try
        {
            var members=new[]{RebirthNpcFollowRuntime.CaptureControllerCustody(scope),RebirthNpcTravelRuntime.CaptureControllerCustody(scope),
                RebirthNpcGuardNavigationRuntime.CaptureControllerCustody(scope),RebirthNpcPatrolRuntime.CaptureControllerCustody(scope),
                RebirthNpcStationaryActivityRuntime.CaptureControllerCustody(scope),RebirthNpcMissionRuntime.CaptureControllerCustody(scope),
                RebirthNpcWorkRuntime.CaptureControllerCustody(scope),RebirthNpcNavigationRuntimeAdapter.CaptureControllerCustody(scope)};
            foreach(var item in members)if(item==null||!item.Current())return false;
            receipt=new RebirthNpcRetainedControllerCustody(scope,members);return true;
        }
        catch{return false;}
    }
    internal static Member Capture<T>(string name,RebirthNpcSaveQuarantineScope scope,World world,ulong generation,long timer,
        Dictionary<int,T> records,Func<T,RebirthNpcControllerOriginalScope> source,Func<T,ulong> lease,Func<T,ulong> activity,
        Func<T,string> fields,Func<T,object[]> handles,Func<bool> current,Func<bool> detached,Action detach,Action rollback,Owner[] extra=null,string extraImage=null)
    {
        if(scope==null||!ThreadManager.IsMainThread()||records.Count>512||world!=null&&!ReferenceEquals(world,scope.World))throw new InvalidOperationException("Original controller custody is unavailable");
        var observed=new Dictionary<int,T>(records);var snapshots=new Dictionary<int,string>();var pointers=new Dictionary<int,object[]>();
        var owners=new List<Owner>();var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("retainedNativeController");doc.AppendChild(root);
        root.SetAttribute("generation",generation.ToString(CultureInfo.InvariantCulture));root.SetAttribute("nextTick",timer.ToString(CultureInfo.InvariantCulture));
        root.SetAttribute("retainedNative","1");
        if(extraImage!=null){var pending=doc.CreateElement("retainedEndingFields");pending.InnerText=extraImage;root.AppendChild(pending);}
        if(extra!=null)foreach(var owner in extra)
        {
            if(owner?.Original==null||owner.Original.StableId.IsEmpty||!ReferenceEquals(owner.Original.World,scope.World))throw new InvalidOperationException("Retained controller ending lineage unavailable");
            owners.Add(owner);
        }
        var ids=new List<int>(records.Keys);ids.Sort();
        foreach(int id in ids)
        {
            var value=records[id];var original=source(value);
            if(original==null||original.Actor==null||original.Runtime==null||original.StableId.IsEmpty||!ReferenceEquals(original.World,scope.World)||
                !ReferenceEquals(original.Actor.world,scope.World)||!ReferenceEquals(original.Actor.RebirthRuntimeState,original.Runtime)||original.Runtime.StableId!=original.StableId||
                original.Generation!=scope.Generation||original.Actor.entityId!=id)throw new InvalidOperationException("Original controller native lineage unresolved");
            string image=fields(value);snapshots.Add(id,image);pointers.Add(id,handles(value));
            owners.Add(new Owner{Original=original,Lease=lease(value),Activity=activity(value),NativeNavigator=original.Actor.getNavigator()});
            var row=doc.CreateElement("session");root.AppendChild(row);row.SetAttribute("entity",id.ToString(CultureInfo.InvariantCulture));
            row.SetAttribute("stable",original.StableId.ToString());row.SetAttribute("lease",lease(value).ToString(CultureInfo.InvariantCulture));
            row.SetAttribute("activity",activity(value).ToString(CultureInfo.InvariantCulture));row.InnerText=image;
        }
        bool Current()
        {
            if(!current()||!RebirthNpcWorkCallbackObservation.IsIdle||records.Count!=observed.Count)return false;
            foreach(var pair in observed)
            {
                if(!records.TryGetValue(pair.Key,out var value)||!ReferenceEquals(source(value),source(pair.Value))||fields(value)!=snapshots[pair.Key])return false;
                var now=handles(value);var prior=pointers[pair.Key];if(now.Length!=prior.Length)return false;
                for(int i=0;i<now.Length;i++)if(!ReferenceEquals(now[i],prior[i]))return false;
            }
            return true;
        }
        var domain=new RebirthNpcSaveQuarantineDomain(name,doc,Current,detach,rollback);
        return new Member(domain,owners.ToArray(),Current,detached);
    }
    internal static string Fields(params object[] values)
    {
        var result=new System.Text.StringBuilder();foreach(var value in values)
        {
            string text=value==null?null:value is Vector3 vector?vector.x.ToString("R",CultureInfo.InvariantCulture)+","+vector.y.ToString("R",CultureInfo.InvariantCulture)+","+vector.z.ToString("R",CultureInfo.InvariantCulture):
                value is float number?number.ToString("R",CultureInfo.InvariantCulture):Convert.ToString(value,CultureInfo.InvariantCulture);
            result.Append(text==null?-1:text.Length).Append(':').Append(text).Append(';');
        }
        return result.ToString();
    }
}
