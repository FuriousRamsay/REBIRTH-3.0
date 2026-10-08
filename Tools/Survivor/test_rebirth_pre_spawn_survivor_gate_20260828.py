#!/usr/bin/env python3
from pathlib import Path
import xml.etree.ElementTree as ET
import sys

ROOT=Path(__file__).resolve().parents[2]
errors=[]
def text(rel): return (ROOT/rel).read_text(encoding='utf-8',errors='replace')
def check(cond,msg):
    if not cond: errors.append(msg)

for f in (ROOT/'Config').rglob('*.xml'):
    try: ET.parse(f)
    except Exception as e: errors.append(f'XML parse failed {f.relative_to(ROOT)}: {e}')

# Character Progression root routing remains explicit for every paired gameplay document.
base={p.name for p in (ROOT/'Config/_Base').glob('*.xml')}
rebirth={p.name for p in (ROOT/'Config/_Rebirth').glob('*.xml')}
check(base==rebirth, f'_Base/_Rebirth file sets differ: base={sorted(base)} rebirth={sorted(rebirth)}')
for name in sorted(base):
    root_name='items.xml' if name=='items_starter.xml' else name
    t=text('Config/'+root_name)
    check("character_progression('Rebirth')" in t, f'{root_name} missing character_progression(Rebirth)')
    check(f'_Base/{name}' in t and f'_Rebirth/{name}' in t, f'{root_name} missing both routes for {name}')

# First spawn is one modified native spawnselection window. The approved concept keeps READY-only
# profile selection, rich summary, native multiplayer friend selection, Create/Cancel/Spawn, and
# the final Spawn action inside that one group. There is no second SELECT button or fake Spawn cover.
win=text('Config/XUi_Menu/windows.xml')
check('xpath="/windows/window[@name=\'spawnselection\']/rect[@name=\'buttons\']"' in win,
      'profile browser is not appended directly inside native spawnselection/buttons')
for token in ['rebirthSurvivorSpawnGate','spawnProfileRow0','spawnProfileRow3',
              'btnSpawnProfileCreate','spawnSelectedPlayerPreview','spawnPlayerModelTexture',
              'spawnProfileDetailIcon0','spawnProfileDetailIcon18','spawnProfileTraitNativeScrollHost',
              'spawnProfileStartingItemRow5','rebirthSpawnOptionsPanel','btnRebirthSpawnModeRandom',
              'btnRebirthSpawnModeFriend','spawnFriendSelectionHint','btnRebirthSpawnCancel',
              'btnRebirthSpawnAction','SPAWN']:
    check(token in win, 'approved native spawnselection concept missing '+token)
check('btnSpawnProfileSelect' not in win,
      'obsolete separate SELECT PROFILE button remains; clicking a READY profile must select it directly')
check('rebirthSpawnLockedCover' not in win,
      'obsolete fake Spawn lock cover remains')
check('spawnProfileRow4' not in win,
      'spawnselection should use the compact four-row profile list')
check('btnOpenSurvivorProfilesForSpawn' not in win,
      'obsolete Choose/Create handoff button still exists')
check('controller="RebirthSpawnSelectionSurvivorGate, RebirthUtils"' in win,
      'native spawnselection integrated browser controller missing')
check('controller="SDCSPreviewWindow"' not in win[win.find('rebirthSurvivorSpawnGate'):win.find('</append>', win.find('rebirthSurvivorSpawnGate'))],
      'spawnselection must not embed native SDCSPreviewWindow outside its required PlayerProfile hierarchy')
check('controller="SpawnNearFriendsList"' not in win or 'boxSpawnNearFriend' in win,
      'native multiplayer friend selector contract unexpectedly removed')

# Controller shows only profiles compatible with current definitions, renders the normal-manager
# style live player/model + icon summary, and stages ONLY after explicit Select.
gate=text('Scripts/Survivor/UI/XUiC_RebirthSpawnSelectionSurvivorGate.cs')
for token in ['VisibleRows = 4','StartingItemVisibleRows = 6',
              'RebirthSurvivorProfileStore.GetProfilesSnapshot()',
              'RebirthSurvivorProfileCompatibilityKind.Ready','ready.Add(p)',
              'StageExistingProfile(profile, out error)','StageSelectedProfile(profiles[index])',
              'btnSpawnFirstTime','btnSpawn','btnSpawnNearFriend','boxSpawnNearFriend',
              'ApplyNativeSpawnLock','OpenCreateForSpawnSelection','SELECTED FOR THIS WORLD',
              'RebirthPlayerProfileModelBinder','spawnPlayerModelTexture',
              'SpawnAction_OnPressed','TryInvokeButtonPress','RenderSelectedProfileDetails',
              'windowGroup.isEscClosable','ApplyNativeLayout']:
    check(token in gate, 'integrated spawn browser missing '+token)
check('OpenForSpawnSelection(xui)' not in gate,
      'spawnselection still opens the separate Survivor Profile Manager')
check('ResetActionSets' not in gate and 'SetNavigationLockView' not in gate,
      'integrated browser must not own/reset the native spawn action stack')
check('ShouldBlockNativeClose' not in gate and 'BeginIntentionalNativeClose' not in gate,
      'obsolete GUIWindowManager close interception shims remain')

