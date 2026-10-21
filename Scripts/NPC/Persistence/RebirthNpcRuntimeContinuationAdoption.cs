using System;
using System.Collections.Generic;

// Incoming native actor provenance supplies presence, never completion of an
// old action. Individual store adapters retain their saved/custody obligations.
internal sealed class RebirthNpcRuntimeContinuationAdoption
{
    private readonly World outgoing;
    private readonly string directory;
    private readonly HashSet<RebirthNpcStableId> required;
    private readonly Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> sources=
        new Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry>();
    private readonly Func<bool> originalCurrent;
    private readonly Func<World,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry>,bool> adopt;
    private RebirthNpcRuntimeNativeAdoption.NativeReentry anchor;
    private RebirthDogLifecycleService.NativeOwnerReentry ownerAnchor;
    private Action<RebirthNpcRuntimeNativeAdoption.NativeReentry> nativeObservation;
    internal RebirthNpcRuntimeContinuationAdoption(RebirthNpcSaveQuarantineScope scope,HashSet<RebirthNpcStableId> required,
        Func<bool> current,Func<World,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry>,bool> adopt)
    {outgoing=scope.World;directory=scope.Directory;this.required=required;originalCurrent=current;this.adopt=adopt;}
    internal bool IsOriginalCurrent=>ThreadManager.IsMainThread()&&originalCurrent();
    internal static RebirthNpcRuntimeContinuationAdoption FromEmptySavedStore(RebirthNpcSaveQuarantineScope scope,
        RebirthNpcSaveQuarantineSavedAdoption.Store store,HashSet<RebirthNpcStableId> participants=null,
        Func<World,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry>,bool> qualify=null,
        Func<World,bool> qualifiedCurrent=null,Action<RebirthNpcRuntimeNativeAdoption.NativeReentry> observed=null)
    {
        if(store==null)return null;
        RebirthNpcRuntimeContinuationAdoption result=null;
        bool qualified=false;
        result=new RebirthNpcRuntimeContinuationAdoption(scope,participants??new HashSet<RebirthNpcStableId>(),()=>store.IsOriginalCurrent,
            (world,native)=>
            {
                if(qualified)return qualifiedCurrent!=null&&qualifiedCurrent(world);
                bool saved=result.anchor!=null?store.TryAdopt(result.anchor):result.ownerAnchor!=null&&store.TryAdopt(result.ownerAnchor);
                if(!saved||qualify!=null&&!qualify(world,native))return false;
                if(qualify!=null)qualified=true;
                return true;
            });
        result.nativeObservation=observed;
        return result;
    }
    internal bool Observe(RebirthNpcRuntimeNativeAdoption.NativeReentry source)
    {
        if(!ThreadManager.IsMainThread()||source?.Current()!=true||ReferenceEquals(source.World,outgoing)||
            !RebirthNpcSaveQuarantineScope.SameDirectory(directory,source.Directory))return false;
        if(anchor!=null&&!ReferenceEquals(anchor.World,source.World))return false;
        anchor=source;
        if(required.Contains(source.StableId))sources[source.StableId]=source;
        bool adopted=TryAdopt();
        if(adopted&&source.Current()){nativeObservation?.Invoke(source);return TryAdopt();}
        return adopted;
    }
    internal bool TryAdopt()
    {
        if(anchor==null&&ownerAnchor!=null)return TryAdoptOwnerScope(ownerAnchor);
        if(!ThreadManager.IsMainThread()||anchor?.Current()!=true||ReferenceEquals(anchor.World,outgoing)||
            !RebirthNpcSaveQuarantineScope.SameDirectory(directory,GameIO.GetSaveGameDir()))return false;
        foreach(var id in required)if(!sources.TryGetValue(id,out var source)||!ReferenceEquals(source.World,anchor.World)||!source.Current())return false;
        return adopt(anchor.World,sources);
    }
    // A real player native load may establish a namespace for this family
    // only when its private capture declared no native NPC obligations.
    // The family closure still validates/adopts its complete retained state;
    // zero required actors is never permission to erase loaded saved rows.
    internal bool ObserveOwnerScope(RebirthDogLifecycleService.NativeOwnerReentry source)
    {
        if(required.Count!=0||anchor!=null||!ThreadManager.IsMainThread()||source?.Current()!=true||
            ReferenceEquals(source.World,outgoing)||!RebirthNpcSaveQuarantineScope.SameDirectory(directory,source.Directory)||
            ownerAnchor!=null&&!ReferenceEquals(ownerAnchor.World,source.World))return false;
        ownerAnchor=source;return TryAdoptOwnerScope(source);
    }
    private bool TryAdoptOwnerScope(RebirthDogLifecycleService.NativeOwnerReentry source)
    {
        if(required.Count!=0||!ThreadManager.IsMainThread()||source?.Current()!=true||ReferenceEquals(source.World,outgoing)||
            !RebirthNpcSaveQuarantineScope.SameDirectory(directory,source.Directory)||
            !RebirthNpcSaveQuarantineScope.SameDirectory(directory,GameIO.GetSaveGameDir()))return false;
        return adopt(source.World,sources)&&source.Current();
    }
}
