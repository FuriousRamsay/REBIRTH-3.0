"""Focused live comparison using real vehicle input actions, never forced velocity."""
import argparse
import json
from pathlib import Path
import time
import shutil
import urllib.parse
import urllib.request
import urllib.error
import uuid
import os
from watch_game_health import FATAL

ROOT = Path(__file__).resolve().parent
MOD = ROOT.parents[1]

def run(native, camper, wait_ready=False, expected_pid=None, track_receipt=None, candidate=False, lights=False, legacy_manual=False):
    session = json.loads((MOD/'Tools/GameBridge/out/session.json').read_text(encoding='utf-8-sig'))
    folder = ROOT/'reports/handling_comparison'/uuid.uuid4().hex
    folder.mkdir(parents=True)
    log_path = Path(os.environ['USERPROFILE'])/'AppData/LocalLow/The Fun Pimps/7 Days To Die/Player.log'
    log_seen = 0
    def check_game_log():
        nonlocal log_seen
        lines = log_path.read_text(encoding='utf-8', errors='replace').splitlines()
        if len(lines) < log_seen:
            log_seen = 0
        errors = [line for line in lines[log_seen:] if FATAL.search(line)]
        log_seen = len(lines)
        if errors:
            shutil.copyfile(log_path, folder/'Player.log')
            raise RuntimeError('Actual game log contains errors: '+ '; '.join(errors[:8]))
    def call(path, method='GET', **params):
        if method == 'POST':
            if expected_pid is None or call('/ping').get('pid') != expected_pid:
                raise RuntimeError('Explicit owned PID must match before every input, including cleanup')
        url = f'http://127.0.0.1:{session["port"]}{path}'
        if params:
            url += '?'+urllib.parse.urlencode(params)
        request = urllib.request.Request(url, method=method, headers={'X-Bridge-Token':session['token']})
        try:
            with urllib.request.urlopen(request,timeout=5) as response:
                result = json.load(response)
        except urllib.error.HTTPError as error:
            raise RuntimeError(f'{path}: HTTP {error.code}: '+error.read().decode('utf-8',errors='replace')) from error
        if not result.get('ok'):
            raise RuntimeError(result)
        return result
    report = {'kind':'NATIVE_4X4_CAMPER_INPUT_COMPARISON','status':'RUNNING','vehicles':[],
              'scope':'Identical start on a prepared flat disposable-save pad; fixture mount/full fuel, real native input actions.',
              'forcedVelocity':False}
    try:
        track = json.loads(Path(track_receipt).read_text(encoding='utf-8-sig'))
        if track.get('kind') != 'FLAT_NATIVE_CONCRETE_TEST_PAD' or track.get('width') != 40 or track.get('length') != 80:
            raise RuntimeError('An explicit full80x40 handling pad is required; parked-only fixtures cannot qualify driving')
        report['track'] = track
        shutil.copyfile(track_receipt, folder/'track.json')
        start_x = track['xMin'] + track['width']/2
        start_z = track['zMin'] + 12
        fixture_y = track['surfaceY'] + 1.1
        def console_checked(command):
            result = call('/console','POST',cmd=command)
            output = [line for entry in result.get('results',[]) for line in entry.get('output',[])]
            if result.get('hadError') or any('*** ERROR' in line or '***ERROR' in line for line in output):
                raise RuntimeError('Fixture refused: '+str(output))
            return result
        def place(entity, x, z):
            result = console_checked(f'camperfixture {entity} {x} {fixture_y} {z} 0')
            if not any('[CamperTrack] PREPARED ' in line for entry in result.get('results',[]) for line in entry.get('output',[])):
                raise RuntimeError('No confirmed vehicle placement receipt; do not retry uncertain mutation')
            time.sleep(2)
            from run_camper_qualification import parked_ready
            deadline = time.monotonic() + 15
            idle = 0
            while time.monotonic() < deadline:
                check_game_log()
                diagnostic = console_checked(f'camperdiag {entity}')
                paths = [line.split('PASS_CAPTURE ', 1)[1] for entry in diagnostic.get('results', [])
                         for line in entry.get('output', []) if 'PASS_CAPTURE ' in line]
                if len(paths) != 1:
                    raise RuntimeError('Unique parked-before-mount capture required.')
                data = json.loads(Path(paths[0]).read_text(encoding='utf-8-sig'))
                shutil.copyfile(paths[0], folder / Path(paths[0]).name)
                idle = idle + 1 if parked_ready(data, entity) else 0
                if idle >= 2:
                    return
                time.sleep(.5)
            raise RuntimeError('Fixture vehicle did not settle; no mount or comparison input sent.')
        def capture_lamps(label, entity, expected):
            reply = console_checked(f'camperdiag {entity}')
            paths = [line.split('PASS_CAPTURE ',1)[1] for entry in reply.get('results',[])
                for line in entry.get('output',[]) if 'PASS_CAPTURE ' in line]
            if len(paths) != 1:
                raise RuntimeError('Missing actual lamp-state capture')
            data = json.loads(Path(paths[0]).read_text(encoding='utf-8-sig'))
            shutil.copyfile(paths[0],folder/(label+'_lamps.json'))
            rear = data.get('rearLamp',{})
            if not rear.get('present') or rear.get('productionOwners') != ['rebirth.camper.production:RebirthUtils']:
                raise RuntimeError('New rear geometry and exactly one actual production lamp owner are required')
            red = [light for light in data.get('lights',[]) if light.get('path','').startswith('M/RearLights/')]
            emission = rear.get('emission',{})
            if data.get('headlightBroken') or len(red) != 2 or abs(emission.get('r',-1)-expected) > .001:
                raise RuntimeError('Rear lamp emission does not match the actual requested state')
            if any(abs(emission.get(channel,1)) > .001 for channel in ('g','b')) or any(
                abs(light['intensity']-expected) > .001 or light['enabled'] != (expected > 0)
                or light['color']['r'] < .99 or light['color']['g'] > .001 or light['color']['b'] > .001
                for light in red):
                raise RuntimeError('Both rear emitters must have the requested red intensity and enabled state')
            photo = call('/screenshot','POST',name=label+'_rear_lamps')
            shutil.copyfile(photo['path'],folder/Path(photo['path']).name)
            return data
        check_game_log()
        initial_ping = call('/ping')
        if expected_pid is None or initial_ping.get('pid') != expected_pid:
            raise RuntimeError('An explicit matching owned game PID is required before any input')
        if not wait_ready and call('/state',sections='world').get('world',{}).get('gameName') != 'CodexTest':
            raise RuntimeError('Active save must be the disposable CodexTest before input')
        if wait_ready:
            deadline=time.monotonic()+600
            last_print=0
            while time.monotonic()<deadline:
                check_game_log()
                ping=call('/ping')
                if expected_pid is not None and ping.get('pid') != expected_pid:
                    raise RuntimeError('The game PID changed; leave other sessions untouched')
                if ping.get('state')=='ingame':
                    break
                if time.monotonic()-last_print>15:
                    print('[WAIT] '+ping.get('state','unknown'),flush=True)
                    last_print=time.monotonic()
                time.sleep(2)
            else:
                raise RuntimeError('The game did not finish loading within the observation window')
        if call('/state',sections='world').get('world',{}).get('gameName') != 'CodexTest':
            raise RuntimeError('Active save must be the disposable CodexTest before fixture changes')
        from run_camper_qualification import prepare_safe_arrival
        prepare_safe_arrival(lambda path, **params: call(path, 'POST', **params), call)
        ping = call('/ping')
        if ping.get('pid') != expected_pid:
            raise RuntimeError('The game PID changed; leave other sessions untouched')
        if ping.get('state') != 'ingame':
            raise RuntimeError('Game is not ready')
        last_log = ping['lastLogSeq']
        fixtures=call('/entities',radius=80,limit=100).get('entities',[])
        if not any(e['id']==camper and e.get('class')=='rebirthCamper80s' for e in fixtures):
            raise RuntimeError('Existing camper fixture missing')
        if not any(e['id']==native and e.get('class')=='vehicleTruck4x4' for e in fixtures):
            raise RuntimeError('Specified native 4x4 is not present; place and validate it before comparing')
        runs = [('native4x4',native),('camper_baseline',camper)]
        if candidate:
            runs.append(('camper_force_candidate',camper))
        for label, entity in runs:
            check_game_log()
            call('/release','POST')
            attached = call('/vehicle').get('attached',False)
            if attached:
                call('/dismount','POST')
            if label == 'camper_force_candidate':
                match = console_checked(f'campermatch {camper} {native} force')
                candidate_paths = [line.split('[CamperCalibration] CANDIDATE ',1)[1]
                    for entry in match.get('results',[]) for line in entry.get('output',[])
                    if '[CamperCalibration] CANDIDATE ' in line]
                if len(candidate_paths) != 1:
                    raise RuntimeError('No confirmed force-candidate receipt; do not drive an uncertain calibration')
                candidate_data = json.loads(Path(candidate_paths[0]).read_text(encoding='utf-8-sig'))
                if candidate_data.get('matchForce') is not True or candidate_data.get('camper') != camper:
                    raise RuntimeError('Calibration receipt does not confirm the requested force candidate')
                shutil.copyfile(candidate_paths[0],folder/'force_candidate.json')
            place(entity,start_x,start_z)
            call('/mount','POST',entity=entity)
            record = {'label':label,'entity':entity,'setup':'fixture mount and full fuel','samples':[]}
            report['vehicles'].append(record)
            starting_health = next(e['health'] for e in fixtures if e['id'] == entity)
            last_health_check = 0
            diagnostic=call('/console','POST',cmd=f'camperdiag {entity}')
            paths=[line.split('PASS_CAPTURE ',1)[1] for result in diagnostic.get('results',[])
                   for line in result.get('output',[]) if 'PASS_CAPTURE ' in line]
            if not paths:
                raise RuntimeError('Native physics diagnostic did not produce a capture')
            source=Path(paths[0])
            captured = json.loads(source.read_text(encoding='utf-8-sig'))
            if 'turnTowardsLook' not in captured:
                raise RuntimeError('Steering mode is missing from the diagnostic; update the owned helper before driving')
            if captured['turnTowardsLook']:
                shutil.copyfile(source, folder/(label+'_mouse_steering_before.json'))
                call('/press','POST',action='vehicle.ToggleTurnMode',seconds=.1)
                diagnostic=call('/console','POST',cmd=f'camperdiag {entity}')
                paths=[line.split('PASS_CAPTURE ',1)[1] for entry in diagnostic.get('results',[])
                       for line in entry.get('output',[]) if 'PASS_CAPTURE ' in line]
                if not paths:
                    raise RuntimeError('Steering-mode verification capture missing')
                source=Path(paths[0])
                captured=json.loads(source.read_text(encoding='utf-8-sig'))
                if captured.get('turnTowardsLook') is not False:
                    raise RuntimeError('Native keyboard steering toggle did not take effect; do not compare camera steering')
            record['steeringMode']='native keyboard steering, captured before motion'
            wheels = captured.get('wheels',[])
            if len(wheels) != 4 or any(not w.get('grounded') or not w.get('contactPoint')
                or abs(w['contactPoint']['y']-track['surfaceY']) > .08
                or w.get('contactNormal',{}).get('y',0) < .99 for w in wheels):
                raise RuntimeError('All four actual wheel contacts must be on the same flat pad before driving')
            shutil.copyfile(source,folder/(label+'_physics.json'))
            record['physicsCapture']=label+'_physics.json'
            check_lights = lights and label == 'camper_baseline'
            if check_lights:
                if captured.get('headlightsOn'):
                    call('/press','POST',action='vehicle.ToggleFlashlight',seconds=.1)
                time.sleep(.2)
                capture_lamps(label+'_off',entity,0)
                call('/press','POST',action='vehicle.ToggleFlashlight',seconds=.1)
                time.sleep(.2)
                on = capture_lamps(label+'_headlights_on',entity,.3)
                if on.get('headlightsOn') is not True:
                    raise RuntimeError('Native headlight input did not turn headlights on')
                call('/press','POST',action='vehicle.ToggleFlashlight',seconds=.1)
                time.sleep(.2)
                off = capture_lamps(label+'_headlights_off',entity,0)
                if off.get('headlightsOn') is not False:
                    raise RuntimeError('Native headlight input did not turn headlights off')
            if not legacy_manual:
                from run_vehicle_sequence import run as collect_sequence
                from compare_vehicle_sequences import load as measure_sequence
                audio_reply = console_checked(f'camperaudiotrace {entity} 20')
                audio_exports = Path(os.environ['APPDATA']) / '7DaysToDie/Mods/REBIRTH_VehicleInspector_Dev/Exports/Camper'
                audio_lines = [line for entry in audio_reply.get('results', []) for line in entry.get('output', [])]
                audio_paths = [Path(line.split('[CamperAudio] STARTED ', 1)[1]).resolve() for line in audio_lines if '[CamperAudio] STARTED ' in line]
                if len(audio_paths) != 1 or not audio_paths[0].is_relative_to(audio_exports.resolve()):
                    raise RuntimeError('Unique confined native audio trace start required.')
                collected = collect_sequence(expected_pid, entity,bounds=[track['xMin']+4,track['xMin']+track['width']-4,
                    track['zMin']+4,track['zMin']+track['length']-4])
                result_path = collected / 'native_result.json'
                expected_name = 'vehicleTruck4x4' if entity == native else 'rebirthCamper80s'
                sequence, metrics = measure_sequence(result_path, expected_name)
                audio_deadline = time.monotonic() + 22
                while not audio_paths[0].exists():
                    check_game_log()
                    if call('/ping').get('pid') != expected_pid or time.monotonic() > audio_deadline:
                        raise RuntimeError('Native audio trace did not finish in the owned session.')
                    time.sleep(.1)
                audio_data = json.loads(audio_paths[0].read_text(encoding='utf-8-sig'))
                shutil.copyfile(audio_paths[0], folder / (label + '_audio.json'))
                from validate_vehicle_audio import validate as validate_audio
                record['nativeAudio'] = validate_audio(audio_data, entity, expected_name)
                if check_lights:
                    from compare_vehicle_sequences import validate_native_rear_lamps
                    record['nativeBrakeReverseLamps'] = validate_native_rear_lamps(sequence)
                for sample in sequence.get('samples', []):
                    position = sample.get('position', [])
                    if len(position) != 3 or not (track['xMin']+4 <= position[0] <= track['xMin']+track['width']-4
                        and track['zMin']+4 <= position[2] <= track['zMin']+track['length']-4):
                        raise RuntimeError('Native sequence left the safe pad interior; handling comparison rejected')
                record['sequenceResult'] = str(result_path.resolve())
                record['metrics'] = metrics
                record['visualAcceptance'] = False
                record['audibleHornAcceptance'] = False
                call('/release','POST')
                if call('/vehicle').get('speed',0) > .1:
                    raise RuntimeError('Native sequence finished with motion; do not conceal it with fixture placement')
                call('/dismount','POST')
                place(entity,track['xMin']+5,track['zMin']+(5 if entity==native else track['length']-8))
                print(f'[{label}] Native sequence collected with nine phase screenshots.',flush=True)
                continue
            phases = [('settle',1,()), ('accelerate',3,('MoveForward',)),
                      ('steer',1.5,('MoveForward','MoveRight')),
                      ('brake',2,('Brake',)), ('reverse',2,('MoveBack',)), ('stop',1.5,('Brake',))]
            for phase,duration,actions in phases:
                call('/release','POST')
                for action in actions:
                    call('/press','POST',action='vehicle.'+action,hold=1)
                start=time.monotonic()
                while time.monotonic()-start < duration:
                    check_game_log()
                    state=call('/vehicle')
                    sample={'phase':phase,'seconds':round(time.monotonic()-start,3),**state}
                    record['samples'].append(sample)
                    if not state.get('attached') or state.get('speed',0)>16:
                        raise RuntimeError('Unexpected attachment loss or excessive speed')
                    position = state.get('position')
                    if not position or not (track['xMin']+4 <= position[0] <= track['xMin']+track['width']-4
                        and track['zMin']+4 <= position[2] <= track['zMin']+track['length']-4):
                        raise RuntimeError('Vehicle left the safe pad interior; comparison aborted')
                    if time.monotonic() - last_health_check >= .25:
                        nearby = call('/entities',radius=250,limit=100).get('entities',[])
                        current = next((e for e in nearby if e['id'] == entity), None)
                        if current is None or current['health'] < starting_health:
                            raise RuntimeError('Vehicle disappeared or took damage; route is not a valid handling comparison')
                        last_health_check = time.monotonic()
                    time.sleep(0.1)
                ping=call('/ping')
                if ping.get('mainThreadStallMs',0)>5000:
                    raise RuntimeError('Main thread stopped responding normally')
                errors=call('/log',since=last_log,level='error')
                last_log=ping['lastLogSeq']
                if errors.get('count',0):
                    raise RuntimeError('New game errors during comparison')
                phase_samples=[s for s in record['samples'] if s['phase']==phase]
                if check_lights and phase in ('brake','reverse'):
                    lamp_state = capture_lamps(label+'_'+phase,entity,1.5)
                    if phase == 'brake' and lamp_state.get('braking') is not True:
                        raise RuntimeError('Brake input did not produce native braking state')
                    if phase == 'reverse' and lamp_state.get('forwardVelocity',0) >= -.05:
                        raise RuntimeError('Reverse input did not produce actual backward movement')
                print(f'[{label}] {phase}: peak={max(s["speed"] for s in phase_samples):.3f}, end={phase_samples[-1]["speed"]:.3f}',flush=True)
            call('/release','POST')
            time.sleep(.5)
            if call('/vehicle').get('speed',0) > .1:
                raise RuntimeError('Vehicle has not stopped; fixture placement would conceal unstable motion')
            call('/dismount','POST')
            place(entity,track['xMin']+5,track['zMin']+(5 if entity==native else track['length']-8))
            record['metrics'] = {phase: {'peakSpeed':max(s['speed'] for s in record['samples'] if s['phase']==phase),
                'endSpeed':[s['speed'] for s in record['samples'] if s['phase']==phase][-1]}
                for phase,_,_ in phases}
        if not legacy_manual:
            from compare_vehicle_sequences import compare
            report['comparison'] = compare(Path(report['vehicles'][0]['sequenceResult']),Path(report['vehicles'][1]['sequenceResult']))
            first_audio, second_audio = [row['nativeAudio'] for row in report['vehicles'][:2]]
            if first_audio['hornClips'] != second_audio['hornClips'] or not set(first_audio['engineGroups']).intersection(second_audio['engineGroups']):
                raise RuntimeError('Camper/native4x4 actual sound playback identities do not match.')
            report['nativeAudioComparison'] = dict(matchedHornClips=first_audio['hornClips'],
                sharedEngineGroups=sorted(set(first_audio['engineGroups']).intersection(second_audio['engineGroups'])), humanListeningVerified=False)
        report['status']='CAPTURED_REQUIRES_COMPARISON_AND_TUNING'
    except Exception as error:
        report.update(status='FAIL',error=str(error))
    finally:
        try:
            call('/release','POST')
        except Exception:
            pass
        (folder/'result.json').write_text(json.dumps(report,indent=2)+'\n')
        print(f'[RESULT] {report["status"]}; report={folder}',flush=True)
    return 1 if report['status']=='FAIL' else 0

if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--native',type=int,required=True)
    parser.add_argument('--camper',type=int,required=True)
    parser.add_argument('--wait-ready',action='store_true')
    parser.add_argument('--pid',type=int,required=True)
    parser.add_argument('--track-receipt',required=True)
    parser.add_argument('--candidate',action='store_true')
    parser.add_argument('--legacy-manual',action='store_true',help='Use the older individual-input loop instead of the default in-game sequence')
    parser.add_argument('--lights',action='store_true',help='Qualify installed production rear lamps during the camper baseline run; images still require inspection')
    args=parser.parse_args()
    raise SystemExit(run(args.native,args.camper,args.wait_ready,args.pid,args.track_receipt,args.candidate,args.lights,args.legacy_manual))
