using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// One main-thread observation, through the existing authenticated /console route.
// Does not pulse services, activate rooms, modify records or force chunk loading.
public sealed class ConsoleCmdRebirthPurgeObservation : ConsoleCmdAbstract
{
    // Explicit observation only: do not drain evidence, discover targets, admit a
    // reset, retry writes or advance its native iterator from this diagnostic.
    private static JObject ObserveManualReset(World world, EntityPlayer player)
    {
        var result = new JObject { ["observationOnly"] = true };
        try
        {
            result["witnessesReady"] = RebirthPoiNativeResetWitnesses.IsReady;
            RebirthPoiWorldStore store;
            bool ready = RebirthPoiWorldLifecycle.Instance.TryGetEvidenceStore(out store);
            var snapshot = ready ? store.Published : null;
            result["storeReady"] = ready;
            result["storePending"] = ready && store.HasPending;
            result["bindingCurrent"] = snapshot != null && snapshot.Binding.IsCurrent;
            var prefabs = GameManager.Instance?.GetDynamicPrefabDecorator()?.GetPrefabsFromWorldPosInside(player.position, FastTags<TagGroup.Global>.none);
            result["targetCount"] = prefabs?.Count ?? 0;
            result["truncated"] = prefabs != null && prefabs.Count > 64;
            var targets = new JArray();
            if (prefabs != null)
                for (int index = 0; index < prefabs.Count && index < 64; index++)
                {
                    var prefab = prefabs[index];
                    var origin = prefab.boundingBoxPosition;
                    var size = prefab.boundingBoxSize;
                    RebirthPoiNativeAuthoredManifest authored = null;
                    bool captured = snapshot != null && RebirthPoiNativeAuthoredManifest.TryCapture(snapshot.Binding, world, prefab, out authored);
                    RebirthPoiClearanceRecord record = null;
                    bool recorded = captured && snapshot.TryGet(authored.Identity, out record);
                    targets.Add(new JObject {
                        ["prefab"] = prefab.prefab?.PrefabName,
                        ["origin"] = new JArray(origin.x, origin.y, origin.z),
                        ["size"] = new JArray(size.x, size.y, size.z),
                        ["centerBiome"] = world.GetBiome(origin.x + size.x / 2, origin.z + size.z / 2)?.m_sBiomeName,
                        ["sleeperDefinitions"] = prefab.prefab?.SleeperVolumeList?.Count ?? -1,
                        ["triggerDefinitions"] = prefab.prefab?.TriggerVolumeList?.Count ?? -1,
                        ["authoredCaptured"] = captured,
                        ["authoredSlots"] = captured ? authored.Slots.Count : -1,
                        ["recordFound"] = recorded,
                        ["recordState"] = recorded ? record.State.ToString() : null
                    });
                }
            result["targets"] = targets;
        }
        catch (System.Exception error) { result["unavailable"] = error.GetType().Name; }
        return result;
    }
    // SdtdConsole rejects commands before Execute when no world is running.
    // The authenticated bridge GET uses this same observation at the menu.
    internal static JObject ObserveLifecycle()
    {
        var world = GameManager.Instance?.World;
        return new JObject {
            ["schema"] = 1, ["observationOnly"] = true, ["worldPresent"] = world != null,
            ["equipmentCachedWorld"] = RebirthPurgeSupplyEquipment.HasCachedWorld,
            ["equipmentCachedPlayers"] = RebirthPurgeSupplyEquipment.CachedPlayerCount,
            ["equipmentCacheMatchesWorld"] = RebirthPurgeSupplyEquipment.CacheMatchesCurrentWorld,
            ["npcWorkCoordinatorReport"] = RebirthNpcWorkAssignmentCoordinator.GetReport(),
            ["versionLabelDiscoveryPasses"] = XUiC_RebirthVersionLabel.DiscoveryPasses,
            ["completionRecoveryDiagnostic"] = RebirthWorkstationCompletionEmptyRecovery.Diagnostic(world)
        };
    }

    internal static void HandleLifecycle(BridgeRequest req)
    {
        if (req.Method != "GET") { req.Fail("Purge lifecycle observation is read-only", 405); return; }
        req.Complete(ObserveLifecycle());
    }

