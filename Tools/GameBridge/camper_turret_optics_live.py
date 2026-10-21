"""Real native CCTV input qualification; parent owns launch, setup and batch lease.

Requires an already open CCTV with at least one usable supplied scope. Does not
install equipment, fire, create a runtime, acquire a turret or launch the game.
"""
import argparse
import json
import math
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
    need(not args.report.exists(), 'preserve previous evidence')
    need(os.environ.get('REBIRTH_BRIDGE_SESSION') and os.environ.get('REBIRTH_BRIDGE_RUN_TOKEN'),
         'parent exclusive session required')
    bridge = Runner({},args.report.with_suffix('.requests.json'),session_path=os.environ['REBIRTH_BRIDGE_SESSION'])

    report = {'schema': 1, 'passed': False, 'session': bridge.scope, 'steps': [],
              'scope': 'native GUI wheel, Auto gate and held RMB projection in existing CCTV'}
    started = time.monotonic()
    initial = None
    corner = None
    held = False

    def observe():
        need(time.monotonic() - started < 40, 'optics case exceeded 40 seconds')
        state = bridge.call('/camperturrets', entity=args.entity)
        view = state['presentation']
        need(view['open'] and view['cameraActive'], 'actual CCTV must remain open')
        if corner is not None:
            need(view['cameraCorner'] == corner, 'CCTV corner changed')
        return state

    def press(action):
        reply = bridge.call('/press', 'POST', action=action, frames=3)
        state = observe()
        report['steps'].append({'action': action, 'reply': reply, 'presentation': state['presentation']})
        return state

    def manual():
        if observe()['presentation']['automaticZoom']:
            need(not press('gui.MiddleClick')['presentation']['automaticZoom'], 'MMB did not enter manual')

    try:
        need(bridge.call('/ping')['state'] == 'ingame', 'game not ready')
        initial = observe()
        corner = initial['presentation']['cameraCorner']
        slot = initial['corners'][corner]
        choices = slot['availableZoom']
        need(len(choices) > 1 and choices == sorted(set(choices)), 'supply at least one usable scope')
        need(max(choices) <= slot['zoomCap'], 'available scopes exceed ammo/mode cap')
        before_ammo = slot['ammo']
        manual()
        for _ in range(3):
            press('gui.scrollDown')
        need(observe()['presentation']['selectedZoom'] == 1, 'wheel lower bound is not 1x')
        for expected in choices[1:]:
            need(press('gui.scrollUp')['presentation']['selectedZoom'] == expected, 'wheel skipped usable scope')
        need(press('gui.scrollUp')['presentation']['selectedZoom'] == choices[-1], 'wheel exceeded upper cap')
        for expected in reversed(choices[:-1]):
            need(press('gui.scrollDown')['presentation']['selectedZoom'] == expected, 'wheel zoom-out order wrong')
        press('gui.scrollUp')
        selected = observe()['presentation']['selectedZoom']
        bridge.call('/press', 'POST', action='gui.RightClick', hold=1)
        held = True
        expected_fov = math.degrees(2 * math.atan(math.tan(math.radians(80) / 2) / selected))
        deadline = time.monotonic() + 2
        while True:
            state = observe()
            if abs(state['presentation']['fieldOfView'] - expected_fov) < .1:
                break
            need(time.monotonic() < deadline, 'held RMB did not apply selected projection')
            time.sleep(.08)
        report['zoomed'] = state['presentation']
        bridge.call('/release', 'POST', action='gui.RightClick')
        held = False
        deadline = time.monotonic() + 2
        while abs(observe()['presentation']['fieldOfView'] - 80) >= .1:
            need(time.monotonic() < deadline, 'RMB release did not restore 80 degree view')
            time.sleep(.08)
        need(press('gui.MiddleClick')['presentation']['automaticZoom'], 'MMB did not enable Auto')
        selected = observe()['presentation']['selectedZoom']
        for action in ('gui.scrollUp', 'gui.scrollDown'):
            view = press(action)['presentation']
            need(view['automaticZoom'] and view['selectedZoom'] == selected, 'wheel changed Auto/manual selection')
        need(observe()['corners'][corner]['ammo'] == before_ammo, 'optics inputs consumed ammunition')
        report['passed'] = True
    except Exception as error:
        report['failure'] = type(error).__name__ + ': ' + str(error)
    finally:
        try:
            if held:
                bridge.call('/release', 'POST', action='gui.RightClick')
            if initial is not None and corner is not None:
                manual()
                for _ in range(3):
                    press('gui.scrollDown')
                original = initial['presentation']['selectedZoom']
                for _ in range(3):
                    if observe()['presentation']['selectedZoom'] >= original:
                        break
                    press('gui.scrollUp')
                if initial['presentation']['automaticZoom']:
                    press('gui.MiddleClick')
                report['restored'] = observe()['presentation']
        except Exception as error:
            report['passed'] = False
            report['cleanupFailure'] = str(error)
        report['seconds'] = round(time.monotonic() - started, 3)
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, indent=2), encoding='utf-8')
    return 0 if report['passed'] else 1


if __name__ == '__main__':
    raise SystemExit(main())

