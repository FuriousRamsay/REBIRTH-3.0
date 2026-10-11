using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine.Scripting;

// Installed only with the completed Purge release. The native entity blob remains
// self-contained; crate buff CVars are not a persistence channel in this ABI.
internal static class RebirthPurgeSupplyCrateSerialization
{
    private sealed class Entry
    {
        internal RebirthPurgeSupplyCrateStamp Stamp;
        internal bool Authoritative,SavedSource;
        internal int EntityId;internal ulong Born;
    }
    private static ConditionalWeakTable<EntitySupplyCrate,Entry> entries=new ConditionalWeakTable<EntitySupplyCrate,Entry>();
    internal static void Reset()=>entries=new ConditionalWeakTable<EntitySupplyCrate,Entry>();
    internal static bool TryAttach(EntitySupplyCrate crate,RebirthPurgeSupplyCrateStamp stamp,object nativeOwner,Func<bool> current)=>
        Attach(crate,stamp,nativeOwner,current,false);
    internal static bool TryAttachBeforeSpawn(EntitySupplyCrate crate,RebirthPurgeSupplyCrateStamp stamp,object nativeOwner,Func<bool> current)=>
        Attach(crate,stamp,nativeOwner,current,true);
    private static bool Attach(EntitySupplyCrate crate,RebirthPurgeSupplyCrateStamp stamp,object nativeOwner,Func<bool> current,bool beforeSpawn)
    {
        try
        {
            if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge||!ThreadManager.IsMainThread()||crate==null||stamp==null||nativeOwner==null||current==null||!current())return false;
            var world=crate.world as World;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            Entity registered;Guid guid;RebirthPurgeSupplyAccount account;
            var accounts=RebirthPurgeSupplyService.Published;
            if(world==null||manager==null||!manager.IsServer||world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)||!Guid.TryParseExact(world.worldState?.Guid,"N",out guid)||guid!=stamp.World||
                (beforeSpawn?world.Entities.dict.ContainsKey(crate.entityId):!world.Entities.dict.TryGetValue(crate.entityId,out registered)||!ReferenceEquals(registered,crate))||accounts==null||!accounts.TryGetValue(stamp.Player,out account)||(account.InFlight!=stamp.Sequence&&account.DeliveredDrops<stamp.Sequence)||RebirthPurgeSupplyAccount.DeliveryToken(guid,stamp.Player,stamp.Sequence)!=stamp.Token)return false;
            Entry existing;if(entries.TryGetValue(crate,out existing)){if(existing.EntityId==crate.entityId&&existing.Born==crate.WorldTimeBorn)return existing.Authoritative&&Same(existing.Stamp,stamp);entries.Remove(crate);}
            entries.Add(crate,new Entry{Stamp=stamp,Authoritative=true,EntityId=crate.entityId,Born=crate.WorldTimeBorn});return true;
        }
        catch{return false;}
    }
    private static bool Same(RebirthPurgeSupplyCrateStamp a,RebirthPurgeSupplyCrateStamp b)=>a.World==b.World&&a.Player==b.Player&&a.Sequence==b.Sequence&&a.Token==b.Token;
    // Projection is read-only presentation. It never proves server custody,
    // permits loot, completes delivery or accepts a client-origin trailer.
    internal static bool TryReadProjection(EntitySupplyCrate crate,out RebirthPurgeSupplyCrateStamp stamp)
    {
        stamp=null;Entry entry;Entity registered;Guid native;
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge||!ThreadManager.IsMainThread()||
           crate==null||!ReferenceEquals(GameManager.Instance?.World,crate.world)||!entries.TryGetValue(crate,out entry)||
           entry.EntityId!=crate.entityId||entry.Born!=crate.WorldTimeBorn||
           !crate.world.Entities.dict.TryGetValue(crate.entityId,out registered)||!ReferenceEquals(registered,crate)||
           !Guid.TryParseExact((crate.world as World)?.worldState?.Guid,"N",out native)||native!=entry.Stamp.World)return false;
        stamp=entry.Stamp;return true;
    }
    internal static bool TryReadAuthoritative(EntitySupplyCrate crate,Guid world,out RebirthPurgeSupplyCrateStamp stamp)=>
        TryRead(crate,world,false,out stamp);
    internal static bool TryReadSaved(EntitySupplyCrate crate,Guid world,out RebirthPurgeSupplyCrateStamp stamp)=>
        TryRead(crate,world,true,out stamp);
    private static bool TryRead(EntitySupplyCrate crate,Guid world,bool requireSavedSource,out RebirthPurgeSupplyCrateStamp stamp)
    {
        stamp=null;Entry entry;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge||!ThreadManager.IsMainThread()||manager==null||!manager.IsServer||crate?.world==null||crate.world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,crate.world))return false;
        if(crate==null||world==Guid.Empty||!entries.TryGetValue(crate,out entry)||!entry.Authoritative||requireSavedSource&&!entry.SavedSource||entry.Stamp.World!=world)return false;
        Entity registered;if(!crate.world.Entities.dict.TryGetValue(crate.entityId,out registered)||!ReferenceEquals(registered,crate))return false;
        Guid native; if(entry.EntityId!=crate.entityId||entry.Born!=crate.WorldTimeBorn||!Guid.TryParseExact((crate.world as World)?.worldState?.Guid,"N",out native)||native!=world)return false;
        stamp=entry.Stamp;return true;
    }
    internal static void AfterWrite(EntitySupplyCrate __instance,PooledBinaryWriter _bw,StreamModeWrite _eStreamMode,bool __runOriginal)
    {
        if(!__runOriginal||!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge||_bw==null||_eStreamMode==StreamModeWrite.ToServer)return;
        Entry entry;Guid native;if(__instance==null||!entries.TryGetValue(__instance,out entry)||!entry.Authoritative||entry.EntityId!=__instance.entityId||entry.Born!=__instance.WorldTimeBorn||!Guid.TryParseExact((__instance.world as World)?.worldState?.Guid,"N",out native)||native!=entry.Stamp.World)return;
        // EntityCreationData frames each entity blob with an unsigned16-bit length.
        // Refuse oversized append instead of truncating or changing the native envelope.
        if(!_bw.BaseStream.CanSeek||_bw.BaseStream.Length>ushort.MaxValue-RebirthPurgeSupplyCrateStamp.EncodedLength)throw new InvalidOperationException("Supply crate blob exceeds native serialization capacity.");
        entry.Stamp.Write(_bw);
    }
    internal static void AfterRead(EntitySupplyCrate __instance,PooledBinaryReader _br,StreamModeRead _eStreamMode,bool __runOriginal)
    {
        if(!__runOriginal||!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge||__instance==null||_br==null||_eStreamMode==StreamModeRead.FromClient)return;
        RebirthPurgeSupplyCrateStamp stamp;if(!RebirthPurgeSupplyCrateStamp.TryRead(_br,out stamp))return;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        bool authoritative=_eStreamMode==StreamModeRead.Persistency; // Source retained; current server/world authority is checked on use.
        Entry existing;
        if(entries.TryGetValue(__instance,out existing))return;
        entries.Add(__instance,new Entry{Stamp=stamp,Authoritative=authoritative,SavedSource=authoritative,EntityId=__instance.entityId,Born=__instance.WorldTimeBorn});
    }
}
