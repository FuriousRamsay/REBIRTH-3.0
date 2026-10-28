"""Create verified changed-files and cumulative Vehicle Studio candidate checkpoints."""
import hashlib
import json
from pathlib import Path
import uuid
import zipfile
from camper_integration import STUDIO, file_hash

MOD = STUDIO.parents[1]
SKIP = {'__pycache__', 'obj', 'bin', 'packages'}


def selected():
    production = ['Config/vehicles.xml', 'Config/entityclasses.xml', 'Config/items.xml', 'Config/Localization.csv',
                  'Config/_Vehicles/camper_vehicles.xml', 'Config/_Vehicles/camper_entityclasses.xml',
                  'Config/_Vehicles/camper_items.xml', 'Resources/rebirth_camper80s.unity3d',
                  'Tools/GameBridge/tests/camper_driver_capture.json', '_Documentation/VehicleStudio/TAKEOVER_STATUS.md',
                  '_Documentation/VehicleStudio/CHECKPOINT_COMPLETION.md', '_Documentation/VehicleStudio/GOAL_REQUIREMENTS.md']
    changes = {name: MOD / name for name in production}
    for directory in ('Scripts/Vehicles/Camper', '_Documentation/VehicleStudio'):
        for path in (MOD / directory).rglob('*'):
            if path.is_file():
                changes[path.relative_to(MOD).as_posix()] = path
    cumulative = dict(changes)
    for path in STUDIO.rglob('*'):
        relative = path.relative_to(STUDIO)
        if not path.is_file() or SKIP.intersection(relative.parts):
            continue
        # The authoritative production bundle is included once, at Resources/.
        if path.suffix == '.unity3d':
            continue
        name = path.relative_to(MOD).as_posix()
        cumulative[name] = path
        if relative.parts[0] not in ('source', 'inputs'):
            changes[name] = path
    cumulative['_Documentation/VehicleStudio/COORDINATION_BACKLOG_REPLY.md'] = MOD / '_Documentation/VehicleStudio/COORDINATION_BACKLOG_REPLY.md'
    return changes, cumulative


def package(destination, files, note):
    manifest = {'kind': 'REBIRTH_VEHICLE_STUDIO_CHECKPOINT_MANIFEST', 'completion': 'NOT COMPLETE', 'files': []}
    with zipfile.ZipFile(destination, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=1) as archive:
        for name, path in sorted(files.items()):
            before = file_hash(path)
            archive.write(path, name)
            if file_hash(path) != before:
                raise ValueError(f'Concurrent file change during packaging: {path}')
            manifest['files'].append({'path': name, 'sha256': before, 'bytes': path.stat().st_size})
        archive.writestr('CHECKPOINT_README.txt', note)
        archive.writestr('CHECKPOINT_MANIFEST.json', json.dumps(manifest, indent=2))
    with zipfile.ZipFile(destination) as archive:
        if len(archive.namelist()) != len(set(archive.namelist())):
            raise ValueError('Duplicate archive entry')
        for entry in manifest['files']:
            digest = hashlib.sha256()
            with archive.open(entry['path']) as stream:
                for chunk in iter(lambda: stream.read(1024 * 1024), b''):
                    digest.update(chunk)
            if digest.hexdigest() != entry['sha256']:
                raise ValueError(f'Archive checksum mismatch: {entry["path"]}')
    return {'path': str(destination), 'sha256': file_hash(destination), 'filesVerified': len(manifest['files']), 'bytes': destination.stat().st_size}


if __name__ == '__main__':
    run = uuid.uuid4().hex
    folder = STUDIO / 'packages' / run
    folder.mkdir(parents=True)
    note = '''REBIRTH VEHICLE STUDIO - PARTIAL RUNTIME CALIBRATION CHECKPOINT
Source: REBIRTH_VehicleStudio_v1.9.0_CUMULATIVE_SOURCE.zip and supplied evidence, integrated against the current local REBIRTH working tree.
Target: zzz_REBIRTH__3_0; installed 7DTD 3.3.0 b18; Unity 2022.3.62f2.
NOT COMPLETE. Native placement, refuel, driver/front passenger pose and hip alignment have partial runtime evidence. Steering shaft rotation was captured; final palm grip and rear capacity remain pending. A new live IK target candidate is evidence only and is not persisted to XML/bundle. Final passenger capacity follows actual comfortable cabin space, per the owner's newer instruction. See CHECKPOINT_COMPLETION.md and GOAL_REQUIREMENTS.md for outstanding checks.
Authorized disposable-save game sessions were performed. No shared-root build or RebirthUtils.dll replacement was performed.
Bundle built and reopened in Unity. Earlier static checks are historical evidence, not a rerun against every later change. Current native dye construction/clone/removal/replacement/serialization and legacy cosmetic-slot migration passed; Brown paint was visually demonstrated. World dye save/pickup and all requested colors remain pending. Native forge state codec passed independent instances and100000 molten units; mobile crafting UI/ticks/persistence remain incomplete. Camper-only terrain diagnostic logging was corrected and runtime exercised, while physical rolling/recovery stability remains failed. Developer diagnostic compiled separately; no shared root build.
The cumulative archive is the complete Vehicle Studio tooling/source checkpoint plus candidate integration, not a full REBIRTH or Unity project replacement.
Original Unity project/assets remain in place at C:\\REBIRTH\\VehicleStudio_QuickTest. Prior Unity artifact paths and preservation hashes are recorded in reports.
Shared config files are snapshots of the live working tree and contain other developers' pre-existing changes. Do not blindly overwrite another checkout with them. The integration adapter and its receipt identify camper-owned additions and selective rollback.
All project files retain project-relative paths. Read _Documentation/VehicleStudio/TAKEOVER_STATUS.md and deployment receipts before using this candidate.
Developer diagnostics require Build-CamperDiagnostics.ps1 and Install-CamperDiagnostics.ps1; no diagnostic DLL is included in the production mod.
Final delivery awaits authorized disposable-save gameplay and any required calibration.
'''
    result = {'kind': 'REBIRTH_VEHICLE_STUDIO_CHECKPOINT_PACKAGE', 'status': 'FAIL', 'completion': 'NOT COMPLETE', 'gameTested': False}
    print('[1 INVENTORY] Snapshotting current owned changes and preserved tooling source', flush=True)
    try:
        changes, cumulative = selected()
        print('[2 PACKAGE] Writing and checksum-verifying changed-files checkpoint', flush=True)
        result['changedFiles'] = package(folder / 'REBIRTH_Camper_CHANGED_FILES_NOT_GAME_TESTED.zip', changes, note)
        print('[3 PACKAGE] Writing and checksum-verifying cumulative Vehicle Studio checkpoint', flush=True)
        result['cumulativeVehicleStudio'] = package(folder / 'REBIRTH_VehicleStudio_CUMULATIVE_NOT_GAME_TESTED.zip', cumulative, note)
        result['status'] = 'PASS_PACKAGED_NOT_GAME_TESTED'
    except Exception as exc:
        result['error'] = str(exc)
    (folder / 'result.json').write_text(json.dumps(result, indent=2) + '\n')
    print(f'[4 {result["status"]}] Report: {folder / "result.json"}', flush=True)
    raise SystemExit(0 if result['status'].startswith('PASS') else 1)
