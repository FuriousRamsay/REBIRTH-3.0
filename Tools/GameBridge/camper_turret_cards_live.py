"""Four installed corner power/CCTV buttons through native player input.

Parent owns launch, a clear test area, actual open management and exclusive lease.
This does not qualify competing remote peers, firing or moving-vehicle operation.
"""
import argparse
import json
import os
from pathlib import Path
import time
from camper_consolidated_regression_live import Runner


def need(value, message):
    if not value:
        raise AssertionError(message)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--entity', type=int, required=True)
    parser.add_argument('--report', type=Path, required=True)
    args = parser.parse_args()
    need(not args.report.exists(), 'fresh report required')
    need(os.environ.get('REBIRTH_BRIDGE_SESSION') and os.environ.get('REBIRTH_BRIDGE_RUN_TOKEN'),
         'parent exclusive session required')
    report = {'schema': 1, 'passed': False, 'steps': [],
              'scope': 'four native power toggles and local CCTV claim/release; no remote contention claim'}
    bridge = Runner(report, args.report)
    start = time.monotonic()
    initial = None
    touched = set()
    cleaning = False

    def observe():
        need(cleaning or time.monotonic() - start < 45, 'corner case exceeded 45 seconds')
        return bridge.call('/camperturrets', entity=args.entity)

    def wait(predicate, message):
        deadline = time.monotonic() + 2
        while True:
            value = observe()
            if predicate(value):
                return value
            need(time.monotonic() < deadline, message)
            time.sleep(.06)

    def click(identifier):
        nodes = bridge.call('/ui/tree')['nodes']
        found = [n for n in nodes if n.get('id') == identifier and n.get('clickable')
                 and n.get('enabled') is not False]
        need(len(found) == 1, 'one enabled native control required: ' + identifier)
        bridge.call('/ui/click', 'POST', path=found[0]['path'])

    def inventory(value):
        return [{key: row[key] for key in ('installed', 'ammo', 'silencer', 'scopes', 'loadedSlot', 'sniperMode')}
                for row in value['corners']]

    try:
        need(bridge.call('/state', sections='world')['world']['gameName'] == 'CodexTest', 'disposable CodexTest only')
        initial = observe()
        need(initial.get('presentation', {}).get('open') and not initial['presentation']['cameraActive'],
             'actual management required')
        need(all(c['installed'] and c['controllerEntity'] is None for c in initial['corners']),
             'four installed unreserved corners required')
        for corner in range(4):
            original = initial['corners'][corner]['enabled']
            touched.add(corner)
            click('turretEnabled' + str(corner))
            off = wait(lambda v: v['corners'][corner]['enabled'] != original, 'power button did not change state')
            click('turretEnabled' + str(corner))
            restored = wait(lambda v: v['corners'][corner]['enabled'] == original, 'power state did not restore')
            click('turretCctv' + str(corner))
            camera = wait(lambda v: v['presentation']['cameraActive']
                          and v['presentation']['cameraCorner'] == corner
                          and v['corners'][corner]['controllerEntity'] is not None,
                          'CCTV did not acquire selected corner')
            need(sum(c['controllerEntity'] is not None for c in camera['corners']) == 1,
                 'local player claimed more than one turret')
            need(camera['presentation']['localPlayerFirstPerson'] is False,
                 'CCTV did not generate complete native third-person player')
            capture = bridge.call('/screenshot', 'POST', name='camper_cctv_complete_player_corner_' + str(corner))
            bridge.call('/key', 'POST', name='E')
            released = wait(lambda v: not v['presentation']['cameraActive']
                            and v['presentation']['open']
                            and v['corners'][corner]['controllerEntity'] is None,
                            'CCTV exit did not return management and release control')
            need(inventory(released) == inventory(initial), 'button test changed turret equipment/ammunition')
            need(released['presentation']['localPlayerFirstPerson'] == initial['presentation']['localPlayerFirstPerson'],
                 'CCTV exit did not restore exact prior native player view')
            report['steps'].append({'corner': corner, 'powerChanged': off['corners'][corner]['enabled'],
                                    'powerRestored': restored['corners'][corner]['enabled'],
                                    'controller': camera['corners'][corner]['controllerEntity'],
                                    'released': released['corners'][corner]['controllerEntity'],
                                    'screenshot': capture,
                                    'visualReviewRequired': 'Inspect complete local player head when in frame; view flag alone is not visual proof'})
        report['passed'] = True
    except Exception as error:
        report['failure'] = type(error).__name__ + ': ' + str(error)
    finally:
        # Only unwind this driver's local CCTV; never manufacture a control release.
        cleaning = True
        try:
            state = observe()
            if state.get('presentation', {}).get('cameraActive'):
                bridge.call('/key', 'POST', name='E')
            for corner in touched:
                state = observe()
                if state['corners'][corner]['enabled'] != initial['corners'][corner]['enabled']:
                    click('turretEnabled' + str(corner))
                    wait(lambda v: v['corners'][corner]['enabled'] == initial['corners'][corner]['enabled'],
                         'cleanup could not restore original power state')
            report['final'] = observe()
        except Exception as error:
            report['cleanupFailure'] = str(error)
            report['passed'] = False
        report['seconds'] = round(time.monotonic() - start, 3)
        bridge.save()
    return 0 if report['passed'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
