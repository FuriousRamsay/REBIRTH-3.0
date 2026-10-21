using System;
using System.Collections.Generic;

// An incoming domain can be staged with owner-specific original custody held.
// Staging is not terminal retirement and never replays a native operation.
internal sealed class RebirthNpcNativeTransientAdoption:IDisposable
{
    internal readonly string Name;
    private readonly World outgoing;private readonly string directory;
    private readonly Func<bool> original,available,pending;
    private readonly Action<RebirthNpcRuntimeNativeAdoption.NativeReentry> observe;
    private readonly Func<RebirthNpcRuntimeNativeAdoption.NativeReentry,bool> stage;
    private readonly Action release;
    private readonly Func<RebirthNpcStableId,bool> blocksOwner;
    private readonly Func<RebirthDogLifecycleService.NativeOwnerReentry,bool> ownerStage;
    private IDisposable memory;private bool disposed,staged;
    internal RebirthNpcNativeTransientAdoption(string name,RebirthNpcSaveQuarantineScope scope,Func<bool> original,Func<bool> available,
        Func<bool> pending,Action<RebirthNpcRuntimeNativeAdoption.NativeReentry> observe,
        Func<RebirthNpcRuntimeNativeAdoption.NativeReentry,bool> stage,long bytes,Action release=null,Func<RebirthNpcStableId,bool> blocksOwner=null,
        Func<RebirthDogLifecycleService.NativeOwnerReentry,bool> ownerStage=null)
    {
        if(scope==null||bytes<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*bytes+1048576),out memory))throw new InvalidOperationException("Native transient custody budget unavailable");
        Name=name;outgoing=scope.World;directory=scope.Directory;this.original=original;this.available=available;this.pending=pending;this.observe=observe;this.stage=stage;this.release=release;this.blocksOwner=blocksOwner;
        this.ownerStage=ownerStage;
    }
    internal bool IsOriginalCurrent=>!disposed&&ThreadManager.IsMainThread()&&original();
    internal bool HasRetainedOriginals=>!disposed&&pending();
    internal bool BlocksOwner(RebirthNpcStableId owner)=>!disposed&&!owner.IsEmpty&&pending()&&blocksOwner?.Invoke(owner)==true;
    private bool Scope(RebirthNpcRuntimeNativeAdoption.NativeReentry source)=>!disposed&&ThreadManager.IsMainThread()&&source?.Current()==true&&
        !ReferenceEquals(outgoing,source.World)&&RebirthNpcSaveQuarantineScope.SameDirectory(directory,source.Directory);
    internal bool CanAdopt(RebirthNpcRuntimeNativeAdoption.NativeReentry source)
    {try{return Scope(source)&&available();}catch{return false;}}
    internal bool TryAdopt(RebirthNpcRuntimeNativeAdoption.NativeReentry source)
    {
        try{if(!CanAdopt(source))return false;observe(source);if(!stage(source)||!Scope(source))return false;staged=true;return true;}
        catch{return false;}
    }
    internal void Observe(RebirthNpcRuntimeNativeAdoption.NativeReentry source)
    {if(staged&&Scope(source)){observe(source);stage(source);}}
    internal bool CanAdoptOwnerScope(RebirthDogLifecycleService.NativeOwnerReentry source)
    {
        try{return !disposed&&ownerStage!=null&&ThreadManager.IsMainThread()&&source?.Current()==true&&
            !ReferenceEquals(outgoing,source.World)&&RebirthNpcSaveQuarantineScope.SameDirectory(directory,source.Directory)&&available();}
        catch{return false;}
    }
    internal bool TryAdoptOwnerScope(RebirthDogLifecycleService.NativeOwnerReentry source)
    {
        try{if(!CanAdoptOwnerScope(source)||!ownerStage(source)||!source.Current())return false;staged=true;return true;}
        catch{return false;}
    }
    public void Dispose()
    {
        // A staged service owns this original through its owner fence. A
        // bundle release cannot silently abandon a callback or queued action.
        if(disposed||staged&&pending())return;disposed=true;release?.Invoke();memory?.Dispose();memory=null;
    }
}

internal sealed class RebirthNpcNativeActorReentryMap
{
    private readonly Dictionary<int,RebirthNpcRuntimeNativeAdoption.Outgoing> originals=new Dictionary<int,RebirthNpcRuntimeNativeAdoption.Outgoing>();
    private readonly Dictionary<int,RebirthNpcRuntimeNativeAdoption.NativeReentry> incoming=new Dictionary<int,RebirthNpcRuntimeNativeAdoption.NativeReentry>();
    internal void Add(World world,int entity)
    {
        if(entity<=0||originals.ContainsKey(entity))return;
        if(world?.GetEntity(entity) is not EntityRebirthNPC npc||npc.RebirthRuntimeState==null)throw new InvalidOperationException("Original native actor unavailable");
        var source=RebirthNpcRuntimeNativeAdoption.CaptureOutgoing(npc,npc.RebirthRuntimeState);
        if(source==null)throw new InvalidOperationException("Original native actor serialization unavailable");originals.Add(entity,source);
    }
    internal bool Current(){foreach(var source in originals.Values)if(!source.Current())return false;return true;}
    internal void Observe(RebirthNpcRuntimeNativeAdoption.NativeReentry source)
    {foreach(var pair in originals)if(pair.Value.Matches(source))incoming[pair.Key]=source;}
    internal bool TryResolve(int original,out EntityRebirthNPC actor)
    {actor=null;if(!incoming.TryGetValue(original,out var source)||!source.Current()||!originals[original].Matches(source))return false;actor=source.Actor;return true;}
    internal bool Contains(RebirthNpcStableId id){foreach(var source in originals.Values)if(source.StableId==id)return true;return false;}
    internal bool IsOriginalActor(int entity,RebirthNpcStableId id)=>originals.TryGetValue(entity,out var source)&&source.StableId==id;
}
