"""Run prepared Purge scenarios efficiently in an already-loaded disposable save.
No launch, fabricated kills/credits or automatic source build. Restore setup in finally.
"""
from pathlib import Path
import argparse, json, subprocess, time, urllib.request, os, tempfile, re

root = Path(__file__).resolve().parents[2]

def main():
    temporary = Path('E:/_Haven/shared/REBIRTHTemp')
    temporary.mkdir(parents=True, exist_ok=True)
    os.environ['TEMP'] = os.environ['TMP'] = str(temporary)
    tempfile.tempdir = str(temporary)
    parser = argparse.ArgumentParser()
    parser.add_argument('scenarios', nargs='+', type=Path)
    args = parser.parse_args()
    paths = [p.resolve() for p in args.scenarios]
    for path in paths:
        if not path.is_file():
            raise RuntimeError(f'Scenario missing: {path}')
    session = json.loads((root / 'Tools/GameBridge/out/session.json').read_text(encoding='utf-8-sig'))
    def call(path, body=None):
        request = urllib.request.Request(f"http://127.0.0.1:{session['port']}{path}",
            data=None if body is None else body.encode(),
            headers={'X-Bridge-Token': session['token']}, method='GET' if body is None else 'POST')
        with urllib.request.urlopen(request, timeout=20) as reply:
            value = json.load(reply)
        if value.get('ok') is not True or value.get('hadError'):
            raise RuntimeError(f'Bridge command failed: {value}')
        return value
    def observe():
        reply = call('/console', 'rbpurgetestobserve')
        rows = [json.loads(line) for result in reply.get('results', [])
                for line in result.get('output', []) if line.startswith('{')]
        if len(rows) != 1 or rows[0].get('schema') != 1 or rows[0].get('observationOnly') is not True:
            raise RuntimeError('Missing supported Purge observation')
        return rows[0]
    if call('/ping').get('state') != 'ingame':
        raise RuntimeError('Load the disposable CodexTest Purge world first')
    folder = root / 'Tools/GameBridge/out/purge_runs' / time.strftime('%Y%m%d-%H%M%S')
    folder.mkdir(parents=True)
    report = {'scenarios': [], 'setupRestored': False}
    started = False
    original_spawns = None
    horde_cleanup_day = None
    restore_scanner_fly = False
    try:
        before = observe()
        report['before'] = before
        if before.get('purgeEnabled') is not True or before.get('releaseEnabled') is not True:
            raise RuntimeError('Purge theme and qualification build required; no tests executed')
        original_spawns = call('/enemyspawns', '').get('enemySpawns')
        if not isinstance(original_spawns, bool):
            raise RuntimeError('Cannot capture original enemy spawning setting')
        started = True  # Attempt restoration even if the setup request fails after applying.
        call('/console', 'rbpurgetestsetup on')
        for path in paths:
            scenario = json.loads(path.read_text(encoding='utf-8-sig'))
            if path.stem == 'purge_scanner_fly_cleanup':
                # A current scanner proves the player's original fly state is OFF.
                # Do not alter an unknown initial state if this prerequisite fails.
                if observe().get('scannerCurrent') is not True:
                    raise RuntimeError('Enter the real scanner POI on foot before the fly cleanup case')
                restore_scanner_fly = True
            horde_cleanup_day = None
            if path.stem.startswith('horde_') and path.stem.endswith('_attribution'):
                for step in scenario.get('steps', []):
                    match = re.fullmatch(r'settime (\d+) 21 59', step.get('console', ''))
                    if match:
                        horde_cleanup_day = int(match.group(1)) + 1
                        break
                if horde_cleanup_day is None:
                    raise RuntimeError('Horde attribution scenario has no supported native night setup')
            begin = time.perf_counter()
            result = subprocess.run(['powershell', '-ExecutionPolicy', 'Bypass', '-File',
                str(root/'Tools/GameBridge/gamebridge.ps1'), 'test', str(path)],
                cwd=root, capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=600)
            (folder/(path.stem+'.txt')).write_text(result.stdout+'\n'+result.stderr, encoding='utf-8')
            report['scenarios'].append({'file':str(path), 'exitCode':result.returncode,
                'seconds':time.perf_counter()-begin, 'after':observe()})
            print(f'{path.name}: exit {result.returncode}', flush=True)
            if result.returncode:
                raise RuntimeError(f'Scenario failed: {path.name}; inspect retained evidence')
    except Exception as error:
        report['failure'] = str(error)
        raise
    finally:
        try:
            if started:
                report['cleanupErrors'] = []
                commands = []
                if restore_scanner_fly:
                    commands.append(('/fly?on=0', ''))
                if horde_cleanup_day is not None:
                    commands.append(('/console', f'settime {horde_cleanup_day} 9 0'))
                if original_spawns is not None:
                    commands.append((f'/enemyspawns?on={int(original_spawns)}', ''))
                commands.append(('/console', 'rbpurgetestsetup off'))
                for endpoint, body in commands:
                    try:
                        call(endpoint, body)
                    except Exception as error:
                        report['cleanupErrors'].append(str(error))
                report['setupRestored'] = not report['cleanupErrors']
                if report['cleanupErrors']:
                    raise RuntimeError('Test cleanup incomplete; inspect summary.json')
        finally:
            (folder/'summary.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
            print(f'Evidence: {folder}', flush=True)

if __name__ == '__main__':
    main()