# Existing full creation workflow opens modally ABOVE the still-open native spawnselection. This is
# the same proven lifecycle used by the normal Profile Manager and prevents the blank loading screen.
creator=text('Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs')
for token in ['OpenCreateForSpawnSelection','spawnselection-create-profile',
              'manager.Open((GUIWindow)controller.windowGroup, false, true)',
              'spawnselection creator OVERLAY open before[spawn=']:
    check(token in creator, 'spawnselection creator-return contract missing '+token)
check('manager.Close("spawnselection")' not in creator,
      'spawnselection must remain open underneath the modal Survivor Creator')
check('StageNewSurvivor' not in gate,
      'creating a reusable profile from spawnselection must not auto-stage it')
check('name="rebirthSurvivorCreatorWindow"' in win and 'depth="300" controller="RebirthSurvivorCreator, RebirthUtils"' in win,
      'Survivor Creator must render above the depth-250 native spawnselection window')
bridge=text('Scripts/Survivor/UI/RebirthNativePlayerProfileBridge.cs')
model=text('Scripts/Survivor/UI/RebirthPlayerProfileModelBinder.cs')
check('TryResolvePlayerProfileArchetype' in bridge and 'PlayerProfile.LoadLocalProfile()' in bridge and 'CreateTempArchetype()' in bridge,
      'named reusable Player Profiles are not resolved through their actual native PlayerProfile/SDCS appearance')
check('TryResolvePlayerProfileArchetype' in model and 'SDCSUtils.CreateVizUI' in model,
      'spawnselection full-player model renderer is not using actual Player Profile SDCS data')
check('native spawn button PRESS' in gate,
      'native Spawn button press diagnostics are missing')
check('<setattribute xpath="/windows/window[@name=\'spawnselection\']" name="height">1010</setattribute>' in win and
      '<setattribute xpath="/windows/window[@name=\'spawnselection\']/rect[@name=\'buttons\']/table[@name=\'content\']" name="pos">810,-665</setattribute>' in win and
      'name="rebirthSurvivorSpawnGate" pos="0,-55" width="1460" height="900"' in win,
      'approved spawnselection geometry was not applied')
check('btnSpawnProfileLockBlocker' not in win,
      'obsolete clickable lock-cover blocker remains')

first=text('Scripts/Survivor/UI/RebirthSurvivorFirstEntryUiService.cs')
for token in ['RegisterNativeSpawnSelectionHost','ShouldShowSpawnProfileSelector',
              'StagedPreSpawnProfileId','StageExistingProfile','DispatchStagedCommit',
              'creationUiRequired || stagedSelectionReady','post-spawn owner already Ready; standalone chooser fallback suppressed']:
    check(token in first, 'first-entry service missing '+token)

# ESC ownership is now native XUi window-group state, not brittle Harmony Close interception.
patches=text('Scripts/Survivor/UI/RebirthSurvivorPreSpawnPatches.cs')
check('nameof(GUIWindowManager.Open)' not in patches and 'nameof(GUIWindowManager.Close)' not in patches,
      'GUIWindowManager Open/Close interception must not return')
check('RebirthSurvivorSpawnSelectionCloseGuardPatch' not in patches and 'RebirthSurvivorSpawnSelectionCloseWindowGuardPatch' not in patches,
      'obsolete spawnselection close guard patch remains')
for token in ['nameof(GameManager.PlayerSpawnedInWorld)','NotifyNativePlayerSpawned']:
    check(token in patches, 'pre-spawn staged commit patch missing '+token)
installer=text('Scripts/Survivor/UI/RebirthSurvivorUiInstaller.cs')
check('RebirthSurvivorSpawnSelectionCloseGuardPatch' not in installer and 'RebirthSurvivorSpawnSelectionCloseWindowGuardPatch' not in installer,
      'UI installer still installs obsolete GUIWindowManager Close guards')
check('RebirthSurvivorNativePlayerSpawnedPatch' in installer,
      'UI installer does not install staged commit hook')

persistence=text('Scripts/Survivor/Persistence/RebirthSurvivorPersistenceLifecycle.cs')
metrepo=text('Scripts/Metabolism/RebirthMetabolismStateRepository.cs')
creation=text('Scripts/Survivor/Creation/RebirthSurvivorCreationService.cs')
check('RebirthMetabolismStateRepository.Reset(data.AsServer)' in persistence,
      'metabolism persistence authority is not rebound at GameStarting')
check('RebirthMetabolismStateRepository.Reset(false)' in persistence,
      'metabolism repository is not cleared on world shutdown')
check('public static bool IsServerAuthority' in metrepo,
      'metabolism repository authority diagnostic surface missing')
check('RebirthMetabolismStateRepository.SetServerAuthority(true)' in creation and
      'creation precommit authority worldRepo=' in creation,
      'creation commit does not defensively synchronize/log metabolism authority')

# Pre-world logging stays enabled until the lifecycle is proven in game.
debug=ET.parse(ROOT/'Config/_Survivor/debug.xml').getroot()
check(debug.get('pre_spawn_logging')=='true', 'pre_spawn_logging must be enabled')
check(debug.get('spawn_flow_logging')=='true', 'spawn_flow_logging must be enabled')
check(debug.get('ui_route_logging')=='true', 'ui_route_logging must be enabled during spawn/creator acceptance')
check(debug.get('player_profile_bridge_logging')=='true', 'player_profile_bridge_logging must be enabled during spawn/creator acceptance')

if errors:
    print('RESULT FAIL')
    for e in errors: print('FAIL:',e)
    sys.exit(1)
print('RESULT PASS')
print('Approved integrated spawnselection concept + READY profile switching + native multiplayer friend selector + creator/spawn lifecycle audit passed')