    public override string[] getCommands() => new[] { "rbpurgetestobserve" };
    private static JObject ObserveServerPlayer(World world, EntityPlayer actor)
    {
        var poi = RebirthPurgeScannerPoi.Resolve(world, actor.position);
        var result = new JObject {
            ["observationOnly"] = true, ["player"] = actor.entityId, ["remote"] = world.IsRemote(),
            ["saveName"] = GamePrefs.GetString(EnumGamePrefs.GameName), ["worldKey"] = world.worldState?.Guid,
            ["purgeEnabled"] = RebirthSandboxOptionManager.Current.IsPurge,
            ["position"] = new JArray(actor.position.x, actor.position.y, actor.position.z),
            ["prefab"] = poi?.prefab?.PrefabName,
            ["scannerDiagnostic"] = RebirthPoiNativeEvidence.Instance.ScannerStatus(world, actor),
            ["completionDiagnostic"] = RebirthPoiNativeEvidence.Instance.CompletionStatus(world, actor)
        };
        var rooms = new JArray();
        if (poi?.prefab?.SleeperVolumeList != null)
            for (int index = 0; index < poi.prefab.SleeperVolumeList.Count; index++)
            {
                var definition = poi.prefab.SleeperVolumeList[index];
                var min = poi.boundingBoxPosition + definition.startPos; var max = min + definition.size;
                int nativeId = world.FindSleeperVolume(min, max);
                var volume = nativeId < 0 ? null : world.GetSleeperVolume(nativeId);
                rooms.Add(new JObject {
                    ["index"] = index, ["nativeId"] = nativeId, ["registered"] = volume != null,
                    ["minimum"] = new JArray(min.x, min.y, min.z), ["maximum"] = new JArray(max.x, max.y, max.z),
                    ["authoredMin"] = definition.spawnCountMin, ["authoredMax"] = definition.spawnCountMax,
                    ["cleared"] = volume == null ? null : new JValue(volume.wasCleared),
                    ["spawned"] = volume == null ? null : new JValue(volume.isSpawned),
                    ["numSpawned"] = volume == null ? null : new JValue(volume.numSpawned),
                    ["spawning"] = volume == null ? null : new JValue(volume.isSpawning),
                    ["scriptRunning"] = volume?.minScript != null && volume.minScript.IsRunning(),
                    ["pendingIds"] = volume == null ? null : new JArray(volume.pendingSpawnMap),
                    ["respawnIds"] = volume == null ? null : new JArray(volume.respawnMap.Keys)
                });
            }
        result["rooms"] = rooms;
        result["authoredRoomRegistration"] = rooms;
        result["scannerNativePoi"] = poi == null ? null : new JObject {
            ["prefab"] = poi.prefab?.PrefabName, ["tier"] = poi.prefab?.DifficultyTier,
            ["origin"] = new JArray(poi.boundingBoxPosition.x,poi.boundingBoxPosition.y,poi.boundingBoxPosition.z),
            ["size"] = new JArray(poi.boundingBoxSize.x,poi.boundingBoxSize.y,poi.boundingBoxSize.z)
        };
        RebirthPoiWorldStore store;
        var snapshot = RebirthPoiWorldLifecycle.Instance.TryGetStore(out store) ? store.Published : null;
        if (snapshot != null && RebirthPoiNativeManifest.TryResolve(world, poi, out var manifest)
            && snapshot.TryGet(manifest.Identity, out var record))
        {
            result["record"] = new JObject { ["state"] = record.State.ToString(), ["epoch"] = record.Epoch,
                ["revision"] = record.Revision, ["verifiedRooms"] = record.Clear?.VerifiedVolumes,
                ["participants"] = record.Clear?.SpawnedParticipants };
            int enrolled=0,deaths=0;
            if(record.Observations!=null)foreach(var room in record.Observations.Volumes.Values)
                foreach(var proof in room.Actors.Values)
                {enrolled++;if(proof.Dead&&proof.TerminalKind==RebirthPoiActorTerminalKind.Death)deaths++;}
            result["savedEvidence"] = new JObject { ["state"] = record.State.ToString(),
                ["epoch"] = record.Epoch,["enrolled"] = enrolled,["deathReceipts"] = deaths };
        }
        string owner=RebirthPurgeKillContributor.Identify(actor,world);
        var accounts=RebirthPurgeSupplyService.Published;
        result["supplyReady"]=accounts!=null;
        if(owner!=null&&accounts!=null&&accounts.TryGetValue(owner,out var account))
            result["supply"]=new JObject { ["observedCredits"]=account.ObservedCredits,
                ["spentCredits"]=account.SpentCredits,["earnedDrops"]=account.EarnedDrops,
                ["deliveredDrops"]=account.DeliveredDrops,["inFlight"]=account.InFlight };
        var crates=world.aiDirector?.GetComponent<AIDirectorAirDropComponent>()?.supplyCrates;
        result["nativeSupplyCacheCount"]=crates?.Count??-1;
        result["recordCount"]=snapshot?.Count??-1;
#if PURGE_QUALIFICATION
        result["feedbackTrace"]=RebirthPurgeFeedbackTrace.Observe();
        result["firstVisitFeedback"]=RebirthPurgeFirstVisitFeedback.Diagnostic();
        result["discoveryAdmission"]=JToken.FromObject(RebirthPoiNativeEvidence.Instance.ObserveDiscoveryAdmission());
#endif
        return result;
    }
    public override string getDescription() => "Collect current Purge test evidence as JSON.";
    public override string getHelp() => "rbpurgetestobserve [lifecycle|projection|player ID] (read-only, local Game Bridge only).";
    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        var world = GameManager.Instance?.World;
        var player = world?.GetPrimaryPlayer() as EntityPlayerLocal;
        if (sender.RemoteClientInfo == null && RebirthGameBridge.IsRunning && world != null)
        {
            if (args.Count == 2 && args[0] == "player" && !world.IsRemote() && int.TryParse(args[1], out var id)
                && world.GetEntity(id) is EntityPlayer remotePlayer)
            {
                SdtdConsole.Instance.Output(ObserveServerPlayer(world, remotePlayer).ToString(Formatting.None));
                return;
            }
            if (args.Count == 1 && args[0] == "projection" && player != null)
            {
                var projected = RebirthPurgeScanner.Observe(player);
                var remoteObservation=new JObject {
                    ["observationOnly"] = true, ["remote"] = world.IsRemote(), ["player"] = player.entityId,
                    ["purgeEnabled"] = RebirthSandboxOptionManager.Current.IsPurge,
                    ["worldKey"] = GamePrefs.GetString(EnumGamePrefs.GameGuidClient),
                    ["scannerCurrent"] = projected != null, ["markers"] = RebirthPurgeScanner.ObservedMarkerCount,
                    ["hud"] = RebirthPurgeScanner.Hud(player),
                    ["projection"] = projected == null ? null : new JObject {
                        ["poiKey"] = projected.PoiKey, ["prefab"] = projected.Prefab, ["tier"] = projected.Tier,
                        ["rooms"] = projected.Rooms, ["cleared"] = projected.Cleared, ["enemies"] = projected.Enemies,
                        ["threshold"] = RebirthPurgeScanner.Threshold(projected.Tier, projected.Rooms, projected.Cleared),
                        ["targets"] = new JArray(projected.Targets), ["epoch"] = projected.Epoch,
                        ["world"] = projected.World.ToString("N"), ["sequence"] = projected.Sequence
                    }
                };
                var map=RebirthPoiMapSync.LocalMap;var rows=new JArray();
                if(map!=null)foreach(var entry in map.Values)
                {
                    if(rows.Count==64)break;
                    rows.Add(new JObject { ["key"]=entry.Identity.Key,["prefab"]=entry.Identity.Prefab,
                        ["state"]=entry.State.ToString(),["epoch"]=entry.Epoch,
                        ["discoverer"]=entry.Discoverer,["discoveredAt"]=entry.DiscoveredAt,["purgedAt"]=entry.PurgedAt });
                }
                remoteObservation["mapRows"]=rows;remoteObservation["mapCount"]=map?.Count??-1;
                remoteObservation["mapTruncated"]=map!=null&&map.Count>64;
                remoteObservation["mapRevision"]=RebirthPoiMapSync.LocalRevision;
#if PURGE_QUALIFICATION
                remoteObservation["feedbackTrace"]=RebirthPurgeFeedbackTrace.Observe();
#endif
                SdtdConsole.Instance.Output(remoteObservation.ToString(Formatting.None));
                return;
            }
        }
        if (args.Count == 1 && args[0] == "lifecycle" && sender.RemoteClientInfo == null && RebirthGameBridge.IsRunning)
        {
            SdtdConsole.Instance.Output(ObserveLifecycle().ToString(Formatting.None));
            return;
        }
        if (args.Count != 0 || sender.RemoteClientInfo != null || world == null || world.IsRemote()
            || player == null || !RebirthGameBridge.IsRunning)
        { SdtdConsole.Instance.Output("*** ERROR " + getHelp()); return; }
        var result = new JObject {
            ["schema"] = 1, ["diagnosticVersion"] = 2, ["observationOnly"] = true,
            ["saveName"]=GamePrefs.GetString(EnumGamePrefs.GameName),
            ["manualResetDiagnostic"]=ObserveManualReset(world,player),
            ["startupDiagnostic"]=RebirthPurgeTruckStart.Diagnostic(player),
            ["informationDiagnostic"]=RebirthPurgeInformationService.Diagnostic(player),
            ["supplyDeliveryDiagnostic"]=RebirthPurgeSupplyCoordinator.Diagnostic(),
            ["completionRecoveryDiagnostic"]=RebirthWorkstationCompletionEmptyRecovery.Diagnostic(world),
            ["saveDeletionDiagnostic"]=RebirthDeletedSavePreferences.Status(),
            ["releaseEnabled"] = RebirthPurgeReleasePolicy.Enabled,
            ["purgeEnabled"] = RebirthSandboxOptionManager.Current.IsPurge,
            ["census"] = RebirthPurgePoiCensus.Instance.Status,
            ["scannerDiagnostic"] = RebirthPoiNativeEvidence.Instance.ScannerStatus(world, player),
            ["completionDiagnostic"] = RebirthPoiNativeEvidence.Instance.CompletionStatus(world,player),
            ["roomHud"] = RebirthPurgeScanner.Hud(player),
            ["lifetimeHud"] = RebirthPurgeScanner.LifetimeText(player),
            ["position"] = new JArray(player.position.x, player.position.y, player.position.z),
            ["hostileAmbientAnimalsSuppressed"] = ConsoleCmdRebirthPurgeTestSetup.SuppressHostileAnimals(world)
        };
        var view = RebirthPurgeScanner.Observe(player);
#if PURGE_QUALIFICATION
        result["discoveryAdmission"] = JToken.FromObject(RebirthPoiNativeEvidence.Instance.ObserveDiscoveryAdmission());
        result["feedbackTrace"] = RebirthPurgeFeedbackTrace.Observe();
        result["biomeSpawnTrace"] = RebirthPurgeBiomeSpawnTrace.Observe(world);
        result["firstVisitFeedback"] = RebirthPurgeFirstVisitFeedback.Diagnostic();
        result["progressionPolicy"] = new JObject {
            ["configured"] = RebirthSandboxOptionManager.Current.SpawnProgression.ToString(),
            ["effective"] = RebirthSandboxOptionManager.Current.EffectiveSpawnProgression.ToString()
        };
#endif
        result["scannerCurrent"] = view != null;
        result["visibleMarkerCount"] = RebirthPurgeScanner.ObservedMarkerCount;
        if (view != null)
            result["scanner"] = new JObject {
                ["prefab"] = view.Prefab, ["rooms"] = view.Rooms, ["cleared"] = view.Cleared, ["enemies"] = view.Enemies,
                ["areasLeft"] = System.Math.Max(0, view.Rooms - view.Cleared),
                ["approximate"] = view.Approximate, ["targets"] = new JArray(view.Targets),
                ["threshold"] = RebirthPurgeScanner.Threshold(view.Tier, view.Rooms, view.Cleared),
                ["epoch"] = view.Epoch, ["poiKey"] = view.PoiKey
            };
        RebirthPoiWorldStore store;
        var snapshot = RebirthPoiWorldLifecycle.Instance.TryGetStore(out store) ? store.Published : null;
        result["storeReady"] = snapshot != null;
        if (snapshot != null)
        {
            result["world"] = snapshot.WorldId.ToString("N");
            result["revision"] = snapshot.Revision; result["recordCount"] = snapshot.Count;
        }
        var pos = player.GetBlockPosition();
        var geometricPrefab = GameManager.Instance.GetDynamicPrefabDecorator()?.GetPrefabFromWorldPosInside(pos.x, pos.z);
        // Preserve native XYZ/tag-aware ownership for strict manifest/record evidence.
        // Scanner XZ occupancy is reported separately from native location ownership.
        var prefab = world.GetPOIAtPosition(player.position);
        result["scannerPlayerGates"] = new JObject {
            ["spawned"] = player.IsSpawned(), ["alive"] = player.IsAlive(),
            ["spectator"] = player.IsSpectator, ["mounted"] = player.AttachedToEntity != null,
            ["flying"] = player.IsFlyMode.Value,
            ["hudAllowed"] = RebirthPurgeHudProgress.Visible(player)
        };
        var scannerAuthored = prefab?.prefab;
        if (scannerAuthored != null)
            result["scannerNativePoi"] = new JObject {
                ["prefab"] = scannerAuthored.PrefabName,
                ["origin"] = new JArray(prefab.boundingBoxPosition.x, prefab.boundingBoxPosition.y, prefab.boundingBoxPosition.z),
                ["size"] = new JArray(prefab.boundingBoxSize.x, prefab.boundingBoxSize.y, prefab.boundingBoxSize.z),
                ["tier"] = scannerAuthored.DifficultyTier,
                ["authoredRooms"] = scannerAuthored.SleeperVolumeList?.Count ?? 0,
                ["loadedRooms"] = prefab.sleeperVolumes.Count,
                ["eligibilityAvailable"] = RebirthPurgePoiEligibility.Available,
                ["authoredAllowed"] = RebirthPurgePoiEligibility.Allows(scannerAuthored.PrefabName,
                    scannerAuthored.bTraderArea,
                    scannerAuthored.Tags.Test_AnySet(FastTags<TagGroup.Poi>.Parse("rwgonly")),
                    scannerAuthored.Tags.Test_AnySet(FastTags<TagGroup.Poi>.Parse("streettile")))
            };
        // Native location HUD uses player.prefab + authored info volumes/tags,
        // alongside the old first-XZ lookup used for comparison. Expose both
        // without changing either selection or forcing native prefab refresh.
        var scannerPoi=RebirthPurgeScannerPoi.Resolve(world,player.position);
        // Explicit read-only command diagnostics must work even when the strict
        // whole-POI manifest is precisely the thing being rejected.
        if(scannerPoi?.prefab?.SleeperVolumeList!=null)
        {
            var registration=new JArray();
            var definitions=scannerPoi.prefab.SleeperVolumeList;
            for(int index=0;index<definitions.Count;index++)
            {
                var definition=definitions[index];
                var minimum=scannerPoi.boundingBoxPosition+definition.startPos;
                var maximum=minimum+definition.size;
                int nativeId=world.FindSleeperVolume(minimum,maximum);
                SleeperVolume room=null;
                if(nativeId>=0)try{room=world.GetSleeperVolume(nativeId);}catch{ }
                var item=new JObject{
                    ["index"]=index,["nativeId"]=nativeId,["registered"]=room!=null,
                    ["authoredMin"]=definition.spawnCountMin,["authoredMax"]=definition.spawnCountMax,
                    ["authoredFlags"]=definition.flags,["script"]=definition.minScript??"",
                    ["authoredUsed"]=definition.Used,["authoredGroupId"]=definition.groupId,
                    ["authoredGroupName"]=definition.groupName,
                    ["authoredCleanGroupName"]=definition.groupName==null?null:GameStageGroup.CleanName(definition.groupName),
                    ["minimum"]=new JArray(minimum.x,minimum.y,minimum.z),
                    ["maximum"]=new JArray(maximum.x,maximum.y,maximum.z)
                };
                if(room!=null)
                {
                    item["samePrefab"]=ReferenceEquals(room.prefabInstance,scannerPoi);
                    item["listed"]=scannerPoi.sleeperVolumes.Contains(room);
                    item["boundsMatch"]=room.BoxMin.Equals(minimum)&&room.BoxMax.Equals(maximum);
                    item["runtimeMin"]=room.spawnCountMin;item["runtimeMax"]=room.spawnCountMax;item["runtimeFlags"]=room.flags;
                    item["runtimeGroupId"]=room.groupId;item["runtimeGroupName"]=room.groupName;
                    item["cleared"]=room.wasCleared;item["spawning"]=room.isSpawning;item["numSpawned"]=room.numSpawned;
                    item["respawnIds"]=new JArray(room.respawnMap.Keys);
                    item["pendingIds"]=new JArray(room.pendingSpawnMap);
                    item["pendingOperations"]=room.pendingSpawnOps.Count;
                    item["unfinishedOperations"]=System.Linq.Enumerable.Count(room.pendingSpawnOps,h=>h==null||!h.IsCompleted||h.onComplete!=null);
                    item["scriptRunning"]=room.minScript!=null&&room.minScript.IsRunning();
                }
                registration.Add(item);
            }
            result["authoredRoomRegistration"]=registration;
        }
        result["scannerOccupancyFound"]=scannerPoi?.prefab!=null;
        if(scannerPoi?.prefab!=null)
        {
            var scannerOrigin=scannerPoi.boundingBoxPosition;var scannerSize=scannerPoi.boundingBoxSize;
            result["scannerOccupancy"]=new JObject {
                ["prefab"]=scannerPoi.prefab.PrefabName,
                ["origin"]=new JArray(scannerOrigin.x,scannerOrigin.y,scannerOrigin.z),
                ["size"]=new JArray(scannerSize.x,scannerSize.y,scannerSize.z),
                ["verticalOffset"]=player.position.y-scannerOrigin.y,
                ["withinNativeHeight"]=player.position.y>=scannerOrigin.y&&player.position.y<scannerOrigin.y+scannerSize.y,
                ["matchesNativeOwner"]=ReferenceEquals(scannerPoi,prefab)
            };
        }
        var nativePrefab=player.prefab;
        var nativeAuthored=nativePrefab?.prefab;
        var locationTags=new JArray();
        foreach(string tag in new[]{"part","streettile","navonly","hideui"})
            if(nativeAuthored!=null&&nativeAuthored.Tags.Test_AnySet(FastTags<TagGroup.Poi>.Parse(tag)))locationTags.Add(tag);
        bool insideInfoArea=nativeAuthored!=null&&nativePrefab.IsWithinInfoArea(player.position);
        result["locationDiagnostic"]=new JObject {
            ["nativePlayerPrefab"]=nativeAuthored?.PrefabName,
            ["geometricPrefab"]=geometricPrefab?.prefab?.PrefabName,
            ["nativeResolvedPrefab"]=prefab?.prefab?.PrefabName,
            ["insideNativeInfoArea"]=insideInfoArea,
            ["nativeInfoVolumeCount"]=nativeAuthored?.InfoVolumeList?.Count??0,
            ["excludedNativeTags"]=locationTags,
            ["standingBiome"]=player.biomeStandingOn?.m_sBiomeName,
            ["nativePoiNameEligible"]=nativeAuthored!=null&&insideInfoArea&&locationTags.Count==0
        };
        RebirthPoiNativeManifest manifest;
        result["insidePrefab"] = prefab?.prefab?.PrefabName;
        bool resolved = RebirthPoiNativeManifest.TryResolve(world, prefab, out manifest);
        result["manifestResolved"] = resolved;
        if (resolved)
        {
            result["poiKey"] = manifest.Identity.Key; result["nativeRooms"] = manifest.Volumes.Length;
            var nativeVolumes = new JArray();
            foreach (var volume in manifest.Volumes)
                nativeVolumes.Add(new JObject {
                    ["id"] = world.FindSleeperVolume(volume.BoxMin, volume.BoxMax),
                    ["minimum"] = new JArray(volume.BoxMin.x, volume.BoxMin.y, volume.BoxMin.z),
                    ["maximum"] = new JArray(volume.BoxMax.x, volume.BoxMax.y, volume.BoxMax.z),
                    ["cleared"] = volume.wasCleared, ["spawning"] = volume.isSpawning,
                    ["numSpawned"] = volume.numSpawned, ["respawnIds"] = new JArray(volume.respawnMap.Keys),
                    ["respawnList"] = new JArray(volume.respawnList),
                    ["pendingIds"] = new JArray(volume.pendingSpawnMap),
                    ["pendingOperations"] = volume.pendingSpawnOps.Count,
                    ["spawnDelay"] = volume.spawnDelay
                });
            result["nativeVolumes"] = nativeVolumes;
            var triggerBoxes = new JArray();
            var origin = prefab.boundingBoxPosition;
            for (int index = 0; index < prefab.prefab.TriggerVolumeList.Count; index++)
            {
                var trigger = prefab.prefab.TriggerVolumeList[index];
                if (!trigger.Used) continue;
                var minimum = origin + trigger.startPos;
                var maximum = minimum + trigger.size;
                triggerBoxes.Add(new JObject {
                    ["minimum"] = new JArray(minimum.x, minimum.y, minimum.z),
                    ["maximum"] = new JArray(maximum.x, maximum.y, maximum.z),
                    ["triggers"] = new JArray(trigger.TriggersIndices)
                });
            }
            result["authoredTriggerBoxes"] = triggerBoxes;
            RebirthPoiClearanceRecord record;
            if (snapshot != null && snapshot.TryGet(manifest.Identity, out record))
            {
                result["record"] = new JObject {
                    ["state"] = record.State.ToString(), ["epoch"] = record.Epoch,
                    ["revision"] = record.Revision, ["resetOnly"] = record.ResetOnly,
                    ["resetId"] = record.ResetId.ToString("N"),
                    ["verifiedRooms"] = record.Clear?.VerifiedVolumes,
                    ["spawnedParticipants"] = record.Clear?.SpawnedParticipants
                };
            }
        }
        // Read the actual published projection, not history.Get (which can recover
        // and mutate history). Keep scanner-only POIs observable if resolution fails.
        string mapKey = resolved ? manifest.Identity.Key : view?.PoiKey;
        var mapRecords = RebirthPoiMapSync.LocalMap;
        RebirthPoiMapRecord mapRecord;
        result["mapRevision"] = RebirthPoiMapSync.LocalRevision;
        result["mapRecordFound"] = false;
        if (!string.IsNullOrEmpty(mapKey) && mapRecords != null && mapRecords.TryGetValue(mapKey, out mapRecord))
        {
            result["mapRecordFound"] = true;
            result["mapRecord"] = new JObject {
                ["poiKey"] = mapRecord.Identity.Key, ["epoch"] = mapRecord.Epoch,
                ["state"] = mapRecord.State.ToString(),
                ["areasLeft"] = mapRecord.AreasLeft, ["estimatedEntities"] = mapRecord.EstimatedEntities,
                ["approximate"] = mapRecord.Approximate,
                ["discoverer"] = mapRecord.Discoverer, ["contributors"] = mapRecord.Contributors,
                ["discoveredAt"] = mapRecord.DiscoveredAt, ["purgedAt"] = mapRecord.PurgedAt
            };
            if(snapshot!=null&&snapshot.TryGet(mapRecord.Identity,out var savedRecord))
            {
                int enrolled=0,deaths=0;
                var savedRooms=new JArray();
                if(savedRecord.Observations!=null)foreach(var room in savedRecord.Observations.Volumes.Values)
                {
                    int dead=0;foreach(var actor in room.Actors.Values)
                        if(actor.Dead&&actor.TerminalKind==RebirthPoiActorTerminalKind.Death)dead++;
                    enrolled+=room.Actors.Count;deaths+=dead;
                    savedRooms.Add(new JObject{["nativeId"]=room.NativeVolumeId,["enrolled"]=room.Actors.Count,["deathReceipts"]=dead});
                }
                result["savedEvidence"]=new JObject{["state"]=savedRecord.State.ToString(),["epoch"]=savedRecord.Epoch,
                    ["revision"]=savedRecord.Revision,["enrolled"]=enrolled,["deathReceipts"]=deaths,["rooms"]=savedRooms};
            }
        }
        var biomes=new JArray();
        var progress=RebirthPurgeObjectiveProgress.Instance;
        if(progress.Published!=null)foreach(var value in progress.Published.Values)
            biomes.Add(new JObject{["biome"]=value.Biome,["eligible"]=value.Eligible,["discovered"]=value.Discovered,
                ["cleared"]=value.Cleared,["eligibleUnits"]=value.EligibleUnits,["clearedUnits"]=value.ClearedUnits});
        result["progression"]=new JObject{["revision"]=progress.Revision,["targetPercent"]=progress.TargetPercentage,["biomes"]=biomes};
        var milestones=new JArray();
        var receipts=RebirthPurgeMilestoneService.PublishedAll;
        if(receipts!=null)foreach(var receipt in receipts.Values)
            milestones.Add(new JObject{["biome"]=receipt.Biome,["id"]=receipt.Id,["tier"]=receipt.Tier,["percent"]=receipt.Percent,["revision"]=receipt.Revision});
        result["milestones"]=milestones;
        result["localPreferenceScopeReady"]=RebirthHudTrackingPreferenceStore.TryResolveCurrentContext(out var preferenceScope,out var preferenceError);
        result["localPreferenceWorld"]=preferenceScope?.WorldKey??preferenceError;
        string key = RebirthPurgeKillContributor.Identify(player, world);
        var drops=new JArray();
        var air=world.aiDirector?.GetComponent<AIDirectorAirDropComponent>();
        if(air!=null&&air.supplyCrates.Count<=4096)
        {
            System.Guid.TryParse(world.worldState?.Guid,out var nativeWorld);
            foreach(var cache in air.supplyCrates)
            {
                var crate=world.GetEntity(cache.entityId) as EntitySupplyCrate;
                RebirthPurgeSupplyCrateStamp stamp;
                bool owned=crate!=null&&RebirthPurgeSupplyCrateSerialization.TryReadAuthoritative(crate,nativeWorld,out stamp)&&stamp.Player==key;
                drops.Add(new JObject{["id"]=cache.entityId,["loaded"]=crate!=null,["ownedLoadedCrate"]=owned,
                    ["custody"]=RebirthPurgeSupplyCrateSerialization.CaptureAudit(crate,key),
                    ["position"]=new JArray(cache.blockPos.x,cache.blockPos.y,cache.blockPos.z)});
                if(drops.Count>=256)break;
            }
        }
        result["nativeSupplyCrates"]=drops;
        result["nativeSupplyFlightActive"]=air?.activeAirDrop!=null;
        var accounts = RebirthPurgeSupplyService.Published;
        RebirthPurgeSupplyAccount account = null;
        result["supplyReady"] = accounts != null;
        if (key != null && accounts != null && accounts.TryGetValue(key, out account))
            result["supply"] = new JObject {
                ["observedCredits"] = account.ObservedCredits, ["spentCredits"] = account.SpentCredits,
                ["earnedDrops"] = account.EarnedDrops, ["deliveredDrops"] = account.DeliveredDrops,
                ["inFlight"] = account.InFlight,["deliveryPhase"]=account.Delivery?.Phase.ToString()??"none"
            };
        var rules = RebirthPurgeSupplyPolicy.Current;
        int equippedTier = RebirthPurgeSupplyEquipment.Tier(player);
        var hands = player.equipment?.GetSlotItem((int)EquipmentSlots.Hands);
        var modifications = new JArray();
        if (hands?.modifications != null)
            foreach (var modification in hands.modifications)
                if (modification != null && !modification.IsEmpty())
                    modifications.Add(modification.ItemClass?.GetItemName() ?? "unresolved-native-item");
        long balance = account == null ? 0 : account.ObservedCredits - account.SpentCredits;
        var modifierDefinitions = new JArray();
        for (int tier = 1; tier <= 5; tier++)
        {
            string name = "ModPurgeTier" + tier;
            var definition = ItemClass.GetItemClass(name);
            var recipe = CraftingManager.GetRecipe(name);
            var ingredients = new JArray();
            if (recipe?.ingredients != null) foreach (var ingredient in recipe.ingredients)
                if (ingredient?.itemValue != null)
                    ingredients.Add(new JObject { ["name"] = ingredient.itemValue.ItemClass?.GetItemName(), ["count"] = ingredient.count });
            modifierDefinitions.Add(new JObject { ["name"] = name, ["itemLoaded"] = definition != null,
                ["recipeLoaded"] = recipe != null, ["area"] = recipe?.craftingArea,
                ["outputCount"] = recipe == null ? JValue.CreateNull() : new JValue(recipe.count), ["ingredients"] = ingredients });
        }
        result["supplyRules"] = new JObject {
            ["ready"] = rules != null, ["accountPresent"] = account != null,
            ["equippedTier"] = equippedTier,
            ["handsItem"] = hands == null || hands.IsEmpty() ? "" : hands.ItemClass?.GetItemName(),
            ["handsMods"] = modifications,
            ["nativeDisableSmelter"] = XUiM_Recipes.DisableSmelter,
            ["modifierDefinitions"] = modifierDefinitions,
            ["baseKills"] = rules == null ? JValue.CreateNull() : new JValue(rules.BaseKills),
            ["increment"] = rules == null ? JValue.CreateNull() : new JValue(rules.Increment),
            ["maximumBonus"] = rules == null ? JValue.CreateNull() : new JValue(rules.MaximumBonus),
            ["earnedDrops"] = account?.EarnedDrops ?? 0, ["unspentCredits"] = balance,
            ["effectiveNextTarget"] = rules == null ? JValue.CreateNull() : new JValue(rules.Target(account?.EarnedDrops ?? 0, equippedTier)),
            ["hudKnown"] = player.Buffs.HasCustomVar("_rbPurgeSupplyKnown") && player.Buffs.GetCustomVar("_rbPurgeSupplyKnown") != 0,
            ["hudTarget"] = player.Buffs.HasCustomVar("_rbPurgeSupplyTarget") ? new JValue(player.Buffs.GetCustomVar("_rbPurgeSupplyTarget")) : JValue.CreateNull(),
            ["hudRemaining"] = player.Buffs.HasCustomVar("_rbPurgeSupplyRemaining") ? new JValue(player.Buffs.GetCustomVar("_rbPurgeSupplyRemaining")) : JValue.CreateNull()
        };
        SdtdConsole.Instance.Output(result.ToString(Formatting.None));
    }
}
