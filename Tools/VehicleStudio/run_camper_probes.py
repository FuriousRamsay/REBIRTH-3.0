"""Focused native probes for an explicitly identified, already authorized owned session."""
import argparse
import json
import os
from pathlib import Path
import shutil
import time
import urllib.parse
import urllib.request
import uuid
from watch_game_health import FATAL, heartbeat_stall_ms
from run_camper_qualification import prepare_safe_arrival

STUDIO = Path(__file__).resolve().parent
MOD = STUDIO.parents[1]

def run(pid):
    folder = STUDIO / 'reports/runtime_probes' / uuid.uuid4().hex
    folder.mkdir(parents=True)
    log = Path(os.environ['USERPROFILE']) / 'AppData/LocalLow/The Fun Pimps/7 Days To Die/Player.log'
    session = json.loads((MOD / 'Tools/GameBridge/out/session.json').read_text(encoding='utf-8-sig'))
    if session['pid'] != pid:
        raise RuntimeError('Session receipt does not identify the explicit owned PID.')
    result = {'status': 'RUNNING', 'pid': pid, 'gameplayAcceptance': False, 'probes': []}
    def call(path, method='GET', **parameters):
        if method == 'POST' and call('/ping').get('pid') != pid:
            raise RuntimeError('Owned PID changed; no mutation sent.')
        url = f'http://127.0.0.1:{session["port"]}{path}'
        if parameters:
            url += '?' + urllib.parse.urlencode(parameters)
        request = urllib.request.Request(url, method=method, headers={'X-Bridge-Token': session['token']})
        with urllib.request.urlopen(request, timeout=5) as response:
            data = json.load(response)
        if not data.get('ok'):
            raise RuntimeError(data)
        return data
    def health():
        ping = call('/ping')
        if ping.get('pid') != pid:
            raise RuntimeError('PID changed; leave the other session untouched.')
        limit = 10000 if ping.get('state') == 'ingame' else 30000
        if heartbeat_stall_ms(ping) > limit:
            raise RuntimeError('Main-thread responsiveness limit exceeded.')
        errors = [line for line in log.read_text(encoding='utf-8', errors='replace').splitlines() if FATAL.search(line)]
        if errors:
            raise RuntimeError('Actual game log contains errors: ' + '; '.join(errors[:4]))
        return ping
    try:
        deadline = time.monotonic() + 600
        last_progress = 0
        while time.monotonic() < deadline:
            ping = health()
            if ping.get('state') == 'ingame':
                break
            if time.monotonic() - last_progress > 15:
                print('Loading: ' + ping.get('state', 'unknown'), flush=True)
                last_progress = time.monotonic()
            time.sleep(2)
        else:
            raise RuntimeError('Observation window ended; inspect the same process before deciding next action.')
        world = call('/state', sections='world').get('world', {})
        if world.get('gameName') != 'CodexTest':
            raise RuntimeError('Active save is not the disposable CodexTest; no fixture changes allowed.')
        prepare_safe_arrival(lambda path, **params: call(path, 'POST', **params), call)
        for command in ('camperdyecheck', 'camperstationcheck', 'camperstoragecheck'):
            health()
            probe = call('/console', 'POST', cmd=command)
            result['probes'].append({'command': command, 'response': probe})
            output = [line for entry in probe.get('results', []) for line in entry.get('output', [])]
            captures = [Path(line.split('] PASS ', 1)[1]) for line in output if '] PASS ' in line]
            if probe.get('hadError') or any('*** ERROR' in line or '***ERROR' in line for line in output) or len(captures) != 1:
                raise RuntimeError(command + ': missing unique successful diagnostic receipt: ' + str(output))
            source = captures[0]
            evidence = json.loads(source.read_text(encoding='utf-8-sig'))
            if not evidence.get('status', '').startswith('PASS_'):
                raise RuntimeError(command + ': captured diagnostic did not pass.')
            if command == 'camperstationcheck' and not evidence.get('stationManifestRoundTrip'):
                raise RuntimeError('Extended station manifest probe is not installed.')
            if command == 'camperstationcheck' and not evidence.get('populatedNativeStateRoundTrip'):
                raise RuntimeError('Populated queue/fuel/timer/receipt probe is not installed.')
            if command == 'camperstationcheck' and not evidence.get('canonicalRecoveryImageChecked'):
                raise RuntimeError('Current clock/revision-normalized recovery image probe is not installed.')
            if command == 'camperstationcheck' and (evidence.get('snapshotFormat') != 'RCS4'
                    or evidence.get('previousInputExactCount') != 100000 or evidence.get('completionExactCount') != 100000):
                raise RuntimeError('Current lossless previous-input/completion probe is not installed.')
            if command == 'camperstationcheck' and not evidence.get('moduleAndRecipeDefinitionsChecked'):
                raise RuntimeError('Current station-definition compatibility probe is not installed.')
            if command == 'camperstationcheck' and not evidence.get('nativePersistenceVersionsChecked'):
                raise RuntimeError('Current native persistence-header probe is not installed.')
            if command == 'camperstationcheck' and not evidence.get('nativePortableModuleItemRoundTrip'):
                raise RuntimeError('Current portable native-item module probe is not installed.')
            if command == 'camperstationcheck' and not evidence.get('nativeMetadataEntryBoundaryChecked'):
                raise RuntimeError('Current native metadata entry-boundary probe is not installed.')
            if command == 'camperstoragecheck' and evidence.get('status') != 'PASS_IN_MEMORY_CONTAINER_MANIFEST_AND_SHRINK_PLAN':
                raise RuntimeError('Expected container manifest/shrink probe did not run.')
            shutil.copyfile(source, folder / source.name)
            result['probes'][-1]['evidence'] = evidence
            print(command + ': ' + evidence['status'], flush=True)
        health()
        result['status'] = 'FAIL_PROBE' if any(p['response'].get('hadError') for p in result['probes']) else 'PROBE_REPLIES_CAPTURED_NOT_GAMEPLAY_ACCEPTANCE'
    except Exception as error:
        result.update(status='FAIL', error=str(error))
    finally:
        if log.exists():
            shutil.copyfile(log, folder / 'Player.log')
        (folder / 'result.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
        print(str(folder / 'result.json'), flush=True)
    return 1 if result['status'].startswith('FAIL') else 0

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--owned-pid', required=True, type=int)
    raise SystemExit(run(parser.parse_args().owned_pid))
