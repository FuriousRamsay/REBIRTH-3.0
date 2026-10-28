using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// One main-thread observation, through the existing authenticated /console route.
// Does not pulse services, activate rooms, modify records or force chunk loading.
public sealed class ConsoleCmdRebirthPurgeObservation : ConsoleCmdAbstract
{
    public override string[] getCommands() => new[] { "rbpurgetestobserve" };
    public override string getDescription() => "Collect current Purge test evidence as JSON.";
    public override string getHelp() => "rbpurgetestobserve (read-only, local Game Bridge only).";
    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        var world = GameManager.Instance?.World;
        var player = world?.GetPrimaryPlayer() as EntityPlayerLocal;
        if (args.Count != 0 || sender.RemoteClientInfo != null || world == null || world.IsRemote()
            || player == null || !RebirthGameBridge.IsRunning)
        { SdtdConsole.Instance.Output("*** ERROR " + getHelp()); return; }
        var result = new JObject {
            ["schema"] = 1, ["observationOnly"] = true,
            ["releaseEnabled"] = RebirthPurgeReleasePolicy.Enabled,
            ["purgeEnabled"] = RebirthSandboxOptionManager.Current.IsPurge,
            ["census"] = RebirthPurgePoiCensus.Instance.Status,
            ["scannerDiagnostic"] = RebirthPoiNativeEvidence.Instance.ScannerStatus(world, player),
            ["roomHud"] = RebirthPurgeScanner.Hud(player),
            ["lifetimeHud"] = RebirthPurgeScanner.LifetimeText(player),
            ["position"] = new JArray(player.position.x, player.position.y, player.position.z),
            ["hostileAmbientAnimalsSuppressed"] = ConsoleCmdRebirthPurgeTestSetup.SuppressHostileAnimals(world)
        };
        var view = RebirthPurgeScanner.Observe(player);
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
        }
        string key = RebirthPurgeKillContributor.Identify(player, world);
        var accounts = RebirthPurgeSupplyService.Published;
        RebirthPurgeSupplyAccount account;
        result["supplyReady"] = accounts != null;
        if (key != null && accounts != null && accounts.TryGetValue(key, out account))
            result["supply"] = new JObject {
                ["observedCredits"] = account.ObservedCredits, ["spentCredits"] = account.SpentCredits,
                ["earnedDrops"] = account.EarnedDrops, ["deliveredDrops"] = account.DeliveredDrops,
                ["inFlight"] = account.InFlight
            };
        SdtdConsole.Instance.Output(result.ToString(Formatting.None));
    }
}
