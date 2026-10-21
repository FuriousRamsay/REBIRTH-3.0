using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

// Actual private claims, native state and callback graphs remain in their old
// World. This token permits fenced coexistence, never replay or cancellation.
internal sealed class RebirthNpcRetainedWorkPackages
{
    internal sealed class Keys
    {
        internal readonly HashSet<RebirthNpcStableId> Owners=new HashSet<RebirthNpcStableId>();
        internal readonly HashSet<ulong> Assignments=new HashSet<ulong>(),Leases=new HashSet<ulong>(),Activities=new HashSet<ulong>();
        internal readonly HashSet<string> Targets=new HashSet<string>(StringComparer.OrdinalIgnoreCase),Stations=new HashSet<string>(StringComparer.Ordinal);
        internal readonly List<object> Executors=new List<object>();
        internal void Assignment(RebirthNpcSaveQuarantineScope scope,ulong id)
        {
            if(!RebirthNpcWorkAssignmentService.TryCaptureRetainedAssignmentOwner(scope,id,out var owner))throw new InvalidOperationException("Retained Work package assignment owner is unknown");
            Assignments.Add(id);Owners.Add(owner);
        }
        internal void Actor(World world,int entity)
        {
            var actor=world?.GetEntity(entity) as EntityRebirthNPC;
            if(actor?.RebirthRuntimeState==null||actor.RebirthRuntimeState.StableId.IsEmpty||!actor.NativeIdentityReadable)throw new InvalidOperationException("Retained Work native actor is unknown");
            Owners.Add(actor.RebirthRuntimeState.StableId);
        }
    }
    internal sealed class Member
    {
        internal readonly RebirthNpcSaveQuarantineDomain Domain;internal readonly Keys Keys;
        internal readonly Func<bool> Current,Detached;
        internal readonly bool Empty;
        internal Member(RebirthNpcSaveQuarantineDomain domain,Keys keys,Func<bool> current,Func<bool> detached,bool empty)
        {Domain=domain;Keys=keys;Current=current;Detached=detached;Empty=empty;}
    }
    private static readonly List<RebirthNpcRetainedWorkPackages> retained=new List<RebirthNpcRetainedWorkPackages>();
    private readonly RebirthNpcSaveQuarantineScope scope;private readonly Member[] members;
    private bool registered;private RebirthNpcRuntimeNativeAdoption.NativeReentry incomingSource;
    private RebirthDogLifecycleService.NativeOwnerReentry incomingOwner;
    private bool PrivateEmpty{get{foreach(var member in members)if(!member.Empty||member.Keys.Owners.Count!=0)return false;return true;}}
    internal bool ObserveOwnerScope(RebirthDogLifecycleService.NativeOwnerReentry source)
    {
        if(!registered||!PrivateEmpty||source?.Current()!=true||ReferenceEquals(scope.World,source.World)||
            !RebirthNpcSaveQuarantineScope.SameDirectory(scope.Directory,source.Directory))return false;
        if(incomingSource!=null&&!ReferenceEquals(incomingSource.World,source.World)||incomingOwner!=null&&!ReferenceEquals(incomingOwner.World,source.World))return false;
        incomingOwner=source;return true;
    }
    private RebirthNpcRetainedWorkPackages(RebirthNpcSaveQuarantineScope scope,Member[] members){this.scope=scope;this.members=members;}
    internal bool IsOriginalCurrent{get{if(registered||!ThreadManager.IsMainThread())return false;foreach(var member in members)if(!member.Current())return false;return true;}}
    internal bool CanRegister=>IsOriginalCurrent&&retained.Count<8;
    internal bool RegisterDetached()
    {
        if(!ThreadManager.IsMainThread())return false;if(registered)return retained.Contains(this);
        if(retained.Count>=8)return false;foreach(var member in members)if(!member.Detached())return false;
        retained.Add(this);registered=true;return true;
    }
    internal bool AbortAfterRollback()
    {
        if(!ThreadManager.IsMainThread())return false;foreach(var member in members)if(!member.Current())return false;
        if(registered&&!retained.Remove(this))return false;registered=false;incomingSource=null;incomingOwner=null;return true;
    }
    internal bool ObserveIncoming(RebirthNpcRuntimeNativeAdoption.NativeReentry source)
    {
        if(!registered||source?.Current()!=true||ReferenceEquals(scope.World,source.World)||!RebirthNpcSaveQuarantineScope.SameDirectory(scope.Directory,source.Directory))return false;
        if(incomingSource!=null&&!ReferenceEquals(incomingSource.World,source.World))return false;incomingSource=source;return true;
    }
    internal bool IncomingSafe(World incoming,string directory)
    {
        bool native=incomingSource?.Current()==true&&ReferenceEquals(incomingSource.World,incoming)||
            PrivateEmpty&&incomingOwner?.Current()==true&&ReferenceEquals(incomingOwner.World,incoming);
        if(!ThreadManager.IsMainThread()||!registered||!retained.Contains(this)||!native||
            !ReferenceEquals(GameManager.Instance?.World,incoming)||incoming?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||
            !RebirthNpcSaveQuarantineScope.SameDirectory(scope.Directory,directory)||!RebirthNpcSaveQuarantineScope.SameDirectory(directory,GameIO.GetSaveGameDir()))return false;
        foreach(var member in members)if(!member.Detached())return false;return true;
    }
    private bool Relevant=>registered&&!ReferenceEquals(scope.World,GameManager.Instance?.World)&&RebirthNpcSaveQuarantineScope.SameDirectory(scope.Directory,GameIO.GetSaveGameDir());
    private static bool Any(Func<Keys,bool> match){foreach(var item in retained)if(item.Relevant)foreach(var member in item.members)if(match(member.Keys))return true;return false;}
    internal static bool BlocksOwner(RebirthNpcStableId owner)=>!owner.IsEmpty&&Any(keys=>keys.Owners.Contains(owner));
    internal static bool BlocksAssignment(ulong id)=>Any(keys=>keys.Assignments.Contains(id));
    internal static bool BlocksLease(ulong id)=>Any(keys=>keys.Leases.Contains(id));
    internal static bool BlocksTarget(string target)=>target!=null&&Any(keys=>keys.Targets.Contains(target));
    internal static bool BlocksStation(Vector3i position)=>Any(keys=>keys.Stations.Contains(position.ToString()));
    internal static bool BlocksExecutor(object executor)=>executor!=null&&Any(keys=>keys.Executors.Any(value=>ReferenceEquals(value,executor)));
    internal static bool TryCapture(RebirthNpcSaveQuarantineScope scope,out RebirthNpcRetainedWorkPackages receipt)
    {
        receipt=null;if(scope==null||!ThreadManager.IsMainThread()||!RebirthNpcWorkCallbackObservation.IsIdle)return false;
        try
        {
            var members=CaptureMembers(scope);foreach(var member in members)if(member==null||!member.Current())return false;
            receipt=new RebirthNpcRetainedWorkPackages(scope,members);return true;
        }
        catch{return false;}
    }
    private static Member[] CaptureMembers(RebirthNpcSaveQuarantineScope scope)=>new[]{
        RebirthNpcWorkExecutorRegistry.CapturePackageCustody(scope),RebirthNpcWorkTargetRegistry.CapturePackageCustody(scope),
        RebirthNpcWorkReservationService.CapturePackageCustody(scope),RebirthNpcWorkAssignmentCoordinator.CapturePackageCustody(scope),
        RebirthNpcWorkstationExecutionService.CapturePackageCustody(scope),RebirthNpcGuardDutyWorkPackageService.CapturePackageCustody(scope),
        RebirthNpcMedicalTreatmentWorkPackageService.CapturePackageCustody(scope),RebirthNpcGuardPostWorkAdapter.CapturePackageCustody(scope),
        RebirthNpcMedicalTreatmentAdapter.CapturePackageCustody(scope),RebirthNpcWorkNavigationService.CapturePackageCustody(scope)};
    internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids)
    {
        RebirthNpcWorkExecutorRegistry.AppendPackageIds(ids);RebirthNpcWorkTargetRegistry.AppendPackageIds(ids);
        RebirthNpcWorkReservationService.AppendPackageIds(ids);RebirthNpcWorkAssignmentCoordinator.AppendPackageIds(ids);
        RebirthNpcGuardDutyWorkPackageService.AppendPackageIds(ids);RebirthNpcMedicalTreatmentWorkPackageService.AppendPackageIds(ids);
        RebirthNpcGuardPostWorkAdapter.AppendPackageIds(ids);RebirthNpcMedicalTreatmentAdapter.AppendPackageIds(ids);
        RebirthNpcWorkNavigationService.AppendPackageIds(ids);
    }
    internal static Member Capture<K,T>(string name,RebirthNpcSaveQuarantineScope scope,World originalWorld,Dictionary<K,T> records,
        Func<T,object[]> handles,Func<string> image,Keys keys,Func<bool> current,Func<bool> detached,Action detach,Action rollback)
    {
        if(scope==null||!ThreadManager.IsMainThread()||!RebirthNpcWorkCallbackObservation.IsIdle||records.Count>8192||
            originalWorld!=null&&!ReferenceEquals(originalWorld,scope.World)||originalWorld==null&&records.Count!=0)throw new InvalidOperationException("Original Work native package scope unavailable");
        string captured=image();var pointers=new Dictionary<K,object[]>();foreach(var pair in records)pointers.Add(pair.Key,handles(pair.Value));
        bool Current()
        {
            if(!RebirthNpcWorkCallbackObservation.IsIdle||!current()||records.Count!=pointers.Count||image()!=captured)return false;
            foreach(var pair in pointers)
            {
                if(!records.TryGetValue(pair.Key,out var value))return false;var now=handles(value);if(now.Length!=pair.Value.Length)return false;
                for(int i=0;i<now.Length;i++)if(!ReferenceEquals(now[i],pair.Value[i]))return false;
            }
            return true;
        }
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("retainedNativeWorkPackage");doc.AppendChild(root);root.SetAttribute("retainedNative","1");root.InnerText=captured;
        return new Member(new RebirthNpcSaveQuarantineDomain(name,doc,Current,detach,rollback),keys,Current,detached,records.Count==0);
    }
    internal static string Fields(params object[] values)=>RebirthNpcRetainedControllerCustody.Fields(values);
    internal static string Rows<K,T>(Dictionary<K,T> rows,Func<T,string> format)
    {var text=new System.Text.StringBuilder();foreach(var pair in rows.OrderBy(pair=>Convert.ToString(pair.Key,System.Globalization.CultureInfo.InvariantCulture),StringComparer.Ordinal))text.Append(Fields(pair.Key,format(pair.Value)));return text.ToString();}
    internal static string Order<T>(IEnumerable<T> values){var text=new System.Text.StringBuilder();foreach(var value in values)text.Append(Fields(value));return text.ToString();}
    internal static RebirthNpcStableId NativeOwner(World world,int entity)
    {var npc=world?.GetEntity(entity) as EntityRebirthNPC;if(npc?.RebirthRuntimeState==null||npc.RebirthRuntimeState.StableId.IsEmpty)throw new InvalidOperationException("Retained Work actor unavailable");return npc.RebirthRuntimeState.StableId;}
    internal static EntityRebirthNPC NativeActor(World world,RebirthNpcStableId id)
    {
        if(!RebirthNpcRuntimeRegistry.TryGetEntityId(id,out int entity))return null;
        var npc=world?.GetEntity(entity) as EntityRebirthNPC;
        return npc?.RebirthRuntimeState?.StableId==id?npc:null;
    }
}
