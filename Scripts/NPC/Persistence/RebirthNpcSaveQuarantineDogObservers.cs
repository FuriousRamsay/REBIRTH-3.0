using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using UnityEngine;

public static partial class RebirthDogChunkObserverService
{
    private static World observerWorld;
    private static bool ObserverScope(bool bind=false)
    {
        var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||world.m_ChunkManager==null||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return false;
        lock(Sync){if(observerWorld!=null&&!ReferenceEquals(observerWorld,world))return false;if(observerWorld==null){if(Observers.Count!=0)return false;if(bind)observerWorld=world;}return true;}
    }
    private static bool CurrentObserverDog(EntityRebirthDogCompanion dog)
    {var world=GameManager.Instance?.World;return dog!=null&&dog.NativeIdentityReadable&&ReferenceEquals(dog.world,world)&&ReferenceEquals(world?.GetEntity(dog.entityId),dog);}
    private static bool ObserverNativeScope(ObserverEntry entry)
    {return ThreadManager.IsMainThread()&&entry?.World!=null&&ReferenceEquals(entry.World,observerWorld)&&entry.Manager!=null&&ReferenceEquals(entry.Manager.m_World,entry.World)&&ReferenceEquals(entry.World.m_ChunkManager,entry.Manager)&&entry.Observer!=null&&entry.Observer.id==entry.NativeId&&entry.Manager.m_ObservedEntities!=null&&entry.Manager.m_ObservedEntities.Count<=16384;}
    private static bool OwnsNativeObserver(ObserverEntry entry)
    {
        if(!ObserverNativeScope(entry))return false;int ids=0,references=0;
        foreach(var observer in entry.Manager.m_ObservedEntities){if(ReferenceEquals(observer,entry.Observer))references++;if(observer!=null&&observer.id==entry.NativeId)ids++;}
        return ids==1&&references==1;
    }
    private static bool NativeObserverRemoved(ObserverEntry entry)
    {
        if(!ObserverNativeScope(entry))return false;
        foreach(var observer in entry.Manager.m_ObservedEntities)if(ReferenceEquals(observer,entry.Observer)||observer!=null&&observer.id==entry.NativeId)return false;return true;
    }
    internal static bool CanRetireEmptyObservers{get{lock(Sync)return ThreadManager.IsMainThread()&&Observers.Count==0;}}
    internal static bool CanBeginFreshGeneration(World world)
    {lock(Sync){if(observerWorld!=null&&!ReferenceEquals(observerWorld,world))return false;foreach(var entry in Observers.Values)if(!OwnsNativeObserver(entry)||entry.FollowRecoveryRequested||entry.ManualRecallAllOrders)return false;return true;}}
    internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids)
    {lock(Sync)foreach(var id in Observers.Keys)RebirthNpcSaveQuarantineIds.Add(ids,id);}
    internal static RebirthNpcSaveQuarantineDomain PrepareDogObserverQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||scope==null||(observerWorld!=null&&!ReferenceEquals(observerWorld,scope.World))||(observerWorld==null&&Observers.Count!=0))throw new InvalidOperationException("Original dog observer native scope unavailable");
            var entries=Observers;var world=observerWorld;var next=new Dictionary<RebirthNpcStableId,ObserverEntry>();var doc=DogObserverImage();
            // Strong original World/Manager/observer references remain in entries.
            // No constructor, manager mutation, re-registration or B reuse occurs.
            return new RebirthNpcSaveQuarantineDomain("dog-native-observers",doc,
                ()=>{lock(Sync)return ReferenceEquals(Observers,entries)&&ReferenceEquals(observerWorld,world)&&DogObserverImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){Observers=next;observerWorld=null;}},
                ()=>{lock(Sync){Observers=entries;observerWorld=world;}});
        }
    }
    private static XmlDocument DogObserverImage()
    {
        if(Observers.Count>1024)throw new InvalidOperationException("Dog observer retention bound exceeded");var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("dogObservers");doc.AppendChild(root);root.SetAttribute("nativeInternals","retained-original-manager-handles-only");
        var ids=new List<RebirthNpcStableId>(Observers.Keys);ids.Sort((a,b)=>string.CompareOrdinal(a.ToString(),b.ToString()));foreach(var id in ids)
        {
            var entry=Observers[id];if(entry==null||entry.StableId!=id||!OwnsNativeObserver(entry)||(entry.OwnerId?.Length??0)>4096)throw new InvalidOperationException("Original native observer ownership unavailable; handle retained");
            var row=doc.CreateElement("observer");root.AppendChild(row);row.SetAttribute("id",id.ToString());row.SetAttribute("owner",entry.OwnerId??string.Empty);row.SetAttribute("ownerNull",entry.OwnerId==null?"1":"0");row.SetAttribute("nativeId",entry.NativeId.ToString(CultureInfo.InvariantCulture));row.SetAttribute("followRecovery",entry.FollowRecoveryRequested?"1":"0");row.SetAttribute("manualAll",entry.ManualRecallAllOrders?"1":"0");Vector(row,"intended",entry.Position);Vector(row,"native",entry.Observer.position);row.SetAttribute("chunk",entry.Observer.curChunkPos.ToString());row.SetAttribute("view",entry.Observer.viewDim.ToString(CultureInfo.InvariantCulture));row.SetAttribute("mesh",entry.Observer.bBuildVisualMeshAround?"1":"0");row.SetAttribute("recipient",entry.Observer.entityIdToSendChunksTo.ToString(CultureInfo.InvariantCulture));
        }
        return doc;
    }
    private static void Vector(XmlElement row,string prefix,Vector3 value)
    {if(float.IsNaN(value.x)||float.IsNaN(value.y)||float.IsNaN(value.z)||float.IsInfinity(value.x)||float.IsInfinity(value.y)||float.IsInfinity(value.z))throw new InvalidOperationException("Native observer vector nonfinite");row.SetAttribute(prefix+"X",value.x.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute(prefix+"Y",value.y.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute(prefix+"Z",value.z.ToString("R",CultureInfo.InvariantCulture));}
}
