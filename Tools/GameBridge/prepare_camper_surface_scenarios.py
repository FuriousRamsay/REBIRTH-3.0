"""Generate executable real-input scenarios from one explicit /camper observation.

No bridge request, process launch or game mutation is performed by this preparer.
"""
import argparse
import json
from pathlib import Path


def generate(observation):
    entity = int(observation['entity'])
    if entity < 0:
        raise ValueError('Actual camper entity ID required')
    p = observation['presentation']
    if not p['bodyInstalled']:
        raise ValueError('Prepare the disposable fitted-body fixture with native UI first')

    def action(command, **args):
        return {'action': {'command': command, 'args': args}}

    def check(field, op, value=None, expected=None):
        row = {'field': 'presentation.' + field, 'op': op}
        row['expectedField' if expected else 'value'] = 'presentation.' + expected if expected else value
        return row

    def expect(*checks, timeout=0):
        return {'expectCamper': {'entity': entity, 'timeout': timeout, 'checks': list(checks)}}

    def travel_to(approach, aim):
        return [action('walkto', x=p[approach]['x'], z=p[approach]['z']), action('lookat', **p[aim])]

    files = {}
    for mode in ('Travel', 'Camp'):
        files['camper_hood_ladders_' + mode.lower() + '.json'] = {
            'name': 'Actual fitted camper hood and ladder in ' + mode,
            'stopOnFailure': True,
            'precondition': 'Disposable save; camper unchanged from observation; on foot with gameplay UI closed. This scenario walks and jumps using real inputs. Guard remains enabled.',
            'steps': [action('cleararea', radius=40), action('restore'),
                      expect(check('mode', '==', mode), check('bodyInstalled', '==', True)),
                      *travel_to('hoodApproach', 'hoodAim'), action('move', forward=1, seconds=.65, jump=1), {'wait': .25},
                      expect(check('selectionProxyIgnoresController', '==', True), check('playerGrounded', '==', True),
                             check('playerSupportedByVehicle', '==', True), check('playerFeetLocalY', '>', .8),
                             check('playerFeetLocalZ', '>', .7), timeout=3),
                      {'screenshot': 'hood_supported_' + mode.lower()},
                      *travel_to('ladderApproach', 'ladderAim'), action('move', forward=1, seconds=.8, jump=1),
                      expect(check('playerOnLadder', '==', True), timeout=1),
                      {'screenshot': 'ladder_native_' + mode.lower()}, {'expectNoErrors': True}]}

    files['camper_relays_station_fade.json'] = {
        'name': 'Actual relay red-stripe centers and shared station deployment fade', 'stopOnFailure': True,
        'precondition': 'Disposable fitted camper in Travel; native Energy Management opened through real vehicle interaction before this scenario. Keep vehicle fixed after the source observation.',
        'steps': [{'expectWindowOpen': 'rebirthCamperEnergy'},
                  expect(check('mode', '==', 'Travel'), check('relayCount', '==', 2), check('relayActiveEndpointCount', '==', 0),
                         check('relayPlateMinY', '>', 2.344), check('relayPlateMaxY', '<', 2.346), check('relayEnabledEndpointCount', '==', 0)),
                  action('click', id='energyModeToggle'), expect(check('mode', '==', 'Deploying'), timeout=2), {'wait': 2},
                  expect(check('relayMinOpacity', '>', 0), check('relayMaxOpacity', '<', 1), check('relayFadeRendererCount', '>', 0),
                         check('relayMinOpacity', '==', expected='stationFadeOpacity'), check('relayEnabledEndpointCount', '==', 0)),
                  {'screenshot': 'relay_station_mid_deploy'},
                  expect(check('mode', '==', 'Camp'), check('relayMinOpacity', '==', 1), check('relayEnabledEndpointCount', '==', 2), timeout=15),
                  {'screenshot': 'relay_red_stripe_center_camp'}, action('click', id='energyModeToggle'),
                  expect(check('mode', '==', 'Packing'), timeout=2), {'wait': 2},
                  expect(check('relayMinOpacity', '>', 0), check('relayMaxOpacity', '<', 1),
                         check('relayMinOpacity', '==', expected='stationFadeOpacity'), check('relayEnabledEndpointCount', '==', 0)),
                  {'screenshot': 'relay_station_mid_pack'},
                  expect(check('mode', '==', 'Travel'), check('relayMaxOpacity', '==', 0), check('relayActiveEndpointCount', '==', 0),
                         check('relayEnabledEndpointCount', '==', 0), timeout=15), action('key', name='Escape'), {'expectNoErrors': True}]}

    files['camper_reverse_illumination.json'] = {
        'name': 'Actual reverse beam intensity/cone and native colors', 'stopOnFailure': True,
        'precondition': 'Disposable flat clear road; fitted camper in Travel, local player already seated as native driver via real interaction. No open UI; safe clear space behind the vehicle. Screenshots still require visual review.',
        'steps': [action('cleararea', radius=40), action('restore'), {'console': 'settime 1 0 0'},
                  expect(check('mode', '==', 'Travel')),
                  {'expectCamper': {'entity': entity, 'checks': [{'field': 'vehicleDriverEntityId', 'op': '==', 'expectedField': 'localPlayerEntityId'}]}},
                  action('move', forward=-1, seconds=2),
                  expect(check('forwardVelocity', '<', -.05), check('rearEmittingCount', '>=', 2), check('rearMaxEffectiveIntensity', '>=', 3.24),
                         check('rearMaxRange', '>=', 6), check('rearMaxSpotAngle', '>=', 60), timeout=2),
                  {'screenshot': 'reverse_brighter_wider_ground'}, action('move', forward=0, seconds=1),
                  expect(check('forwardVelocity', '>=', -.05), check('rearMaxEffectiveIntensity', '<=', .65), timeout=15),
                  {'screenshot': 'reverse_released_native_rear_profile'}, {'expectNoErrors': True}]}
    return files


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('observation', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    observation = json.loads(args.observation.read_text(encoding='utf-8-sig'))
    args.output.mkdir(parents=True, exist_ok=True)
    for name, scenario in generate(observation).items():
        target = args.output / name
        target.write_text(json.dumps(scenario, indent=2) + '\n')
        print(target)
