using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using UnityEngine;

public static partial class RebirthDogRuntimeService
{
    private static World dogRuntimeWorld;
    private static bool CurrentRuntimeDog(EntityRebirthDogCompanion dog,bool authoritative=true)
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world==null||dog==null||!dog.NativeIdentityReadable||dog.RebirthRuntimeState==null||dog.RebirthRuntimeState.StableId.IsEmpty||!ReferenceEquals(dog.world,world)||!ReferenceEquals(world.GetEntity(dog.entityId),dog))return false;
        if(world.IsRemote())return !authoritative;
        if(SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return false;
        lock(Sync){if(dogRuntimeWorld!=null&&!ReferenceEquals(dogRuntimeWorld,world))return false;if(dogRuntimeWorld==null){lock(MovementTraceSync)if(Cache.Count!=0||ActiveMovementTrace!=null)return false;dogRuntimeWorld=world;}return true;}
    }
    private static void RetireRemovedRuntimeDog(int entityId)
    {
        if(!ThreadManager.IsMainThread()||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return;
        lock(Sync)
        {
            if(dogRuntimeWorld==null||!ReferenceEquals(dogRuntimeWorld,GameManager.Instance?.World)||!Cache.TryGetValue(entityId,out var record)||!record.NativeRemoved||ReferenceEquals(record.Actor,null)||!ReferenceEquals(record.Actor.world,dogRuntimeWorld)||ReferenceEquals(dogRuntimeWorld.GetEntity(entityId),record.Actor))return;
            // Primitive diagnostic/movement cache only, after original native body
            // removal. This is never an inventory/lease/payment release witness.
            Cache.Remove(entityId);
            lock(MovementTraceSync)if(ActiveMovementTrace!=null&&ReferenceEquals(ActiveMovementTrace.Actor,record.Actor))ActiveMovementTrace=null;
        }
    }
    // Sole native successful-removal hook only. This fact has no inventory,
    // payment, lease or native body reconstruction authority.
    internal static void OnNativeRuntimeRemoved(World originalWorld,EntityRebirthDogCompanion actor)
    {
        if(!ThreadManager.IsMainThread()||originalWorld==null||ReferenceEquals(actor,null)||!ReferenceEquals(originalWorld,dogRuntimeWorld)||!ReferenceEquals(actor.world,originalWorld)||ReferenceEquals(originalWorld.GetEntity(actor.entityId),actor))return;
        lock(Sync)
        {
            if(Cache.TryGetValue(actor.entityId,out var record)&&ReferenceEquals(record.Actor,actor)&&record.StableId==actor.RebirthRuntimeState?.StableId)record.NativeRemoved=true;
            lock(MovementTraceSync)if(ActiveMovementTrace!=null&&ReferenceEquals(ActiveMovementTrace.Actor,actor)&&ActiveMovementTrace.StableId==actor.RebirthRuntimeState?.StableId)ActiveMovementTrace.NativeRemoved=true;
        }
        if(!ReferenceEquals(originalWorld,GameManager.Instance?.World)||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return;
        RetireRemovedRuntimeDog(actor.entityId);
        lock(MovementTraceSync)if(ActiveMovementTrace!=null&&ActiveMovementTrace.NativeRemoved&&ReferenceEquals(ActiveMovementTrace.Actor,actor))ActiveMovementTrace=null;
    }
    internal static bool CanRetireEmptyDogRuntime
    {get{lock(Sync)lock(MovementTraceSync)return ThreadManager.IsMainThread()&&Cache.Count==0&&ActiveMovementTrace==null;}}
    internal static bool CanBeginFreshGeneration(World world)
    {
        lock(Sync)lock(MovementTraceSync)
        {
            if(!ThreadManager.IsMainThread()||world==null||(dogRuntimeWorld!=null&&!ReferenceEquals(dogRuntimeWorld,world))||ActiveMovementTrace!=null)return false;
            foreach(var pair in Cache)
            {
                var c=pair.Value;
                if(c==null||!OriginalRuntimeActor(c.Actor,c.StableId,pair.Key,c.NativeRemoved)||c.HasRecallGroundTarget||c.RecallGroundGuardUntilTicks>DateTime.UtcNow.Ticks)return false;
            }
            return true;
        }
    }
    internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids)
    {
        lock(Sync){foreach(var record in Cache.Values){if(record==null)throw new InvalidOperationException("Dog runtime cache missing");RebirthNpcSaveQuarantineIds.Add(ids,record.StableId);}lock(MovementTraceSync)if(ActiveMovementTrace!=null)RebirthNpcSaveQuarantineIds.Add(ids,ActiveMovementTrace.StableId);}
    }
    internal static RebirthNpcSaveQuarantineDomain PrepareDogRuntimeQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)lock(MovementTraceSync)
        {
            if(!ThreadManager.IsMainThread()||scope==null||(dogRuntimeWorld!=null&&!ReferenceEquals(dogRuntimeWorld,scope.World))||(dogRuntimeWorld==null&&!CanRetireEmptyDogRuntime))throw new InvalidOperationException("Original dog runtime binding unavailable");
            var records=Cache;var trace=ActiveMovementTrace;var world=dogRuntimeWorld;var next=new Dictionary<int,RuntimeCache>();var doc=DogRuntimeImage();
            return new RebirthNpcSaveQuarantineDomain("dog-runtime-cache",doc,
                ()=>{lock(Sync)lock(MovementTraceSync)return ReferenceEquals(Cache,records)&&ReferenceEquals(ActiveMovementTrace,trace)&&ReferenceEquals(dogRuntimeWorld,world)&&DogRuntimeImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync)lock(MovementTraceSync){Cache=next;ActiveMovementTrace=null;dogRuntimeWorld=null;}},
                ()=>{lock(Sync)lock(MovementTraceSync){Cache=records;ActiveMovementTrace=trace;dogRuntimeWorld=world;}});
        }
    }
    private static bool OriginalRuntimeActor(EntityRebirthDogCompanion actor,RebirthNpcStableId id,int entityId,bool removed=false)
    {
        if(ReferenceEquals(actor,null)||dogRuntimeWorld==null||id.IsEmpty||!ReferenceEquals(actor.world,dogRuntimeWorld)||actor.entityId!=entityId||ReferenceEquals(dogRuntimeWorld.GetEntity(entityId),actor)==removed||!actor.NativeIdentityReadable||actor.RebirthRuntimeState?.StableId!=id)return false;
        var observed=actor.ObservedNativeStableIdentity;return observed.IsEmpty||observed==id;
    }
    private static XmlDocument DogRuntimeImage()
    {
        if(Cache.Count>1024)throw new InvalidOperationException("Dog runtime retention bound exceeded");var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("dogRuntime");doc.AppendChild(root);root.SetAttribute("nativeActorInternals","retained-original-only");long characters=0;
        var keys=new List<int>(Cache.Keys);keys.Sort();foreach(int id in keys)
        {
            var c=Cache[id];if(c==null||!OriginalRuntimeActor(c.Actor,c.StableId,id,c.NativeRemoved))throw new InvalidOperationException("Dog runtime native incarnation unknown; original retained");var row=doc.CreateElement("cache");root.AppendChild(row);Attr(row,"entity",id);row.SetAttribute("stable",c.StableId.ToString());Attr(row,"nativeRemoved",c.NativeRemoved?1:0);
            Attr(row,"nextMovement",c.NextMovementTicks);Attr(row,"nextCombat",c.NextCombatTicks);Attr(row,"nextHeal",c.NextHealTicks);Attr(row,"nextPersist",c.NextPersistTicks);Attr(row,"nextWander",c.NextWanderTicks);Vector(row,"lastPosition",c.LastPosition);Attr(row,"lastMoved",c.LastMovedTicks);Attr(row,"nextRecovery",c.NextRecoveryTicks);Attr(row,"combatTarget",c.CombatTargetId);Attr(row,"combatSince",c.CombatTargetSinceTicks);Attr(row,"combatProgress",c.CombatLastProgressTicks);Number(row,"combatDistance",c.CombatLastDistance);Attr(row,"nextPath",c.NextPathRequestTicks);Attr(row,"hasPath",c.HasPathDestination?1:0);Vector(row,"lastPath",c.LastPathDestination);Vector(row,"seek",c.SeekOffset);Text(row,"movementDecision",c.LastMovementDecision,ref characters);Text(row,"combatDecision",c.LastCombatDecision,ref characters);Attr(row,"combatCandidate",c.LastCombatCandidateId);Text(row,"candidateReason",c.LastCombatCandidateReason,ref characters);Attr(row,"pathRequests",c.PathRequests);Attr(row,"pathSkips",c.PathSkips);Attr(row,"lastPathRequest",c.LastPathRequestTicks);Vector(row,"requestedDestination",c.LastRequestedDestination);Attr(row,"recallUntil",c.RecallGroundGuardUntilTicks);Attr(row,"hasRecallTarget",c.HasRecallGroundTarget?1:0);Vector(row,"recallTarget",c.RecallGroundTarget);
        }
        var trace=ActiveMovementTrace;if(trace!=null)
        {
            if(!OriginalRuntimeActor(trace.Actor,trace.StableId,trace.EntityId,trace.NativeRemoved))throw new InvalidOperationException("Movement trace original native incarnation unknown");var row=doc.CreateElement("movementTrace");root.AppendChild(row);Attr(row,"entity",trace.EntityId);row.SetAttribute("stable",trace.StableId.ToString());Attr(row,"nativeRemoved",trace.NativeRemoved?1:0);Attr(row,"end",trace.EndTicks);Attr(row,"nextSample",trace.NextSampleTicks);Attr(row,"samples",trace.Samples);Text(row,"requestedBy",trace.RequestedBy,ref characters);Attr(row,"hasPrevious",trace.HasPrevious?1:0);Vector(row,"previous",trace.PreviousPosition);Number(row,"yaw",trace.PreviousYaw);
        }
        return doc;
    }
    private static void Attr(XmlElement row,string name,IFormattable value)=>row.SetAttribute(name,value.ToString(null,CultureInfo.InvariantCulture));
    private static void Number(XmlElement row,string name,float value)
    {if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidOperationException("Dog runtime nonfinite scalar");row.SetAttribute(name,value.ToString("R",CultureInfo.InvariantCulture));}
    private static void Vector(XmlElement row,string prefix,Vector3 value){Number(row,prefix+"X",value.x);Number(row,prefix+"Y",value.y);Number(row,prefix+"Z",value.z);}
    private static void Text(XmlElement row,string name,string value,ref long characters)
    {characters+=value?.Length??0;if((value?.Length??0)>65536||characters>4L*1024*1024)throw new InvalidOperationException("Dog runtime text retention bound exceeded");row.SetAttribute(name+"Null",value==null?"1":"0");row.SetAttribute(name,value??string.Empty);}
}
