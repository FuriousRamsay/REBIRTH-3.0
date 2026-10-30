using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Opt-in host diagnostic through the existing authenticated /console route.
// No reconciliation, receipt publication, file I/O, constructive singleton
// access, localization getters, lifecycle calls or authority changes.
public sealed class ConsoleCmdRebirthBossRewardObservation : ConsoleCmdAbstract
{
    private const int OriginalLimit=580,NavLimit=4096,RowLimit=64,StampLimit=8192,JsonLimit=131072;
    public override string[] getCommands()=>new[]{"rbbossrewardtestobserve"};
    public override string getDescription()=>"Read original boss reward custody and native marker evidence.";
    public override string getHelp()=>"rbbossrewardtestobserve <event GUID N> <positive original reward ID> (CodexTest Bridge host only).";
    private static string Text(string value,int limit=512)=>value==null?null:value.Length<=limit?value:value.Substring(0,limit);
    private static JArray Position(Vector3 value)=>new JArray(value.x,value.y,value.z);
    private static bool Proof(string value)=>value!=null&&value.Length==64&&RebirthBossNativePublication.ValidProof(value);
    private static JObject Refuse(string reason)=>new JObject{["schema"]=1,["observationOnly"]=true,["accepted"]=false,["complete"]=false,["reason"]=reason};
    public override void Execute(List<string> args,CommandSenderInfo sender)
    {
        JObject result;
        try{result=Capture(args,sender);}catch(Exception error){result=Refuse("unavailable:"+error.GetType().Name);}
        string json=result.ToString(Formatting.None);
        if(json.Length>JsonLimit)json=Refuse("unavailable:output-limit").ToString(Formatting.None);
        SdtdConsole.Instance.Output(json);
    }
    internal static JObject Capture(List<string> args,CommandSenderInfo sender)
    {
        Guid eventId;int id;
        if(!ThreadManager.IsMainThread())return Refuse("main-thread-required");
        if(args==null||args.Count!=2||!Guid.TryParseExact(args[0],"N",out eventId)||eventId==Guid.Empty||!int.TryParse(args[1],out id)||id<=0)return Refuse("exact-event-and-original-reward-id-required");
        if(sender.RemoteClientInfo!=null||!RebirthGameBridge.IsRunning)return Refuse("local-CodexTest-Bridge-host-required");
        string gameName=GamePrefs.GetObject(EnumGamePrefs.GameName) as string;
        if(gameName!="CodexTest")return Refuse("local-CodexTest-Bridge-host-required");
        // IsDedicatedServer initializes native caches. Never invoke it here.
        if(!GameManager.isDedicatedChecked)return Refuse("unavailable:dedicated-cache-not-initialized");
        if(GameManager.isDedicated)return Refuse("local-CodexTest-Bridge-host-required");
        var world=GameManager.Instance?.World;
        var actor=world?.GetPrimaryPlayer() as EntityPlayerLocal;Entity registered;
        if(world==null||world.IsRemote()||ConnectionManager.Instance==null||!ConnectionManager.Instance.IsServer||actor==null||
           !ReferenceEquals(actor.world,world)||!world.Entities.dict.TryGetValue(actor.entityId,out registered)||!ReferenceEquals(registered,actor))return Refuse("current-registered-host-actor-required");
        Guid native;
        if(!Guid.TryParseExact(world.worldState?.Guid,"N",out native)||native==Guid.Empty)return Refuse("native-world-identity-unavailable");

        // Local storage path construction uses native initialized path-cache
        // dictionary reads only. Do not invoke roaming platform getters.
        string directory=null;string pathStatus="unavailable:nonlocal-storage";
        string gameWorld=GamePrefs.GetObject(EnumGamePrefs.GameWorld) as string;
        if(gameWorld==null||gameWorld.Length>32768)return Refuse("unavailable:game-world-identity");
        object storageValue=GamePrefs.GetObject(EnumGamePrefs.GameSaveStorageType);
        int storage=storageValue is int?(int)storageValue:-1;
        if(storage==0)
        {
            try{
                string root=GameIO.GetCachedUserDataPath((Platform.UserDataStorageType)storage,"Saves");
                if(root!=null&&root.Length+gameWorld.Length+gameName.Length+2<=32768)
                {directory=root+"/"+gameWorld+"/"+gameName;pathStatus="available:initialized-native-local-path-cache";}
                else pathStatus="unavailable:local-path-length";
            }
            catch{pathStatus="unavailable:native-local-path-cache";}
        }
        string boundPath,cachedDirectory;bool failed,cachedMatches;
        bool boundWorld=RebirthBossEventPersistence.ObserveBinding(world,out boundPath,out cachedDirectory,out failed,out cachedMatches);
        bool registryAvailable=boundWorld&&!failed&&cachedMatches&&boundPath!=null&&directory!=null&&
            string.Equals(boundPath,System.IO.Path.Combine(directory,"RebirthBossEvents.dat"),StringComparison.Ordinal);
        var result=new JObject{
            ["schema"]=1,["observationOnly"]=true,["accepted"]=true,["complete"]=false,["scope"]="local-authoritative-host",
            ["remotePresentationObserved"]=false,["nativeWorld"]=world.worldState.Guid,["sampleRealtime"]=Time.realtimeSinceStartup,["sampleWorldTime"]=world.worldTime,
            ["requestedEvent"]=eventId.ToString("N"),["requestedRewardId"]=id,
            ["actor"]=new JObject{["id"]=actor.entityId,["born"]=actor.WorldTimeBorn,["name"]=Text(actor.EntityName)},
            ["save"]=new JObject{["storage"]=storage,["gameName"]=gameName,["gameWorld"]=gameWorld,
                ["directory"]=directory,["directoryStatus"]=pathStatus,["boundPath"]=Text(boundPath,32768),["boundPathTruncated"]=boundPath!=null&&boundPath.Length>32768,
                ["cachedDirectory"]=Text(cachedDirectory,32768),["cachedDirectoryTruncated"]=cachedDirectory!=null&&cachedDirectory.Length>32768,["boundWorldMatches"]=boundWorld,
                ["loadFailed"]=failed,["cachedPathMatchesBound"]=cachedMatches,["registryLookupAvailable"]=registryAvailable,["freshIsReadyGetterInvoked"]=false},
            ["diskDurabilityProved"]=false
        };
        if(!registryAvailable){result["eventLookup"]="unavailable:binding-or-local-save-identity";return result;}
        RebirthBossEventInstance e;bool archived=false;
        bool found=RebirthBossEventRegistry.TryGet(eventId,out e);
        if(!found){found=RebirthBossEventRegistry.TryObserveArchived(eventId,out e);archived=found;}
        result["eventLookup"]=found?"available":"absent";
        result["eventArchived"]=archived;
        if(!found||e==null)return result;
        if(e.EventId!=eventId){result["accepted"]=false;result["reason"]="event-identity-mismatch";return result;}
        int originalCount=e.SpawnOriginals.Count,matches=0,role3Count=0;RebirthBossSpawnOriginal original=null;
        for(int i=0;i<Math.Min(originalCount,OriginalLimit);i++)
        {var value=e.SpawnOriginals[i];if(value!=null&&value.Role==3){role3Count++;if(value.EntityId==id){original=value;matches++;}}}
        result["originalScan"]=new JObject{["total"]=originalCount,["examined"]=Math.Min(originalCount,OriginalLimit),["truncated"]=originalCount>OriginalLimit,["role3CountExamined"]=role3Count,["matchingOriginalsExamined"]=matches};
        if(originalCount>OriginalLimit||matches!=1||e.RewardEntityId>=0&&e.RewardEntityId!=id)
        {result["accepted"]=false;result["reason"]=originalCount>OriginalLimit?"original-scan-incomplete":matches>1?"ambiguous-original":"reward-original-id-mismatch";return result;}
        string actorStable=RebirthBossEventIdentity.GetStableId(actor);
        result["actor"]["stableId"]=Text(actorStable);
        result["event"]=new JObject{
            ["id"]=e.EventId.ToString("N"),["state"]=e.State.ToString(),["rewardIssued"]=e.RewardIssued,["rewardPlaced"]=e.RewardBlockPlaced,
            ["currentRewardId"]=e.RewardEntityId,["rewardPosition"]=Position((Vector3)e.RewardBlockPosition),["rewardExpiresWorldTime"]=e.RewardExpiresWorldTime,
            ["expiredAtSample"]=world.worldTime>=e.RewardExpiresWorldTime,["ownerStableId"]=Text(e.Progression?.OwnerStableId),
            ["ownerEntityId"]=e.Progression==null?null:(JToken)new JValue(e.Progression.OwnerEntityId),
            ["stableOwnerMatchesActor"]=e.Progression==null||string.IsNullOrEmpty(actorStable)?null:(JToken)new JValue(e.Progression.OwnerStableId==actorStable)
        };
        result["original"]=new JObject{
            ["operation"]=original.Operation.ToString("N"),["id"]=original.EntityId,["class"]=original.EntityClass,["born"]=original.WorldTimeBorn,["role"]=original.Role,["phase"]=original.Phase,
            ["nativeProof"]=Text(original.NativeProof),["nativeProofShapeValid"]=Proof(original.NativeProof),["abandoned"]=original.Abandoned,
            ["removalInvoking"]=original.RemovalInvoking,["nativeRemoved"]=original.NativeRemoved,["removalProof"]=Text(original.RemovalProof),
            ["removalProofShapeValid"]=Proof(original.RemovalProof),["proofsAreInMemoryFieldsOnly"]=true
        };
        Entity entity;bool loaded=world.Entities.dict.TryGetValue(id,out entity)&&entity!=null;
        bool identity=loaded&&ReferenceEquals(entity.world,world)&&entity.entityId==id&&entity.entityClass==original.EntityClass&&entity.WorldTimeBorn==original.WorldTimeBorn;
        var loadedRow=new JObject{["lookupAvailable"]=true,["present"]=loaded,["idClassBirthWorldMatch"]=identity,["aliveTestUsed"]=false};
        if(loaded){loadedRow["id"]=entity.entityId;loadedRow["class"]=entity.entityClass;loadedRow["born"]=entity.WorldTimeBorn;loadedRow["position"]=Position(entity.position);loadedRow["addedToChunk"]=entity.addedToChunk;}
        result["loaded"]=loadedRow;
        var stamp=new JObject{["available"]=false,["state"]=loaded?"unavailable:not-added-to-loaded-chunk":"unavailable:entity-not-loaded",["comparisonAvailable"]=false,["existingMatchesAvailable"]=false};
        if(loaded&&entity.addedToChunk)
        {
            var chunk=world.GetChunkSync(entity.chunkPosAddedEntityTo.x,entity.chunkPosAddedEntityTo.z) as Chunk;
            if(chunk==null)stamp["state"]="unavailable:chunk-not-loaded";
            else if(chunk.InProgressUnloading)stamp["state"]="unavailable:chunk-unloading";
            else
            {
                string key=RebirthBossNativePublication.Key(original);
                if(chunk.ChunkCustomData==null||chunk.ChunkCustomData.dict==null)stamp["state"]="unavailable:chunk-custom-data";
                else
                {
                    stamp["available"]=true;
                    bool exists=chunk.ChunkCustomData.dict.TryGetValue(key,out var datum);stamp["present"]=exists;stamp["key"]=key;stamp["state"]=exists?"present":"absent";
                    bool bounded=!exists;
                    if(exists)
                    {
                        var bytes=datum.data;stamp["byteCount"]=bytes==null?null:(JToken)new JValue(bytes.Length);
                        stamp["expires"]=datum.expiresInWorldTime;stamp["savedToNetwork"]=datum.isSavedToNetwork;stamp["writeCallbackPresent"]=datum.TriggerWriteDataDelegate!=null;
                        bounded=bytes!=null&&bytes.Length<=StampLimit;stamp["bytesBounded"]=bounded;
                        if(bounded&&e.Progression?.OwnerStableId!=null&&e.Progression.OwnerStableId.Length<=1024)
                        {
                            var expected=RebirthBossNativePublication.Stamp(world,e,original);bool equal=bytes.Length==expected.Length;
                            for(int i=0;equal&&i<bytes.Length;i++)equal=bytes[i]==expected[i];
                            stamp["comparisonAvailable"]=true;stamp["expectedBytesMatch"]=equal;
                            using(var hash=SHA256.Create())stamp["sha256"]=BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
                        }
                    }
                    // This qualified path is the stationary container branch:
                    // existing Matches performs loaded-cache/stamp reads only.
                    if(bounded&&entity is EntityLootContainer&&e.Progression?.OwnerStableId!=null&&e.Progression.OwnerStableId.Length<=1024)
                    {stamp["existingMatchesAvailable"]=true;stamp["existingMatches"]=RebirthBossNativePublication.Matches(world,e,original,entity);}
                }
            }
        }
        result["loadedStamp"]=stamp;
        var manager=NavObjectManager.HasInstance?NavObjectManager.Instance:null;
        NavObject owned;var helper=JObject.FromObject(RebirthBossRewardMarkers.Observe(world,manager,directory,id,out owned));
        result["helper"]=helper;
        var navs=new JObject{["available"]=manager!=null&&manager.NavObjectList!=null};
        if(manager!=null&&manager.NavObjectList!=null)
        {
            var rows=new JArray();int count=0,collision=0;bool ownedSeen=false;
            int total=manager.NavObjectList.Count,examined=Math.Min(total,NavLimit);
            Vector3 reference=identity?entity.position:(Vector3)e.RewardBlockPosition;
            for(int i=0;i<examined;i++)
            {
                var nav=manager.NavObjectList[i];if(ReferenceEquals(nav,owned)&&owned!=null)ownedSeen=true;
                if(nav==null||nav.NavObjectClass?.NavObjectClassName!=RebirthBossRewardMarkers.NavClass)continue;
                bool positionAvailable=nav.TrackType==NavObject.TrackTypes.Position||nav.TrackType==NavObject.TrackTypes.Entity&&nav.trackedEntity!=null;
                Vector3 position=nav.TrackType==NavObject.TrackTypes.Entity&&nav.trackedEntity!=null?nav.trackedEntity.position:nav.trackedPosition;
                if(nav.EntityID!=id){if(positionAvailable&&position.Equals(reference))collision++;continue;}
                count++;
                if(rows.Count<RowLimit)rows.Add(new JObject{
                    ["id"]=nav.EntityID,["class"]=nav.NavObjectClass.NavObjectClassName,["tracking"]=nav.TrackType.ToString(),
                    ["name"]=Text(nav.name),["nameTruncated"]=nav.name!=null&&nav.name.Length>512,["usingLocalizationId"]=nav.usingLocalizationId,
                    ["displayName"]=nav.usingLocalizationId?null:Text(nav.name),["displayNameAvailable"]=!nav.usingLocalizationId,
                    ["positionAvailable"]=positionAvailable,["position"]=positionAvailable?Position(position):null,
                    ["tracksLoadedReference"]=loaded&&nav.IsTrackedEntity(entity),["helperOwnedReference"]=ReferenceEquals(nav,owned)
                });
            }
            navs["totalIndexCount"]=total;navs["examined"]=examined;navs["scanTruncated"]=total>NavLimit;navs["countsComplete"]=total<=NavLimit;
            navs["matchingIdAndClassCountExamined"]=count;navs["coincidentForeignIdSameClassCountExamined"]=collision;navs["collisionReferencePosition"]=Position(reference);
            navs["rowsTruncated"]=count>RowLimit;navs["rows"]=rows;navs["helperOwnedReferenceSeenInExaminedIndex"]=ownedSeen;
            result["complete"]=total<=NavLimit&&count<=RowLimit&&
                (bool)helper["scopeWorldMatches"]&&(bool)helper["scopeNativeWorldMatches"]&&(bool)helper["scopeManagerMatches"]&&
                helper["scopeSaveMatchesObserved"].Type==JTokenType.Boolean&&(bool)helper["scopeSaveMatchesObserved"]&&
                (!loaded||(bool)stamp["available"]&&
                    (stamp["present"]?.Type==JTokenType.Boolean&&!(bool)stamp["present"]||(bool)stamp["comparisonAvailable"]));
        }
        else navs["reason"]="unavailable:native-nav-index";
        result["markers"]=navs;
        result["completeness"]=new JObject{
            ["eventOriginalLookup"]=true,["nativeMarkerCounts"]=navs["countsComplete"]??false,
            ["loadedStamp"]=stamp["available"],["helperCurrentScope"]=helper["scopeWorldMatches"],
            ["remotePresentation"]=false,["diskDurability"]=false
        };
        return result;
    }
}
