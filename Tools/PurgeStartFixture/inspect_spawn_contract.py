"""Static lifecycle audit; does not claim native game execution."""
from pathlib import Path
import json
import argparse

root = Path(__file__).resolve().parents[2]
source = (root / 'Scripts/Purge/Runtime/RebirthPurgeTruckStart.cs').read_text(encoding='utf-8')
packet = (root / 'Scripts/Purge/Network/NetPackageRebirthPurgeStartReadiness.cs').read_text(encoding='utf-8')
road=(root/'Scripts/Purge/Runtime/RebirthPurgeForestRoad.cs').read_text(encoding='utf-8')
parser=argparse.ArgumentParser()
parser.add_argument('--native-source',type=Path,default=root/'Tools/CamperTurretNativeInspect/move-purge-load.txt',help='Exact target PlayerMoveController source export, when using a different assembly revision')
native_path=parser.parse_args().native_source
native = native_path.read_text(encoding='utf-8-sig')
native_respawn = native[native.index('public void updateRespawn()'):native.index('public bool CheckWorldReadyForSpawn()')]
update = source[source.index('private static void Update('):source.index('private static bool Clear(')]
clear = source[source.index('private static bool Clear('):source.index('[HarmonyPatch(typeof(SpawnPointList)')]
checks = {
    'native readiness occurs before Spawned and loading close': native_respawn.index('if (!CheckWorldReadyForSpawn())') < native_respawn.index('entityPlayerLocal.Spawned = true') < native_respawn.index('XUiC_ProgressWindow.Close'),
    'actual native readiness method is installed as Harmony gate': 'harmony.CreateClassProcessor(typeof(RebirthPurgeInitialWorldReadiness)).Patch()' in source and '[HarmonyPatch(typeof(PlayerMoveController),nameof(PlayerMoveController.CheckWorldReadyForSpawn))]' in source,
    'truck preparation precedes spawn and character commit waits': update.index('truck.create-bare') < update.index('wait.native-spawn-with-truck-ready') < update.index('wait.character-creation') < update.index('driver.attach-request'),
    'separate sampled player staging anchor rather than shared truck origin': 'RebirthPurgeForestRoad.PlayerAnchor(_world,road,out var player)' in source and '__result=player' in source and '*10;' in road and 'point.y=provider.m_TerrainGenerator.GetTerrainHeightAt' in road,
    'actual wheel contacts and loaded collision qualify readiness': 'wheel.wheelC.GetGroundHit(out var hit)' in source and 'chunk.IsCollisionMeshGenerated' in source and 'job.Stability.Observe(now,Stable(vehicle))' in update,
    'post-attachment native owner stability precedes reveal': update.index('job.Stability.Reset();job.Ready=false') < update.index('wait.attached-truck-grounded-and-stable') < update.index('gameplay.enable') and 'clientStability.Observe(Time.realtimeSinceStartup,Stable(driven))' in source,
    'remote handoff requires actual native spawn event rather than IsSpawned': 'job.NativeSpawnObserved=true' in source and 'NativeSpawnObserved=nativeSpawnObserved.Contains(player.entityId)' in source and 'RebirthPurgeStartHandoff.CanRun(job.NativeSpawnObserved,p is EntityPlayerLocal,p.IsSpawned())' in update,
    'prepared vehicle identity is persisted before world publication': update.index('record.Vehicle=vehicle.entityId;Write(record)') < update.index('job.World.SpawnEntityInWorld(vehicle)'),
    'cover is restored after native move update': 'private static void Postfix(PlayerMoveController __instance){if(RebirthPurgeTruckStart.Enabled)RebirthPurgeTruckStart.PresentStart' in source and 'player.EnableCamera(false)' in source,
    'creator can finish without an overlaid progress popup': 'manager.IsWindowOpen(XUiC_RebirthSurvivorCreator.WindowGroupId)' in source and 'if(creatorOpen||player.IsSpawned()&&RebirthCharacterCreationHoldService.IsHeld(player))' in source and source.index('if(creatorOpen||') < source.index('string text=Localization.Get'),
    'intro suppression belongs only to the actual starter': '&&RebirthPurgeTruckStart.OwnsInitialStart(__instance)?__instance:null' in source,
    'complete voxel intersection helper is used': 'RebirthPurgeStartFootprint.IntersectsVoxel' in clear,
    'multiblock child cells are refused before air shortcut': clear.index('if(block.ischild)return false') < clear.index('if(block.isair)continue'),
    'dynamic entity pass excludes only intended player': 'foreach(var entity in world.Entities.list)' in clear and 'ReferenceEquals(entity,owner)' in clear,
    'remote query is direction-authenticated and actor checked': 'PackageDirection=>NetPackageDirection.ToServer' in packet and 'ValidEntityIdForSender(actor)' in packet and 'Clients.ForEntityId(actor),sender' in source,
    'readiness receivers fail closed off the main thread': source[source.index('internal static void Query('):source.index('internal static void PrepareRemote(')].count('!ThreadManager.IsMainThread()') == 2,
    'remote status uses authoritative client world identifier': source.count('GamePrefs.GetString(EnumGamePrefs.GameGuidClient)') == 2,
    'reply cannot regress or replace current truck': 'if(state<clientState||clientVehicle>0&&vehicle!=clientVehicle)return' in source,
    'completed presentation retires before later dismounts': 'clientState=5' in source and 'clientState!=5' in source,
    'failed road search and loaded obstruction have bounded retry clocks': 'nextPreparation[player.entityId]=now+5' in source and 'job.Next=now+2' in source and 'RetryFailedSearch(world)' in source,
}
result = {'kind': 'static source contract audit, not gameplay', 'native_source': str(native_path), 'checks': checks, 'pass': all(checks.values())}
print(json.dumps(result, indent=2))
assert result['pass'], 'Spawn contract static check failed'
