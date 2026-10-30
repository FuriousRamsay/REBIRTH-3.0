using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

// Presentation of an existing original reward only. Never creates a reward,
// consumes an entitlement, changes access, or acknowledges native removal.
internal static class RebirthBossRewardMarkers
{
    internal const string NavClass = "FuriousRamsayDroppedLoot";
    private sealed class Projection
    {
        internal Guid Operation;
        internal Vector3 Position;
        internal string Name;
        internal float NextSend;
    }
    private static readonly Dictionary<int,Projection> published = new Dictionary<int,Projection>();
    private static readonly Dictionary<int,NavObject> fallback = new Dictionary<int,NavObject>();
    private static readonly HashSet<int> seen = new HashSet<int>();
    private static readonly List<int> retired = new List<int>();
    private static World scope,stopped;
    private static NavObjectManager manager;
    private static string nativeWorld,saveDirectory;
    private static float nextUpdate;

    internal static void Install()
    {
        var harmony=new Harmony("rebirth.boss-events.reward-markers");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RegisterEntityPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(ReceivePatch));
    }
    internal static void Begin(){Clear();stopped=null;}
    // Explicit observation only: no Context, manager getter, reconciliation,
    // ownership cleanup or timer reset. Caller serializes primitive values.
    internal static object Observe(World world,NavObjectManager observedManager,string observedDirectory,int id,out NavObject owned)
    {
        fallback.TryGetValue(id,out owned);Projection value;bool found=published.TryGetValue(id,out value);
        return new {
            scopeWorldMatches=ReferenceEquals(scope,world)&&world!=null,
            scopeNativeWorld=nativeWorld,scopeNativeWorldMatches=nativeWorld!=null&&nativeWorld==world?.worldState?.Guid,
            scopeSaveDirectory=saveDirectory==null?null:saveDirectory.Length<=32768?saveDirectory:saveDirectory.Substring(0,32768),
            scopeSaveDirectoryTruncated=saveDirectory!=null&&saveDirectory.Length>32768,
            scopeSaveMatchesObserved=observedDirectory==null?(bool?)null:saveDirectory==observedDirectory,
            scopeManagerMatches=ReferenceEquals(manager,observedManager)&&manager!=null,
            stoppedForWorld=ReferenceEquals(stopped,world)&&world!=null,
            projectionCount=published.Count,fallbackReferenceCount=fallback.Count,nextUpdate,
            projectionPresent=found,projectionOperation=found&&value.Operation!=Guid.Empty?value.Operation.ToString("N"):null,
            projectionPosition=found?new[]{value.Position.x,value.Position.y,value.Position.z}:null,
            projectionName=found&&value.Name!=null?(value.Name.Length<=512?value.Name:value.Name.Substring(0,512)):null,
            projectionNameTruncated=found&&value.Name!=null&&value.Name.Length>512,projectionNextSend=found?(float?)value.NextSend:null,
            fallbackReferencePresent=owned!=null,fallbackReferenceShapeMatches=Own(owned,id)
        };
    }
    internal static void Stop(World world){Clear();stopped=world;}
    private static bool Context(World world)
    {
        if(!ThreadManager.IsMainThread()||world==null||ReferenceEquals(stopped,world)||
           !ReferenceEquals(GameManager.Instance?.World,world)||NavObjectManager.Instance==null)return false;
        string native=world.worldState?.Guid,directory=GameIO.GetSaveGameDir();
        if(string.IsNullOrEmpty(native))return false;
        if(!ReferenceEquals(scope,world)||!ReferenceEquals(manager,NavObjectManager.Instance)||nativeWorld!=native||saveDirectory!=directory)
        {
            Clear();scope=world;manager=NavObjectManager.Instance;nativeWorld=native;saveDirectory=directory;
        }
        return true;
    }
    private static bool Own(NavObject nav,int id)=>nav!=null&&nav.EntityID==id&&
        nav.TrackType==NavObject.TrackTypes.Position&&nav.NavObjectClass?.NavObjectClassName==NavClass;
    private static void DropLocal(int id)
    {
        NavObject nav;
        if(fallback.TryGetValue(id,out nav))
        {
            // Native pools/reuses NavObjects. A retained reference alone does
            // not authorize unregistering a later native entity marker.
            if(Own(nav,id)&&manager!=null&&manager.NavObjectList.Contains(nav))manager.UnRegisterNavObject(nav);
            fallback.Remove(id);
        }
    }
    private static void Clear()
    {
        foreach(var pair in fallback)
            if(Own(pair.Value,pair.Key)&&manager!=null&&manager.NavObjectList.Contains(pair.Value))manager.UnRegisterNavObject(pair.Value);
        fallback.Clear();published.Clear();seen.Clear();retired.Clear();
        scope=null;manager=null;nativeWorld=null;saveDirectory=null;nextUpdate=0f;
    }
    private static bool LoadedMarker(Entity entity)=>entity!=null&&entity.NavObject!=null&&
        entity.NavObject.IsTrackedEntity(entity)&&entity.NavObject.NavObjectClass?.NavObjectClassName==NavClass;
    private static void ShowLocal(int id,Vector3 position,string name)
    {
        var entity=scope.GetEntity(id);
        if(entity!=null)
        {
            // Never bind a projection to an unrelated/reused entity ID.
            if(entity.entityClass!=EntityClass.FromString("RB_EventLootContainer")||LoadedMarker(entity))
            {DropLocal(id);return;}
        }
        NavObject nav;
        if(!fallback.TryGetValue(id,out nav)||!Own(nav,id)||!manager.NavObjectList.Contains(nav))
        {
            fallback.Remove(id);
            nav=manager.RegisterNavObject(NavClass,position,string.Empty,false,id,null);
            // Native coalesces coincident positional markers. Do not relabel
            // another crate's marker or steal its ID when that happens.
            if(!Own(nav,id))return;
            fallback[id]=nav;
        }
        nav.TrackedPosition=position;nav.name=name??string.Empty;nav.usingLocalizationId=false;
    }
    private static void Send(int id,Projection value,bool add)
    {
        // Same native class, ID, name and transport as entity markers. Keeping
        // the class on retirement distinguishes presentation-only withdrawal
        // from the native empty-class remove packet that owns entity removal.
        var packet=NetPackageManager.GetPackage<NetPackageNavObject>().Setup(NavClass,value.Name,value.Position,add,false,id);
        ConnectionManager.Instance.SendPackage(packet,false,-1,-1,-1,null,192);
    }
    internal static void Update(World world)
    {
        // Stable frames do not rebuild paths, allocate snapshots or scan navs.
        if(ReferenceEquals(scope,world)&&ReferenceEquals(GameManager.Instance?.World,world)&&
           ReferenceEquals(manager,NavObjectManager.Instance)&&Time.realtimeSinceStartup<nextUpdate)return;
        if(!Context(world))return;
        nextUpdate=Time.realtimeSinceStartup+1f;
        if(world.IsRemote())
        {
            if(!RebirthHotPathFeatureFlags.IsMapMarkersReservationsEnabled)
            {retired.Clear();foreach(int id in fallback.Keys)retired.Add(id);foreach(int id in retired)DropLocal(id);}
            else foreach(var pair in published)ShowLocal(pair.Key,pair.Value.Position,pair.Value.Name);
            return;
        }
        if(!RebirthBossEventPersistence.IsReady||ConnectionManager.Instance==null||!ConnectionManager.Instance.IsServer)return;
        seen.Clear();
        if(RebirthHotPathFeatureFlags.IsMapMarkersReservationsEnabled)
        foreach(var e in RebirthBossEventRegistry.SnapshotEvents())
        {
            if(e?.Progression==null||!e.RewardIssued||!e.RewardBlockPlaced||e.RewardEntityId<0||world.worldTime>=e.RewardExpiresWorldTime)continue;
            var original=e.SpawnOriginals.Find(value=>value.Role==3);
            if(original==null||original.EntityId!=e.RewardEntityId||original.EntityClass!=EntityClass.FromString("RB_EventLootContainer")||
               original.Phase!=3||original.NativeProof.Length!=64||original.Abandoned||original.RemovalInvoking||original.NativeRemoved)continue;
            var entity=world.GetEntity(original.EntityId);
            if(entity!=null&&!RebirthBossNativePublication.Matches(world,e,original,entity))continue;
            int id=original.EntityId;seen.Add(id);
            Vector3 position=entity!=null?entity.position:(Vector3)e.RewardBlockPosition;
            string name=e.Progression.OwnerDisplayName??string.Empty;
            Projection projection;
            if(!published.TryGetValue(id,out projection))published[id]=projection=new Projection{Operation=original.Operation};
            if(projection.Operation!=original.Operation||!projection.Position.Equals(position)||projection.Name!=name)
            {DropLocal(id);projection.Operation=original.Operation;projection.NextSend=0f;}
            projection.Position=position;projection.Name=name;
            ShowLocal(id,position,name);
            // Also send when loaded on the server: an observer can be outside
            // that chunk. Its local loaded native marker suppresses duplication.
            if(Time.realtimeSinceStartup>=projection.NextSend)
            {Send(id,projection,true);projection.NextSend=Time.realtimeSinceStartup+10f;}
        }
        retired.Clear();foreach(int id in published.Keys)if(!seen.Contains(id))retired.Add(id);
        foreach(int id in retired){Send(id,published[id],false);DropLocal(id);published.Remove(id);}
    }
    internal static void BeforeEntityMarker(string navClass,Entity entity)
    {
        if(navClass==NavClass&&entity!=null&&Context(entity.world as World))DropLocal(entity.entityId);
    }
    internal static bool Receive(NetPackageNavObject packet,World world)
    {
        if(packet==null)return true;
        if(packet.navObjectClass!=NavClass)
        {
            // Native final removal remains native. Forget only our own fallback.
            if(!packet.isAdd&&Context(world)){DropLocal(packet.entityId);published.Remove(packet.entityId);}
            return true;
        }
        if(!Context(world)||!world.IsRemote())return false;
        if(!packet.isAdd){DropLocal(packet.entityId);published.Remove(packet.entityId);return false;}
        // Retain presentation while the native entity is loaded too. Its later
        // local chunk unload can restore the fallback at the next 1s reconcile
        // without waiting for another network replay. No TTL/deletion inference.
        published[packet.entityId]=new Projection{Position=packet.position,Name=packet.name};
        if(RebirthHotPathFeatureFlags.IsMapMarkersReservationsEnabled)ShowLocal(packet.entityId,packet.position,packet.name);
        return false;
    }
    [HarmonyPatch(typeof(NavObjectManager),nameof(NavObjectManager.RegisterNavObject),new Type[]{typeof(string),typeof(Entity),typeof(string),typeof(bool)})]
    private static class RegisterEntityPatch
    {
        private static void Prefix(string __0,Entity __1)=>BeforeEntityMarker(__0,__1);
    }
    [HarmonyPatch(typeof(NetPackageNavObject),nameof(NetPackageNavObject.ProcessPackage),new Type[]{typeof(World),typeof(GameManager)})]
    private static class ReceivePatch
    {
        private static bool Prefix(NetPackageNavObject __instance,World __0)=>Receive(__instance,__0);
    }
}
