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
    private sealed class Audit
    {
        internal string ReadMode,WriteMode,WriteDecision;
        internal bool Parsed,ExistingAtRead;
        internal long ReadBefore,ReadAfter;
        internal int Reads,Writes;
    }
    private static readonly ConditionalWeakTable<EntitySupplyCrate,Audit> audits=new ConditionalWeakTable<EntitySupplyCrate,Audit>();
    private static readonly object auditGate=new object();
    private static readonly System.Collections.Generic.Dictionary<int,Newtonsoft.Json.Linq.JObject> lastWrites=new System.Collections.Generic.Dictionary<int,Newtonsoft.Json.Linq.JObject>();
    private static void RememberWrite(EntitySupplyCrate crate,Audit audit)
    {
        if(crate==null||audit==null)return;
        Entry entry;bool known=entries.TryGetValue(crate,out entry);
        var row=new Newtonsoft.Json.Linq.JObject{["diagnosticOnly"]=true,["utc"]=DateTime.UtcNow.ToString("o"),["mode"]=audit.WriteMode,
            ["decision"]=audit.WriteDecision,["entryPresent"]=known,["authoritative"]=known&&entry.Authoritative,
            ["idMatches"]=known&&entry.EntityId==crate.entityId,["birthMatches"]=known&&entry.Born==crate.WorldTimeBorn,
            ["entryBorn"]=known?new Newtonsoft.Json.Linq.JValue(entry.Born):null,["nativeBorn"]=crate.WorldTimeBorn};
        lock(auditGate){if(lastWrites.Count>=128&&!lastWrites.ContainsKey(crate.entityId))lastWrites.Clear();lastWrites[crate.entityId]=row;}
    }
    // Explicit observer only: no authority repair, identifier rebinding or packet dispatch.
    internal static Newtonsoft.Json.Linq.JObject CaptureAudit(EntitySupplyCrate crate,string player)
    {
        if(crate==null)return null;
        Entry entry;Audit audit;bool known=entries.TryGetValue(crate,out entry);audits.TryGetValue(crate,out audit);
        Guid native;bool worldParsed=Guid.TryParseExact((crate.world as World)?.worldState?.Guid,"N",out native);
        Entity registered;bool registeredSame=crate.world!=null&&crate.world.Entities.dict.TryGetValue(crate.entityId,out registered)&&ReferenceEquals(registered,crate);
        Newtonsoft.Json.Linq.JObject lastWrite;lock(auditGate){lastWrite=lastWrites.TryGetValue(crate.entityId,out var priorWrite)?(Newtonsoft.Json.Linq.JObject)priorWrite.DeepClone():null;}
        return new Newtonsoft.Json.Linq.JObject {
            ["entryPresent"]=known,["authoritative"]=known&&entry.Authoritative,["savedSource"]=known&&entry.SavedSource,
            ["idMatches"]=known&&entry.EntityId==crate.entityId,["birthMatches"]=known&&entry.Born==crate.WorldTimeBorn,
            ["entryBorn"]=known?new Newtonsoft.Json.Linq.JValue(entry.Born):null,["nativeBorn"]=crate.WorldTimeBorn,
            ["ownerMatches"]=known&&entry.Stamp.Player==player,["worldMatches"]=known&&worldParsed&&entry.Stamp.World==native,
            ["registeredSame"]=registeredSame,["currentWorld"]=ReferenceEquals(GameManager.Instance?.World,crate.world),
            ["lastReadMode"]=audit?.ReadMode,["readTrailerParsed"]=audit?.Parsed??false,["existingAtRead"]=audit?.ExistingAtRead??false,
            ["readBefore"]=audit?.ReadBefore??-1,["readAfter"]=audit?.ReadAfter??-1,["reads"]=audit?.Reads??0,
            ["lastWriteMode"]=audit?.WriteMode,["lastWriteDecision"]=audit?.WriteDecision,["writes"]=audit?.Writes??0,
            ["lastWriteByEntityIdDiagnosticOnly"]=lastWrite
        };
    }
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
        Audit audit=null;if(__instance!=null&&RebirthPurgeFeedbackTrace.Allowed){audit=audits.GetOrCreateValue(__instance);audit.Writes++;audit.WriteMode=_eStreamMode.ToString();audit.WriteDecision="gated";}
        try {
        if(!__runOriginal||!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge||_bw==null||_eStreamMode==StreamModeWrite.ToServer)return;
        if(audit!=null)audit.WriteDecision="entry-or-identity-refused";
        Entry entry;Guid native;if(__instance==null||!entries.TryGetValue(__instance,out entry)||!entry.Authoritative||entry.EntityId!=__instance.entityId||entry.Born!=__instance.WorldTimeBorn||!Guid.TryParseExact((__instance.world as World)?.worldState?.Guid,"N",out native)||native!=entry.Stamp.World)return;
        // EntityCreationData frames each entity blob with an unsigned16-bit length.
        // Refuse oversized append instead of truncating or changing the native envelope.
        if(!_bw.BaseStream.CanSeek||_bw.BaseStream.Length>ushort.MaxValue-RebirthPurgeSupplyCrateStamp.EncodedLength)throw new InvalidOperationException("Supply crate blob exceeds native serialization capacity.");
        entry.Stamp.Write(_bw);
        if(audit!=null)audit.WriteDecision="trailer-written";
        } finally {RememberWrite(__instance,audit);}
    }
    internal static void AfterRead(EntitySupplyCrate __instance,PooledBinaryReader _br,StreamModeRead _eStreamMode,bool __runOriginal)
    {
        if(!__runOriginal||!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge||__instance==null||_br==null||_eStreamMode==StreamModeRead.FromClient)return;
        Audit audit=RebirthPurgeFeedbackTrace.Allowed?audits.GetOrCreateValue(__instance):null;
        if(audit!=null){audit.Reads++;audit.ReadMode=_eStreamMode.ToString();audit.ExistingAtRead=entries.TryGetValue(__instance,out var prior);audit.ReadBefore=_br.BaseStream.CanSeek?_br.BaseStream.Position:-1;}
        RebirthPurgeSupplyCrateStamp stamp;bool parsed=RebirthPurgeSupplyCrateStamp.TryRead(_br,out stamp);
        if(audit!=null){audit.Parsed=parsed;audit.ReadAfter=_br.BaseStream.CanSeek?_br.BaseStream.Position:-1;}if(!parsed)return;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        bool authoritative=_eStreamMode==StreamModeRead.Persistency; // Source retained; current server/world authority is checked on use.
        Entry existing;
        if(entries.TryGetValue(__instance,out existing))return;
        entries.Add(__instance,new Entry{Stamp=stamp,Authoritative=authoritative,SavedSource=authoritative,EntityId=__instance.entityId,Born=__instance.WorldTimeBorn});
    }
}
