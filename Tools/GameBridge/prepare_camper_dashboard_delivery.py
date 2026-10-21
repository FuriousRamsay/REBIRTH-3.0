"""Prepare the explicit camper dashboard revision overlay; no build/game actions."""
import hashlib,json,argparse
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
EVIDENCE=Path('E:/_Haven/shared/REBIRTHTemp/CamperTraceBuild')
parser=argparse.ArgumentParser()
parser.add_argument('--verification',default='dashboard-lighting-verification-20261015-b.json')
parser.add_argument('--manifest',default='dashboard-lighting-reviewed-overlay-20261015.json')
parser.add_argument('--candidate',default=str(EVIDENCE/'RebirthUtils.dll'))
args=parser.parse_args()
verified=json.loads((EVIDENCE/args.verification).read_text(encoding='utf-8-sig'))
assert verified['allowInstall'] and verified['buildExitCode']==0
def row(path):
    data=path.read_bytes()
    return dict(path=path.relative_to(ROOT).as_posix(),bytes=len(data),sha256=hashlib.sha256(data).hexdigest())
fence=[]
for original in verified['sourceAfter']:
    current=row(Path(original['path']))
    assert current['sha256']==original['sha256'].lower()
    fence.append(current)
fence.sort(key=lambda r:r['path'])
fence_hash=hashlib.sha256(json.dumps(fence,sort_keys=True,separators=(',',':')).encode()).hexdigest()
# Runtime overlay for this revision, not a replacement for the whole mod.
paths={ROOT/'RebirthUtils.dll',ROOT/'Config/Localization.csv',ROOT/'Config/Localization.txt',ROOT/'Config/RebirthCamperEnergy.xml',ROOT/'Config/_Survivor/background_bonuses.xml'}
paths.update({ROOT/'Scripts/NPC/Inventory/RebirthNpcExternalInventoryGateway.cs',ROOT/'Scripts/Vehicles/Restoration/RebirthVehicleRestorationSystem.cs'})
for name in ['NetPackageRebirthBlockPickupRequest.cs','NetPackageRebirthWorkstationSecurityAction.cs','NetPackageRebirthRenameContainer.cs','NetPackageRebirthWorkstationOwnerRequest.cs','NetPackageRebirthContainerNameSync.cs','RebirthMetabolismNetPackages.cs','NetPackageRebirthQuestGracePartyComplete.cs','NetPackageRebirthQuestGraceUpdate.cs','NetPackageRebirthWorkstationOwnerSync.cs','NetPackageRebirthQuestGraceSync.cs']:
    paths.add(ROOT/'Scripts/Network'/name)
paths.add(ROOT/'Scripts/GameBridge/RebirthGameBridgeConsumptionCodec.cs')
paths.add(ROOT/'Scripts/Crafting/Cooking/XUiC_RebirthCookingWorkspace.cs')
paths.add(ROOT/'_Documentation/Cooking/PULL_CAPACITY_20261014.txt')
paths.add(ROOT/'Scripts/TraderJobs/RebirthTraderJobs.cs')
paths.add(ROOT/'_Documentation/TraderJobs/FETCH_CONTAINER_IDENTITY_CONSISTENCY_20261014.txt')
for relative in ['Scripts/Network/RebirthHeatMapNetPackages.cs',
                 'Scripts/UI/XUiC_RebirthTargetBar.cs',
                 'Scripts/UI/XUiC_RebirthCompassWindow.cs',
                 'Config/XUi_InGame/windows.xml',
                 'Scripts/Network/NetPackageRebirthSandboxCode.cs',
                 'Scripts/UI/XUiC_RebirthHeatMapHud.cs',
                 'Scripts/Metabolism/RebirthMetabolismService.cs',
                 'Scripts/Companions/RebirthDroneRename.cs',
                 'Scripts/Crafting/RemoteCrafting/RemoteResourceToggleCommand.cs',
                 'Scripts/Crafting/RemoteCrafting/RemoteResourceToggleLegacyPackages.cs']:
    paths.add(ROOT/relative)
for name in ['HEATMAP_NETWORK_ADMISSION_20261014.txt',
             'MultiplayerAudit/SANDBOX_CLIENT_TRANSPORT_SCOPE_20261014.txt',
             'MultiplayerAudit/METABOLISM_PROJECTION_WORLD_SCOPE_20261014.txt',
             'MultiplayerAudit/DRONE_RENAME_CURRENT_SESSION_20261014.txt',
             'REMOTE_RESOURCE_TOGGLE_ADMISSION_20261014.txt']:
    paths.add(ROOT/'_Documentation'/name)
paths.update((ROOT/'Scripts/Metabolism/Consumption').glob('*.cs'))
# Keep the recovery design and its limitations with the source overlay.
for name in ['MultiplayerAudit/VEHICLE_CLIENT_CURSOR_CUSTODY_20261014.txt',
             'MultiplayerAudit/CURSOR_RECOVERY_DELIVERY_20261014.txt',
             'MultiplayerAudit/VEHICLE_CLIENT_CUSTODY_INTEGRATION_REVIEW_20261014.txt',
             'VEHICLE_CURSOR_DROP_PLAN_20261014.txt',
             'MultiplayerAudit/VEHICLE_CURSOR_NATIVE_GEAR_WITNESS_20261014.txt',
             'MultiplayerAudit/VEHICLE_CURSOR_NATIVE_RETURN_PLAN_20261014.txt',
             'VEHICLE_CURSOR_NATIVE_RETURN_PLAN_20261014.txt',
             'VEHICLE_CURSOR_DESTINATION_WITNESS_20261014.txt']:
    paths.add(ROOT/'_Documentation'/name)
# The cursor recovery implementation spans native UI, receipt and protocol helpers.
# Include its explicit source family in the next reviewed overlay, after its build fence.
for pattern in ['RebirthVehicleClientCustody*.cs','RebirthVehicleCursor*.cs',
                'RebirthVehicleCustodyUiGeneration.cs','IRebirthVehicleCursorDestinationWitness.cs',
                'NetPackageRebirthVehicleCustodySnapshot.cs']:
    paths.update((ROOT/'Scripts/Vehicles/Restoration').glob(pattern))
for name in ['CONSUMPTION_CHARACTER_PUBLICATION_V2_20261014.txt','CLIENT_OWNER_RECONNECT_PROJECTION_SCOPE_20261014.txt','QUEST_GRACE_PACKET_CURRENT_SCOPE_20261014.txt','CONSUMPTION_CURRENT_SESSION_20261014.txt','CONTAINER_OWNER_CURRENT_SESSION_20261014.txt','BLOCK_PICKUP_SECURITY_CURRENT_SESSION_20261014.txt']:
    paths.add(ROOT/'_Documentation/MultiplayerAudit'/name)
for directory in ['Scripts/Vehicles/Camper','Config/_Vehicles','_Documentation/Camper/Energy']:
    paths.update(p for p in (ROOT/directory).rglob('*') if p.is_file() and p.suffix in {'.cs','.xml','.csv','.md','.txt'})
plan=dict(schema=1,reviewed=True,scope='changed-files-overlay',deleted=[],files=sorted((row(p) for p in paths),key=lambda r:r['path']),sourceFence=fence,sourceFenceSha256=fence_hash,
    dllCandidate=dict(path=args.candidate,sha256=row(ROOT/'RebirthUtils.dll')['sha256'],sourceFenceSha256=fence_hash))
destination=EVIDENCE/args.manifest
with destination.open('x',encoding='utf-8') as stream:json.dump(plan,stream,indent=2)
print(destination)
